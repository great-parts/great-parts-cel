using System;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The values that the nodes of a partial evaluation had, for the residual expression: the ids of the nodes whose value was known —
// neither an error nor an unknown — in ascending order, and the value of each at the same index. It is immutable.
internal sealed class ValueRecord
{
    private readonly int[] _ids;
    private readonly CelValue[] _values;

    internal ValueRecord(int[] ids, CelValue[] values)
    {
        _ids = ids;
        _values = values;
    }

    // The record of an evaluation that wrote no node.
    internal static ValueRecord Empty { get; } = new([], []);

    // The known value of a node; false when the node was not evaluated or its value was an error or an unknown.
    internal bool TryGet(int id, out CelValue value)
    {
        int index = Array.BinarySearch(_ids, id);
        if (index >= 0)
        {
            value = _values[index];
            return true;
        }

        value = default;
        return false;
    }
}
