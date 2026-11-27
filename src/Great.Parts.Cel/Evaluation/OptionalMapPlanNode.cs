using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A map literal with entries that have a ?, {?k: v}. For each entry in the order of the text the key and then the value are evaluated
// onto the argument stack, and the first error value is the result (an unknown value of a partial evaluation only waits for the
// rest, as in a plain map literal). The value of an entry with ? must be an optional: a value that
// is not one, which only an expression of the static type dyn can give, is EvalNoMatchingOverload with the function ? at the value;
// an empty optional drops the entry, and its key is not looked at, and one that holds a value stores the entry with that value. The
// stored entries get the checks of a plain map: the kind of a key (UnsupportedMapKeyType at the key), then a key equal to an
// earlier stored one (DuplicateMapKey at the literal). The node charges 1 + 2m + ⌈L/10⌉ for each string key of length L stored, m
// the entries stored, after its children and before the map is created.
internal sealed class OptionalMapPlanNode : PlanNode
{
    private readonly ImmutableArray<PlanNode> _keys;
    private readonly ImmutableArray<PlanNode> _values;

    // Whether each entry has a ?.
    private readonly ImmutableArray<bool> _optional;

    internal OptionalMapPlanNode(int offset, ImmutableArray<PlanNode> keys, ImmutableArray<PlanNode> values, ImmutableArray<bool> optional)
        : base(offset, 1)
    {
        _keys = keys;
        _values = values;
        _optional = optional;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        if (!TryEvaluateEntries(frame, out int stored, out int[]? kept, out ulong keyCost, out error))
        {
            return error;
        }

        Begin(frame);
        if (frame.Metered)
        {
            frame.Charge((2 * (ulong)stored) + keyCost);
        }

        ReadOnlySpan<CelValue> pushed = frame.Peek(2 * stored);
        KeyValuePair<CelValue, CelValue>[] entries = stored == 0 ? [] : new KeyValuePair<CelValue, CelValue>[stored];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = new(pushed[2 * i], pushed[(2 * i) + 1]);
        }

        frame.Pop(2 * stored);

        EntryTableMap? map = EntryTableMap.TryCreate(entries, out EntryTableMap.Refusal refusal, out int index);
        if (map is not null)
        {
            return CelValue.FromMap(map);
        }

        CelValue refused = entries[index].Key;
        return refusal == EntryTableMap.Refusal.KeyKind
            ? EvaluationErrors.UnsupportedMapKeyType(refused, frame.Types, frame.Lines, _keys[kept is null ? index : kept[index]].Offset)
            : EvaluationErrors.DuplicateMapKey(refused, frame.Lines, Offset);
    }

    // Evaluates the entries in the order of the text. The key and the value of each entry that is stored are on the stack and
    // counted in stored, with the units of the length of its string key in keyCost when there is a limit. After the first dropped
    // entry, kept holds the index of each stored one, which gives the node of its key. False with the error value that ended the
    // evaluation, and nothing left on the stack. An unknown value of a partial evaluation does not end it: the rest is evaluated,
    // a real error among it is the result, and otherwise the result is the union of the unknown values.
    private bool TryEvaluateEntries(Frame frame, out int stored, out int[]? kept, out ulong keyCost, out CelValue error)
    {
        stored = 0;
        kept = null;
        keyCost = 0;
        error = default;
        CelValue pending = default;
        for (int i = 0; i < _keys.Length; i++)
        {
            CelValue key = _keys[i].Evaluate(frame);
            if (key.Kind == CelValueKind.Error)
            {
                if (!key.IsUnknown)
                {
                    frame.Pop(2 * stored);
                    error = key;
                    return false;
                }

                pending = Defer(frame, pending, key);
            }

            frame.Push(key);
            CelValue value = _values[i].Evaluate(frame);
            Entry entry = ReadValue(frame, i, ref value, ref pending, out error);
            if (entry == Entry.Fail)
            {
                frame.Pop((2 * stored) + 1);
                return false;
            }

            if (entry == Entry.Drop || pending.IsUnknown)
            {
                frame.Pop(1);
                kept ??= FirstIndices(_keys.Length, stored);
                continue;
            }

            if (kept is not null)
            {
                kept[stored] = i;
            }

            frame.Push(value);
            stored++;
            if (frame.Metered && key.TryGetString(out string? text))
            {
                keyCost += ((ulong)text.Length + 9) / 10;
            }
        }

        if (pending.IsUnknown)
        {
            frame.Pop(2 * stored);
            error = pending;
            return false;
        }

        return true;
    }

    private enum Entry
    {
        Store,
        Drop,
        Fail,
    }

    // Reads the value of the entry at index. An error value is the failure, except an unknown one, which is added to pending and
    // drops the entry; for an entry with ? the value is replaced by what the optional holds, and an empty optional drops the entry.
    private Entry ReadValue(Frame frame, int index, ref CelValue value, ref CelValue pending, out CelValue error)
    {
        error = default;
        if (value.Kind == CelValueKind.Error)
        {
            if (!value.IsUnknown)
            {
                error = value;
                return Entry.Fail;
            }

            pending = Defer(frame, pending, value);
            return Entry.Drop;
        }

        if (!_optional[index])
        {
            return Entry.Store;
        }

        if (!value.TryGetOptional(out CelOptional<CelValue> optional))
        {
            error = EvaluationErrors.NotAnOptional(value, frame, _values[index].Offset);
            return Entry.Fail;
        }

        if (!optional.HasValue)
        {
            return Entry.Drop;
        }

        value = optional.Value;
        return Entry.Store;
    }
}
