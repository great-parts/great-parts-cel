using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Values;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.TimeCalls;

namespace Great.Parts.Cel.Tests.Library;

// The 24 receivers of timestamp and duration on values: the ten fields of a timestamp without a zone and with a zone string,
// checked against DateTime as an independent calendar, the edges of the range where the local year is 0 or 10000, the zones of a
// database, the errors of a zone name, and the four receivers of a duration.
public sealed class TimeReceiverOverloadTests
{
    private const long MinSeconds = -62_135_596_800;
    private const long MaxSeconds = 253_402_300_799;

    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // The overload identifiers of the ten fields without a zone and with one, and the field of the local DateTime.
    private static readonly (string Plain, string Zoned, Func<DateTime, int> Field)[] Fields =
    [
        ("timestamp_to_year", "timestamp_to_year_with_tz", local => local.Year),
        ("timestamp_to_month", "timestamp_to_month_with_tz", local => local.Month - 1),
        ("timestamp_to_day_of_year", "timestamp_to_day_of_year_with_tz", local => local.DayOfYear - 1),
        ("timestamp_to_day_of_month", "timestamp_to_day_of_month_with_tz", local => local.Day - 1),
        ("timestamp_to_day_of_month_1_based", "timestamp_to_day_of_month_1_based_with_tz", local => local.Day),
        ("timestamp_to_day_of_week", "timestamp_to_day_of_week_with_tz", local => (int)local.DayOfWeek),
        ("timestamp_to_hours", "timestamp_to_hours_with_tz", local => local.Hour),
        ("timestamp_to_minutes", "timestamp_to_minutes_with_tz", local => local.Minute),
        ("timestamp_to_seconds", "timestamp_to_seconds_tz", local => local.Second),
    ];

    private static readonly (string Name, int Offset)[] FixedZones = [("UTC", 0), ("+03:00", 10_800), ("-05:30", -19_800), ("23:59", 86_340), ("-00:00", 0)];

    private static readonly (long Seconds, int Nanoseconds)[] Instants =
    [
        (1_234_567_890, 123_456_789),
        (-1, 999_999_999),
        (951_825_600, 0),
        (-2_203_891_200, 500_000_000),
        (1_704_067_199, 999_000_000),
        (MinSeconds + (2 * 86_400), 1),
        (MaxSeconds - (2 * 86_400), 999_999_999),
    ];

