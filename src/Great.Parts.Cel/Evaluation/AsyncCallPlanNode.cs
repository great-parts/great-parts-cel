using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A call of a function that has an overload which waits: with one candidate, which waits, or with several, one of which does. It
// evaluates the arguments as a strict call and chooses the candidate as the other call nodes do; a candidate of the application
// that does not wait is called as always. A call of a candidate that waits counts an event and charges as a function of the application,
// and then looks the call up in the table of the evaluation, by this node, the overload and the arguments: a call that completed gives its
// value; any other is an unknown value that describes it, and a pass of the driver registers it so that the driver starts it before the
// next pass. A program without such a candidate has no such node.
internal sealed class AsyncCallPlanNode : PlanNode
{
    private readonly int _id;
    private readonly string _function;

    // The marks of the arguments for each candidate, at the same index.
    private readonly ImmutableArray<ImmutableArray<bool>> _marks;
    private readonly bool _charges;

    // Whether the one candidate needs no check of its arguments: type checking chose it and no argument is marked.
    private readonly bool _unchecked;

    // The object that type checking prepared from the constant argument of the call, and the cache of patterns of the environment.
    private readonly object? _prepared;
    private readonly object? _cache;

    internal AsyncCallPlanNode(
        int offset,
        int id,
        string function,
        ImmutableArray<OverloadBinding> candidates,
        ImmutableArray<ImmutableArray<bool>> marks,
        bool charges,
        ImmutableArray<PlanNode> arguments,
        object? prepared,
        object? cache)
        : base(offset, 0)
    {
        _id = id;
        _function = function;
        _marks = marks;
        _charges = charges;
        _prepared = prepared;
        _cache = cache;
        _unchecked = candidates.Length == 1 && !marks[0].Contains(true) && !candidates[0].ShallowArguments;
        Candidates = candidates;
        Arguments = arguments;
    }

    internal ImmutableArray<OverloadBinding> Candidates { get; }

    internal ImmutableArray<PlanNode> Arguments { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error) || !TryPushArguments(frame, Arguments, out error))
        {
            return error;
        }

        frame.Offset = Offset;
        ReadOnlySpan<CelValue> values = frame.Peek(Arguments.Length);
        CelValue result = Call(frame, values);
        frame.Pop(Arguments.Length);
        return result;
    }

    // The first candidate, in the order of the reference map, whose parameter types the values fit; an element error ends the search.
    private CelValue Call(Frame frame, ReadOnlySpan<CelValue> values)
    {
        for (int i = 0; i < Candidates.Length; i++)
        {
            OverloadBinding candidate = Candidates[i];
            TypeFit fit = _unchecked ? TypeFit.Yes : candidate.Matches(values, _marks[i].AsSpan(), frame.Meter, frame.Types);
            if (fit.IsFit)
            {
                return candidate.IsAsync ? CallAsync(frame, candidate, values) : Invocation.Call(frame, candidate, values, Offset, _charges, _prepared, _cache);
            }

            if (fit.ElementError is not null)
            {
                return EvaluationErrors.NotMatched(fit, _function, values, frame, Offset);
            }
        }

        return EvaluationErrors.NotMatched(TypeFit.No, _function, values, frame, Offset);
    }

    private CelValue CallAsync(Frame frame, OverloadBinding overload, ReadOnlySpan<CelValue> arguments)
    {
        if (!Invocation.TryChargeApplication(frame, overload, arguments, Offset, out CelValue failure))
        {
            return failure;
        }

        AsyncCallTable? table = frame.Calls;
        if (table is null && frame.CollectsCalls)
        {
            table = frame.Calls = new AsyncCallTable();
        }

        // An evaluation without a driver only describes the call: nothing is looked up or kept.
        if (table is null)
        {
            return CelValue.FromUnknown(Describe(overload, [.. arguments]));
        }

        int hash = AsyncCallTable.HashOf(_id, overload, arguments, out bool pairwise);
        ref AsyncCall? bucket = ref table.Bucket(hash);
        if (AsyncCallTable.Find(bucket, _id, overload, arguments, pairwise ? frame.Meter : null, frame) is { } known)
        {
            return known.State == AsyncCallState.Completed ? Invocation.AsyncResult(frame, known) : CelValue.FromUnknown(known.Description);
        }

        ImmutableArray<CelValue> copy = [.. arguments];
        UnknownSet description = Describe(overload, copy);
        table.Register(new AsyncCall(_id, Offset, overload, copy, hash, description), ref bucket);
        return CelValue.FromUnknown(description);
    }

    private UnknownSet Describe(OverloadBinding overload, ImmutableArray<CelValue> arguments) =>
        UnknownSet.Of(new UnknownEntry(_id, overload.Function, overload.Declaration.OverloadId, arguments));
}
