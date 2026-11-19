using System;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The time zones of the operating system, below what the examples fix: the grammar of names row by row, the requests to the
// platform, the charge, the offset at the ends of the range, the conflict with another database.
public sealed class SystemZoneSourceTests
{
    private static readonly CelTimeZoneDatabase Database = CelTimeZoneDatabase.FromSystem();

    [Theory]
    [InlineData("Europe/Moscow")]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("Etc/GMT+5")]
    [InlineData("Etc/GMT-14")]
    [InlineData("GMT0")]
    [InlineData("a")]
    [InlineData("Posix/Europe/Moscow")]
    [InlineData("posixx/Europe/Moscow")]
    [InlineData("Europe/posix")]
    [InlineData("Europe/right")]
    [InlineData("A-B_c+9/d")]
    public static void NameOfTheForm_IsAnIanaName(string name)
    {
        Assert.True(TimeZoneNameRules.IsIanaName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("Europe/")]
    [InlineData("/Europe")]
    [InlineData("Europe//Moscow")]
    [InlineData("Europe/Moscow/")]
    [InlineData("Europe Moscow")]
    [InlineData("Russian Standard Time")]
    [InlineData("Europe\\Moscow")]
    [InlineData("a.b")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../etc/passwd")]
    [InlineData("./Europe/Moscow")]
    [InlineData("Europe/\u041C\u043E\u0441\u043A\u0432\u0430")]
    [InlineData("Europe/Moscöw")]
    [InlineData("Europe/Moscow\0")]
    [InlineData("Europe:Moscow")]
    [InlineData("+03:00:00")]
    [InlineData("posix")]
    [InlineData("right")]
    [InlineData("posix/Europe/Moscow")]
    [InlineData("right/Europe/Moscow")]
    public static void NameThatIsNotOfTheForm_IsNotAnIanaName(string name)
    {
        Assert.False(TimeZoneNameRules.IsIanaName(name));
    }

    [Fact]
    public void LengthOf64_IsTheLongestName()
    {
        Assert.True(TimeZoneNameRules.IsIanaName(new string('a', 64)));
        Assert.True(TimeZoneNameRules.IsIanaName("Europe/" + new string('a', 57)));
        Assert.False(TimeZoneNameRules.IsIanaName(new string('a', 65)));
        Assert.False(TimeZoneNameRules.IsIanaName("Europe/" + new string('a', 58)));
    }

    [Fact]
    public void EveryCharacterOfTheGrammar_IsAllowedAndNoOtherAsciiCharacter()
    {
        for (char c = '\0'; c < 128; c++)
        {
            bool allowed = char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '+' or '/';

            Assert.Equal(allowed, TimeZoneNameRules.IsIanaName("a" + c + "b"));
        }
    }

    // A name outside the grammar never reaches the platform, whether it comes through Contains or through an expression.
    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("posix/Europe/Moscow")]
    [InlineData("right/Europe/Moscow")]
    [InlineData("Russian Standard Time")]
    [InlineData("")]
    [InlineData("Europe//Moscow")]
    public static void NameOutsideTheGrammar_IsNotAskedOfThePlatform(string name)
    {
        long before = SystemZoneSource.PlatformCalls;

        Assert.False(Database.Contains(name));
        Assert.Equal(CelErrorCode.TimeZoneNotFound, EvaluationError("timestamp(0).getHours('" + name + "')").Code);
        Assert.Equal(before, SystemZoneSource.PlatformCalls);
    }

    // A name of the form that the platform does not know is asked each time: a miss is not kept, so a zone that appears later is found.
    [Fact]
    public void Miss_IsAskedEachTimeAndNotKept()
    {
        long before = SystemZoneSource.PlatformCalls;

        Assert.False(Database.Contains("Europe/Nowhere"));
        Assert.False(Database.Contains("Europe/Nowhere"));

        Assert.Equal(before + 2, SystemZoneSource.PlatformCalls);
    }

    [Fact]
    public void FoundZone_IsKept_AndNotAskedAgain()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the zones of the machine are checked on Linux");

        Assert.True(Database.Contains("Asia/Tokyo"));
        long kept = SystemZoneSource.PlatformCalls;

        Assert.True(Database.Contains("Asia/Tokyo"));
        Assert.Equal(12L, Evaluate("timestamp('2026-07-01T03:00:00Z').getHours('Asia/Tokyo')"));
        Assert.Equal(kept, SystemZoneSource.PlatformCalls);
    }

    [Fact]
    public void Contains_NullIdentifier_IsArgumentNullException()
    {
        Assert.Equal("zoneId", Assert.Throws<ArgumentNullException>(() => Database.Contains(null!)).ParamName);
    }

    // Z is calibrated by the typical time of a miss of the platform, 12 to 19 microseconds at 10 nanoseconds a unit (docs/cost.md):
    // 2 000 units, not the 1 000 of the first calibration.
    [Fact]
    public void Z_Is2000Units()
    {
        Assert.Equal(2_000UL, SystemZoneSource.Cost);
    }

    // The charge is the same Z whether the name is found, kept, missed or refused by the grammar, and comes before the search;
    // UTC, a fixed offset and a name that is no offset are settled before the database and cost nothing of it.
    [Theory]
    [InlineData("Europe/Paris")]
    [InlineData("Europe/Pariz")]
    [InlineData("Europe/Par s")]
    [InlineData("../etc/passwd")]
    [InlineData("")]
    public static void NameThatReachesTheDatabase_ChargesZ(string name)
    {
        var meter = new RecordingMeter();

        _ = TimeZoneRules.Resolve(Database, name, meter);
        _ = TimeZoneRules.Resolve(Database, name, meter);

        Assert.Equal(new[] { SystemZoneSource.Cost, SystemZoneSource.Cost }, meter.Charges);
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("+03:00")]
    [InlineData("-01:30")]
    [InlineData("12:xx")]
    public static void NameSettledBeforeTheDatabase_ChargesNothing(string name)
    {
        var meter = new RecordingMeter();

        _ = TimeZoneRules.Resolve(Database, name, meter);

        Assert.Empty(meter.Charges);
    }

    [Fact]
    public void OtherDatabases_ChargeNothing()
    {
        var meter = new RecordingMeter();

        _ = TimeZoneRules.Resolve(CelTimeZoneDatabase.Empty, "Europe/Paris", meter);
        _ = TimeZoneRules.Resolve(null, "Europe/Paris", meter);
        _ = TimeZoneRules.Resolve(Database, "Europe/Paris");

        Assert.Empty(meter.Charges);
    }

    // The offset of a zone at an instant is the one of TimeZoneInfo, at the ends of the range of timestamp too.
    [Theory]
    [InlineData("Europe/Moscow")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kathmandu")]
    [InlineData("Australia/Lord_Howe")]
    [InlineData("Etc/GMT+5")]
    public static void Offset_IsTheOffsetOfTimeZoneInfo(string name)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the zones of the machine are checked on Linux");
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(name);
        TimeZoneResolution resolution = TimeZoneRules.Resolve(Database, name);

        Assert.Equal(TimeZoneResolutionKind.Zone, resolution.Kind);
        long[] instants =
        [
            -62_135_596_800, -2_000_000_000, -1, 0, 1_000_000_000, 1_700_000_000, 1_711_846_800, 1_730_000_000, 4_000_000_000, 253_402_300_799,
        ];
        foreach (long instant in instants)
        {
            long ticks = zone.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(instant)).Ticks;

            Assert.Equal((int)(ticks / TimeSpan.TicksPerSecond), resolution.OffsetAt(instant));
        }
    }

    [Fact]
    public void ZoneWithDaylightSaving_ChangesItsOffsetAtTheTransition()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the zones of the machine are checked on Linux");

        Assert.Equal(1L, Evaluate("timestamp('2026-01-15T00:30:00Z').getHours('Europe/Paris')"));
        Assert.Equal(2L, Evaluate("timestamp('2026-07-15T00:30:00Z').getHours('Europe/Paris')"));
        Assert.Equal(1L, Evaluate("timestamp('2026-03-29T00:59:59Z').getHours('Europe/Paris')"));
        Assert.Equal(3L, Evaluate("timestamp('2026-03-29T01:00:00Z').getHours('Europe/Paris')"));
    }

    [Fact]
    public void Resolution_IsSafeFromManyThreadsAtOnce()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the zones of the machine are checked on Linux");
        string[] names = ["Europe/Berlin", "Europe/Rome", "Asia/Seoul", "America/Chicago", "Africa/Johannesburg", "Europe/Nowhere", "Mars/Olympus"];
        int[] expected = [3_600 * 2, 3_600 * 2, 3_600 * 9, -3_600 * 5, 3_600 * 2, 0, 0];

        Parallel.For(0, 200, index =>
        {
            int position = index % names.Length;
            TimeZoneResolution resolution = TimeZoneRules.Resolve(Database, names[position]);

            Assert.Equal(position < 5 ? TimeZoneResolutionKind.Zone : TimeZoneResolutionKind.NotFound, resolution.Kind);
            Assert.Equal(expected[position], resolution.OffsetAt(1_783_000_000));
        });
    }

    // The same instance in a builder twice is one database; another database next to it is the conflict.
    [Fact]
    public void SystemDatabase_AddedTwice_IsOneDatabase_AndNextToAnotherIsAConflict()
    {
        Assert.True(new CelEnvironmentBuilder().UseTimeZones(Database).UseTimeZones(CelTimeZoneDatabase.FromSystem()).Build().IsSuccess);

        CelBuildResult conflict = new CelEnvironmentBuilder().UseTimeZones(Database).UseTimeZones(CelTimeZoneDatabase.Empty).Build();

        Assert.False(conflict.IsSuccess);
        Assert.Equal(CelErrorCode.TimeZoneDatabaseConflict, Assert.Single(conflict.Errors).Code);
    }

    [Fact]
    public void TimeZoneNotFound_OfTheSystemDatabase_NamesTheZoneAndNoVersion()
    {
        CelError error = EvaluationError("timestamp(0).getHours('Mars/Olympus_Mons')");

        Assert.Equal("Mars/Olympus_Mons", error.Subject);
        Assert.Equal("time zone 'Mars/Olympus_Mons' is not found in the time zone database of the environment", error.Message);
    }

    private static CelEnvironment Environment() =>
        new CelEnvironmentBuilder().UseTimeZones(Database).Build().GetEnvironmentOrThrow();

    private static long Evaluate(string text) =>
        Environment().Compile<long>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static CelError EvaluationError(string text) =>
        Assert.Single(Environment().Compile<long>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).Errors);
}
