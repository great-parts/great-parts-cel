using System;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The values of time as .NET structures. CelTimestamp is a point in time in UTC with nanoseconds, from
// 0001-01-01T00:00:00Z to 9999-12-31T23:59:59.999999999Z, kept as Unix seconds and nanoseconds within the
// second (0 to 999 999 999); default is the epoch. CelDuration is a whole int64 of nanoseconds; default is
// zero. DateTimeOffset and TimeSpan count in ticks of 100 nanoseconds: they convert to the CEL structures
// exactly, the other way only when the nanoseconds are a multiple of 100, and a conversion never rounds.
// The text of both is what string() gives in CEL. The comparison is by the
// point in time and by the length; the hash code is consistent with the equality.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "+R-10")]
public sealed class TimeValueTests
{
    [Fact]
    public void Timestamp_KeepsSecondsAndNanoseconds()
    {
        var timestamp = new CelTimestamp(1234567890, 123456789);

        Assert.Equal((1234567890L, 123456789), (timestamp.Seconds, timestamp.Nanoseconds));
        Assert.Equal((0L, 0), (default(CelTimestamp).Seconds, default(CelTimestamp).Nanoseconds));
        Assert.Equal("1970-01-01T00:00:00Z", default(CelTimestamp).ToString());
    }

    [Fact]
    public void Timestamp_AcceptsTheWholeRangeAndNothingOutside()
    {
        Assert.Equal(-62135596800L, new CelTimestamp(-62135596800, 0).Seconds);
        Assert.Equal(253402300799L, new CelTimestamp(253402300799, 999999999).Seconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(-62135596801, 999999999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(253402300800, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(0, 1_000_000_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(long.MaxValue, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CelTimestamp(long.MinValue, 0));
    }

    [Theory]
    [InlineData(1234567890L, 0, "2009-02-13T23:31:30Z")]
    [InlineData(1234567890L, 500000000, "2009-02-13T23:31:30.5Z")]
    [InlineData(1234567890L, 120000000, "2009-02-13T23:31:30.12Z")]
    [InlineData(1234567890L, 1, "2009-02-13T23:31:30.000000001Z")]
    [InlineData(1234567890L, 123456789, "2009-02-13T23:31:30.123456789Z")]
    [InlineData(-1L, 999999999, "1969-12-31T23:59:59.999999999Z")]
    [InlineData(-62135596800L, 0, "0001-01-01T00:00:00Z")]
    [InlineData(253402300799L, 999999999, "9999-12-31T23:59:59.999999999Z")]
    public static void Timestamp_PrintsRfc3339InUtcWithoutTrailingZeros(long seconds, int nanoseconds, string expected)
    {
        Assert.Equal(expected, new CelTimestamp(seconds, nanoseconds).ToString());
    }

    [Fact]
    public void Timestamp_FromDateTimeOffset_IsTheSameMomentInUtc()
    {
        var utc = new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero);
        var shifted = new DateTimeOffset(2009, 2, 14, 0, 31, 30, TimeSpan.FromHours(1));

        Assert.Equal((1234567890L, 0), (CelTimestamp.FromDateTimeOffset(utc).Seconds, CelTimestamp.FromDateTimeOffset(utc).Nanoseconds));
        Assert.Equal(CelTimestamp.FromDateTimeOffset(utc), CelTimestamp.FromDateTimeOffset(shifted));
        Assert.Equal(100, CelTimestamp.FromDateTimeOffset(utc.AddTicks(1)).Nanoseconds);
        Assert.Equal((-62135596800L, 0), (CelTimestamp.FromDateTimeOffset(DateTimeOffset.MinValue).Seconds, CelTimestamp.FromDateTimeOffset(DateTimeOffset.MinValue).Nanoseconds));
        Assert.Equal((253402300799L, 999999900), (CelTimestamp.FromDateTimeOffset(DateTimeOffset.MaxValue).Seconds, CelTimestamp.FromDateTimeOffset(DateTimeOffset.MaxValue).Nanoseconds));
    }

    [Fact]
    public void Timestamp_TryGetDateTimeOffset_NeverRounds()
    {
        Assert.True(new CelTimestamp(1234567890, 100).TryGetDateTimeOffset(out DateTimeOffset exact));
        Assert.Equal(new DateTimeOffset(2009, 2, 13, 23, 31, 30, TimeSpan.Zero).AddTicks(1), exact);
        Assert.Equal(TimeSpan.Zero, exact.Offset);

        Assert.False(new CelTimestamp(1234567890, 150).TryGetDateTimeOffset(out DateTimeOffset rounded));
        Assert.Equal(default, rounded);
        Assert.False(new CelTimestamp(1234567890, 1).TryGetDateTimeOffset(out _));
        Assert.True(new CelTimestamp(-62135596800, 0).TryGetDateTimeOffset(out DateTimeOffset minimum));
        Assert.Equal(DateTimeOffset.MinValue, minimum);
        Assert.True(new CelTimestamp(253402300799, 999999900).TryGetDateTimeOffset(out DateTimeOffset maximum));
        Assert.Equal(DateTimeOffset.MaxValue, maximum);
        Assert.False(new CelTimestamp(253402300799, 999999999).TryGetDateTimeOffset(out _));
    }

    [Fact]
    public void Timestamp_EqualityOrderAndHash_FollowThePointInTime()
    {
        var early = new CelTimestamp(10, 5);
        var same = new CelTimestamp(10, 5);
        var late = new CelTimestamp(10, 6);
        var later = new CelTimestamp(11, 0);

        Assert.True(early == same);
        Assert.True(early != late);
        Assert.True(early.Equals(same));
        Assert.True(early.Equals((object)same));
        Assert.False(early.Equals((object)late));
        Assert.False(early.Equals(null));
        Assert.False(early.Equals(new CelDuration(10_000_000_005)));
        Assert.Equal(early.GetHashCode(), same.GetHashCode());
        Assert.True(early < late && late < later && early <= same && later > late && later >= new CelTimestamp(11, 0));
        Assert.False(late < early || early > late || later <= late || early >= late);
        Assert.True(early.CompareTo(late) < 0 && later.CompareTo(late) > 0 && early.CompareTo(same) == 0);
        Assert.True(new CelTimestamp(-1, 999999999) < default(CelTimestamp));
    }

    [Fact]
    public void Duration_KeepsAWholeInt64OfNanoseconds()
    {
        Assert.Equal(1500000000L, new CelDuration(1500000000).TotalNanoseconds);
        Assert.Equal(0L, default(CelDuration).TotalNanoseconds);
        Assert.Equal(long.MaxValue, new CelDuration(long.MaxValue).TotalNanoseconds);
        Assert.Equal(long.MinValue, new CelDuration(long.MinValue).TotalNanoseconds);
    }

    [Theory]
    [InlineData(0L, "0s")]
    [InlineData(1500000000L, "1.5s")]
    [InlineData(-1500000000L, "-1.5s")]
    [InlineData(60001000000L, "60.001s")]
    [InlineData(3600000000000L, "3600s")]
    [InlineData(1L, "0.000000001s")]
    [InlineData(-1L, "-0.000000001s")]
    [InlineData(long.MaxValue, "9223372036.854775807s")]
    [InlineData(long.MinValue, "-9223372036.854775808s")]
    public static void Duration_PrintsSecondsAndFractionExactly(long nanoseconds, string expected)
    {
        Assert.Equal(expected, new CelDuration(nanoseconds).ToString());
    }

    [Fact]
    public void Duration_FromTimeSpan_IsExactWithinTheRangeAndRefusesTheRest()
    {
        Assert.Equal(1500000000L, CelDuration.FromTimeSpan(TimeSpan.FromSeconds(1.5)).TotalNanoseconds);
        Assert.Equal(100L, CelDuration.FromTimeSpan(TimeSpan.FromTicks(1)).TotalNanoseconds);
        Assert.Equal(-100L, CelDuration.FromTimeSpan(TimeSpan.FromTicks(-1)).TotalNanoseconds);
        Assert.Equal(long.MaxValue / 100 * 100, CelDuration.FromTimeSpan(TimeSpan.FromTicks(long.MaxValue / 100)).TotalNanoseconds);
        Assert.Equal(long.MinValue / 100 * 100, CelDuration.FromTimeSpan(TimeSpan.FromTicks(long.MinValue / 100)).TotalNanoseconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.FromTicks((long.MaxValue / 100) + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.FromTicks((long.MinValue / 100) - 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => CelDuration.FromTimeSpan(TimeSpan.MinValue));
    }

    [Fact]
    public void Duration_TryGetTimeSpan_NeverRounds()
    {
        Assert.True(new CelDuration(1500000000).TryGetTimeSpan(out TimeSpan exact));
        Assert.Equal(TimeSpan.FromSeconds(1.5), exact);
        Assert.True(new CelDuration(-100).TryGetTimeSpan(out TimeSpan negative));
        Assert.Equal(TimeSpan.FromTicks(-1), negative);

        Assert.False(new CelDuration(150).TryGetTimeSpan(out TimeSpan rounded));
        Assert.Equal(TimeSpan.Zero, rounded);
        Assert.False(new CelDuration(1).TryGetTimeSpan(out _));
        Assert.False(new CelDuration(long.MaxValue).TryGetTimeSpan(out _));
    }

    [Fact]
    public void Duration_EqualityOrderAndHash_FollowTheLength()
    {
        var short1 = new CelDuration(5);
        var same = new CelDuration(5);
        var long1 = new CelDuration(6);
        var negative = new CelDuration(-6);

        Assert.True(short1 == same);
        Assert.True(short1 != long1);
        Assert.True(short1.Equals(same));
        Assert.True(short1.Equals((object)same));
        Assert.False(short1.Equals((object)long1));
        Assert.False(short1.Equals(null));
        Assert.False(short1.Equals(new CelTimestamp(0, 5)));
        Assert.Equal(short1.GetHashCode(), same.GetHashCode());
        Assert.True(negative < short1 && short1 < long1 && short1 <= same && long1 > short1 && long1 >= new CelDuration(6));
        Assert.False(long1 < short1 || short1 > long1 || long1 <= short1 || short1 >= long1);
        Assert.True(short1.CompareTo(long1) < 0 && long1.CompareTo(negative) > 0 && short1.CompareTo(same) == 0);
    }

    [Fact]
    public void ValuesOfTime_AreCelValuesOfTheirKinds()
    {
        var timestamp = new CelTimestamp(1234567890, 5);
        var duration = new CelDuration(1234567890);
        CelValue timestampValue = CelValue.FromTimestamp(timestamp);
        CelValue durationValue = CelValue.FromDuration(duration);

        Assert.Equal(CelValueKind.Timestamp, timestampValue.Kind);
        Assert.Equal(CelValueKind.Duration, durationValue.Kind);
        Assert.True(timestampValue.TryGetTimestamp(out CelTimestamp readTimestamp));
        Assert.Equal(timestamp, readTimestamp);
        Assert.True(durationValue.TryGetDuration(out CelDuration readDuration));
        Assert.Equal(duration, readDuration);

        Assert.False(timestampValue.TryGetDuration(out CelDuration none));
        Assert.Equal(default, none);
        Assert.False(durationValue.TryGetTimestamp(out CelTimestamp noTimestamp));
        Assert.Equal(default, noTimestamp);
        Assert.False(timestampValue.TryGetInt64(out _));
        Assert.False(durationValue.TryGetInt64(out _));
        Assert.False(CelValue.FromInt64(1234567890).TryGetTimestamp(out _));
        Assert.False(CelValue.FromString("1h").TryGetDuration(out _));

        Assert.True(timestampValue == CelValue.FromTimestamp(new CelTimestamp(1234567890, 5)));
        Assert.True(timestampValue != CelValue.FromTimestamp(new CelTimestamp(1234567890, 6)));
        Assert.True(durationValue == CelValue.FromDuration(new CelDuration(1234567890)));
        Assert.True(timestampValue != durationValue);
    }

    [Fact]
    public void TypesOfTime_HaveTheirKinds()
    {
        Assert.Equal(CelTypeKind.Timestamp, CelType.Timestamp.Kind);
        Assert.Equal(CelTypeKind.Duration, CelType.Duration.Kind);
        Assert.Empty(CelType.Timestamp.Parameters);
        Assert.Empty(CelType.Duration.Parameters);
        Assert.NotEqual(CelType.Timestamp, CelType.Duration);
    }
}
