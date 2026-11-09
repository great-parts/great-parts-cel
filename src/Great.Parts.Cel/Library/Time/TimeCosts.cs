using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The cost functions of the arguments of the overloads of time: the part of a cost that the call node charges before the
// implementation runs, beyond the fixed unit of the overload. Lengths are UTF-16 code units; the length of a text result is
// computed from the value, without making the string. An argument of another kind adds nothing, since the implementation
// rejects it.
internal static class TimeCosts
{
    // ⌈Lₐ/10⌉: the string of timestamp(string) and duration(string), and the zone string of a receiver with a zone — the
    // receiver is not a string, so the zone is the only one counted.
    internal static readonly CelCostFunction Text = static arguments =>
    {
        ulong length = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetString(out string? text))
            {
                length += (ulong)text.Length;
            }
        }

        return Tenths(length);
    };

    // ⌈Lᵣ/10⌉ for string(timestamp): Lᵣ is 20, or 21 plus the digits of the fraction.
    internal static readonly CelCostFunction TimestampText = static arguments =>
        arguments.Length > 0 && arguments[0].TryGetTimestamp(out CelTimestamp value)
            ? Tenths((ulong)TimeTextRules.TimestampLength(value))
            : 0;

    // ⌈Lᵣ/10⌉ for string(duration): the sign, the digits of the seconds and, with a fraction, the point and its digits, and
    // the s.
    internal static readonly CelCostFunction DurationText = static arguments =>
        arguments.Length > 0 && arguments[0].TryGetDuration(out CelDuration value)
            ? Tenths((ulong)TimeTextRules.DurationLength(value.TotalNanoseconds))
            : 0;

    // ⌈x/10⌉.
    private static ulong Tenths(ulong length) => (length + 9) / 10;
}
