using Great.Parts.Cel.Diagnostics;
using Xunit;
using static Great.Parts.Cel.Tests.Library.TimeCalls;

namespace Great.Parts.Cel.Tests.Library;

// The six operations + and - of time on values: the carry and the borrow of the nanoseconds, the edges of the range of
// timestamp and of the nanoseconds of a duration, and the kind of each overflow.
public sealed class TimeArithmeticOverloadTests
{
    private const long MinSeconds = -62_135_596_800;
    private const long MaxSeconds = 253_402_300_799;

    // seconds, nanoseconds of the moment, nanoseconds of the duration, seconds and nanoseconds of the sum.
    [Theory]
    [InlineData(0L, 0, 1_500_000_000L, 1L, 500_000_000)]
    [InlineData(0L, 600_000_000, 500_000_000L, 1L, 100_000_000)]
    [InlineData(0L, 0, -1L, -1L, 999_999_999)]
    [InlineData(0L, 500_000_000, -1_500_000_000L, -1L, 0)]
    [InlineData(10L, 999_999_999, 1L, 11L, 0)]
    [InlineData(MaxSeconds, 999_999_998, 1L, MaxSeconds, 999_999_999)]
    [InlineData(MinSeconds, 1, -1L, MinSeconds, 0)]
    [InlineData(MaxSeconds, 0, -9_223_372_036_854_775_808L, MaxSeconds - 9_223_372_037L, 145_224_192)]
    [InlineData(MinSeconds, 0, long.MaxValue, MinSeconds + 9_223_372_036L, 854_775_807)]
    public static void AddingADuration_ShiftsTheMomentInEitherOrder(long seconds, int nanoseconds, long duration, long expectedSeconds, int expectedNanoseconds)
    {
        Assert.Equal((expectedSeconds, expectedNanoseconds), TimestampOf(Call("add_timestamp_duration", Ts(seconds, nanoseconds), Du(duration))));
        Assert.Equal((expectedSeconds, expectedNanoseconds), TimestampOf(Call("add_duration_timestamp", Du(duration), Ts(seconds, nanoseconds))));
    }

    // seconds, nanoseconds of the moment, nanoseconds of the duration, seconds and nanoseconds of the difference.
    [Theory]
    [InlineData(0L, 0, 1L, -1L, 999_999_999)]
    [InlineData(5L, 100, 1_000_000_100L, 4L, 0)]
    [InlineData(5L, 0, -1_500_000_000L, 6L, 500_000_000)]
    [InlineData(MinSeconds, 0, 0L, MinSeconds, 0)]
    [InlineData(MaxSeconds, 999_999_999, 999_999_999L, MaxSeconds, 0)]
    public static void SubtractingADuration_ShiftsTheMomentBack(long seconds, int nanoseconds, long duration, long expectedSeconds, int expectedNanoseconds)
    {
        Assert.Equal((expectedSeconds, expectedNanoseconds), TimestampOf(Call("subtract_timestamp_duration", Ts(seconds, nanoseconds), Du(duration))));
    }

    [Theory]
    [InlineData(MaxSeconds, 999_999_999, 1L)]
    [InlineData(MaxSeconds, 0, 1_000_000_000L)]
    [InlineData(MaxSeconds, 0, long.MaxValue)]
    [InlineData(MinSeconds, 0, -1L)]
    [InlineData(MinSeconds, 0, long.MinValue)]
    public static void AddingADurationPastTheRange_IsTimestampOverflow(long seconds, int nanoseconds, long duration)
    {
        Assert.Equal((CelErrorCode.Overflow, Offset, "timestamp overflow"), Error(Call("add_timestamp_duration", Ts(seconds, nanoseconds), Du(duration))));
        Assert.Equal((CelErrorCode.Overflow, Offset, "timestamp overflow"), Error(Call("add_duration_timestamp", Du(duration), Ts(seconds, nanoseconds))));
    }

    [Theory]
    [InlineData(MinSeconds, 0, 1L)]
    [InlineData(MinSeconds, 999_999_999, 1_000_000_000L)]
    [InlineData(MinSeconds, 0, long.MaxValue)]
    [InlineData(MaxSeconds, 0, long.MinValue)]
    [InlineData(MaxSeconds, 999_999_999, -1_000_000_000L)]
    public static void SubtractingADurationPastTheRange_IsTimestampOverflow(long seconds, int nanoseconds, long duration)
    {
        Assert.Equal((CelErrorCode.Overflow, Offset, "timestamp overflow"), Error(Call("subtract_timestamp_duration", Ts(seconds, nanoseconds), Du(duration))));
    }

