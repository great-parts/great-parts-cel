using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The state of one evaluation: the slots of the variables and the argument stack. A frame serves one
// call of Plan.Evaluate on one thread.
internal sealed class Frame
{
    private readonly IReadOnlyDictionary<string, CelValue> _bindings;
    private readonly CelValue[] _slots;
    private readonly bool[] _filled;
    private readonly CelValue[] _stack;
    private int _count;

    internal Frame(Plan plan, IReadOnlyDictionary<string, CelValue> bindings)
    {
        _bindings = bindings;
        _slots = Allocate<CelValue>(plan.SlotCount);
        _filled = Allocate<bool>(plan.SlotCount);
        _stack = Allocate<CelValue>(plan.StackSize);
        Lines = plan.Lines;
    }

    // The line table of the plan, which turns the offsets of nodes into the positions of errors.
    internal LineTable Lines { get; }

    // Reads a variable. The first read of a slot looks the name up in the bindings and keeps the value;
    // later reads of the slot take it from there. False when the name has no value in the bindings.
    internal bool TryRead(int slot, string name, out CelValue value)
    {
        if (!_filled[slot])
        {
            if (!_bindings.TryGetValue(name, out value))
            {
                return false;
            }

            _slots[slot] = value;
            _filled[slot] = true;
        }

        value = _slots[slot];
        return true;
    }

    internal void Push(CelValue value) => _stack[_count++] = value;

    // The last count values pushed, in the order of pushing.
    internal ReadOnlySpan<CelValue> Peek(int count) => new(_stack, _count - count, count);

    internal void Pop(int count) => _count -= count;

    // An expression without variables needs no slots and one without calls no stack: a length of 0 takes the
    // shared empty array instead of a new one.
    private static T[] Allocate<T>(int length) => length == 0 ? [] : new T[length];
}
