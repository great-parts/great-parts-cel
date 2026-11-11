using System;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The text of time: the strict RFC 3339 form of a timestamp, the grammar and the algorithm of time.ParseDuration for a
// duration, the code of each failure, and the exact printing.
public sealed class TimeTextRulesTests
{
    [Theory]
    [InlineData("2001-09-09T01:46:40Z", 1_000_000_000L, 0)]
    [InlineData("2001-09-09T07:16:40+05:30", 1_000_000_000L, 0)]
    [InlineData("2001-09-08T20:46:40-05:00", 1_000_000_000L, 0)]
    [InlineData("2001-09-09T01:46:40-00:00", 1_000_000_000L, 0)]
    [InlineData("2001-09-09T01:46:40+00:00", 1_000_000_000L, 0)]
    [InlineData("2001-09-09T01:46:40.5Z", 1_000_000_000L, 500_000_000)]
    [InlineData("2001-09-09T01:46:40.0Z", 1_000_000_000L, 0)]
    [InlineData("2001-09-09T01:46:40.000000001Z", 1_000_000_000L, 1)]
    [InlineData("2001-09-09T01:46:40.1234567891Z", 1_000_000_000L, 123_456_789)]
    [InlineData("2001-09-09T01:46:40.9999999999999Z", 1_000_000_000L, 999_999_999)]
    [InlineData("2001-09-09T01:46:40.0000000009Z", 1_000_000_000L, 0)]
    [InlineData("1969-12-31T23:59:59.25Z", -1L, 250_000_000)]
    [InlineData("1970-01-01T00:00:00Z", 0L, 0)]
    [InlineData("2000-02-29T12:00:00Z", 951_825_600L, 0)]
    [InlineData("2024-02-29T00:00:00Z", 1_709_164_800L, 0)]
    [InlineData("0001-01-01T00:00:00Z", -62_135_596_800L, 0)]
    [InlineData("0001-01-01T05:00:00+05:00", -62_135_596_800L, 0)]
    [InlineData("0001-01-01T00:00:00.999999999Z", -62_135_596_800L, 999_999_999)]
    [InlineData("9999-12-31T23:59:59.999999999Z", 253_402_300_799L, 999_999_999)]
    [InlineData("9999-12-31T18:59:59-05:00", 253_402_300_799L, 0)]
    [InlineData("9999-12-31T22:59:59-01:00", 253_402_300_799L, 0)]
    [InlineData("9999-12-31T23:59:59-00:00", 253_402_300_799L, 0)]
    public static void Timestamp_ReadsTheStrictForm(string text, long seconds, int nanoseconds)
    {
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseTimestamp(text, out CelTimestamp value));
        Assert.Equal((seconds, nanoseconds), (value.Seconds, value.Nanoseconds));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2001-09-09")]
    [InlineData("2001-09-09T01:46:40")]
    [InlineData("2001-09-09T01:46Z")]
    [InlineData("2001-09-09t01:46:40Z")]
    [InlineData("2001-09-09T01:46:40z")]
    [InlineData("2001-09-09 01:46:40Z")]
    [InlineData("2001-09-09T01:46:40ZZ")]
    [InlineData("2001-09-09T01:46:40Z ")]
    [InlineData(" 2001-09-09T01:46:40Z")]
    [InlineData("2001-09-09T01:46:40.Z")]
    [InlineData("2001-09-09T01:46:40,5Z")]
    [InlineData("2001-09-09T01:46:40.5")]
    [InlineData("2001-09-09T01:46:40+0530")]
    [InlineData("2001-09-09T01:46:40+5:30")]
    [InlineData("2001-09-09T01:46:40+05")]
    [InlineData("2001-09-09T01:46:40+05:3")]
    [InlineData("2001-09-09T01:46:40+05:300")]
    [InlineData("2001-09-09T01:46:40+24:00")]
    [InlineData("2001-09-09T01:46:40+05:60")]
    [InlineData("2001-09-09T01:46:40*05:00")]
    [InlineData("2001-09-09T01:46:60Z")]
    [InlineData("2001-09-09T01:60:40Z")]
    [InlineData("2001-09-09T24:00:00Z")]
    [InlineData("2001-13-09T01:46:40Z")]
    [InlineData("2001-00-09T01:46:40Z")]
    [InlineData("2001-09-00T01:46:40Z")]
    [InlineData("2001-09-31T01:46:40Z")]
    [InlineData("2001-04-31T01:46:40Z")]
    [InlineData("2001-09-32T01:46:40Z")]
    [InlineData("1900-02-29T00:00:00Z")]
    [InlineData("2023-02-29T00:00:00Z")]
    [InlineData("2100-02-29T00:00:00Z")]
    [InlineData("0000-02-30T00:00:00Z")]
    [InlineData("0000-04-31T00:00:00Z")]
    [InlineData("01-09-09T01:46:40Z")]
    [InlineData("12001-09-09T01:46:40Z")]
    [InlineData("2001/09/09T01:46:40Z")]
    [InlineData("2001-09-09T01-46-40Z")]
    [InlineData("+2001-09-09T01:46:40Z")]
    [InlineData("2001-09-09T01:46:4٠Z")]
    [InlineData("٢٠٠١-09-09T01:46:40Z")]
    public static void Timestamp_RefusesWhatIsNotTheStrictForm_AsInvalid(string text)
    {
        Assert.Equal(TimeParseResult.Invalid, TimeTextRules.TryParseTimestamp(text, out CelTimestamp value));
        Assert.Equal(default, value);
    }