    // Every instant, field and zone: the identifier, the instant, the zone or null, and the value from DateTime.
    public static TheoryData<string, long, int, string?, long> FieldCases()
    {
        var cases = new TheoryData<string, long, int, string?, long>();
        foreach ((long seconds, int nanoseconds) in Instants)
        {
            foreach ((string plain, string zoned, Func<DateTime, int> field) in Fields)
            {
                cases.Add(plain, seconds, nanoseconds, null, field(Local(seconds, 0)));
                foreach ((string name, int offset) in FixedZones)
                {
                    cases.Add(zoned, seconds, nanoseconds, name, field(Local(seconds, offset)));
                }
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(FieldCases))]
    public static void Field_IsThatOfTheLocalDateTime(string id, long seconds, int nanoseconds, string? zone, long expected)
    {
        CelValue result = zone is null ? Call(id, Ts(seconds, nanoseconds)) : Call(id, Ts(seconds, nanoseconds), Str(zone));

        Assert.Equal(expected, IntOf(result));
    }

    // The milliseconds are those of the second, whatever the zone.
    [Theory]
    [InlineData(123_456_789, 123)]
    [InlineData(999_999_999, 999)]
    [InlineData(1_000_000, 1)]
    [InlineData(999_999, 0)]
    [InlineData(0, 0)]
    public static void Milliseconds_AreThoseOfTheSecondWithAndWithoutAZone(int nanoseconds, long expected)
    {
        Assert.Equal(expected, IntOf(Call("timestamp_to_milliseconds", Ts(1_234_567_890, nanoseconds))));
        Assert.Equal(expected, IntOf(Call("timestamp_to_milliseconds_with_tz", Ts(1_234_567_890, nanoseconds), Str("-05:30"))));
        Assert.Equal(expected, IntOf(Call("timestamp_to_milliseconds_with_tz", Ts(-1, nanoseconds), Str("+03:00"))));
    }

    // 0001-01-01T00:00:00Z at -05:00 is 0000-12-31 19:00, and year 0 is a leap year; 9999-12-31T23:59:59Z at +05:00 is year 10000.
    [Fact]
    public static void LocalTimeAtTheEdgesOfTheRange_IsNotClamped()
    {
        CelValue minimum = Ts(MinSeconds);
        CelValue maximum = Ts(MaxSeconds);

        Assert.Equal(
            (0L, 11L, 365L, 30L, 31L, 19L),
            (
                IntOf(Call("timestamp_to_year_with_tz", minimum, Str("-05:00"))),
                IntOf(Call("timestamp_to_month_with_tz", minimum, Str("-05:00"))),
                IntOf(Call("timestamp_to_day_of_year_with_tz", minimum, Str("-05:00"))),
                IntOf(Call("timestamp_to_day_of_month_with_tz", minimum, Str("-05:00"))),
                IntOf(Call("timestamp_to_day_of_month_1_based_with_tz", minimum, Str("-05:00"))),
                IntOf(Call("timestamp_to_hours_with_tz", minimum, Str("-05:00")))));
        Assert.Equal(
            (10_000L, 0L, 0L, 0L, 1L, 4L, 59L, 59L),
            (
                IntOf(Call("timestamp_to_year_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_month_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_day_of_year_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_day_of_month_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_day_of_month_1_based_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_hours_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_minutes_with_tz", maximum, Str("+05:00"))),
                IntOf(Call("timestamp_to_seconds_tz", maximum, Str("+05:00")))));
    }

    // The zones of a database: a fixed change of offset, and a rule with daylight time.
    [Fact]
    public static void ZoneOfTheDatabase_GivesTheOffsetAtTheInstant()
    {
        CelTimeZoneDatabase database = Database();

        // Test/Moved is +01:00 before 1_000_000_000 (2001-09-09T01:46:40Z) and +02:00 from it.
        Assert.Equal(2, IntOf(Call("timestamp_to_hours_with_tz", database, Ts(999_999_999), Str("Test/Moved"))));
        Assert.Equal(3, IntOf(Call("timestamp_to_hours_with_tz", database, Ts(1_000_000_000), Str("Test/Moved"))));

        // America/Test follows EST5EDT: 2023-07-22T04:26:40Z is 00:26 in daylight time, 2023-11-14T22:13:20Z is 17:13.
        Assert.Equal(0, IntOf(Call("timestamp_to_hours_with_tz", database, Ts(1_690_000_000), Str("America/Test"))));
        Assert.Equal(26, IntOf(Call("timestamp_to_minutes_with_tz", database, Ts(1_690_000_000), Str("America/Test"))));
        Assert.Equal(17, IntOf(Call("timestamp_to_hours_with_tz", database, Ts(1_700_000_000), Str("America/Test"))));
    }

    // A fixed offset and UTC do not need the database, and an entry named like a fixed offset or UTC is never used.
    [Fact]
    public static void FixedOffsetAndUtc_NeedNoDatabase()
    {
        Assert.Equal(2, IntOf(Call("timestamp_to_hours_with_tz", null, Ts(82_800), Str("+03:00"))));
        Assert.Equal(23, IntOf(Call("timestamp_to_hours_with_tz", CelTimeZoneDatabase.Empty, Ts(82_800), Str("UTC"))));
    }

    [Fact]
    public static void NameNotInTheDatabase_IsTimeZoneNotFoundWithTheVersion()
    {
        CelTimeZoneDatabase database = Database();

        foreach (string name in new[] { "Mars/Base", "", "Local", "utc", "test/moved", " UTC" })
        {
            Assert.Equal(
                (CelErrorCode.TimeZoneNotFound, Offset, "time zone '" + name + "' is not found in the time zone database of the environment (version 2026a)"),
                Error(Call("timestamp_to_year_with_tz", database, Ts(0), Str(name))));
        }
    }

    [Fact]
    public static void NameNotFound_WithADatabaseWithoutAVersion_HasNoVersionInTheText()
    {
        Assert.Equal(
            (CelErrorCode.TimeZoneNotFound, Offset, "time zone 'Mars/Base' is not found in the time zone database of the environment"),
            Error(Call("timestamp_to_year_with_tz", CelTimeZoneDatabase.Empty, Ts(0), Str("Mars/Base"))));
    }

    [Fact]
    public static void NameNotFound_WithoutADatabase_SaysThereIsNone()
    {
        Assert.Equal(
            (CelErrorCode.TimeZoneNotFound, Offset, "time zone 'Europe/Moscow' is not found: the environment has no time zone database"),
            Error(Call("timestamp_to_year_with_tz", null, Ts(0), Str("Europe/Moscow"))));
    }

    [Theory]
    [InlineData("+1:5")]
    [InlineData("+001:30")]
    [InlineData("+24:00")]
    [InlineData("+03:60")]
    [InlineData("Europe:Moscow")]
    [InlineData(":")]
    public static void NameWithAColonThatIsNoOffset_IsInvalidTimeZone(string name)
    {
        Assert.Equal(
            (CelErrorCode.InvalidTimeZone, Offset, "invalid time zone '" + name + "': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM"),
            Error(Call("timestamp_to_year_with_tz", Database(), Ts(0), Str(name))));
    }

    [Theory]
    [InlineData("timestamp_to_year_with_tz")]
    [InlineData("timestamp_to_month_with_tz")]
    [InlineData("timestamp_to_day_of_year_with_tz")]
    [InlineData("timestamp_to_day_of_month_with_tz")]
    [InlineData("timestamp_to_day_of_month_1_based_with_tz")]
    [InlineData("timestamp_to_day_of_week_with_tz")]
    [InlineData("timestamp_to_hours_with_tz")]
    [InlineData("timestamp_to_minutes_with_tz")]
    [InlineData("timestamp_to_seconds_tz")]
    [InlineData("timestamp_to_milliseconds_with_tz")]
    public static void EveryReceiverWithAZone_FailsOnABadZoneAtTheCall(string id)
    {
        Assert.Equal(CelErrorCode.TimeZoneNotFound, Error(Call(id, Database(), Ts(0), Str("Mars/Base"))).Code);
        Assert.Equal(CelErrorCode.InvalidTimeZone, Error(Call(id, Database(), Ts(0), Str("+1:5"))).Code);
        Assert.Equal(Offset, Error(Call(id, Database(), Ts(0), Str("Mars/Base"))).Offset);
    }

    // The kinds of the arguments are checked before the zone name is read.
    [Fact]
    public static void KindsOfTheArguments_ComeBeforeTheZoneName()
    {
        AssertNoMatchingOverload("timestamp_to_year_with_tz", Ts(0), Int(5));
        AssertNoMatchingOverload("timestamp_to_year_with_tz", Du(0), Str("UTC"));
        AssertNoMatchingOverload("timestamp_to_hours_with_tz", Ts(0), Du(1));
    }

    [Theory]
    [InlineData("timestamp_to_year")]
    [InlineData("timestamp_to_month")]
    [InlineData("timestamp_to_day_of_year")]
    [InlineData("timestamp_to_day_of_month")]
    [InlineData("timestamp_to_day_of_month_1_based")]
    [InlineData("timestamp_to_day_of_week")]
    [InlineData("timestamp_to_hours")]
    [InlineData("timestamp_to_minutes")]
    [InlineData("timestamp_to_seconds")]
    [InlineData("timestamp_to_milliseconds")]
    [InlineData("duration_to_hours")]
    [InlineData("duration_to_minutes")]
    [InlineData("duration_to_seconds")]
    [InlineData("duration_to_milliseconds")]
    public static void EveryReceiverWithoutAZone_RejectsAValueOfAnotherKind(string id)
    {
        AssertNoMatchingOverload(id, id is ['d', ..] ? Ts(0) : Du(0));
        AssertNoMatchingOverload(id, Int(0));
    }

    // The whole hours, minutes and seconds are truncated toward zero; the milliseconds are those within the second, with the
    // sign of the duration.
    [Theory]
    [InlineData(5_400_000_000_000L, 1L, 90L, 5_400L, 0L)]
    [InlineData(-5_401_234_567_000L, -1L, -90L, -5_401L, -234L)]
    [InlineData(1_234_000_000L, 0L, 0L, 1L, 234L)]
    [InlineData(-999_999_999L, 0L, 0L, 0L, -999L)]
    [InlineData(0L, 0L, 0L, 0L, 0L)]
    [InlineData(long.MinValue, -2_562_047L, -153_722_867L, -9_223_372_036L, -854L)]
    [InlineData(long.MaxValue, 2_562_047L, 153_722_867L, 9_223_372_036L, 854L)]
    public static void DurationReceivers_TruncateTowardZero(long nanoseconds, long hours, long minutes, long seconds, long milliseconds)
    {
        Assert.Equal(
            (hours, minutes, seconds, milliseconds),
            (
                IntOf(Call("duration_to_hours", Du(nanoseconds))),
                IntOf(Call("duration_to_minutes", Du(nanoseconds))),
                IntOf(Call("duration_to_seconds", Du(nanoseconds))),
                IntOf(Call("duration_to_milliseconds", Du(nanoseconds)))));
    }

    [Fact]
    public static void Functions_HaveTheirOverloadsInTheOrderOfTheStandardLibraryPage()
    {
        Assert.Equal(
            ["timestamp_to_year", "timestamp_to_year_with_tz"],
            Ids("getFullYear"));
        Assert.Equal(
            ["timestamp_to_hours", "timestamp_to_hours_with_tz", "duration_to_hours"],
            Ids("getHours"));
        Assert.Equal(
            ["timestamp_to_milliseconds", "timestamp_to_milliseconds_with_tz", "duration_to_milliseconds"],
            Ids("getMilliseconds"));
    }

    private static DateTime Local(long seconds, int offset) => Epoch.AddTicks((seconds + offset) * 10_000_000L);

    private static CelTimeZoneDatabase Database() => CelTimeZoneDatabase.Create(
        [
            TzifFile.Pair("Test/Moved", TzifFile.Build([1_000_000_000], [1], [3_600, 7_200], string.Empty)),
            TzifFile.Pair("America/Test", TzifFile.WithRule("EST5EDT,M3.2.0,M11.1.0", 0)),
        ],
        "2026a");
}
