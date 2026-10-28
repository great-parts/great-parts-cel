using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// The registry of known differences from cel-go, read only for its signatures: an entry of the corpus skip list with the
/// verdict <c>cel-go-bug</c> or <c>by-design</c> needs a row of the registry with the same signature.
/// </summary>
internal sealed class KnownDifferences
{
    private static readonly string[] Columns = ["Signature", "Class", "Reference"];

    private KnownDifferences(IReadOnlySet<string> signatures, IReadOnlyList<Finding> findings)
    {
        Signatures = signatures;
        Findings = findings;
    }

    /// <summary>The signatures of the rows of the registry.</summary>
    internal IReadOnlySet<string> Signatures { get; }

    /// <summary>The <c>ENG800</c> findings of the form of the registry table, by line.</summary>
    internal IReadOnlyList<Finding> Findings { get; }

    /// <summary>Reads the registry.</summary>
    /// <param name="path">The path of the registry relative to the repository root.</param>
    /// <param name="text">The text of the registry.</param>
    internal static KnownDifferences Read(string path, string text)
    {
        MarkdownTable table = MarkdownTable.Read(text, Columns);
        return new KnownDifferences(
            table.Rows.Select(row => row.Cells[0]).ToHashSet(StringComparer.Ordinal),
            [.. table.Problems.Select(problem => SkipList.Invalid(path, problem.Line, problem.Message))]);
    }
}
