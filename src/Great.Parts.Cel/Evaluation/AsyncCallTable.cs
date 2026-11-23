using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// How far a call of an overload that waits has come.
internal enum AsyncCallState
{
    // Met by a pass, not started: its description is the unknown value that the node of the call gives.
    Registered,

    // Started by the driver, its task not yet awaited.
    Started,

    // Its result or its exception is stored.
    Completed,
}

// One call of an overload that waits: the node of the expression it is made at, the overload, the arguments, and what became of
// it. The driver starts it and stores its outcome between two passes; the node of the call reads it in the next pass.
internal sealed class AsyncCall : IEvaluationMeter
{
    internal AsyncCall(int id, int offset, OverloadBinding overload, ImmutableArray<CelValue> arguments, int hash, UnknownSet description)
    {
        Id = id;
        Offset = offset;
        Overload = overload;
        Arguments = arguments;
        Hash = hash;
        Description = description;
    }

    // The id of the syntax node of the call.
    internal int Id { get; }

    // The offset of the call in the text, the place of its errors.
    internal int Offset { get; }

    internal OverloadBinding Overload { get; }

    internal ImmutableArray<CelValue> Arguments { get; }

    internal int Hash { get; }

    // The unknown value that stands for the call until it has completed: one entry that names the function, the overload and the arguments.
    internal UnknownSet Description { get; }

    // The next call of the same bucket of the table.
    internal AsyncCall? Next { get; set; }

    internal AsyncCallState State { get; set; }

    // The task of a started call; the driver awaits it once.
    internal ValueTask<CelValue> Pending { get; set; }

    // The result of a call that completed without an exception, as the implementation gave it; the node checks it in each pass.
    internal CelValue Result { get; set; }

    // The exception of a call that threw, thrown or in the task.
    internal Exception? Exception { get; set; }

    // The units that the work of the library for this call counted: the copies of the arguments when the delegate converts them and the
    // copy of its result. Only an evaluation with a limit counts them; the driver adds them to the cost after the wave, in the order in which
    // the calls started. They are written by the delegate, which may run on any thread, and read after the call has been awaited.
    internal ulong Units { get; private set; }

    // The call has started: its task is kept until the driver awaits it.
    internal void Begin(ValueTask<CelValue> pending)
    {
        Pending = pending;
        State = AsyncCallState.Started;
    }

    // Counts units without checking the limit: an exception inside the code of the application is not wanted, and the driver checks the
    // sum of the wave.
    public void Charge(ulong units)
    {
        ulong sum = Units + units;
        Units = sum < Units ? ulong.MaxValue : sum;
    }

    // The conversions do not make events: the events of the call were counted when the pass made it.
    public void Event()
    {
    }
}

// The calls of one evaluation of an overload that waits, in the order in which the passes met them; a call is found again by its node,
// its overload and its arguments, which are equal by CEL equality with a NaN equal to a NaN at any depth and the numbers of all kinds
// on one line. The table lives from the first registration to the end of the evaluation, outside the frames of the passes, and is
// used by one evaluation at a time.
internal sealed class AsyncCallTable
{
    private readonly List<AsyncCall> _calls = [];
    private readonly Dictionary<int, AsyncCall> _buckets = [];

    // The calls in the order of registration.
    internal List<AsyncCall> Calls => _calls;

    // The number of calls that were started: those before this index are started or completed, those from it on are registered.
    internal int Started { get; set; }

    // Whether a call was registered and not started.
    internal bool HasPending => Started < _calls.Count;

    // The hash of a call: the node, the overload and the hashes of the arguments. The hash of an argument is that of the value rules
    // (ValueHasher, in its mode for a table of calls), which agrees with CEL equality: lists by their elements in order, maps by their
    // entries in any order, optionals by their value, numbers of the kinds int, uint and double by the double they compare as, a NaN by a
    // constant, bytes, timestamps and durations by content. It is computed by walking the value, once per call of a pass, which is the
    // work its construction was charged for. A value that holds an object of the application, an error or a map that gives an error
    // has no hash of its content: such a value hashes by its group, so all of them share one bucket, and pairwise is true for them.
    internal static int HashOf(int id, OverloadBinding overload, ReadOnlySpan<CelValue> arguments, out bool pairwise)
    {
        var hash = new HashCode();
        hash.Add(id);
        hash.Add(RuntimeHelpers.GetHashCode(overload));
        var hasher = new ValueHasher(null, forCallTable: true);
        pairwise = false;
        foreach (CelValue argument in arguments)
        {
            ValueGroup group = hasher.Measure(argument, out int argumentHash);
            pairwise |= group == ValueGroup.Pairwise;
            hash.Add(group == ValueGroup.Hashed ? argumentHash : (int)group);
        }

        return hash.ToHashCode();
    }

    // The head of the calls of a hash, null when there is none: the slot that Find reads and Register writes, so that a call that is new
    // costs one lookup of the hash. The reference holds until another call is registered.
    internal ref AsyncCall? Bucket(int hash) => ref CollectionsMarshal.GetValueRefOrAddDefault(_buckets, hash, out _);

    // The call among those of a bucket that is the same call as this one, or null. Only a meter that is not null charges the
    // comparisons: the caller gives it for a call whose arguments hold application data, which a comparison reads.
    internal static AsyncCall? Find(
        AsyncCall? head,
        int id,
        OverloadBinding overload,
        ReadOnlySpan<CelValue> arguments,
        IEvaluationMeter? meter,
        Frame frame)
    {
        for (AsyncCall? call = head; call is not null; call = call.Next)
        {
            if (call.Id == id && ReferenceEquals(call.Overload, overload) && SameArguments(call.Arguments, arguments, meter, frame))
            {
                return call;
            }
        }

        return null;
    }

    // Adds a call after those registered before it, at the head of its bucket.
    internal void Register(AsyncCall call, ref AsyncCall? head)
    {
        _calls.Add(call);
        call.Next = head;
        head = call;
    }

    private static bool SameArguments(ImmutableArray<CelValue> known, ReadOnlySpan<CelValue> arguments, IEvaluationMeter? meter, Frame frame)
    {
        if (known.Length != arguments.Length)
        {
            return false;
        }

        for (int i = 0; i < arguments.Length; i++)
        {
            if (!SameValue(known[i], arguments[i], meter, frame))
            {
                return false;
            }
        }

        return true;
    }

    // CEL equality with a NaN equal to a NaN at any depth, and two values that are one instance of an object equal without reading it.
    // A comparison that fails — an application description whose equality threw — counts as equal: the arguments come from the same
    // node and the same data, so the same call must be found again whatever the equality of the description says, or the evaluation
    // would register it anew in every pass.
    private static bool SameValue(CelValue left, CelValue right, IEvaluationMeter? meter, Frame frame) =>
        EqualityRules.AreEqual(left, right, meter, out _, frame.Types, frame, nanEqual: true) != ValueEquality.NotEqual;
}
