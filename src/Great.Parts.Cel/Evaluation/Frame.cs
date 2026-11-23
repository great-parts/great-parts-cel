using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The state of one evaluation: the slots of the variables and of the comprehensions, the argument stack,
// the meter of the cost limit, the event counter of cancellation, the type index of the environment and the offset of
// the node whose own operation runs. A frame serves one call of Plan.Run on one thread. The frame of an input that is a structure is
// the class derived from this one (InputFrame<TInput>), which holds the structure itself.
//
// The frame of a call is rented: the evaluation takes one that its thread kept from an earlier evaluation, or makes a new one, and
// gives it back when it ends, however it ends, cleared of everything of the evaluation; so a thread that evaluates again and again
// allocates no frame. An evaluation inside an evaluation, which application code makes, finds the outer frame taken and rents another.
internal class Frame : IEvaluationMeter, IApplicationBoundary
{
    // How many events begin between two checks of the token.
    private const uint EventsPerCheck = 64;

    // A frame with an array longer than this is not kept: a thread keeps the frames of the expressions of a usual size, not those of one
    // that is huge, which are made again for each call.
    private const int KeptLength = 256;

    // What a frame that waits for its next evaluation holds instead of the bindings of the last one.
    private static readonly FrozenDictionary<string, CelValue> NoBindings = FrozenDictionary<string, CelValue>.Empty;

    private IReadOnlyDictionary<string, CelValue> _bindings;

    private Plan _plan;

    private CelValue[] _slots = [];

    // Two slots for each comprehension: its iteration variable and its accumulator.
    private CelValue[] _locals = [];
    private bool[] _filled = [];

    // The cells of the slots of the blocks, with the state of each: the epoch of the block shifted left by one, and its low bit
    // set when the slot is evaluated; the epoch of each block; and the counter that gives a new epoch at each entry of a block.
    private CelValue[] _cells = [];
    private ulong[] _cellStates = [];
    private ulong[] _blockEpochs = [];
    private ulong _epoch;
    private CelValue[] _stack = [];
    private int _count;

    // The lengths of the arrays that the plan of the evaluation uses: a kept array may be longer, and only these places are dirty.
    private int _variableCount;
    private int _localCount;
    private int _cellCount;
    private int _blockCount;
    private int _stackSize;

    // The meter: only with a cost limit.
    private bool _metered;
    private ulong _limit;
    private ulong _remaining;

    // The event counter: only when the token can be canceled.
    private bool _counts;
    private CancellationToken _token;
    private uint _events;

    // A frame for one evaluation, not kept for another: the form that the tests of the plan use. The evaluation of a program rents.
    internal Frame(Plan plan, IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, CancellationToken cancellationToken, object? input = null)
    {
        Prepare(plan, bindings, costLimit, input, cancellationToken);
    }

    // Takes a frame that the thread kept and prepares it for the evaluation, or makes one; Release gives it back.
    internal static Frame Rent(Plan plan, IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, object? input, CancellationToken cancellationToken)
    {
        Frame? kept = FrameSpares<Frame>.Take();
        if (kept is null)
        {
            return new Frame(plan, bindings, costLimit, cancellationToken, input);
        }

        kept.Prepare(plan, bindings, costLimit, input, cancellationToken);
        return kept;
    }

    // Ends the evaluation of a rented frame: clears it of everything of the evaluation, and the thread keeps it for the next one.
    internal virtual void Release()
    {
        if (Clear())
        {
            FrameSpares<Frame>.Put(this);
        }
    }

