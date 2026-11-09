using System;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Environment;

// Results do not depend on the time zone of the process (docs/design/tests.md, "Culture of the process"): in a process whose
// zone is Asia/Kathmandu, +05:45, a zone whose offset is not a whole number of hours, the expectations are those of a process in
// UTC. On Linux the test sets TZ, clears the cache of the zone and asserts that the zone of the process is the one it set, so a
// run that cannot set the zone fails instead of passing; it restores both in finally. On another operating system the zone is
// not set and the assertion of the offset is left out; the expectations and the data are the same.
[Trait("Requirement", "R-5")]
public sealed class TimeZoneFitnessTests
{
    private const string ProcessZone = "Asia/Kathmandu";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseTimeZones(ZoneInfoFile.Database())
        .AddVariable<DateTimeOffset>("offset")
        .AddVariable<DateTime>("utc")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public static void ProcessZone_IsTheZoneTheTestSet() =>
        InProcessZone(() =>
        {
            if (OperatingSystem.IsLinux())
            {
                Assert.Equal(new TimeSpan(5, 45, 0), TimeZoneInfo.Local.BaseUtcOffset);
            }
        });

    [Theory]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getHours()", 0)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getMinutes()", 0)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getDate()", 1)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getDayOfMonth()", 0)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getDayOfWeek()", 0)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getHours('UTC')", 0)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getHours('+00:00')", 0)]
    [InlineData("timestamp('2022-12-31T23:59:59Z').getFullYear()", 2022)]
    [InlineData("timestamp('2022-12-31T23:59:59Z').getFullYear('UTC')", 2022)]
    [InlineData("timestamp(0).getMilliseconds()", 0)]
    [InlineData("timestamp(1234567890).getSeconds()", 30)]
    [InlineData("duration('36h').getHours()", 36)]
    public static void ReceiversWithoutAZone_AreInUtc(string expression, long expected) =>
        InProcessZone(() => Assert.Equal(expected, Environment.Evaluate<long>(expression, new CelBindings()).GetValueOrThrow()));

    [Theory]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getHours('Asia/Kathmandu')", 5)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getMinutes('Asia/Kathmandu')", 45)]
    [InlineData("timestamp('2023-01-01T00:00:00Z').getHours('+05:45')", 5)]
    [InlineData("timestamp('2023-07-01T00:00:00Z').getHours('America/New_York')", 20)]
    public static void ReceiversWithAZone_UseTheZoneOfTheCallAndNotOfTheProcess(string expression, long expected) =>
        InProcessZone(() => Assert.Equal(expected, Environment.Evaluate<long>(expression, new CelBindings()).GetValueOrThrow()));

    [Fact]
    public static void Timestamp_IsTextInUtc() =>
        InProcessZone(() =>
        {
            Assert.Equal("1970-01-01T00:00:00Z", Environment.Evaluate<string>("string(timestamp(0))", new CelBindings()).GetValueOrThrow());
            Assert.Equal(
                "2023-01-01T00:00:00Z",
                Environment.Evaluate<string>("string(timestamp('2023-01-01T05:45:00+05:45'))", new CelBindings()).GetValueOrThrow());
        });

    [Fact]
    public static void DateTimeOffset_OfOffsetFiveFortyFive_IsTheSameMoment() =>
        InProcessZone(() =>
        {
            var local = new DateTimeOffset(2023, 1, 1, 5, 45, 0, new TimeSpan(5, 45, 0));
            var bindings = new CelBindings().Add("offset", local).Add("utc", new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.True(Environment.Evaluate<bool>("offset == timestamp('2023-01-01T00:00:00Z')", bindings).GetValueOrThrow());
            Assert.Equal(0, Environment.Evaluate<long>("offset.getHours()", bindings).GetValueOrThrow());
            Assert.Equal(5, Environment.Evaluate<long>("offset.getHours('Asia/Kathmandu')", bindings).GetValueOrThrow());
            Assert.True(Environment.Evaluate<bool>("utc == offset", bindings).GetValueOrThrow());
        });

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public static void DateTime_OfAnotherKindThanUtc_IsRefused(DateTimeKind kind) =>
        InProcessZone(() => Assert.Throws<ArgumentException>(() => new CelBindings().Add("utc", new DateTime(2023, 1, 1, 0, 0, 0, kind))));

    [Theory]
    [InlineData("timestamp(0).getHours('Local')", CelErrorCode.TimeZoneNotFound)]
    [InlineData("timestamp(0).getHours('')", CelErrorCode.TimeZoneNotFound)]
    [InlineData("timestamp(0).getHours('local')", CelErrorCode.TimeZoneNotFound)]
    [InlineData("timestamp(0).getHours('+1:5')", CelErrorCode.InvalidTimeZone)]
    public static void ZoneOfTheProcess_IsNotAZoneName(string expression, CelErrorCode code) =>
        InProcessZone(() =>
        {
            CelEvalResult<long> result = Environment.Evaluate<long>(expression, new CelBindings());

            Assert.Equal(code, Assert.Single(result.Errors).Code);
        });

    [Fact]
    public static void Results_OfTheTypesOfTime_HaveNoOffsetOfTheProcess() =>
        InProcessZone(() =>
        {
            DateTimeOffset moment = Environment.Evaluate<DateTimeOffset>("timestamp('2023-01-01T00:00:00Z')", new CelBindings()).GetValueOrThrow();
            TimeSpan span = Environment.Evaluate<TimeSpan>("duration('90m')", new CelBindings()).GetValueOrThrow();

            Assert.Equal(TimeSpan.Zero, moment.Offset);
            Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), moment.UtcDateTime);
            Assert.Equal(TimeSpan.FromMinutes(90), span);
        });

    // Runs the check in a process whose zone is Asia/Kathmandu: on Linux TZ is set and the cache of the zone cleared, and both are
    // restored in finally. The variable is read by TimeZoneInfo.Local on its first use after ClearCachedData.
    private static void InProcessZone(Action check)
    {
        if (!OperatingSystem.IsLinux())
        {
            check();
            return;
        }

        string? previous = System.Environment.GetEnvironmentVariable("TZ");
        try
        {
            System.Environment.SetEnvironmentVariable("TZ", ProcessZone);
            TimeZoneInfo.ClearCachedData();
            Assert.Equal(new TimeSpan(5, 45, 0), TimeZoneInfo.Local.BaseUtcOffset);
            check();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TZ", previous);
            TimeZoneInfo.ClearCachedData();
        }
    }
}
