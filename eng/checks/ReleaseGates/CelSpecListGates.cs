using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG803</c>: every record of the skip list of the cel-spec suite has a reason and a link
/// <c>https://github.com/great-parts/great-parts-cel/issues/&lt;N&gt;</c>.
/// </summary>
internal static class SkipReasonGate
{
    private static readonly Regex IssueLink = new(
        @"^https://github\.com/great-parts/great-parts-cel/issues/[1-9][0-9]*\z",
        RegexOptions.CultureInvariant);

    /// <summary>Checks the list.</summary>
    /// <param name="root">The full path of the repository root.</param>
    internal static IEnumerable<Finding> Check(string root)
    {
        List<Finding> findings = [];
        foreach (ListRecord record in CelSpecList.Read(root, "Case", "Reason", "Issue"))
        {
            if (record["Reason"].Length == 0)
            {
                findings.Add(CelSpecList.At(record, EngCodes.Eng803, $"entry '{record["Case"]}' has no reason"));
            }

            if (!IssueLink.IsMatch(record["Issue"]))
            {
                findings.Add(CelSpecList.At(record, EngCodes.Eng803, $"entry '{record["Case"]}' has no issue link to great-parts/great-parts-cel"));
            }
        }

        return findings;
    }
}

/// <summary>
/// <c>ENG906</c>: in the release <c>1.0.0</c> every record of the skip list of the cel-spec suite has a <c>Reference</c>, a link to the
/// language definition or to a minimal case. The gate does not apply to another version, and an empty list passes.
/// </summary>
internal static class SkipReferenceGate
{
    private const string Release = "1.0.0";

    /// <summary>Checks the list.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="tag">The release tag.</param>
    internal static IEnumerable<Finding> Check(string root, ReleaseTag tag)
    {
        List<Finding> findings = [];
        if (!string.Equals(tag.Version, Release, StringComparison.Ordinal))
        {
            return findings;
        }

        foreach (ListRecord record in CelSpecList.Read(root, "Case", "Reference"))
        {
            if (record["Reference"].Length == 0)
            {
                findings.Add(CelSpecList.At(record, EngCodes.Eng906, $"release {Release}: entry '{record["Case"]}' has no reference to the language definition or a minimal case"));
            }
        }

        return findings;
    }
}

/// <summary>The skip list of the cel-spec suite, read for the gates <c>ENG803</c> and <c>ENG906</c>.</summary>
internal static class CelSpecList
{
    /// <summary>The path of the list.</summary>
    internal const string ListPath = "tests/Great.Parts.Cel.Conformance/CelSpec/skips.md";

    /// <summary>Reads the records of the list.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="columns">The columns that the table has to have.</param>
    internal static IReadOnlyList<ListRecord> Read(string root, params string[] columns) => MarkdownList.Read(root, ListPath, columns);

    /// <summary>A finding at the line of a record, column 1.</summary>
    /// <param name="record">The record.</param>
    /// <param name="code">The code.</param>
    /// <param name="message">The message.</param>
    internal static Finding At(ListRecord record, string code, string message) =>
        new(ListPath, record.Line, 1, FindingSeverity.Error, code, message);
}
