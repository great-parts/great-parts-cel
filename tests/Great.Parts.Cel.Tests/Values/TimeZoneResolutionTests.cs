using System;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The resolution of a time zone name (UTC, a fixed offset, an invalid name, a zone of the database) and the local fields of an
// instant: the fields from the calendar of the library, at ordinary instants and at the edges of the range of timestamp.
public sealed class TimeZoneResolutionTests
{
    [Fact]
    public static void Utc_NeedsNoDatabase_AndIsOrdinal()
    {
        TimeZoneResolution withoutDatabase = TimeZoneRules.Resolve(null, "UTC");
        TimeZoneResolution empty = TimeZoneRules.Resolve(CelTimeZoneDatabase.Empty, "UTC");

        Assert.Equal(TimeZoneResolutionKind.FixedOffset, withoutDatabase.Kind);
        Assert.Equal(0, withoutDatabase.OffsetAt(1_700_000_000));
        Assert.Equal(TimeZoneResolutionKind.FixedOffset, empty.Kind);
        Assert.Equal(TimeZoneResolutionKind.NotFound, TimeZoneRules.Resolve(null, "utc").Kind);
        Assert.Equal(TimeZoneResolutionKind.NotFound, TimeZoneRules.Resolve(null, "Utc").Kind);
    }

    // An entry of the database named UTC is never used: UTC is the offset 0 whatever the database holds.
    [Fact]
    public static void DatabaseEntryNamedUtc_IsNotUsed()
    {
        CelTimeZoneDatabase database = CelTimeZoneDatabase.Create([TzifFile.Pair("UTC", TzifFile.Fixed(3_600, "<+01>-1"))]);

        TimeZoneResolution resolution = TimeZoneRules.Resolve(database, "UTC");

        Assert.Equal(TimeZoneResolutionKind.FixedOffset, resolution.Kind);
        Assert.Equal(0, resolution.OffsetAt(0));
        Assert.True(database.Contains("UTC"));
    }

    [Theory]
    [InlineData("+00:00", 0)]
    [InlineData("-00:00", 0)]
    [InlineData("00:00", 0)]
    [InlineData("+03:00", 10_800)]
    [InlineData("03:00", 10_800)]
    [InlineData("-05:30", -19_800)]
    [InlineData("+05:45", 20_700)]
    [InlineData("-23:59", -86_340)]
    [InlineData("+23:59", 86_340)]
    [InlineData("23:59", 86_340)]
    [InlineData("+00:59", 3_540)]
    [InlineData("-12:00", -43_200)]
    public static void FixedOffset_NeedsNoDatabase(string name, int seconds)
    {
        TimeZoneResolution resolution = TimeZoneRules.Resolve(null, name);

        Assert.Equal(TimeZoneResolutionKind.FixedOffset, resolution.Kind);
        Assert.Equal(seconds, resolution.OffsetAt(0));
        Assert.Equal(seconds, resolution.OffsetAt(long.MaxValue));
    }