    // Makes the frame ready for the evaluation of the plan: arrays that are long enough are kept, shorter ones are made again, and all
    // the state of an evaluation is set.
    [MemberNotNull(nameof(_plan), nameof(_bindings))]
    private protected void Prepare(Plan plan, IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, object? input, CancellationToken cancellationToken)
    {
        Input = input;
        _plan = plan;
        _bindings = bindings;
        _variableCount = plan.VariableCount;
        _localCount = plan.LocalCount;
        _cellCount = plan.SlotCount;
        _blockCount = plan.BlockCount;
        _stackSize = plan.StackSize;
        Fit(ref _slots, _variableCount);
        Fit(ref _filled, _variableCount);
        Fit(ref _locals, _localCount);
        Fit(ref _cells, _cellCount);
        Fit(ref _cellStates, _cellCount);
        Fit(ref _blockEpochs, _blockCount);
        Fit(ref _stack, _stackSize);
        if (costLimit is { } limit)
        {
            _metered = true;
            _limit = limit;
            _remaining = limit;
        }
        else
        {
            _metered = false;
            _limit = 0;
            _remaining = 0;
        }

        _counts = cancellationToken.CanBeCanceled;
        _token = cancellationToken;
    }

    // Clears the frame of the evaluation, so that a frame that waits holds no value, bindings, plan or token of it, and tells whether
    // the arrays are short enough for the thread to keep it.
    private protected bool Clear()
    {
        _slots.AsSpan(0, _variableCount).Clear();
        _filled.AsSpan(0, _variableCount).Clear();
        _locals.AsSpan(0, _localCount).Clear();
        _cells.AsSpan(0, _cellCount).Clear();
        _cellStates.AsSpan(0, _cellCount).Clear();
        _blockEpochs.AsSpan(0, _blockCount).Clear();
        _stack.AsSpan(0, _stackSize).Clear();
        _count = 0;
        _epoch = 0;
        _events = 0;
        Offset = 0;
        Input = null;
        Partial = null;
        Calls = null;
        CollectsCalls = false;
        _bindings = NoBindings;
        _plan = null!;
        _token = default;
        _counts = false;
        _metered = false;
        _limit = 0;
        _remaining = 0;
        return Math.Max(Math.Max(_slots.Length, _locals.Length), Math.Max(Math.Max(_cells.Length, _blockEpochs.Length), _stack.Length)) <= KeptLength;
    }

    // The instance of the input type of the environment whose fields the evaluation reads; null when the call passed none, or when
    // the input is a structure that the frame holds itself and has not boxed.
    internal object? Input { get; private protected set; }

    // The patterns of unknown data, the keys of the chain being read and the record of the values of the nodes, during a partial
    // evaluation; null otherwise, so that the nodes that serve it do nothing in any other evaluation.
    internal PartialState? Partial { get; set; }

    // Whether the evaluation is a pass of the asynchronous driver: a call of an overload that waits registers in the table of the calls
    // and is an unknown value until it has completed. Without it (an evaluation that is partial and synchronous) the call is an unknown
    // value that describes it and nothing registers.
    internal bool CollectsCalls { get; set; }

    // The table of the asynchronous calls of the evaluation: given by the driver for the passes after the first, and made by the first
    // registration of a pass that has none; null while no call has registered.
    internal AsyncCallTable? Calls { get; set; }

    // The events counted so far, which the driver carries from one pass to the next.
    internal uint Events => _events;

    // Continues an evaluation in a later pass: the units charged by the passes before it, which the limit of this pass already counts,
    // and the events they counted. The limit is the one the frame was prepared with.
    internal void Resume(ulong charged, uint events)
    {
        if (_metered)
        {
            _remaining = _limit - charged;
        }

        _events = events;
    }

    // Reads the field of the input that an identifier names, after the unit of the identifier: false when the call passed no input.
    // The frame of a structure overrides it to read the structure in place.
    internal virtual bool TryReadInputField(CelObjectType owner, int number, int offset, out CelValue value)
    {
        if (Input is { } input)
        {
            value = FieldPlanNode.Read(this, owner, input, number, false, offset);
            return true;
        }

        value = default;
        return false;
    }

    // The line table of the plan, which turns the offsets of nodes into the positions of errors.
    internal LineTable Lines => _plan.Lines;

    // The type index of the environment of the plan, which describes object values.
    internal TypeIndex Types => _plan.Types;

    // The time zone database of the environment of the plan, which the receivers of time with a zone resolve names against.
    internal CelTimeZoneDatabase? TimeZones => _plan.TimeZones;

    // Whether the evaluation has a cost limit.
    internal bool Metered => _metered;

