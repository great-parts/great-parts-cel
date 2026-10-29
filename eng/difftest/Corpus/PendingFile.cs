using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Difftest.Corpus;

/// <summary>The text of <c>pending.textproto</c> and the line of the <c>test</c> token of each case.</summary>
/// <param name="Text">The text of the file.</param>
/// <param name="TestLines">The line of the <c>test</c> token of each case, in the order of the cases; the column is <see cref="PendingFile.TestColumn"/>.</param>
internal sealed record PendingText(string Text, IReadOnlyList<int> TestLines);

/// <summary>
/// The file <c>pending.textproto</c> of the regression corpus (docs/design/eng-difftest.md, "Known signatures and the corpus"):
/// the cases of new signatures of the last run. Without cases it is the empty form, byte for byte as committed, with LF line
/// ends. The cases are in the section <c>new</c>; the text of each case is the answer of the oracle, copied unchanged between
/// the lines <c>test {</c> and <c>}</c>.
/// </summary>
internal static class PendingFile
{
    /// <summary>The path of the file relative to the repository root.</summary>
    internal const string Path = "tests/Great.Parts.Cel.Conformance/Regression/pending.textproto";

    /// <summary>The empty form of the file, without cases.</summary>
    internal const string EmptyForm =
        "# Written by eng/difftest: the cases of new divergence signatures of the last run. Committed without cases.\n"
        + "name: \"pending\"\n"
        + "description: \"New divergence signatures of the last run of eng/difftest\"\n";

    /// <summary>The column of the <c>test</c> token of a case.</summary>
    internal const int TestColumn = 3;

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Builds the file: the empty form, then the section with the cases.</summary>
    /// <param name="cases">The cases of new signatures, in the order the run found them.</param>
    internal static PendingText Build(IReadOnlyList<NewSignature> cases)
    {
        StringBuilder text = new(EmptyForm);
        List<int> testLines = [];
        if (cases.Count > 0)
        {
            int line = Lines(EmptyForm) + 1;
            text.Append("section {\n").Append("  name: \"").Append(DivergenceSearch.SectionName).Append("\"\n");
            line += 2;
            foreach (NewSignature found in cases)
            {
                testLines.Add(line);
                string body = found.CaseText.EndsWith('\n') ? found.CaseText : found.CaseText + "\n";
                text.Append("  test {\n").Append(body).Append("  }\n");
                line += 2 + Lines(body);
            }

            text.Append("}\n");
        }

        return new PendingText(text.ToString(), testLines);
    }

    /// <summary>Writes the file under a repository root: UTF-8 without a byte order mark, the text as it is.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="pending">The file.</param>
    internal static void Write(string root, PendingText pending)
    {
        File.WriteAllText(System.IO.Path.Combine(root, Path), pending.Text, Utf8WithoutBom);
    }

    /// <summary>
    /// The <c>ENG820</c> findings: one per new signature, at the <c>test</c> token of its case. The message names the signature,
    /// its severity, both outcomes, the seed and the number of the case.
    /// </summary>
    /// <param name="cases">The cases of new signatures.</param>
    /// <param name="pending">The file built from the cases.</param>
    internal static IReadOnlyList<Finding> Findings(IReadOnlyList<NewSignature> cases, PendingText pending)
    {
        List<Finding> findings = [];
        for (int index = 0; index < cases.Count; index++)
        {
            NewSignature found = cases[index];
            Divergence divergence = found.Divergence;
            string message = string.Create(
                CultureInfo.InvariantCulture,
                $"new divergence signature '{divergence.Signature}' ({divergence.Kind.Severity()}): cel-go returns {divergence.Oracle.Describe()}, we return {divergence.Library.Describe()}; seed {found.Seed}, case {found.Number}");
            findings.Add(new Finding(Path, pending.TestLines[index], TestColumn, FindingSeverity.Error, EngCodes.Eng820, message));
        }

        return findings;
    }

    private static int Lines(string text) => text.AsSpan().Count('\n');
}
