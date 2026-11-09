using System.Collections.Generic;
using System.Linq;
using System.Text;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The strings and numbers of time that the generator writes into expressions (docs/design/eng-difftest.md, "Profile", "Time"):
/// strings of <c>timestamp</c> and of <c>duration</c> from their boundaries, mutated now and then so that errors occur; the
/// <c>int</c> of <c>timestamp(int)</c>; the zone arguments of the receivers. A string of time has at most
/// <see cref="MaxCodePoints"/> code points.
/// </summary>
internal static class TimeStrings
{
    /// <summary>The largest number of Unicode scalar values of a string of time and of a zone argument.</summary>
    internal const int MaxCodePoints = 40;

    // A string is mutated with a chance of one in this.
    private const int MutatedOneIn = 4;

    private static readonly string[] Years = ["0000", "0001", "1900", "2000", "2023", "2024", "9999"];
    private static readonly string[] Months = ["00", "01", "02", "12", "13"];
    private static readonly string[] Days = ["00", "01", "28", "29", "30", "31", "32"];
    private static readonly string[] Hours = ["00", "23", "24"];
    private static readonly string[] MinutesAndSeconds = ["00", "59", "60"];
    private static readonly string[] Offsets = ["Z", "+00:00", "-00:00", "+23:59", "-23:59", "+24:00", "+03:60"];

    // The numbers of a term of a duration, among them the limits of the nanoseconds of an int64.
    private static readonly string[] DurationNumbers =
        ["0", "1", "9223372036854775807", "9223372036854775808", "2562047", "2562048", "1.5", "0.001", ".5", "5.", "000", "72"];

    private static readonly string[] Units = ["ns", "us", "µs", "μs", "ms", "s", "m", "h"];
    private static readonly string[] WrongUnits = ["H", "d", "x", string.Empty];

    private const long LowestSecond = -62_135_596_800;
    private const long HighestSecond = 253_402_300_799;

    // The ints around the limits of the range of a timestamp.
    private static readonly long[] TimestampIntegers =
    [
        0, 1, -1, LowestSecond, HighestSecond, LowestSecond - 1, HighestSecond + 1, LowestSecond + 1, HighestSecond - 1, long.MinValue, long.MaxValue,
    ];

    private static readonly string[] FixedOffsets = ["+05:30", "-03:00", "00:00"];
    private static readonly string[] BadOffsets = ["+1:5", "+001:30", "+24:00", "+03:60", ":"];

    /// <summary>A string for <c>timestamp(string)</c>.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string Timestamp(PCG pcg)
    {
        StringBuilder text = new();
        text.Append(pcg.Pick(Years)).Append('-').Append(pcg.Pick(Months)).Append('-').Append(pcg.Pick(Days));
        text.Append('T').Append(pcg.Pick(Hours)).Append(':').Append(pcg.Pick(MinutesAndSeconds)).Append(':').Append(pcg.Pick(MinutesAndSeconds));
        int fraction = pcg.Below(4) == 0 ? pcg.Below(13) : 0;
        if (fraction > 0)
        {
            text.Append('.');
            for (int i = 0; i < fraction; i++)
            {
                text.Append((char)('0' + pcg.Below(10)));
            }
        }

        text.Append(pcg.Pick(Offsets));
        return pcg.OneIn(MutatedOneIn) ? MutateTimestamp(pcg, text.ToString()) : text.ToString();
    }

    /// <summary>A string for <c>duration(string)</c>, at most <see cref="MaxCodePoints"/> code points.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string Duration(PCG pcg)
    {
        if (pcg.OneIn(10))
        {
            return pcg.Pick<string>(["0", "+0", "-0"]);
        }

        string sign = pcg.Below(4) switch
        {
            0 => "-",
            1 => "+",
            _ => string.Empty,
        };
        List<string> terms = [];
        int count = 1 + pcg.Below(3);
        int length = sign.Length;
        for (int i = 0; i < count; i++)
        {
            string term = Number(pcg) + (pcg.OneIn(MutatedOneIn * 2) ? pcg.Pick(WrongUnits) : pcg.Pick(Units));
            if (terms.Count > 0 && length + term.Length > MaxCodePoints)
            {
                break;
            }

            terms.Add(term);
            length += term.Length;
        }

        return sign + string.Concat(terms);
    }

    /// <summary>The <c>int</c> of <c>timestamp(int)</c>: the limits of the range and the numbers next to them, or a random 40-bit number.</summary>
    /// <param name="pcg">The random source.</param>
    internal static long TimestampInteger(PCG pcg) =>
        pcg.OneIn(4) ? (long)(pcg.Next64() >> 24) * (pcg.OneIn(2) ? 1 : -1) : pcg.Pick(TimestampIntegers);

    /// <summary>
    /// A zone argument of a receiver: a zone name of the data file, <c>UTC</c>, a fixed offset, a form outside the grammar of
    /// offsets, <c>""</c> or <c>Local</c>, a name of the file with the case changed, or a name that no file has.
    /// </summary>
    /// <param name="pcg">The random source.</param>
    internal static string Zone(PCG pcg) => pcg.Below(10) switch
    {
        3 => "UTC",
        4 => pcg.Pick(FixedOffsets),
        5 => pcg.Pick(BadOffsets),
        6 => pcg.Pick<string>([string.Empty, "Local"]),
        7 => ChangeCase(pcg.Pick(ZoneData.Zones)),
        8 => "Mars/Base",
        _ => pcg.Pick(ZoneData.Zones),
    };

    // A name with the case of its ASCII letters changed: upper case when it has a lower case letter, lower case otherwise.
    private static string ChangeCase(string name) =>
        name.Any(char.IsAsciiLetterLower)
            ? name.ToUpperInvariant()
            : string.Create(name.Length, name, static (span, source) =>
            {
                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = char.IsAsciiLetterUpper(source[i]) ? (char)(source[i] | 0x20) : source[i];
                }
            });

    // The number of a term: a boundary, or 1 to 20 digits with a point now and then.
    private static string Number(PCG pcg)
    {
        if (pcg.OneIn(2))
        {
            return pcg.Pick(DurationNumbers);
        }

        string digits = string.Concat(Enumerable.Range(0, 1 + pcg.Below(pcg.OneIn(4) ? 20 : 3)).Select(_ => (char)('0' + pcg.Below(10))));
        return pcg.Below(4) switch
        {
            0 => digits + ".",
            1 => "." + digits,
            2 => digits + "." + string.Concat(Enumerable.Range(0, pcg.Below(10)).Select(_ => (char)('0' + pcg.Below(10)))),
            _ => digits,
        };
    }

    // A lower case t or z, a space for T, no zone, a point with no digit, an extra character, or a missing sign.
    private static string MutateTimestamp(PCG pcg, string text)
    {
        int zone = text.LastIndexOfAny(['Z', '+', '-']);
        bool signed = zone > 0 && text[zone] != 'Z';
        return pcg.Below(6) switch
        {
            0 => text.Replace('T', 't'),
            1 when text.EndsWith('Z') => text[..^1] + "z",
            2 => text.Replace('T', ' '),
            3 => text[..zone],
            4 => text[..zone] + "." + text[zone..],
            _ when signed => text[..zone] + text[(zone + 1)..],
            _ => text + pcg.Pick<string>(["x", " ", "Z"]),
        };
    }
}
