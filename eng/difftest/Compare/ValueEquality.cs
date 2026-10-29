using System.Linq;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>
/// The equality of the outcome values of the two sides: the <c>cel.expr.Value</c> messages have the same kind and value. Lists
/// compare element by element, maps as sets of entries, doubles by <c>==</c> or both NaN. It is not CEL equality: <c>1</c> and
/// <c>1u</c> differ, so a difference of types is not hidden.
/// </summary>
internal static class ValueEquality
{
    /// <summary>Whether two values are equal.</summary>
    /// <param name="left">A value.</param>
    /// <param name="right">A value.</param>
    internal static bool Equal(Value left, Value right)
    {
        if (left.KindCase != right.KindCase)
        {
            return false;
        }

        return left.KindCase switch
        {
            Value.KindOneofCase.DoubleValue => left.DoubleValue == right.DoubleValue
                || (double.IsNaN(left.DoubleValue) && double.IsNaN(right.DoubleValue)),
            Value.KindOneofCase.ListValue => left.ListValue.Values.Count == right.ListValue.Values.Count
                && left.ListValue.Values.Zip(right.ListValue.Values).All(pair => Equal(pair.First, pair.Second)),
            Value.KindOneofCase.MapValue => SameEntries(left.MapValue, right.MapValue) && SameEntries(right.MapValue, left.MapValue),
            _ => left.Equals(right),
        };
    }

    // Every entry of one map has an equal entry in the other, and the maps have as many entries.
    private static bool SameEntries(MapValue map, MapValue other) =>
        map.Entries.Count == other.Entries.Count
        && map.Entries.All(entry => other.Entries.Any(candidate => Equal(entry.Key, candidate.Key) && Equal(entry.Value, candidate.Value)));
}
