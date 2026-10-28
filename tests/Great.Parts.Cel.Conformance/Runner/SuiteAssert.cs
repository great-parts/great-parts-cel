using System;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>The end of the test of a suite: the summary line and the findings.</summary>
internal static class SuiteAssert
{
    /// <summary>
    /// Writes the summary line of the run to the output of the test, with the date of today in UTC, and fails with the lines of
    /// the findings when the run has a finding with the severity <c>error</c>.
    /// </summary>
    /// <param name="result">The result of the run.</param>
    /// <param name="source">The source of the suite in the summary line.</param>
    internal static void PassOrAreSkipped(SuiteResult result, string source)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(result.Summary(source, DateOnly.FromDateTime(DateTime.UtcNow)));
        string[] errors = [.. result.Findings.Where(finding => finding.Severity == FindingSeverity.Error).Select(finding => finding.ToString())];
        if (errors.Length > 0)
        {
            Assert.Fail(string.Join('\n', errors));
        }
    }
}
