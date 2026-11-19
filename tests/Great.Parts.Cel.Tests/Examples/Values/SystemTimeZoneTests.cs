using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The time zones of the operating system: CelTimeZoneDatabase.FromSystem() reads a zone through TimeZoneInfo, only by an IANA
// identifier that passes the grammar of docs/limits.md ("Time zones of the operating system"). Anything else is TimeZoneNotFound with the
// name, on every OS. The rows that need a zone of the machine run on Linux, where the CI runs them; elsewhere they are skipped.
// The test may use TimeZoneInfo: the ban is on the library.
[Trait("Requirement", "F-15")]
public sealed class SystemTimeZoneTests
{
    private const string Moment = "timestamp('2026-07-01T12:00:00Z')";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .UseTimeZones(CelTimeZoneDatabase.FromSystem())
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void FromSystem_IsOneInstanceWithoutAListOrAVersion()
    {
        Assert.Same(CelTimeZoneDatabase.FromSystem(), CelTimeZoneDatabase.FromSystem());
        Assert.Empty(CelTimeZoneDatabase.FromSystem().ZoneIds);
        Assert.Null(CelTimeZoneDatabase.FromSystem().Version);
    }

    // Names outside the grammar never reach the platform; a Windows identifier is refused on every OS.
    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("Russian Standard Time")]
    [InlineData("UTC-02")]
    [InlineData("../etc/passwd")]
    [InlineData("posix/Europe/Moscow")]
    [InlineData("right/Europe/Moscow")]
    [InlineData("Europe//Moscow")]
    [InlineData("Local")]
    [InlineData("")]
    [InlineData("Europe/Moscow/")]
    public static void NameThatIsNotAnIanaZone_IsTimeZoneNotFound(string name)
    {
        CelError error = EvaluationError(Moment + ".getHours('" + name + "')");

        Assert.Equal(CelErrorCode.TimeZoneNotFound, error.Code);
        Assert.Equal(name, error.Subject);
        Assert.False(CelTimeZoneDatabase.FromSystem().Contains(name));
    }

    [Fact]
    public void NameLongerThan64_IsTimeZoneNotFound()
    {
        string name = "Europe/" + new string('a', 58);

        Assert.Equal(65, name.Length);
        Assert.Equal(CelErrorCode.TimeZoneNotFound, EvaluationError(Moment + ".getHours('" + name + "')").Code);
    }

    [Theory]
    [InlineData("Europe/Moscow")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kathmandu")]
    public static void IanaZone_HasTheOffsetOfTimeZoneInfo(string name)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the zones of the machine are checked on Linux");

        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(name);
        DateTimeOffset local = TimeZoneInfo.ConvertTime(new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero), zone);

        Assert.True(CelTimeZoneDatabase.FromSystem().Contains(name));
        Assert.Equal((long)local.Hour, Evaluate<long>(Moment + ".getHours('" + name + "')"));
        Assert.Equal((long)local.Minute, Evaluate<long>(Moment + ".getMinutes('" + name + "')"));
    }

    // UTC and a fixed offset are resolved before the database and do not cost Z.
    [Fact]
    public void UtcAndFixedOffset_DoNotNeedTheSystem()
    {
        Assert.Equal(12L, Evaluate<long>(Moment + ".getHours('UTC')"));
        Assert.Equal(15L, Evaluate<long>(Moment + ".getHours('+03:00')"));
    }

    // The cost of a name that reaches the database is the same whether it is found, stored or not found, and whether it passed
    // the grammar: 1 + ceil(L/10) + Z for the receiver, the same L in each row.
    [Fact]
    public void Cost_DoesNotDependOnTheOutcome()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the zones of the machine are checked on Linux");

        ulong found = Cost(Moment + ".getHours('Europe/Paris')");
        ulong stored = Cost(Moment + ".getHours('Europe/Paris')");
        ulong missing = Cost(Moment + ".getHours('Europe/Pariz')");
        ulong refused = Cost(Moment + ".getHours('Europe/Par s')");

        Assert.Equal(found, stored);
        Assert.Equal(found, missing);
        Assert.Equal(found, refused);

        // A fixed offset of 6 characters costs one unit of text less and no Z; Z is at least 1 000.
        Assert.True(found - Cost(Moment + ".getHours('+01:00')") >= 1 + 1_000);
    }

    private static T Evaluate<T>(string text) =>
        Environment.Compile<T>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static CelError EvaluationError(string text)
    {
        CelEvalResult<long> result = Environment.Compile<long>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        return Assert.Single(result.Errors);
    }

    // The cost of an evaluation with a limit, whatever its outcome.
    private static ulong Cost(string text)
    {
        CelEvalResult<long> result = Environment.Compile<long>(text).GetProgramOrThrow()
            .Evaluate(new CelBindings(), new CelEvalOptions { CostLimit = ulong.MaxValue }, CancellationToken.None);

        return result.Cost!.Value;
    }
}
