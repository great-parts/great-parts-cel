using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A map literal: evaluates the key and then the value of each entry from left to right onto the
// argument stack and creates a map in the order of insertion. The literal is strict: the first error
// value of a key or a value is its value, and the entries are allocated only after every key and value
// has a value. Then a key that cannot be a map key is UnsupportedMapKeyType at the key, and after that a
// key equal to an earlier one is DuplicateMapKey at the literal. Costs 1 + 2n for n entries, a key and a
// value each, and ⌈L/10⌉ for each string key of length L, known only once the keys are evaluated.
internal sealed class MapPlanNode : PlanNode
{
    internal MapPlanNode(int offset, ImmutableArray<PlanNode> keys, ImmutableArray<PlanNode> values)
        : base(offset, 1 + (2 * (ulong)keys.Length))
    {
        Keys = keys;
        Values = values;
    }

    // The keys and the values of the entries, both in the order of the text.
    internal ImmutableArray<PlanNode> Keys { get; }

    internal ImmutableArray<PlanNode> Values { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        ulong keyCost = 0;
        for (int i = 0; i < Keys.Length; i++)
        {
            CelValue key = Keys[i].Evaluate(frame);
            if (key.Kind == CelValueKind.Error)
            {
                frame.Pop(2 * i);
                return key;
            }

            frame.Push(key);
            CelValue value = Values[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                frame.Pop((2 * i) + 1);
                return value;
            }

            frame.Push(value);
            if (frame.Metered && key.TryGetString(out string? text))
            {
                keyCost += ((ulong)text.Length + 9) / 10;
            }
        }

        Begin(frame);
        if (keyCost != 0)
        {
            frame.Charge(keyCost);
        }

        ReadOnlySpan<CelValue> pushed = frame.Peek(2 * Keys.Length);
        KeyValuePair<CelValue, CelValue>[] entries = Keys.Length == 0 ? [] : new KeyValuePair<CelValue, CelValue>[Keys.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            entries[i] = new(pushed[2 * i], pushed[(2 * i) + 1]);
        }

        frame.Pop(2 * Keys.Length);

        EntryTableMap? map = EntryTableMap.TryCreate(entries, out EntryTableMap.Refusal refusal, out int index);
        if (map is not null)
        {
            return CelValue.FromMap(map);
        }

        CelValue refused = entries[index].Key;
        return refusal == EntryTableMap.Refusal.KeyKind
            ? EvaluationErrors.UnsupportedMapKeyType(refused.Kind, frame.Lines, Keys[index].Offset)
            : EvaluationErrors.DuplicateMapKey(refused, frame.Lines, Offset);
    }
}
