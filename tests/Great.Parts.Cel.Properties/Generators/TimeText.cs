using System.Text;
using CsCheck;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Properties.Generators;

/// <summary>
/// The strings, numbers and values of time of <see cref="TypedExpressions"/> (docs/design/tests.md, "Property tests and the seed",
/// the part of time): strings of <c>timestamp</c> and of <c>duration</c> from their boundaries, valid and mutated so that errors
/// occur, the <c>int</c> of <c>timestamp(int)</c> near the limits of the range, the pool of zone names of the receivers, and the
/// bindings of <c>ts</c> and <c>du</c> at the limits of their ranges and next to them.
/// </summary>
internal static class TimeText
{
    private const long LowestSecond = -62_135_596_800;
    private const long HighestSecond = 253_402_300_799;

    // A string is mutated with a chance of one in this.
    private const uint MutatedOneIn = 4;

    private static readonly string[] Years = ["0000", "0001", "1900", "2000", "2023", "2024", "9999"];
    private static readonly string[] Months = ["00", "01", "02", "12", "13"];
    private static readonly string[] Days = ["00", "01", "28", "29", "30", "31", "32"];
    private static readonly string[] Hours = ["00", "23", "24"];
    private static readonly string[] MinutesAndSeconds = ["00", "59", "60"];
    private static readonly string[] Offsets = ["Z", "+00:00", "-00:00", "+23:59", "-23:59", "+24:00", "+03:60"];

    // The numbers of a term of a duration, among them the limits of the nanoseconds of an int64.
    private static readonly string[] DurationNumbers =
        ["0", "1", "9223372036854775807", "9223372036854775808", "2562047", "2562048", "1.5", "0.001", ".5", "5.", "000", "72"];

    private static readonly string[] BareDurations = ["0", "+0", "-0"];

    private static readonly string[] Units = ["ns", "us", "µs", "μs", "ms", "s", "m", "h"];
    private static readonly string[] WrongUnits = ["H", "d", "x", ""];

    private static readonly string[] Extras = ["x", " ", "Z"];

    // The ints around the limits of the range of a timestamp.
    private static readonly long[] TimestampIntegers =
    [
        0, 1, -1, LowestSecond, HighestSecond, LowestSecond - 1, HighestSecond + 1, LowestSecond + 1, HighestSecond - 1, long.MinValue, long.MaxValue,
    ];

    // The zone names of the receivers: names of the database, UTC, offsets in and outside the grammar, names that give errors.
    private static readonly string[] Zones =
        ["UTC", "+05:45", "-03:30", "Asia/Kathmandu", "America/New_York", "Mars/Base", "", "+1:5"];

    // The seconds of a bound timestamp: the limits of the range, zero and the seconds next to them.
    private static readonly long[] BoundSeconds = [LowestSecond, HighestSecond, 0, 1, -1, LowestSecond + 1, HighestSecond - 1];

    private static readonly int[] BoundNanoseconds = [0, 1, 999_999_999];

    // The nanoseconds of a bound duration: zero, the limits of the type and the values next to them.
    private static readonly long[] BoundDurations =
        [0, 1, -1, long.MinValue, long.MaxValue, long.MinValue + 1, long.MaxValue - 1, 1_000_000_000, -1_000_000_000];

    /// <summary>A string for <c>timestamp(string)</c>, valid or mutated.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The text, without quotes.</returns>
    internal static string Timestamp(PCG random)
    {
        var text = new StringBuilder();
        text.Append(Pick(random, Years)).Append('-').Append(Pick(random, Months)).Append('-').Append(Pick(random, Days));
        text.Append('T').Append(Pick(random, Hours)).Append(':').Append(Pick(random, MinutesAndSeconds)).Append(':').Append(Pick(random, MinutesAndSeconds));
        int fraction = random.Next(4) == 0 ? (int)random.Next(13) : 0;
        if (fraction > 0)
        {
            text.Append('.');
            for (int index = 0; index < fraction; index++)
            {
                text.Append((char)('0' + (int)random.Next(10)));
            }
        }

        text.Append(Pick(random, Offsets));
        return random.Next(MutatedOneIn) == 0 ? MutatedTimestamp(random, text.ToString()) : text.ToString();
    }

    /// <summary>A string for <c>duration(string)</c>, valid or wrong.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The text, without quotes.</returns>
    internal static string Duration(PCG random)
    {
        if (random.Next(10) == 0)
        {
            return Pick(random, BareDurations);
        }

        string sign = random.Next(4) switch
        {
            0 => "-",
            1 => "+",
            _ => "",
        };
        var text = new StringBuilder(sign);
        int terms = 1 + (int)random.Next(3);
        for (int index = 0; index < terms; index++)
        {
            text.Append(Pick(random, DurationNumbers));
            text.Append(random.Next(MutatedOneIn * 2) == 0 ? Pick(random, WrongUnits) : Pick(random, Units));
        }

        return text.ToString();
    }

    /// <summary>The <c>int</c> of <c>timestamp(int)</c>: the limits of the range and the numbers next to them.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The number.</returns>
    internal static long TimestampInteger(PCG random) => TimestampIntegers[random.Next((uint)TimestampIntegers.Length)];

    /// <summary>A zone name of the pool of the receivers.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The name, without quotes.</returns>
    internal static string Zone(PCG random) => Pick(random, Zones);

    /// <summary>A timestamp at the limits of its range or next to them.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The value.</returns>
    internal static CelValue BoundTimestamp(PCG random) =>
        CelValue.FromTimestamp(new CelTimestamp(Pick(random, BoundSeconds), Pick(random, BoundNanoseconds)));

    /// <summary>A duration at the limits of its range or next to them.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The value.</returns>
    internal static CelValue BoundDuration(PCG random) =>
        CelValue.FromDuration(new CelDuration(BoundDurations[random.Next((uint)BoundDurations.Length)]));

    private static T Pick<T>(PCG random, T[] choices) => choices[(int)random.Next((uint)choices.Length)];

    // A lower case t or z, a space for T, no zone, a point with no digit, an extra character, or a missing sign.
    private static string MutatedTimestamp(PCG random, string text)
    {
        int zone = text.LastIndexOfAny(['Z', '+', '-']);
        bool signed = text[zone] != 'Z';
        return random.Next(6) switch
        {
            0 => text[..10] + "t" + text[11..],
            1 when !signed => text[..zone] + "z",
            2 => text[..10] + " " + text[11..],
            3 => text[..zone],
            4 => text[..zone] + "." + text[zone..],
            _ when signed => text[..zone] + text[(zone + 1)..],
            _ => text + Pick(random, Extras),
        };
    }
}
