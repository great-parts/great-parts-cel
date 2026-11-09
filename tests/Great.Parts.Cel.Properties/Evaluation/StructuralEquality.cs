using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Properties.Evaluation;

/// <summary>
/// The structural comparison of two values that the properties of evaluation use (docs/design/tests.md, "Properties of
/// evaluation"): the same kind, lists element by element, maps as sets of entries, NaN equal to NaN, type values by name, object values by the instance. It
/// does not use the equality of the library, which the properties check.
/// </summary>
internal static class StructuralEquality
{
    internal static bool Equal(CelValue left, CelValue right)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        switch (left.Kind)
        {
            case CelValueKind.Null:
                return true;
            case CelValueKind.Bool:
                _ = left.TryGetBoolean(out bool leftBool);
                _ = right.TryGetBoolean(out bool rightBool);
                return leftBool == rightBool;
            case CelValueKind.Int:
                _ = left.TryGetInt64(out long leftInt);
                _ = right.TryGetInt64(out long rightInt);
                return leftInt == rightInt;
            case CelValueKind.UInt:
                _ = left.TryGetUInt64(out ulong leftUInt);
                _ = right.TryGetUInt64(out ulong rightUInt);
                return leftUInt == rightUInt;
            case CelValueKind.Double:
                // double.Equals takes NaN as equal to NaN.
                _ = left.TryGetDouble(out double leftDouble);
                _ = right.TryGetDouble(out double rightDouble);
                return leftDouble.Equals(rightDouble);
            case CelValueKind.String:
                _ = left.TryGetString(out string? leftString);
                _ = right.TryGetString(out string? rightString);
                return string.Equals(leftString, rightString, StringComparison.Ordinal);
            case CelValueKind.Bytes:
                _ = left.TryGetBytes(out ReadOnlyMemory<byte> leftBytes);
                _ = right.TryGetBytes(out ReadOnlyMemory<byte> rightBytes);
                return leftBytes.Span.SequenceEqual(rightBytes.Span);
            case CelValueKind.Type:
                _ = left.TryGetType(out CelType? leftType);
                _ = right.TryGetType(out CelType? rightType);
                return string.Equals(leftType!.Name, rightType!.Name, StringComparison.Ordinal);
            case CelValueKind.List:
                _ = left.TryGetList(out CelList? leftList);
                _ = right.TryGetList(out CelList? rightList);
                return ListsEqual(leftList!, rightList!);
            case CelValueKind.Map:
                _ = left.TryGetMap(out CelMap? leftMap);
                _ = right.TryGetMap(out CelMap? rightMap);
                return MapsEqual(leftMap!, rightMap!);
            case CelValueKind.Object:
                // Object values are the same instance; their descriptions are checked by the properties that compare them.
                _ = left.TryGetObject(out object? leftObject);
                _ = right.TryGetObject(out object? rightObject);
                return ReferenceEquals(leftObject, rightObject);
            default:
                throw new NotSupportedException($"The comparison has no rule for the kind {left.Kind}.");
        }
    }

    private static bool ListsEqual(CelList left, CelList right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!Equal(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    // Every entry of one map has an entry with an equal key and an equal value in the other, and the counts are equal.
    private static bool MapsEqual(CelMap left, CelMap right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (KeyValuePair<CelValue, CelValue> entry in left)
        {
            bool found = false;
            foreach (KeyValuePair<CelValue, CelValue> other in right)
            {
                if (Equal(entry.Key, other.Key))
                {
                    found = Equal(entry.Value, other.Value);
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
