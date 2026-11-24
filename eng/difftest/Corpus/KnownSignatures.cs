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

    /// <summary>
    /// The tail of the one signature that the registry may write with <c>*</c> for the node: a call whose first argument is unknown
    /// and whose second is an error, where cel-go gives the unknown and the library the error. It is one cause, whatever the function.
    /// </summary>
    internal const string UnknownBeforeError = "(unknown, error) we-known";

    private const string AnyNode = "*";

    // The nodes of that signature that are no strict call: cel-go gives the unknown in the same way as the library for the logical
    // operators, and the elements of a list or the entries of a map are not the arguments of a call. A divergence there is not the cause.
    private static readonly HashSet<string> NodesWithoutTheCause = ["_&&_", "_||_", "list", "map", "select", "has", "ident", "literal", "comprehension", "parse"];

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

    /// <summary>
    /// Whether a signature is known: a row has it, or it is <c>&lt;call&gt;(unknown, error) we-known</c> and a row has
    /// <c>*(unknown, error) we-known</c>.
    /// </summary>
    /// <param name="signature">The signature, <c>&lt;node&gt;(&lt;types&gt;) &lt;kind&gt;</c>.</param>
    internal bool Contains(string signature) => signatures.Contains(signature) || IsCoveredByTheRowOfAnyCall(signature);

    private bool IsCoveredByTheRowOfAnyCall(string signature)
    {
        if (!signature.EndsWith(UnknownBeforeError, StringComparison.Ordinal) || !signatures.Contains(AnyNode + UnknownBeforeError))
        {
            return false;
        }

        string node = signature[..^UnknownBeforeError.Length];
        return node.Length > 0 && !NodesWithoutTheCause.Contains(node);
    }

    private static IEnumerable<string> Column(string file, string[] columns, int index) =>
        MarkdownTable.Read(File.ReadAllText(file), columns).Rows.Select(row => row.Cells[index]);
}
