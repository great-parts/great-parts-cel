using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;

namespace Great.Parts.Cel.Eng.Difftest.Corpus;

/// <summary>
/// The known signatures (docs/design/eng-difftest.md, "Known signatures and the corpus"): the column <c>Signature</c> of the
/// registry of known differences and of the corpus skip list. The form of the two tables is checked by the regression corpus
/// in eng/test; here a row that does not read is not a signature.
/// </summary>
internal sealed class KnownSignatures
{
    /// <summary>The registry of known differences, relative to the repository root.</summary>
    internal const string RegistryPath = "tests/Great.Parts.Cel.Conformance/Regression/known-differences.md";

    /// <summary>The corpus skip list, relative to the repository root.</summary>
    internal const string SkipListPath = "tests/Great.Parts.Cel.Conformance/Regression/skips.md";

    private static readonly string[] RegistryColumns = ["Signature", "Class", "Reference"];
    private static readonly string[] SkipListColumns = ["Case", "Scope", "Signature", "Verdict", "Severity", "Reason", "Issue"];

    private readonly HashSet<string> signatures;

    private KnownSignatures(IEnumerable<string> signatures) => this.signatures = signatures.ToHashSet(StringComparer.Ordinal);

    /// <summary>Known signatures given by a test.</summary>
    /// <param name="signatures">The signatures.</param>
    internal static KnownSignatures Of(params IEnumerable<string> signatures) => new(signatures);

    /// <summary>Reads the known signatures of a repository.</summary>
    /// <param name="root">The repository root.</param>
    internal static KnownSignatures Read(string root) => new(
        [
            .. Column(Path.Combine(root, RegistryPath), RegistryColumns, 0),
            .. Column(Path.Combine(root, SkipListPath), SkipListColumns, 2),
        ]);

    /// <summary>Whether a signature is known.</summary>
    /// <param name="signature">The signature, <c>&lt;node&gt;(&lt;types&gt;) &lt;kind&gt;</c>.</param>
    internal bool Contains(string signature) => signatures.Contains(signature);

    private static IEnumerable<string> Column(string file, string[] columns, int index) =>
        MarkdownTable.Read(File.ReadAllText(file), columns).Rows.Select(row => row.Cells[index]);
}