    // The syntax and the day of the month are checked first, then the range: a well-formed text outside the range is not Invalid.
    [Theory]
    [InlineData("0000-12-31T23:59:59Z")]
    [InlineData("0000-01-01T00:00:00Z")]
    [InlineData("0000-02-29T00:00:00Z")]
    [InlineData("0001-01-01T00:00:00+00:01")]
    [InlineData("0001-01-01T00:00:00+01:00")]
    [InlineData("0001-01-01T00:59:59+01:00")]
    [InlineData("9999-12-31T23:59:59-00:01")]
    [InlineData("9999-12-31T23:59:59-01:00")]
    public static void Timestamp_OutsideTheRange_IsOutOfRange(string text)
    {
        Assert.Equal(TimeParseResult.OutOfRange, TimeTextRules.TryParseTimestamp(text, out CelTimestamp value));
        Assert.Equal(default, value);
    }

    [Theory]
    [InlineData(MinSecondsValue, 0)]
    [InlineData(MaxSecondsValue, 999_999_999)]
    [InlineData(0L, 0)]
    [InlineData(-1L, 1)]
    [InlineData(1_234_567_890L, 120_000_000)]
    [InlineData(1_234_567_890L, 123_456_789)]
    [InlineData(1_234_567_890L, 1)]
    public static void Timestamp_PrintedTextReadsBackAndHasTheKnownLength(long seconds, int nanoseconds)
    {
        var value = new CelTimestamp(seconds, nanoseconds);
        string text = value.ToString();

        Assert.Equal(text.Length, TimeTextRules.TimestampLength(value));
        Assert.True(text.Length <= TimeTextRules.MaxTimestampLength);
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseTimestamp(text, out CelTimestamp back));
        Assert.Equal(value, back);
    }

    [Fact]
    public static void Timestamp_RandomValues_RoundTripThroughTheText()
    {
        var random = new TimeSamples(6471);
        Span<char> buffer = stackalloc char[TimeTextRules.MaxTimestampLength];
        for (int index = 0; index < 20_000; index++)
        {
            var value = new CelTimestamp(random.Next(MinSecondsValue, MaxSecondsValue + 1), random.Next(2) == 0 ? 0 : random.Next(1_000_000_000));

            int length = TimeTextRules.WriteTimestamp(value, buffer);

            Assert.Equal(TimeTextRules.TimestampLength(value), length);
            Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseTimestamp(buffer[..length], out CelTimestamp back));
            Assert.Equal(value, back);
        }
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("+0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("0s", 0L)]
    [InlineData("1s", 1_000_000_000L)]
    [InlineData("-1s", -1_000_000_000L)]
    [InlineData("+7ns", 7L)]
    [InlineData("3us", 3_000L)]
    [InlineData("3µs", 3_000L)]
    [InlineData("3μs", 3_000L)]
    [InlineData("250ms", 250_000_000L)]
    [InlineData("2m", 120_000_000_000L)]
    [InlineData("2h", 7_200_000_000_000L)]
    [InlineData("1.5s", 1_500_000_000L)]
    [InlineData(".5s", 500_000_000L)]
    [InlineData("-.5s", -500_000_000L)]
    [InlineData("5.s", 5_000_000_000L)]
    [InlineData("0.25h", 900_000_000_000L)]
    [InlineData("1.5ms", 1_500_000L)]
    [InlineData("1h30m", 5_400_000_000_000L)]
    [InlineData("-1h30m10s", -5_410_000_000_000L)]
    [InlineData("2h45m30.5s", 9_930_500_000_000L)]
    [InlineData("1s1s", 2_000_000_000L)]
    [InlineData("1m1h", 3_660_000_000_000L)]
    [InlineData("00012s", 12_000_000_000L)]
    [InlineData("1.0000000001s", 1_000_000_000L)]
    [InlineData("1.0000000009s", 1_000_000_000L)]
    [InlineData("9223372036854775807ns", long.MaxValue)]
    [InlineData("-9223372036854775808ns", long.MinValue)]
    [InlineData("-9223372036854775807ns1ns", long.MinValue)]
    [InlineData("9223372036.854775807s", long.MaxValue)]
    [InlineData("-9223372036.854775808s", long.MinValue)]
    [InlineData("2562047h47m16.854775807s", long.MaxValue)]
    public static void Duration_ReadsTheGrammarOfGo(string text, long nanoseconds)
    {
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal(nanoseconds, value.TotalNanoseconds);
    }

    // The digits of a fraction run up to 2^63 = 9223372036854775808, the last value of the fraction that Go keeps: it is kept
    // with its power of ten, the next digit and all after it are skipped, and the fraction still scales to the right unit.
    [Theory]
    [InlineData("1.9223372036854775808s", 1_922_337_203L)]
    [InlineData("0.92233720368547758089999s", 922_337_203L)]
    [InlineData("0.9223372036854775809s", 922_337_203L)]
    [InlineData("0.9223372036854775808h", 3_320_413_933_267L)]
    [InlineData("0.9223372036854775808m", 55_340_232_221L)]
    [InlineData("0.9223372036854775808ms", 922_337L)]
    [InlineData("-0.9223372036854775808s", -922_337_203L)]
    public static void Duration_FractionOfTwoToThe63_IsKeptAndScaled(string text, long nanoseconds)
    {
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal(nanoseconds, value.TotalNanoseconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("s")]
    [InlineData(".s")]
    [InlineData("-.s")]
    [InlineData(".")]
    [InlineData("1")]
    [InlineData("00")]
    [InlineData("1.5")]
    [InlineData("1x")]
    [InlineData("1S")]
    [InlineData("1H")]
    [InlineData("1sec")]
    [InlineData("1d")]
    [InlineData("1µ")]
    [InlineData("1 s")]
    [InlineData("1s ")]
    [InlineData(" 1s")]
    [InlineData("1h30")]
    [InlineData("1h-1m")]
    [InlineData("1h 30m")]
    [InlineData("1ms1")]
    [InlineData("1.2.3s")]
    [InlineData("--1s")]
    [InlineData("+-1s")]
    [InlineData("١s")]
    [InlineData("1٠s")]
    [InlineData("1nsµs")]
    [InlineData("0 ")]
    public static void Duration_RefusesWhatIsNotTheGrammar_AsInvalid(string text)
    {
        Assert.Equal(TimeParseResult.Invalid, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal(0L, value.TotalNanoseconds);
    }

    // Each of these overflows at one step of the algorithm; the digits of the whole part overflow before the unit is read.
    [Theory]
    [InlineData("9223372036854775808ns")]
    [InlineData("9223372036854775809ns")]
    [InlineData("-9223372036854775809ns")]
    [InlineData("99999999999999999999ns")]
    [InlineData("99999999999999999999x")]
    [InlineData("99999999999999999999")]
    [InlineData("9223372036855s")]
    [InlineData("2562048h")]
    [InlineData("-2562048h")]
    [InlineData("9223372036854775807ns1ns")]
    [InlineData("-9223372036854775808ns1ns")]
    [InlineData("9223372036854775807ns9223372036854775807ns")]
    [InlineData("1000000000000s1000000000000s")]
    [InlineData("9223372036.854775808s")]
    [InlineData("9223372036.99999s")]
    public static void Duration_Overflow_IsOutOfRange(string text)
    {
        Assert.Equal(TimeParseResult.OutOfRange, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal(0L, value.TotalNanoseconds);
    }

    // The sum of two groups of exactly 2^63 wraps at 2^64 in the uint64 of Go, and the wrapped zero passes the check: the
    // algorithm is kept as it is, so that the library reads such a text as time.ParseDuration does.
    [Fact]
    public static void Duration_TwoGroupsOfTwoToThe63_WrapLikeGo()
    {
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration("9223372036854775808ns9223372036854775808ns", out CelDuration value));
        Assert.Equal(0L, value.TotalNanoseconds);
    }

    // The one floating-point step is the fraction: its product with the unit is cut, as the uint64 conversion of Go cuts it,
    // so a long run of nines rounds up through the double, and digits beyond the 19th are skipped.
    [Theory]
    [InlineData("0.123456789012345678901234s", 123_456_789L)]
    [InlineData("0.9999999999999999999s", 1_000_000_000L)]
    [InlineData("1.999999999999s", 1_999_999_999L)]
    [InlineData("0.1h", 360_000_000_000L)]
    [InlineData("0.3h", 1_080_000_000_000L)]
    [InlineData("0.7h", 2_520_000_000_000L)]
    [InlineData("1.7ns", 1L)]
    public static void Duration_Fraction_FollowsTheFloatingPointStepOfGo(string text, long nanoseconds)
    {
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal(nanoseconds, value.TotalNanoseconds);
    }

    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(1L, "0.000000001s")]
    [InlineData(1_500_000_000L, "1.5s")]
    [InlineData(-1_500_000_000L, "-1.5s")]
    [InlineData(60_001_000_000L, "60.001s")]
    [InlineData(long.MaxValue, "9223372036.854775807s")]
    [InlineData(long.MinValue, "-9223372036.854775808s")]
    public static void Duration_PrintedTextHasTheKnownLength_AndReadsBack(long nanoseconds, string expected)
    {
        var value = new CelDuration(nanoseconds);
        string text = value.ToString();

        Assert.Equal(expected, text);
        Assert.Equal(text.Length, TimeTextRules.DurationLength(nanoseconds));
        Assert.True(text.Length <= TimeTextRules.MaxDurationLength);
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(text, out CelDuration back));
        Assert.Equal(value, back);
    }

    [Fact]
    public static void Duration_RandomValues_RoundTripThroughTheText()
    {
        var random = new TimeSamples(6472);
        Span<char> buffer = stackalloc char[TimeTextRules.MaxDurationLength];
        for (int index = 0; index < 20_000; index++)
        {
            long nanoseconds = random.Next(3) == 0 ? random.Next(-5_000_000_000, 5_000_000_000) : unchecked((long)random.NextBits());

            int length = TimeTextRules.WriteDuration(nanoseconds, buffer);

            Assert.Equal(TimeTextRules.DurationLength(nanoseconds), length);
            Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(buffer[..length], out CelDuration back));
            Assert.Equal(nanoseconds, back.TotalNanoseconds);
        }
    }

    private const long MinSecondsValue = -62_135_596_800;
    private const long MaxSecondsValue = 253_402_300_799;
}
