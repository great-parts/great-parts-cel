using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// CelTimestamp and CelDuration as structures: the limits of the range, the conversions to and from DateTimeOffset and
// TimeSpan, equality and order, and the text.
public sealed class TimeStructureTests
{
    private const long MinSeconds = -62_135_596_800;
    private const long MaxSeconds = 253_402_300_799;

    [Fact]
    public static void Timestamp_Default_IsTheEpoch()
    {
        CelTimestamp epoch = default;

        Assert.Equal((0L, 0), (epoch.Seconds, epoch.Nanoseconds));
        Assert.Equal("1970-01-01T00:00:00Z", epoch.ToString());
    }

    [Fact]
    public static void Timestamp_TakesTheFirstAndTheLastMomentOfTheRange()
    {
        var first = new CelTimestamp(MinSeconds, 0);
        var last = new CelTimestamp(MaxSeconds, 999_999_999);

        Assert.Equal("0001-01-01T00:00:00Z", first.ToString());
        Assert.Equal("9999-12-31T23:59:59.999999999Z", last.ToString());
        Assert.True(first < default(CelTimestamp) && default(CelTimestamp) < last);
    }

    [Theory]
    [InlineData(MinSeconds - 1, 0)]
    [InlineData(MinSeconds - 1, 999_999_999)]
    [InlineData(MaxSeconds + 1, 0)]
    [InlineData(long.MinValue, 0)]
    [InlineData(long.MaxValue, 0)]
    public static void Timestamp_OutsideTheRange_ThrowsAndNamesTheSeconds(long seconds, int nanoseconds)
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(seconds, nanoseconds));

        Assert.Equal("seconds", error.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1_000_000_000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public static void Timestamp_NanosecondsOutsideTheSecond_ThrowAndNameTheNanoseconds(int nanoseconds)
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(0, nanoseconds));

        Assert.Equal("nanoseconds", error.ParamName);
    }

    // The nanoseconds of an instant before the epoch count forward from the whole second below it.
    [Fact]
    public static void Timestamp_BeforeTheEpoch_CountsNanosecondsForward()
    {
        var oneNanosecondBefore = new CelTimestamp(-1, 999_999_999);

        Assert.Equal("1969-12-31T23:59:59.999999999Z", oneNanosecondBefore.ToString());
        Assert.True(oneNanosecondBefore < default(CelTimestamp));
        Assert.Equal(-1, oneNanosecondBefore.CompareTo(default));
    }

    [Theory]
    [InlineData(MinSeconds, 0, "0001-01-01T00:00:00Z")]
    [InlineData(MinSeconds, 1, "0001-01-01T00:00:00.000000001Z")]
    [InlineData(-62_135_596_800L + 86_399, 100, "0001-01-01T23:59:59.0000001Z")]
    [InlineData(951_782_400L, 0, "2000-02-29T00:00:00Z")]
    [InlineData(951_868_799L, 990_000_000, "2000-02-29T23:59:59.99Z")]
    [InlineData(4_107_542_400L, 0, "2100-03-01T00:00:00Z")]
    [InlineData(-2_203_891_201L, 0, "1900-02-28T23:59:59Z")]
    [InlineData(-2_203_891_200L, 5, "1900-03-01T00:00:00.000000005Z")]
    [InlineData(MaxSeconds, 0, "9999-12-31T23:59:59Z")]
    public static void Timestamp_PrintsTheCalendarDateInUtc(long seconds, int nanoseconds, string expected)
    {
        Assert.Equal(expected, new CelTimestamp(seconds, nanoseconds).ToString());
    }

    [Fact]
    public static void Timestamp_Text_AgreesWithDateTimeAcrossTheRange()
    {
        var random = new TimeSamples(647);
        for (int index = 0; index < 20_000; index++)
        {
            long seconds = random.Next(MinSeconds, MaxSeconds + 1);
            DateTime date = DateTime.UnixEpoch.AddSeconds(seconds);

            string expected = date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            Assert.Equal(expected, new CelTimestamp(seconds, 0).ToString());
        }
    }

    [Fact]
    public static void Timestamp_FromDateTimeOffset_IsExactAndDropsTheOffset()
    {
        var plus = new DateTimeOffset(2031, 3, 4, 12, 0, 0, TimeSpan.FromHours(5.5));
        var utc = new DateTimeOffset(2031, 3, 4, 6, 30, 0, TimeSpan.Zero);

        Assert.Equal(CelTimestamp.FromDateTimeOffset(utc), CelTimestamp.FromDateTimeOffset(plus));
        Assert.Equal(300, CelTimestamp.FromDateTimeOffset(utc.AddTicks(3)).Nanoseconds);
        Assert.Equal((MinSeconds, 0), Pair(CelTimestamp.FromDateTimeOffset(DateTimeOffset.MinValue)));
        Assert.Equal((MaxSeconds, 999_999_900), Pair(CelTimestamp.FromDateTimeOffset(DateTimeOffset.MaxValue)));
        Assert.Equal((0L, 0), Pair(CelTimestamp.FromDateTimeOffset(DateTimeOffset.UnixEpoch)));
        Assert.Equal((-1L, 999_999_900), Pair(CelTimestamp.FromDateTimeOffset(DateTimeOffset.UnixEpoch.AddTicks(-1))));
    }

    [Fact]
    public static void Timestamp_ToDateTimeOffset_RoundTripsEveryTickAndRefusesWhatItWouldRound()
    {
        var random = new TimeSamples(6470);
        for (int index = 0; index < 5_000; index++)
        {
            long ticks = random.Next(0, DateTimeOffset.MaxValue.UtcTicks + 1);
            var moment = new DateTimeOffset(ticks, TimeSpan.Zero);

            CelTimestamp timestamp = CelTimestamp.FromDateTimeOffset(moment);

            Assert.True(timestamp.TryGetDateTimeOffset(out DateTimeOffset back));
            Assert.Equal(moment, back);
            Assert.Equal(TimeSpan.Zero, back.Offset);
        }

        Assert.False(new CelTimestamp(10, 1).TryGetDateTimeOffset(out DateTimeOffset none));
        Assert.Equal(default, none);
        Assert.False(new CelTimestamp(10, 99).TryGetDateTimeOffset(out _));
        Assert.False(new CelTimestamp(10, 101).TryGetDateTimeOffset(out _));
        Assert.False(new CelTimestamp(MaxSeconds, 999_999_999).TryGetDateTimeOffset(out _));
        Assert.True(new CelTimestamp(MaxSeconds, 999_999_900).TryGetDateTimeOffset(out DateTimeOffset max));
        Assert.Equal(DateTimeOffset.MaxValue, max);
        Assert.True(new CelTimestamp(MinSeconds, 0).TryGetDateTimeOffset(out DateTimeOffset min));
        Assert.Equal(DateTimeOffset.MinValue, min);
    }

    [Fact]
    public static void Timestamp_EqualityOrderAndHash_FollowTheSecondsThenTheNanoseconds()
    {
        var a = new CelTimestamp(-5, 900);
        var same = new CelTimestamp(-5, 900);
        var b = new CelTimestamp(-5, 901);
        var c = new CelTimestamp(-4, 0);
        var c2 = new CelTimestamp(-4, 0);

        Assert.True(a == same && a != b && a.Equals(same) && a.Equals((object)same) && !a.Equals((object)b));
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals((object)new CelDuration(-4_999_999_100)));
        Assert.Equal(a.GetHashCode(), same.GetHashCode());
        Assert.True(a < b && b < c && a <= same && c > b && c >= c2);
        Assert.False(b < a || a > b || c <= b || a >= b);
        Assert.True(a.CompareTo(b) < 0 && c.CompareTo(a) > 0 && a.CompareTo(same) == 0);

        var sorted = new List<CelTimestamp> { c, a, b };
        sorted.Sort();
        Assert.Equal([a, b, c], sorted);
    }

    [Fact]
    public static void Duration_Default_IsZero_AndAnyLongIsAValidLength()
    {
        Assert.Equal(0L, default(CelDuration).TotalNanoseconds);
        Assert.Equal("0s", default(CelDuration).ToString());
        Assert.Equal(long.MaxValue, new CelDuration(long.MaxValue).TotalNanoseconds);
        Assert.Equal(long.MinValue, new CelDuration(long.MinValue).TotalNanoseconds);
    }

    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(7L, "0.000000007s")]
    [InlineData(-7L, "-0.000000007s")]
    [InlineData(999_999_999L, "0.999999999s")]
    [InlineData(1_000_000_000L, "1s")]
    [InlineData(1_010_000_000L, "1.01s")]
    [InlineData(-1_100_000_000L, "-1.1s")]
    [InlineData(86_400_000_000_000L, "86400s")]
    [InlineData(123_456_789_012L, "123.456789012s")]
    [InlineData(-123_456_789_012L, "-123.456789012s")]
    [InlineData(long.MaxValue, "9223372036.854775807s")]
    [InlineData(long.MinValue, "-9223372036.854775808s")]
    [InlineData(long.MinValue + 1, "-9223372036.854775807s")]
    public static void Duration_PrintsTheExactSecondsWithoutFloatingPoint(long nanoseconds, string expected)
    {
        Assert.Equal(expected, new CelDuration(nanoseconds).ToString());
    }

    [Fact]
    public static void Duration_FromTimeSpan_IsExactInsideTheRange_AndRefusesOutsideIt()
    {
        Assert.Equal(2_500_000_000L, CelDuration.FromTimeSpan(TimeSpan.FromSeconds(2.5)).TotalNanoseconds);
        Assert.Equal(100L, CelDuration.FromTimeSpan(TimeSpan.FromTicks(1)).TotalNanoseconds);
        Assert.Equal(0L, CelDuration.FromTimeSpan(TimeSpan.Zero).TotalNanoseconds);
        Assert.Equal((long.MaxValue / 100) * 100, CelDuration.FromTimeSpan(TimeSpan.FromTicks(long.MaxValue / 100)).TotalNanoseconds);
        Assert.Equal((long.MinValue / 100) * 100, CelDuration.FromTimeSpan(TimeSpan.FromTicks(long.MinValue / 100)).TotalNanoseconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.FromTicks((long.MaxValue / 100) + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.FromTicks((long.MinValue / 100) - 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.MinValue));
    }

    [Fact]
    public static void Duration_ToTimeSpan_IsExactOrRefused()
    {
        Assert.True(new CelDuration(-3_000).TryGetTimeSpan(out TimeSpan exact));
        Assert.Equal(TimeSpan.FromTicks(-30), exact);
        Assert.True(new CelDuration(0).TryGetTimeSpan(out TimeSpan zero));
        Assert.Equal(TimeSpan.Zero, zero);
        Assert.True(new CelDuration((long.MaxValue / 100) * 100).TryGetTimeSpan(out TimeSpan max));
        Assert.Equal(TimeSpan.FromTicks(long.MaxValue / 100), max);

        Assert.False(new CelDuration(1).TryGetTimeSpan(out TimeSpan refused));
        Assert.Equal(TimeSpan.Zero, refused);
        Assert.False(new CelDuration(-150).TryGetTimeSpan(out _));
        Assert.False(new CelDuration(long.MaxValue).TryGetTimeSpan(out _));
    }

    [Fact]
    public static void Duration_EqualityOrderAndHash_FollowTheLength()
    {
        var a = new CelDuration(-8);
        var same = new CelDuration(-8);
        var b = new CelDuration(3);
        var b2 = new CelDuration(3);

        Assert.True(a == same && a != b && a.Equals(same) && a.Equals((object)same) && !a.Equals((object)b));
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals((object)new CelTimestamp(0, 8)));
        Assert.Equal(a.GetHashCode(), same.GetHashCode());
        Assert.True(a < b && a <= same && b > a && b >= b2);
        Assert.False(b < a || a > b || b <= a || a >= b);
        Assert.True(a.CompareTo(b) < 0 && b.CompareTo(a) > 0 && a.CompareTo(same) == 0);
        Assert.True(new CelDuration(long.MinValue) < new CelDuration(long.MaxValue));
    }

    private static (long Seconds, int Nanoseconds) Pair(CelTimestamp timestamp) => (timestamp.Seconds, timestamp.Nanoseconds);
}
