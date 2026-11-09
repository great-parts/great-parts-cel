using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.TimeCalls;

namespace Great.Parts.Cel.Tests.Library;

// The nine conversions of time on values: the result, the edge of the range and the error of each overload. The calls are at
// the place Offset of a one-line expression.
public sealed class TimeConversionOverloadTests
{
    private const long MinSeconds = -62_135_596_800;
    private const long MaxSeconds = 253_402_300_799;

    [Fact]
    public static void Identities_ReturnTheirArgument()
    {
        Assert.Equal((1_234_567_890L, 5), TimestampOf(Call("timestamp_to_timestamp", Ts(1_234_567_890, 5))));
        Assert.Equal(-7L, DurationOf(Call("duration_to_duration", Du(-7))));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(MinSeconds)]
    [InlineData(MaxSeconds)]
    [InlineData(-1L)]
    [InlineData(1_700_000_000L)]
    public static void IntToTimestamp_IsUnixSecondsWithoutNanoseconds(long seconds)
    {
        Assert.Equal((seconds, 0), TimestampOf(Call("int64_to_timestamp", Int(seconds))));
    }

    [Theory]
    [InlineData(MaxSeconds + 1)]
    [InlineData(MinSeconds - 1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public static void IntToTimestamp_OutsideTheRange_IsConversionRangeAtTheCall(long seconds)
    {
        Assert.Equal(
            (CelErrorCode.ConversionRange, Offset, seconds + " is out of the range of timestamp"),
            Error(Call("int64_to_timestamp", Int(seconds))));
    }

    // The seconds are the floor of the Unix time: a moment before the epoch with a fraction gives the earlier second.
    [Theory]
    [InlineData(1_700_000_000L, 999_999_999, 1_700_000_000L)]
    [InlineData(-1L, 500_000_000, -1L)]
    [InlineData(MinSeconds, 0, MinSeconds)]
    [InlineData(MaxSeconds, 999_999_999, MaxSeconds)]
    public static void TimestampToInt_IsTheFloorOfTheUnixTime(long seconds, int nanoseconds, long expected)
    {
        Assert.Equal(expected, IntOf(Call("timestamp_to_int64", Ts(seconds, nanoseconds))));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(-1_500_000_000L)]
    public static void DurationToInt_IsTheNanoseconds(long nanoseconds)
    {
        Assert.Equal(nanoseconds, IntOf(Call("duration_to_int64", Du(nanoseconds))));
    }

    [Theory]
    [InlineData("2009-02-13T23:31:30Z", 1_234_567_890L, 0)]
    [InlineData("2009-02-13T23:31:30.5Z", 1_234_567_890L, 500_000_000)]
    [InlineData("2009-02-13T23:31:30.123456789123Z", 1_234_567_890L, 123_456_789)]
    [InlineData("2023-01-01T00:00:00-07:00", 1_672_556_400L, 0)]
    [InlineData("2024-02-29T12:00:00Z", 1_709_208_000L, 0)]
    [InlineData("0001-01-01T00:00:00Z", MinSeconds, 0)]
    [InlineData("9999-12-31T23:59:59.999999999Z", MaxSeconds, 999_999_999)]
    public static void StringToTimestamp_ReadsRfc3339(string text, long seconds, int nanoseconds)
    {
        Assert.Equal((seconds, nanoseconds), TimestampOf(Call("string_to_timestamp", Str(text))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("2023-02-30T00:00:00Z")]
    [InlineData("2023-01-01t00:00:00Z")]
    [InlineData("2023-01-01 00:00:00Z")]
    [InlineData("2023-01-01T00:00:60Z")]
    public static void StringToTimestamp_NotRfc3339_IsInvalidArgument(string text)
    {
        Assert.Equal(
            (CelErrorCode.InvalidArgument, Offset, "invalid argument to 'timestamp': cannot parse " + text + " as timestamp"),
            Error(Call("string_to_timestamp", Str(text))));
    }

    [Theory]
    [InlineData("0000-01-01T00:00:00Z")]
    [InlineData("0001-01-01T00:00:00+01:00")]
    [InlineData("9999-12-31T23:59:59-01:00")]
    public static void StringToTimestamp_OutsideTheRange_IsConversionRangeWithTheText(string text)
    {
        Assert.Equal(
            (CelErrorCode.ConversionRange, Offset, text + " is out of the range of timestamp"),
            Error(Call("string_to_timestamp", Str(text))));
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("1h30m", 5_400_000_000_000L)]
    [InlineData("-1.5s", -1_500_000_000L)]
    [InlineData(".5s", 500_000_000L)]
    [InlineData("5.s", 5_000_000_000L)]
    [InlineData("1h1h", 7_200_000_000_000L)]
    [InlineData("1us", 1_000L)]
    [InlineData("1µs", 1_000L)]
    [InlineData("1μs", 1_000L)]
    [InlineData("9223372036854775807ns", long.MaxValue)]
    [InlineData("-9223372036854775808ns", long.MinValue)]
    public static void StringToDuration_ReadsTheGrammarOfGo(string text, long nanoseconds)
    {
        Assert.Equal(nanoseconds, DurationOf(Call("string_to_duration", Str(text))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1H")]
    [InlineData("s")]
    [InlineData("1 s")]
    public static void StringToDuration_NotTheGrammar_IsInvalidArgument(string text)
    {
        Assert.Equal(
            (CelErrorCode.InvalidArgument, Offset, "invalid argument to 'duration': cannot parse " + text + " as duration"),
            Error(Call("string_to_duration", Str(text))));
    }

    [Theory]
    [InlineData("9223372036854775808ns")]
    [InlineData("2562048h")]
    [InlineData("-9223372036854775809ns")]
    public static void StringToDuration_OutsideTheRange_IsConversionRangeWithTheText(string text)
    {
        Assert.Equal(
            (CelErrorCode.ConversionRange, Offset, text + " is out of the range of duration"),
            Error(Call("string_to_duration", Str(text))));
    }

    [Theory]
    [InlineData(1_234_567_890L, 0, "2009-02-13T23:31:30Z")]
    [InlineData(1_234_567_890L, 120_000_000, "2009-02-13T23:31:30.12Z")]
    [InlineData(MinSeconds, 0, "0001-01-01T00:00:00Z")]
    [InlineData(MaxSeconds, 999_999_999, "9999-12-31T23:59:59.999999999Z")]
    [InlineData(-1L, 1, "1969-12-31T23:59:59.000000001Z")]
    public static void TimestampToString_IsRfc3339InUtc(long seconds, int nanoseconds, string expected)
    {
        Assert.Equal(expected, StringOf(Call("timestamp_to_string", Ts(seconds, nanoseconds))));
    }

    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(3_600_000_000_000L, "3600s")]
    [InlineData(1_500_000_000L, "1.5s")]
    [InlineData(-1L, "-0.000000001s")]
    [InlineData(long.MinValue, "-9223372036.854775808s")]
    [InlineData(long.MaxValue, "9223372036.854775807s")]
    public static void DurationToString_IsExactWithoutFloatingPoint(long nanoseconds, string expected)
    {
        Assert.Equal(expected, StringOf(Call("duration_to_string", Du(nanoseconds))));
    }

    // The value rules reach an implementation with a value of another kind when the static type is no guarantee.
    [Theory]
    [InlineData("timestamp_to_timestamp")]
    [InlineData("duration_to_duration")]
    [InlineData("int64_to_timestamp")]
    [InlineData("timestamp_to_int64")]
    [InlineData("duration_to_int64")]
    [InlineData("string_to_timestamp")]
    [InlineData("string_to_duration")]
    [InlineData("timestamp_to_string")]
    [InlineData("duration_to_string")]
    public static void EveryConversion_RejectsAValueOfAnotherKind(string id)
    {
        AssertNoMatchingOverload(id, CelValue.FromBoolean(true));
    }

    [Fact]
    public static void ConversionsOfTimeToTheWrongTime_AreRejected()
    {
        AssertNoMatchingOverload("timestamp_to_int64", Du(1));
        AssertNoMatchingOverload("duration_to_int64", Ts(1));
        AssertNoMatchingOverload("timestamp_to_string", Du(1));
        AssertNoMatchingOverload("duration_to_string", Ts(1));
    }
}
