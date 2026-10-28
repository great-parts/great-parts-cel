using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;

namespace Great.Parts.Cel.Conformance.CelSpec;

/// <summary>
/// The cel-spec suite: the cases of <c>tests/simple/testdata</c> of the pinned cel-spec commit and the skip list
/// <c>CelSpec/skips.md</c>.
/// </summary>
internal static class CelSpecSuite
{
    private const string CommitPath = "tests/cel-spec/COMMIT";
    private const string SkipListPath = "tests/Great.Parts.Cel.Conformance/CelSpec/skips.md";

    /// <summary>The full SHA of the pinned cel-spec commit, without the line end, which a checkout on Windows may turn into CRLF.</summary>
    internal static string Commit { get; } = EmbeddedData.ReadText(CommitPath).Trim();

    /// <summary>The source of the suite in its summary line: <c>cel-spec</c> and the short SHA of the commit.</summary>
    internal static string Source { get; } = "cel-spec " + Commit[..7];

    /// <summary>Reads the cases of all test files of the suite, file by file in ordinal order of the paths.</summary>
    internal static IReadOnlyList<ConformanceCase> ReadCases() =>
        [.. EmbeddedData.TextFiles(EmbeddedData.CelSpecTestData).SelectMany(CaseReader.ReadEmbedded)];

    /// <summary>Runs every case of the suite and matches the results with the skip list.</summary>
    /// <param name="evaluator">Runs a case.</param>
    /// <param name="cell">The cell of the run.</param>
    internal static SuiteResult Run(ICaseEvaluator evaluator, RunCell cell) =>
        SuiteRun.Run(ReadCases(), SkipList.ReadCelSpec(SkipListPath, EmbeddedData.ReadText(SkipListPath), Commit), evaluator, cell);
}
