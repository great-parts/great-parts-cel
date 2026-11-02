using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A map literal: evaluates the key and then the value of each entry from left to right and creates a
// map in the order of insertion. The literal is strict: the first error value of a key or a value is
// its value. Then a key that cannot be a map key is UnsupportedMapKeyType at the key, and after that a
// key equal to an earlier one is DuplicateMapKey at the literal. Costs 1 + n for n entries; the cost of
// the string keys is known only once they are evaluated.
internal sealed class MapPlanNode : PlanNode
{
    internal MapPlanNode(int offset, ImmutableArray<PlanNode> keys, ImmutableArray<PlanNode> values)
        : base(offset, 1 + (ulong)keys.Length)
    {
        Keys = keys;
        Values = values;
    }

    // The keys and the values of the entries, both in the order of the text.
    internal ImmutableArray<PlanNode> Keys { get; }

    internal ImmutableArray<PlanNode> Values { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        var entries = new KeyValuePair<CelValue, CelValue>[Keys.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            CelValue key = Keys[i].Evaluate(frame);
            if (key.Kind == CelValueKind.Error)
            {
                return key;
            }

            CelValue value = Values[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                return value;
            }

            entries[i] = new(key, value);
        }

        EntryTableMap? map = EntryTableMap.TryCreate(entries, out EntryTableMap.Refusal refusal, out int index);
        if (map is not null)
        {
            return CelValue.FromMap(map);
        }

        CelValue refused = entries[index].Key;
        return refusal == EntryTableMap.Refusal.KeyKind
            ? EvaluationErrors.UnsupportedMapKeyType(refused.Kind, frame.Lines.GetLocation(Keys[index].Offset))
            : EvaluationErrors.DuplicateMapKey(refused, frame.Lines.GetLocation(Offset));
    }
}
