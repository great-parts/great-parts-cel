using System;
using System.Linq;
using Great.Parts.Cel.Conformance.CelSpec;
using Great.Parts.Cel.Conformance.Regression;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Conformance.Environment;

// The suites do not depend on the time zone of the process (docs/design/tests.md, "Culture of the process"): in a process whose
// zone is Asia/Kathmandu, +05:45, the cel-spec suite and the regression corpus run with their skip lists once more and fail on
// the same findings as in a normal run. On Linux the test sets TZ, clears the cache of the zone and asserts that the zone of the
// process is the one it set, so a run that cannot set the zone fails instead of passing; it restores both in finally. On another
// operating system the zone is not set and the assertion of the offset is left out.
[Trait("Requirement", "R-5")]
public sealed class TimeZoneFitnessTests
{
    private const string ProcessZone = "Asia/Kathmandu";

    [Fact]
    public static void CelSpecSuite_PassesOrIsSkippedInTheZone() =>
        FailOnErrors(InProcessZone(() => CelSpecSuite.Run(new LibraryEvaluator(), RunCell.Current)));

    [Fact]
    public static void RegressionSuite_PassesOrIsSkippedInTheZone() =>
        FailOnErrors(InProcessZone(() => RegressionSuite.Run(new LibraryEvaluator(), RunCell.Current)));

    // The findings with the severity error, without the summary line of the suite tests.
    private static void FailOnErrors(SuiteResult result)
    {
        string[] errors = [.. result.Findings.Where(finding => finding.Severity == FindingSeverity.Error).Select(finding => finding.ToString())];
        Assert.True(errors.Length == 0, string.Join('\n', errors));
    }

    // Runs the suite in a process whose zone is Asia/Kathmandu: on Linux TZ is set and the cache of the zone cleared, and both are
    // restored in finally. The variable is read by TimeZoneInfo.Local on its first use after ClearCachedData.
    private static SuiteResult InProcessZone(Func<SuiteResult> run)
    {
        if (!OperatingSystem.IsLinux())
        {
            return run();
        }

        string? previous = System.Environment.GetEnvironmentVariable("TZ");
        try
        {
            System.Environment.SetEnvironmentVariable("TZ", ProcessZone);
            TimeZoneInfo.ClearCachedData();
            Assert.Equal(new TimeSpan(5, 45, 0), TimeZoneInfo.Local.BaseUtcOffset);
            return run();
        }
        finally
        {
            System.Environment.SetEnvironmentVariable("TZ", previous);
            TimeZoneInfo.ClearCachedData();
        }
    }
}
