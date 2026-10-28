using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>Assertions on the findings of a check.</summary>
internal static class FindingAssert
{
    /// <summary>
    /// Asserts that the findings are exactly the expected errors, compared by code, path, line, column and message, and
    /// that every finding line has the common form.
    /// </summary>
    /// <param name="actual">The findings of the check.</param>
    /// <param name="expected">The expected errors.</param>
    internal static void Exactly(IReadOnlyList<AnalyzerFinding> actual, params AnalyzerFinding[] expected)
    {
        foreach (AnalyzerFinding finding in actual)
        {
            Assert.Matches(AnalyzerFinding.Pattern, finding.Format());
        }

        Assert.Equal(
            expected.Select(finding => finding.Format()).Order(StringComparer.Ordinal),
            actual.Select(finding => finding.Format()).Order(StringComparer.Ordinal));
    }

    /// <summary>Asserts that the check found nothing.</summary>
    /// <param name="actual">The findings of the check.</param>
    internal static void None(IReadOnlyList<AnalyzerFinding> actual) => Exactly(actual);

    /// <summary>An expected error finding.</summary>
    /// <param name="code">The diagnostic ID.</param>
    /// <param name="path">The path of the file.</param>
    /// <param name="line">The line, starting with one.</param>
    /// <param name="column">The column, starting with one.</param>
    /// <param name="message">The message.</param>
    internal static AnalyzerFinding Error(string code, string path, int line, int column, string message) => new(path, line, column, "error", code, message);
}
