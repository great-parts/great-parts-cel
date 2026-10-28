using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

/// <summary>Assertions on the findings of the runner.</summary>
internal static class FindingAssert
{
    /// <summary>
    /// Asserts that there is one finding, an error with the code, the path, the line, the column and the message, and that its line
    /// has the common form of the findings.
    /// </summary>
    /// <param name="findings">The findings of the runner.</param>
    /// <param name="code">The expected code.</param>
    /// <param name="path">The expected path.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    /// <param name="message">The expected message.</param>
    internal static void Single(IReadOnlyList<Finding> findings, string code, string path, int line, int column, string message)
    {
        Finding finding = Assert.Single(findings);
        Assert.Matches(Finding.Pattern, finding.ToString());
        Assert.Equal(
            (code, path, line, column, FindingSeverity.Error, message),
            (finding.Code, finding.Path, finding.Line, finding.Column, finding.Severity, finding.Message));
    }
}
