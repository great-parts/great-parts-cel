using System;
using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG903</c>: no record of the regression list is a divergence signature that is not resolved yet (<c>Verdict</c> is <c>pending</c>)
/// or an open one of the highest severity (<c>Verdict</c> is <c>our-bug</c> and <c>Severity</c> is <c>S1</c>).
/// </summary>
internal static class RegressionListGate
{
    /// <summary>The list of the regression corpus.</summary>
    internal const string ListPath = "tests/Great.Parts.Cel.Conformance/Regression/skips.md";

    /// <summary>Checks the list.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="tag">The release tag.</param>
    internal static IEnumerable<Finding> Check(string root, ReleaseTag tag)
    {
        List<Finding> findings = [];
        foreach (ListRecord record in MarkdownList.Read(root, ListPath, "Signature", "Verdict", "Severity"))
        {
            string signature = record["Signature"];
            string severity = record["Severity"];
            if (string.Equals(record["Verdict"], "pending", StringComparison.Ordinal))
            {
                findings.Add(Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng903, $"unresolved divergence signature: '{signature}' ({severity})"));
            }
            else if (string.Equals(record["Verdict"], "our-bug", StringComparison.Ordinal) && string.Equals(severity, "S1", StringComparison.Ordinal))
            {
                findings.Add(Finding.ForTag(tag.Name, FindingSeverity.Error, EngCodes.Eng903, $"open divergence of the highest severity: '{signature}' ({severity}), the verdict is our-bug"));
            }
        }

        return findings;
    }
}
