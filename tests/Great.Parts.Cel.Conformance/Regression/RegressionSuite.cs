using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;

namespace Great.Parts.Cel.Conformance.Regression;

/// <summary>
/// The regression corpus of the differential run: the cases of <c>Regression/*.textproto</c>, the skip list
/// <c>Regression/skips.md</c> and the registry of known differences <c>Regression/known-differences.md</c>.
/// </summary>
internal static class RegressionSuite
{
    /// <summary>The source of the suite in its summary line.</summary>
    internal const string Source = "regression corpus";

    private const string Directory = "tests/Great.Parts.Cel.Conformance/Regression/";
    private const string SkipListPath = Directory + "skips.md";
    private const string RegistryPath = Directory + "known-differences.md";

    /// <summary>Reads the cases of all corpus files, file by file in ordinal order of the paths.</summary>
    internal static IReadOnlyList<ConformanceCase> ReadCases() => [.. EmbeddedData.TextFiles(Directory).SelectMany(CaseReader.ReadEmbedded)];

    /// <summary>
    /// Runs every case of the corpus and matches the results with the skip list. The <c>ENG800</c> findings of the registry come
    /// first: an entry of the skip list relies on its signatures.
    /// </summary>
    /// <param name="evaluator">Runs a case.</param>
    /// <param name="cell">The cell of the run.</param>
    internal static SuiteResult Run(ICaseEvaluator evaluator, RunCell cell)
    {
        KnownDifferences registry = KnownDifferences.Read(RegistryPath, EmbeddedData.ReadText(RegistryPath));
        SkipList skipList = SkipList.ReadCorpus(SkipListPath, EmbeddedData.ReadText(SkipListPath), registry.Signatures);
        SuiteResult result = SuiteRun.Run(ReadCases(), skipList, evaluator, cell);
        return result with { Findings = [.. registry.Findings, .. result.Findings] };
    }
}
