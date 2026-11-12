using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

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
            Value.KindOneofCase.ObjectValue => SameObject(left.ObjectValue, right.ObjectValue),
            _ => left.Equals(right),
        };
    }

    // A value of time — an Any of google.protobuf.Timestamp or google.protobuf.Duration — is equal to another by the type and the
    // seconds and the nanoseconds, not by the bytes of the message; any other message by its Any.
    private static bool SameObject(Google.Protobuf.WellKnownTypes.Any left, Google.Protobuf.WellKnownTypes.Any right) =>
        OptionalValue.Is(left) || OptionalValue.Is(right)
            ? SameOptional(left, right)
            : left.Is(Timestamp.Descriptor) && right.Is(Timestamp.Descriptor)
                ? left.Unpack<Timestamp>().Equals(right.Unpack<Timestamp>())
                : left.Is(Duration.Descriptor) && right.Is(Duration.Descriptor)
                    ? left.Unpack<Duration>().Equals(right.Unpack<Duration>())
                    : left.Equals(right);

    // Two optionals are equal when both are empty, or both hold values that are equal: the empty optional, optional.of(null) and a held
    // value differ; an optional is never equal to another kind of value.
    private static bool SameOptional(Google.Protobuf.WellKnownTypes.Any left, Google.Protobuf.WellKnownTypes.Any right)
    {
        if (!OptionalValue.Is(left) || !OptionalValue.Is(right))
        {
            return false;
        }

        bool leftHolds = OptionalValue.TryRead(left, out Value? leftHeld);
        bool rightHolds = OptionalValue.TryRead(right, out Value? rightHeld);
        return leftHolds == rightHolds && (!leftHolds || Equal(leftHeld!, rightHeld!));
    }

    // Every entry of one map has an equal entry in the other, and the maps have as many entries.
    private static bool SameEntries(MapValue map, MapValue other) =>
        map.Entries.Count == other.Entries.Count
        && map.Entries.All(entry => other.Entries.Any(candidate => Equal(entry.Key, candidate.Key) && Equal(entry.Value, candidate.Value)));
}
