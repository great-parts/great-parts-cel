using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The state of one evaluation: the slots of the variables and of the comprehensions, the argument stack,
// the meter of the cost limit, the event counter of cancellation, the type index of the environment and the offset of
// the node whose own operation runs. A frame serves one call of Plan.Run on one thread.
internal sealed class Frame : IEvaluationMeter, IApplicationBoundary
{
    // How many events begin between two checks of the token.
    private const uint EventsPerCheck = 64;

    private readonly IReadOnlyDictionary<string, CelValue> _bindings;

    private readonly CelValue[] _slots;

    // Two slots for each comprehension: its iteration variable and its accumulator.
    private readonly CelValue[] _locals;
    private readonly bool[] _filled;
    private readonly CelValue[] _stack;
    private int _count;

    // The meter: only with a cost limit.
    private readonly bool _metered;
    private readonly ulong _limit;
    private ulong _remaining;

    // The event counter: only when the token can be canceled.
    private readonly bool _counts;
    private readonly CancellationToken _token;
    private uint _events;

    internal Frame(Plan plan, IReadOnlyDictionary<string, CelValue> bindings, ulong? costLimit, CancellationToken cancellationToken)
    {
        _bindings = bindings;
        _slots = Allocate<CelValue>(plan.VariableCount);
        _filled = Allocate<bool>(plan.VariableCount);
        _locals = Allocate<CelValue>(plan.LocalCount);
        _stack = Allocate<CelValue>(plan.StackSize);
        Lines = plan.Lines;
        Types = plan.Types;
        TimeZones = plan.TimeZones;
        if (costLimit is { } limit)
        {
            _metered = true;
            _limit = limit;
            _remaining = limit;
        }

        _counts = cancellationToken.CanBeCanceled;
        _token = cancellationToken;
    }

    // The line table of the plan, which turns the offsets of nodes into the positions of errors.
    internal LineTable Lines { get; }

    // The type index of the environment of the plan, which describes object values.
    internal TypeIndex Types { get; }

    // The time zone database of the environment of the plan, which the receivers of time with a zone resolve names against.
    internal CelTimeZoneDatabase? TimeZones { get; }

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

    internal void Push(CelValue value) => _stack[_count++] = value;

    // The last count values pushed, in the order of pushing.
    internal ReadOnlySpan<CelValue> Peek(int count) => new(_stack, _count - count, count);

    internal void Pop(int count) => _count -= count;

    // An expression without variables needs no slots and one without calls no stack: a length of 0 takes the
    // shared empty array instead of a new one.
    private static T[] Allocate<T>(int length) => length == 0 ? [] : new T[length];
}
