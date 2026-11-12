using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A map literal with entries that have a ?, {?k: v}. For each entry in the order of the text the key and then the value are evaluated
// onto the argument stack, and the first error value is the result. The value of an entry with ? must be an optional: a value that
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
    // evaluation, and nothing left on the stack.
    private bool TryEvaluateEntries(Frame frame, out int stored, out int[]? kept, out ulong keyCost, out CelValue error)
    {
        stored = 0;
        kept = null;
        keyCost = 0;
        error = default;
        for (int i = 0; i < _keys.Length; i++)
        {
            CelValue key = _keys[i].Evaluate(frame);
            if (key.Kind == CelValueKind.Error)
            {
                frame.Pop(2 * stored);
                error = key;
                return false;
            }

            frame.Push(key);
            CelValue value = _values[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                frame.Pop((2 * stored) + 1);
                error = value;
                return false;
            }

            if (_optional[i])
            {
                if (!value.TryGetOptional(out CelOptional<CelValue> optional))
                {
                    frame.Pop((2 * stored) + 1);
                    error = EvaluationErrors.NotAnOptional(value, frame, _values[i].Offset);
                    return false;
                }

                if (!optional.HasValue)
                {
                    frame.Pop(1);
                    kept ??= FirstIndices(_keys.Length, stored);
                    continue;
                }

                value = optional.Value;
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

        return true;
    }
}