    // The meter for the rules that charge, or null without a limit, so that they compute nothing.
    internal IEvaluationMeter? Meter => _metered ? this : null;

    // The limit minus the remaining units; 0 without a meter.
    internal ulong ChargedUnits => _limit - _remaining;

    internal ulong Limit => _limit;

    internal CancellationToken Token => _token;

    // The offset of the node whose own operation runs: the position of CostLimitExceeded and InternalError.
    internal int Offset { get; set; }

    // Charges units before the operation they pay for. More units than remain mean the operation does not
    // run. Called only with a meter.
    public void Charge(ulong units)
    {
        if (units > _remaining)
        {
            throw new CostLimitException();
        }

        _remaining -= units;
    }

    // Counts an event before it begins; the token is checked before the first event and then every 64.
    public void Event()
    {
        if (_counts)
        {
            if (_events % EventsPerCheck == 0)
            {
                _token.ThrowIfCancellationRequested();
            }

            _events++;
        }
    }

    // An exception of application code becomes ApplicationException, except OutOfMemoryException, the internal
    // CostLimitException and the OperationCanceledException of the canceled token.
    public bool IsApplicationException(Exception exception) =>
        exception is not CostLimitException && Plan.IsInternal(exception, _token);

    // ApplicationException of a field read during the operation of the recorded node.
    public CelValue FieldException(Exception exception, CelObjectType owner, int number) =>
        EvaluationErrors.FieldException(exception, owner, number, false, Lines, Offset);

    // ApplicationException of the equality that a description does itself, during the operation of the recorded node.
    public CelValue EqualityException(Exception exception, CelObjectType owner) =>
        EvaluationErrors.EqualityException(exception, owner, Lines, Offset);

    // InternalError at the recorded node.
    public CelValue InternalFailure(Exception exception) => EvaluationErrors.InternalError(exception, Lines, Offset);

    // Reads a variable. The first read of a slot looks the name up in the bindings, and first is true: the
    // caller checks the value and keeps the result with Keep. Later reads take the slot. False when the
    // name has no value in the bindings.
    internal bool TryRead(int slot, string name, out CelValue value, out bool first)
    {
        first = !_filled[slot];
        if (!first)
        {
            value = _slots[slot];
            return true;
        }

        return _bindings.TryGetValue(name, out value);
    }

    // Keeps the checked value of a variable, or its error value, for the later reads of the slot.
    internal void Keep(int slot, CelValue value)
    {
        _slots[slot] = value;
        _filled[slot] = true;
    }

    // Looks a name up in the bindings, without a slot: evaluation without type checking.
    internal bool TryLookup(string name, out CelValue value) => _bindings.TryGetValue(name, out value);

    // The slot of an iteration variable or an accumulator.
    internal ref CelValue Local(int slot) => ref _locals[slot];

    // Takes a new epoch for the block: its slots are unread until a node reads them. The first epoch is 1, so the zero state of a
    // cell is never a state of an epoch.
    internal void EnterBlock(int block) => _blockEpochs[block] = ++_epoch;

    internal ulong BlockEpoch(int block) => _blockEpochs[block];

    internal ulong SlotState(int cell) => _cellStates[cell];

    internal CelValue SlotValue(int cell) => _cells[cell];

    internal void SetSlotState(int cell, ulong state) => _cellStates[cell] = state;

    // Keeps the value of a slot, or its error value, and marks the cell as evaluated.
    internal void KeepSlot(int cell, CelValue value, ulong state)
    {
        _cells[cell] = value;
        _cellStates[cell] = state;
    }

    internal void Push(CelValue value) => _stack[_count++] = value;

    // The last count values pushed, in the order of pushing.
    internal ReadOnlySpan<CelValue> Peek(int count) => new(_stack, _count - count, count);

    internal void Pop(int count) => _count -= count;

    // An array of at least the length: one that is long enough stays, a shorter one is made again, and an expression without variables
    // or calls takes the shared empty array.
    private static void Fit<T>(ref T[] array, int length)
    {
        if (array.Length < length)
        {
            array = new T[length];
        }
    }
}
