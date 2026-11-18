using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The range of a duration is that of google.protobuf.Duration: seconds from -315 576 000 000 to 315 576 000 000 and nanoseconds from
// -999 999 999 to 999 999 999 with the sign of the seconds. The structure, its text, the reading of the text, the arithmetic and the
// conversions at and around the limits.
public sealed class DurationRangeTests
{
    private const long MaxSeconds = 315_576_000_000;
    private const long TicksPerSecond = 10_000_000;

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(5L, 0)]
    [InlineData(-5L, 0)]
    [InlineData(1L, 1)]
    [InlineData(-1L, -1)]
    [InlineData(0L, 999_999_999)]
    [InlineData(0L, -999_999_999)]
    [InlineData(MaxSeconds, 999_999_999)]
    [InlineData(-MaxSeconds, -999_999_999)]
    public static void Constructor_AcceptsTheWholeRangeOfProtobuf(long seconds, int nanoseconds)
    {
        var value = new CelDuration(seconds, nanoseconds);

        Assert.Equal((seconds, nanoseconds), (value.Seconds, value.Nanoseconds));
    }

    [Theory]
    [InlineData(MaxSeconds + 1, 0)]
    [InlineData(-MaxSeconds - 1, 0)]
    [InlineData(long.MaxValue, 0)]
    [InlineData(long.MinValue, 0)]
    [InlineData(0L, 1_000_000_000)]
    [InlineData(0L, -1_000_000_000)]
    [InlineData(1L, -1)]
    [InlineData(-1L, 1)]
    public static void Constructor_RefusesWhatProtobufRefuses(long seconds, int nanoseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelDuration(seconds, nanoseconds));
    }

    [Theory]
    [InlineData(0L, 0L, 0)]
    [InlineData(1_500_000_000L, 1L, 500_000_000)]
    [InlineData(-1_500_000_000L, -1L, -500_000_000)]
    [InlineData(999_999_999L, 0L, 999_999_999)]
    [InlineData(-999_999_999L, 0L, -999_999_999)]
    [InlineData(long.MaxValue, 9_223_372_036L, 854_775_807)]
    [InlineData(long.MinValue, -9_223_372_036L, -854_775_808)]
    public static void ConstructorOfNanoseconds_SplitsThemIntoSecondsAndNanoseconds(long total, long seconds, int nanoseconds)
    {
        var value = new CelDuration(total);

        Assert.Equal((seconds, nanoseconds), (value.Seconds, value.Nanoseconds));
        Assert.Equal(total, value.TotalNanoseconds);
        Assert.Equal(new CelDuration(seconds, nanoseconds), value);
    }

    [Theory]
    [InlineData(MaxSeconds, 0)]
    [InlineData(-MaxSeconds, 0)]
    [InlineData(9_223_372_037L, 0)]
    [InlineData(9_223_372_036L, 854_775_808)]
    [InlineData(-9_223_372_036L, -854_775_809)]
    [InlineData(-9_223_372_037L, 0)]
    public static void TotalNanoseconds_OutOfAnInt64_Throws(long seconds, int nanoseconds)
    {
        var value = new CelDuration(seconds, nanoseconds);

        Assert.Throws<InvalidOperationException>(() => value.TotalNanoseconds);
    }

    [Fact]
    public static void FromTimeSpan_TakesTheWholeRangeOfADuration_AndRefusesWhatIsLonger()
    {
        long most = (MaxSeconds * TicksPerSecond) + 9_999_999;

        Assert.Equal(new CelDuration(MaxSeconds, 999_999_900), CelDuration.FromTimeSpan(TimeSpan.FromTicks(most)));
        Assert.Equal(new CelDuration(-MaxSeconds, -999_999_900), CelDuration.FromTimeSpan(TimeSpan.FromTicks(-most)));
        Assert.Equal(new CelDuration(MaxSeconds, 0), CelDuration.FromTimeSpan(TimeSpan.FromTicks(MaxSeconds * TicksPerSecond)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.FromTicks(most + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.FromTicks(-most - 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.MinValue));
    }

    [Fact]
    public static void TryGetTimeSpan_HoldsTheWholeRange_ExactlyForMultiplesOf100Nanoseconds()
    {
        Assert.True(new CelDuration(MaxSeconds, 999_999_900).TryGetTimeSpan(out TimeSpan longest));
        Assert.Equal((MaxSeconds * TicksPerSecond) + 9_999_999, longest.Ticks);
        Assert.True(new CelDuration(-MaxSeconds, -999_999_900).TryGetTimeSpan(out TimeSpan shortest));
        Assert.Equal(-((MaxSeconds * TicksPerSecond) + 9_999_999), shortest.Ticks);

        Assert.False(new CelDuration(MaxSeconds, 999_999_999).TryGetTimeSpan(out TimeSpan none));
        Assert.Equal(TimeSpan.Zero, none);
    }

    [Fact]
    public static void Comparison_IsByLength_AlsoWhenTheSecondsAreEqual()
    {
        CelDuration[] ascending =
        [
            new(-MaxSeconds, -999_999_999),
            new(-MaxSeconds, 0),
            new(-1, -500_000_000),
            new(-1, 0),
            new(0, -1),
            new(0, 0),
            new(0, 1),
            new(1, 0),
            new(1, 500_000_000),
            new(MaxSeconds, 999_999_999),
        ];

        for (int left = 0; left < ascending.Length; left++)
        {
            for (int right = 0; right < ascending.Length; right++)
            {
                Assert.Equal(left.CompareTo(right), Math.Sign(ascending[left].CompareTo(ascending[right])));
                Assert.Equal(left < right, ascending[left] < ascending[right]);
                Assert.Equal(left >= right, ascending[left] >= ascending[right]);
                Assert.Equal(left == right, ascending[left] == ascending[right]);
                Assert.Equal(left == right, ascending[left].GetHashCode() == ascending[right].GetHashCode() && ascending[left].Equals(ascending[right]));
            }
        }
    }

    [Fact]
    public static void ValueOfADuration_KeepsSecondsAndNanoseconds_AndComparesBoth()
    {
        var negative = new CelDuration(-3, -250_000_000);
        CelValue value = CelValue.FromDuration(negative);

        Assert.True(value.TryGetDuration(out CelDuration read));
        Assert.Equal(negative, read);
        Assert.Equal(value, CelValue.FromDuration(new CelDuration(-3, -250_000_000)));
        Assert.Equal(value.GetHashCode(), CelValue.FromDuration(new CelDuration(-3, -250_000_000)).GetHashCode());
        Assert.NotEqual(value, CelValue.FromDuration(new CelDuration(-3, -250_000_001)));
        Assert.NotEqual(value, CelValue.FromDuration(new CelDuration(-3, 0)));
        Assert.NotEqual(value, CelValue.FromDuration(new CelDuration(3, 250_000_000)));

        CelValue longest = CelValue.FromDuration(new CelDuration(MaxSeconds, 999_999_999));
        Assert.True(longest.TryGetDuration(out CelDuration longestRead));
        Assert.Equal((MaxSeconds, 999_999_999), (longestRead.Seconds, longestRead.Nanoseconds));
    }

    [Theory]
    [InlineData(MaxSeconds, 999_999_999, "315576000000.999999999s")]
    [InlineData(-MaxSeconds, -999_999_999, "-315576000000.999999999s")]
    [InlineData(MaxSeconds, 0, "315576000000s")]
    [InlineData(0L, -1, "-0.000000001s")]
    [InlineData(-1L, -500_000_000, "-1.5s")]
    [InlineData(0L, 0, "0s")]
    public static void Text_OfTheLimits_IsExact_AndReadsBack(long seconds, int nanoseconds, string expected)
    {
        var value = new CelDuration(seconds, nanoseconds);
        string text = value.ToString();

        Assert.Equal(expected, text);
        Assert.Equal(text.Length, TimeTextRules.DurationLength(value));
        Assert.True(text.Length <= TimeTextRules.MaxDurationLength);
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(text, out CelDuration back));
        Assert.Equal(value, back);
    }

    [Theory]
    [InlineData("315576000000s999999999ns", MaxSeconds, 999_999_999)]
    [InlineData("-315576000000s999999999ns", -MaxSeconds, -999_999_999)]
    [InlineData("315576000000.999999999s", MaxSeconds, 999_999_999)]
    [InlineData("315576000000999999999ns", MaxSeconds, 999_999_999)]
    [InlineData("-315576000000999999999ns", -MaxSeconds, -999_999_999)]
    [InlineData("87660000h", MaxSeconds, 0)]
    [InlineData("87660000h999999999ns", MaxSeconds, 999_999_999)]
    [InlineData("9223372036854775808ns", 9_223_372_036L, 854_775_808)]
    [InlineData("-9223372036854775809ns", -9_223_372_036L, -854_775_809)]
    [InlineData("9223372036854775808ns9223372036854775808ns", 18_446_744_073L, 709_551_616)]
    public static void Duration_UpToTheRangeOfProtobuf_IsRead(string text, long seconds, int nanoseconds)
    {
        Assert.Equal(TimeParseResult.Parsed, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal((seconds, nanoseconds), (value.Seconds, value.Nanoseconds));
    }

    [Theory]
    [InlineData("315576000001s")]
    [InlineData("-315576000001s")]
    [InlineData("315576000000s1000000000ns")]
    [InlineData("-315576000000s1000000000ns")]
    [InlineData("315576000001000000000ns")]
    [InlineData("87660000h1s")]
    [InlineData("87660001h")]
    [InlineData("999999999999999999999ns")]
    [InlineData("999999999999999999999999999999999999999999999")]
    [InlineData("315576000000s999999999ns1ns")]
    public static void Duration_AboveTheRangeOfProtobuf_IsOutOfRange(string text)
    {
        Assert.Equal(TimeParseResult.OutOfRange, TimeTextRules.TryParseDuration(text, out CelDuration value));
        Assert.Equal(default, value);
    }

    // The digits of the whole part are read before the unit: a number below the longest duration in nanoseconds is no overflow, and a text
    // that then has no unit is not of the grammar.
    [Fact]
    public static void Duration_NumberWithoutUnit_IsInvalid_WhenItIsWithinTheRange()
    {
        Assert.Equal(TimeParseResult.Invalid, TimeTextRules.TryParseDuration("99999999999999999999", out _));
        Assert.Equal(TimeParseResult.Invalid, TimeTextRules.TryParseDuration("99999999999999999999x", out _));
    }

    [Fact]
    public static void Addition_CarriesAndBorrowsBetweenSecondsAndNanoseconds()
    {
        Assert.Equal((3L, 0), Parts("add_duration_duration", Du(1, 500_000_000), Du(1, 500_000_000)));
        Assert.Equal((0L, 999_999_999), Parts("add_duration_duration", Du(1, 0), Du(0, -1)));
        Assert.Equal((0L, -900_000_000), Parts("add_duration_duration", Du(-1, -500_000_000), Du(0, 600_000_000)));
        Assert.Equal((-1L, -500_000_000), Parts("add_duration_duration", Du(1, 500_000_000), Du(-3, 0)));
        Assert.Equal((MaxSeconds, 999_999_999), Parts("add_duration_duration", Du(MaxSeconds, 0), Du(0, 999_999_999)));
        Assert.Equal((-MaxSeconds, -999_999_999), Parts("add_duration_duration", Du(-MaxSeconds, 0), Du(0, -999_999_999)));
        Assert.Equal((0L, 0), Parts("add_duration_duration", Du(MaxSeconds, 999_999_999), Du(-MaxSeconds, -999_999_999)));
    }

    [Fact]
    public static void Subtraction_CarriesAndBorrowsBetweenSecondsAndNanoseconds()
    {
        Assert.Equal((MaxSeconds, 999_999_999), Parts("subtract_duration_duration", Du(MaxSeconds, 0), Du(0, -999_999_999)));
        Assert.Equal((-MaxSeconds, -999_999_999), Parts("subtract_duration_duration", Du(-MaxSeconds, 0), Du(0, 999_999_999)));
        Assert.Equal((0L, 0), Parts("subtract_duration_duration", Du(MaxSeconds, 999_999_999), Du(MaxSeconds, 999_999_999)));
        Assert.Equal((0L, -500_000_000), Parts("subtract_duration_duration", Du(1, 0), Du(1, 500_000_000)));
        Assert.Equal((0L, 500_000_000), Parts("subtract_duration_duration", Du(1, 0), Du(0, 500_000_000)));
        Assert.Equal((MaxSeconds, 0), Parts("subtract_duration_duration", Du(0, 0), Du(-MaxSeconds, 0)));
    }

    [Theory]
    [InlineData("add_duration_duration", MaxSeconds, 999_999_999, 0L, 1)]
    [InlineData("add_duration_duration", -MaxSeconds, -999_999_999, 0L, -1)]
    [InlineData("add_duration_duration", MaxSeconds, 0, 1L, 0)]
    [InlineData("add_duration_duration", -200_000_000_000L, 0, -200_000_000_000L, 0)]
    [InlineData("subtract_duration_duration", MaxSeconds, 999_999_999, 0L, -1)]
    [InlineData("subtract_duration_duration", -MaxSeconds, -999_999_999, 0L, 1)]
    [InlineData("subtract_duration_duration", MaxSeconds, 0, -1L, 0)]
    public static void ArithmeticOutOfTheRange_IsAnOverflowError(string overload, long leftSeconds, int leftNanoseconds, long rightSeconds, int rightNanoseconds)
    {
        CelValue result = TimeCalls.Call(overload, Du(leftSeconds, leftNanoseconds), Du(rightSeconds, rightNanoseconds));

        Assert.Equal((CelErrorCode.Overflow, TimeCalls.Offset, "duration overflow"), TimeCalls.Error(result));
    }

    [Fact]
    public static void DifferenceOfTwoTimestamps_NeverOverflows_AndKeepsTheNanoseconds()
    {
        const long lowest = -62_135_596_800;
        const long highest = 253_402_300_799;

        Assert.Equal((315_537_897_599L, 0), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(highest), TimeCalls.Ts(lowest)));
        Assert.Equal((-315_537_897_599L, 0), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(lowest), TimeCalls.Ts(highest)));
        Assert.Equal((315_537_897_599L, 999_999_999), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(highest, 999_999_999), TimeCalls.Ts(lowest)));
        Assert.Equal((-315_537_897_599L, -999_999_999), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(lowest), TimeCalls.Ts(highest, 999_999_999)));
        Assert.Equal((0L, 999_999_999), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(5, 999_999_999), TimeCalls.Ts(5)));
        Assert.Equal((0L, -999_999_999), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(5), TimeCalls.Ts(5, 999_999_999)));
        Assert.Equal((0L, 1), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(6), TimeCalls.Ts(5, 999_999_999)));
        Assert.Equal((-1L, -1), Parts("subtract_timestamp_timestamp", TimeCalls.Ts(5, 999_999_999), TimeCalls.Ts(7)));
    }

    [Fact]
    public static void ShiftOfATimestamp_ByTheLongestDuration_ReachesTheOtherEndOfTheRange()
    {
        const long lowest = -62_135_596_800;
        const long highest = 253_402_300_799;
        CelValue span = Du(315_537_897_599L, 999_999_999);

        Assert.Equal((highest, 999_999_999), TimeCalls.TimestampOf(TimeCalls.Call("add_timestamp_duration", TimeCalls.Ts(lowest), span)));
        Assert.Equal((highest, 999_999_999), TimeCalls.TimestampOf(TimeCalls.Call("add_duration_timestamp", span, TimeCalls.Ts(lowest))));
        Assert.Equal((lowest, 0), TimeCalls.TimestampOf(TimeCalls.Call("subtract_timestamp_duration", TimeCalls.Ts(highest, 999_999_999), span)));
        Assert.Equal((highest, 999_999_999), TimeCalls.TimestampOf(TimeCalls.Call("subtract_timestamp_duration", TimeCalls.Ts(lowest), Du(-315_537_897_599L, -999_999_999))));
        Assert.Equal((-1L, 999_999_999), TimeCalls.TimestampOf(TimeCalls.Call("add_timestamp_duration", TimeCalls.Ts(0), Du(0, -1))));
        Assert.Equal((1L, 0), TimeCalls.TimestampOf(TimeCalls.Call("add_timestamp_duration", TimeCalls.Ts(0, 999_999_999), Du(0, 1))));
        Assert.Equal((0L, 999_999_999), TimeCalls.TimestampOf(TimeCalls.Call("subtract_timestamp_duration", TimeCalls.Ts(1), Du(0, 1))));
    }

    [Fact]
    public static void ShiftOfATimestamp_OutOfItsRange_IsAnOverflowError()
    {
        const long highest = 253_402_300_799;

        Assert.Equal(
            (CelErrorCode.Overflow, TimeCalls.Offset, "timestamp overflow"),
            TimeCalls.Error(TimeCalls.Call("add_timestamp_duration", TimeCalls.Ts(highest, 999_999_999), Du(0, 1))));
        Assert.Equal(
            (CelErrorCode.Overflow, TimeCalls.Offset, "timestamp overflow"),
            TimeCalls.Error(TimeCalls.Call("add_timestamp_duration", TimeCalls.Ts(highest), Du(MaxSeconds, 999_999_999))));
        Assert.Equal(
            (CelErrorCode.Overflow, TimeCalls.Offset, "timestamp overflow"),
            TimeCalls.Error(TimeCalls.Call("subtract_timestamp_duration", TimeCalls.Ts(-62_135_596_800), Du(MaxSeconds, 999_999_999))));
    }

    [Fact]
    public static void IntOfADuration_IsItsNanoseconds_UntilTheyDoNotFitAnInt()
    {
        Assert.Equal(long.MaxValue, TimeCalls.IntOf(TimeCalls.Call("duration_to_int64", Du(9_223_372_036L, 854_775_807))));
        Assert.Equal(long.MinValue, TimeCalls.IntOf(TimeCalls.Call("duration_to_int64", Du(-9_223_372_036L, -854_775_808))));

        Assert.Equal(
            (CelErrorCode.ConversionRange, TimeCalls.Offset, "9223372036.854775808s is out of the range of int"),
            TimeCalls.Error(TimeCalls.Call("duration_to_int64", Du(9_223_372_036L, 854_775_808))));
        Assert.Equal(
            (CelErrorCode.ConversionRange, TimeCalls.Offset, "-315576000000.999999999s is out of the range of int"),
            TimeCalls.Error(TimeCalls.Call("duration_to_int64", Du(-MaxSeconds, -999_999_999))));
    }

    [Fact]
    public static void StringConversions_AtTheLimits()
    {
        Assert.Equal("315576000000.999999999s", TimeCalls.StringOf(TimeCalls.Call("duration_to_string", Du(MaxSeconds, 999_999_999))));
        Assert.Equal("-315576000000.999999999s", TimeCalls.StringOf(TimeCalls.Call("duration_to_string", Du(-MaxSeconds, -999_999_999))));
        Assert.Equal((MaxSeconds, 999_999_999), Parts("string_to_duration", TimeCalls.Str("315576000000s999999999ns")));
        Assert.Equal(
            (CelErrorCode.ConversionRange, TimeCalls.Offset, "315576000001s is out of the range of duration"),
            TimeCalls.Error(TimeCalls.Call("string_to_duration", TimeCalls.Str("315576000001s"))));
    }

    [Fact]
    public static void ReceiversOfADuration_ReadTheSecondsAndTheNanoseconds()
    {
        CelValue longest = Du(MaxSeconds, 999_999_999);
        CelValue shortest = Du(-MaxSeconds, -999_999_999);

        Assert.Equal(87_660_000L, TimeCalls.IntOf(TimeCalls.Call("duration_to_hours", longest)));
        Assert.Equal(5_259_600_000L, TimeCalls.IntOf(TimeCalls.Call("duration_to_minutes", longest)));
        Assert.Equal(MaxSeconds, TimeCalls.IntOf(TimeCalls.Call("duration_to_seconds", longest)));
        Assert.Equal(999L, TimeCalls.IntOf(TimeCalls.Call("duration_to_milliseconds", longest)));
        Assert.Equal(-87_660_000L, TimeCalls.IntOf(TimeCalls.Call("duration_to_hours", shortest)));
        Assert.Equal(-MaxSeconds, TimeCalls.IntOf(TimeCalls.Call("duration_to_seconds", shortest)));
        Assert.Equal(-999L, TimeCalls.IntOf(TimeCalls.Call("duration_to_milliseconds", shortest)));
        Assert.Equal(-500L, TimeCalls.IntOf(TimeCalls.Call("duration_to_milliseconds", Du(-1, -500_000_000))));
        Assert.Equal(0L, TimeCalls.IntOf(TimeCalls.Call("duration_to_hours", Du(-3_599, -999_999_999))));
    }

    private static CelValue Du(long seconds, int nanoseconds) => CelValue.FromDuration(new CelDuration(seconds, nanoseconds));

    private static (long Seconds, int Nanoseconds) Parts(string overload, params CelValue[] arguments)
    {
        CelValue result = TimeCalls.Call(overload, arguments);
        Assert.True(result.TryGetDuration(out CelDuration duration), result.Kind.ToString());
        return (duration.Seconds, duration.Nanoseconds);
    }
}