    [Theory]
    [InlineData(9_223_372_036L, 854_775_807, 0L, 0, long.MaxValue)]
    [InlineData(0L, 0, 9_223_372_036L, 854_775_808, long.MinValue)]
    [InlineData(10L, 5, 4L, 999_999_999, 5_000_000_006L)]
    [InlineData(4L, 999_999_999, 10L, 5, -5_000_000_006L)]
    [InlineData(7L, 7, 7L, 7, 0L)]
    public static void TimestampMinusTimestamp_IsTheDurationBetweenThem(long seconds, int nanoseconds, long otherSeconds, int otherNanoseconds, long expected)
    {
        Assert.Equal(expected, DurationOf(Call("subtract_timestamp_timestamp", Ts(seconds, nanoseconds), Ts(otherSeconds, otherNanoseconds))));
    }

    [Theory]
    [InlineData(9_223_372_036L, 854_775_808, 0L, 0)]
    [InlineData(0L, 0, 9_223_372_036L, 854_775_809)]
    [InlineData(MaxSeconds, 999_999_999, MinSeconds, 0)]
    [InlineData(MinSeconds, 0, MaxSeconds, 999_999_999)]
    public static void DifferenceOfMomentsOutsideTheNanosecondsOfALong_IsDurationOverflow(long seconds, int nanoseconds, long otherSeconds, int otherNanoseconds)
    {
        Assert.Equal(
            (CelErrorCode.Overflow, Offset, "duration overflow"),
            Error(Call("subtract_timestamp_timestamp", Ts(seconds, nanoseconds), Ts(otherSeconds, otherNanoseconds))));
    }

    [Theory]
    [InlineData(1L, 2L, 3L)]
    [InlineData(long.MaxValue, -1L, long.MaxValue - 1)]
    [InlineData(long.MinValue, 1L, long.MinValue + 1)]
    [InlineData(long.MaxValue, long.MinValue, -1L)]
    public static void DurationPlusDuration_IsTheSum(long left, long right, long expected)
    {
        Assert.Equal(expected, DurationOf(Call("add_duration_duration", Du(left), Du(right))));
    }

    [Theory]
    [InlineData(long.MaxValue, 1L)]
    [InlineData(long.MinValue, -1L)]
    [InlineData(long.MaxValue, long.MaxValue)]
    [InlineData(long.MinValue, long.MinValue)]
    public static void DurationPlusDurationOutsideALong_IsDurationOverflow(long left, long right)
    {
        Assert.Equal((CelErrorCode.Overflow, Offset, "duration overflow"), Error(Call("add_duration_duration", Du(left), Du(right))));
    }

    [Theory]
    [InlineData(5L, 3L, 2L)]
    [InlineData(long.MinValue, -1L, long.MinValue + 1)]
    [InlineData(long.MaxValue, 1L, long.MaxValue - 1)]
    [InlineData(-1L, long.MaxValue, long.MinValue)]
    [InlineData(0L, long.MaxValue, -long.MaxValue)]
    public static void DurationMinusDuration_IsTheDifference(long left, long right, long expected)
    {
        Assert.Equal(expected, DurationOf(Call("subtract_duration_duration", Du(left), Du(right))));
    }

    [Theory]
    [InlineData(long.MinValue, 1L)]
    [InlineData(long.MaxValue, -1L)]
    [InlineData(0L, long.MinValue)]
    [InlineData(-2L, long.MaxValue)]
    public static void DurationMinusDurationOutsideALong_IsDurationOverflow(long left, long right)
    {
        Assert.Equal((CelErrorCode.Overflow, Offset, "duration overflow"), Error(Call("subtract_duration_duration", Du(left), Du(right))));
    }

    [Theory]
    [InlineData("add_timestamp_duration")]
    [InlineData("add_duration_timestamp")]
    [InlineData("subtract_timestamp_duration")]
    [InlineData("subtract_timestamp_timestamp")]
    [InlineData("add_duration_duration")]
    [InlineData("subtract_duration_duration")]
    public static void EveryOperation_RejectsIntegers(string id)
    {
        AssertNoMatchingOverload(id, Int(1), Int(2));
    }

    // A moment where a duration is expected and the other way round: the kinds of time are not interchangeable.
    [Fact]
    public static void OperationsOfTime_RejectTheWrongKindOfTime()
    {
        AssertNoMatchingOverload("add_timestamp_duration", Du(1), Du(2));
        AssertNoMatchingOverload("add_duration_timestamp", Ts(1), Ts(2));
        AssertNoMatchingOverload("subtract_timestamp_duration", Du(1), Ts(1));
        AssertNoMatchingOverload("subtract_timestamp_timestamp", Ts(1), Du(1));
        AssertNoMatchingOverload("add_duration_duration", Ts(1), Du(1));
        AssertNoMatchingOverload("subtract_duration_duration", Du(1), Ts(1));
    }
}