    // A name with a colon that is no fixed offset is invalid, not a name to look up.
    [Theory]
    [InlineData("+1:5")]
    [InlineData("+1:05")]
    [InlineData("+001:30")]
    [InlineData("+24:00")]
    [InlineData("24:00")]
    [InlineData("+03:60")]
    [InlineData("+03:0")]
    [InlineData("+3:00")]
    [InlineData("++03:00")]
    [InlineData("+03:00:00")]
    [InlineData("Europe:Moscow")]
    [InlineData(":")]
    [InlineData("+٠٣:٠٠")]
    [InlineData("+0३:00")]
    public static void NameWithAColonThatIsNoOffset_IsInvalid(string name)
    {
        Assert.Equal(TimeZoneResolutionKind.Invalid, TimeZoneRules.Resolve(ZoneInfoFile.Database(), name).Kind);
        Assert.Equal(TimeZoneResolutionKind.Invalid, TimeZoneRules.Resolve(null, name).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Local")]
    [InlineData("europe/moscow")]
    [InlineData("Europe/Moscow ")]
    [InlineData("Mars/Olympus")]
    [InlineData("+0300")]
    [InlineData("+03.00")]
    [InlineData("03")]
    public static void NameThatIsNotInTheDatabase_IsNotFound(string name)
    {
        Assert.Equal(TimeZoneResolutionKind.NotFound, TimeZoneRules.Resolve(ZoneInfoFile.Database(), name).Kind);
        Assert.Equal(TimeZoneResolutionKind.NotFound, TimeZoneRules.Resolve(CelTimeZoneDatabase.Empty, name).Kind);
    }

    [Fact]
    public static void IanaName_WithoutADatabase_IsNotFound()
    {
        Assert.Equal(TimeZoneResolutionKind.NotFound, TimeZoneRules.Resolve(null, "Europe/Moscow").Kind);
        Assert.Equal(TimeZoneResolutionKind.NotFound, TimeZoneRules.Resolve(CelTimeZoneDatabase.Empty, "Europe/Moscow").Kind);
    }

    [Fact]
    public static void IanaName_InTheDatabase_IsAZoneWithItsOffsets()
    {
        TimeZoneResolution moscow = TimeZoneRules.Resolve(ZoneInfoFile.Database(), "Europe/Moscow");

        Assert.Equal(TimeZoneResolutionKind.Zone, moscow.Kind);
        Assert.Equal(14_400, moscow.OffsetAt(1_414_274_399));
        Assert.Equal(10_800, moscow.OffsetAt(1_414_274_400));
    }

    // The name is read from a span, so a slice of a larger text resolves without a copy.
    [Fact]
    public static void NameOfASlice_Resolves()
    {
        const string text = "x+03:00y Asia/Kathmandu!";

        Assert.Equal(TimeZoneResolutionKind.FixedOffset, TimeZoneRules.Resolve(null, text.AsSpan(1, 6)).Kind);
        Assert.Equal(TimeZoneResolutionKind.Zone, TimeZoneRules.Resolve(ZoneInfoFile.Database(), text.AsSpan(9, 14)).Kind);
    }

    // The fields: instant, offset and the nine fields, with the weekday from 0 (Sunday) and the zero-based day of the year.
    [Theory]
    [InlineData(0L, 0, 1970, 1, 1, 0, 0, 0, 0, 4, 0)]
    [InlineData(1_234_567_890L, 0, 2009, 2, 13, 23, 31, 30, 0, 5, 43)]
    [InlineData(1_234_567_890L, 20_700, 2009, 2, 14, 5, 16, 30, 0, 6, 44)]
    [InlineData(1_234_567_890L, -17_762, 2009, 2, 13, 18, 35, 28, 0, 5, 43)]
    [InlineData(951_782_400L, 0, 2000, 2, 29, 0, 0, 0, 0, 2, 59)]
    [InlineData(951_868_799L, 0, 2000, 2, 29, 23, 59, 59, 0, 2, 59)]
    [InlineData(951_868_800L, 0, 2000, 3, 1, 0, 0, 0, 0, 3, 60)]
    [InlineData(1_709_251_199L, 0, 2024, 2, 29, 23, 59, 59, 0, 4, 59)]
    [InlineData(1_735_689_599L, 0, 2024, 12, 31, 23, 59, 59, 0, 2, 365)]
    [InlineData(1_735_689_600L, 0, 2025, 1, 1, 0, 0, 0, 0, 3, 0)]
    [InlineData(-1L, 0, 1969, 12, 31, 23, 59, 59, 0, 3, 364)]
    [InlineData(-86_400L, 0, 1969, 12, 31, 0, 0, 0, 0, 3, 364)]
    [InlineData(-86_401L, 0, 1969, 12, 30, 23, 59, 59, 0, 2, 363)]
    public static void LocalFields_OfOrdinaryInstants(
        long seconds, int offset, int year, int month, int day, int hour, int minute, int second, int millisecond, int weekday, int dayOfYear)
    {
        TimeZoneLocalFields fields = TimeZoneLocalFields.Of(seconds, 0, offset);

        Assert.Equal(new TimeZoneLocalFields(year, month, day, hour, minute, second, millisecond, weekday, dayOfYear), fields);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(999_999, 0)]
    [InlineData(1_000_000, 1)]
    [InlineData(123_456_789, 123)]
    [InlineData(999_999_999, 999)]
    public static void Millisecond_IsTheNanosecondsTruncated(int nanoseconds, int millisecond)
    {
        Assert.Equal(millisecond, TimeZoneLocalFields.Of(1_700_000_000, nanoseconds, 3_600).Millisecond);
    }

    // The fields are not clamped to the range of timestamp: year 0 is a leap year, and year 10000 exists.
    [Fact]
    public static void LocalFields_AtTheEdgesOfTheRange_CanLeaveYears1To9999()
    {
        // 0001-01-01T00:00:00Z at -05:00 is 0000-12-31 19:00 in the leap year 0, the day 365 of the year.
        TimeZoneLocalFields first = TimeZoneLocalFields.Of(-62_135_596_800, 0, -18_000);
        // 9999-12-31T23:59:59Z at +05:00 is 10000-01-01 04:59:59.
        TimeZoneLocalFields last = TimeZoneLocalFields.Of(253_402_300_799, 0, 18_000);

        Assert.Equal(new TimeZoneLocalFields(0, 12, 31, 19, 0, 0, 0, 0, 365), first);
        Assert.Equal(new TimeZoneLocalFields(10_000, 1, 1, 4, 59, 59, 0, 6, 0), last);
    }

    [Fact]
    public static void LocalFields_AtTheEdgesOfTheRange_WithOffsetZero()
    {
        TimeZoneLocalFields first = TimeZoneLocalFields.Of(-62_135_596_800, 0, 0);
        TimeZoneLocalFields last = TimeZoneLocalFields.Of(253_402_300_799, 999_999_999, 0);

        Assert.Equal(new TimeZoneLocalFields(1, 1, 1, 0, 0, 0, 0, 1, 0), first);
        Assert.Equal(new TimeZoneLocalFields(9999, 12, 31, 23, 59, 59, 999, 5, 364), last);
    }

    // The fields follow the offset of the zone at the instant, to the second: New York before 1883 is -04:56:02.
    [Fact]
    public static void LocalFields_FollowTheOffsetOfTheZoneToTheSecond()
    {
        TimeZoneResolution newYork = TimeZoneRules.Resolve(ZoneInfoFile.Database(), "America/New_York");
        long instant = -2_717_650_801;

        TimeZoneLocalFields fields = TimeZoneLocalFields.Of(instant, 0, newYork.OffsetAt(instant));

        Assert.Equal(new TimeZoneLocalFields(1883, 11, 18, 12, 3, 57, 0, 0, 321), fields);
    }
}
