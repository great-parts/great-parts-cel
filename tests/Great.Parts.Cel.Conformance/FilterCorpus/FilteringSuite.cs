using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;

namespace Great.Parts.Cel.Conformance.FilterCorpus;

/// <summary>
/// The corpus of the filtering package: the files <c>tests/filtering/testdata/*.textproto</c>, each a <see cref="FilterTestFile"/>. AIP-160
/// has no official test suite, so the corpus is the library's own: it has a number of cases, not a share of conformance.
/// </summary>
internal static class FilteringSuite
{
    private const string DataDirectory = "tests/filtering/testdata/";

    // No case of the corpus may fail: a failing case is a defect to fix, not an entry of a list.
    private const string EmptySkipList = "| Case | Scope | Reason | Reference | Issue |\n|---|---|---|---|---|\n";
    private const string SkipListPath = "tests/filtering/testdata/(no skip list)";

    private static readonly CaseEnvironment NoEnvironment = new(string.Empty, [], false);
    private static readonly Expectation NoExpectation = new(ExpectationKind.Value, "none", null, null);

    /// <summary>The source of the suite in its summary line.</summary>
    internal static string Source => "aip-160 corpus";

    /// <summary>Reads the cases of every file of the corpus, in the order of the paths and of the files.</summary>
    internal static IReadOnlyList<FilterCase> ReadCases() => [.. EmbeddedData.TextFiles(DataDirectory).SelectMany(ReadFile)];

    /// <summary>Reads the cases of one embedded file.</summary>
    /// <param name="path">The path of the text file relative to the repository root.</param>
    internal static IReadOnlyList<FilterCase> ReadFile(string path) =>
        Read(path, FilterTestFile.Parser.ParseFrom(EmbeddedData.ReadBinary(path)), EmbeddedData.ReadText(path));

    /// <summary>Reads the cases of a file.</summary>
    /// <param name="path">The path of the text file relative to the repository root.</param>
    /// <param name="file">The file, parsed from its binary form.</param>
    /// <param name="text">The text of the file, for the positions of the cases.</param>
    /// <exception cref="InvalidDataException">The scan of the text finds other sections or cases than the file has.</exception>
    internal static IReadOnlyList<FilterCase> Read(string path, FilterTestFile file, string text)
    {
        IReadOnlyList<IReadOnlyList<TextPosition>> positions = CasePositions.Read(text);
        if (!positions.Select(section => section.Count).SequenceEqual(file.Section.Select(section => section.Test.Count)))
        {
            throw new InvalidDataException(
                $"The scan of '{path}' finds sections with [{string.Join(", ", positions.Select(section => section.Count))}] test tokens, "
                + $"the file has sections with [{string.Join(", ", file.Section.Select(section => section.Test.Count))}] cases.");
        }

        List<FilterCase> cases = [];
        for (int section = 0; section < file.Section.Count; section++)
        {
            for (int test = 0; test < file.Section[section].Test.Count; test++)
            {
                FilterTest source = file.Section[section].Test[test];
                TextPosition position = positions[section][test];
                cases.Add(new FilterCase($"{file.Name}/{file.Section[section].Name}/{source.Name}", path, position.Line, position.Column, source));
            }
        }

        return cases;
    }

    /// <summary>Runs every case of the corpus.</summary>
    /// <param name="cell">The cell of the run.</param>
    internal static SuiteResult Run(RunCell cell) => Run(ReadCases(), cell);

    /// <summary>Runs cases.</summary>
    /// <param name="cases">The cases.</param>
    /// <param name="cell">The cell of the run.</param>
    /// <exception cref="InvalidDataException">Two cases have one name.</exception>
    internal static SuiteResult Run(IReadOnlyList<FilterCase> cases, RunCell cell)
    {
        Dictionary<string, FilterTest> byName = new(StringComparer.Ordinal);
        List<ConformanceCase> common = [];
        foreach (FilterCase testCase in cases)
        {
            if (!byName.TryAdd(testCase.Name, testCase.Test))
            {
                throw new InvalidDataException($"The case '{testCase.Name}' of '{testCase.Path}' is not unique.");
            }

            common.Add(new ConformanceCase(testCase.Name, testCase.Path, testCase.Line, testCase.Column, string.Empty, NoEnvironment, new Dictionary<string, ExprValue>(), CheckMode.Check, NoExpectation));
        }

        FilterJudge judge = new();
        return SuiteRun.Run(common, SkipList.ReadPolicy(SkipListPath, EmptySkipList), testCase => judge.Judge(byName[testCase.Name]), cell);
    }
}

/// <summary>A case of the corpus with its place in the files.</summary>
/// <param name="Name">The name, <c>&lt;file&gt;/&lt;section&gt;/&lt;test&gt;</c> from the names in the file.</param>
/// <param name="Path">The path of the text file relative to the repository root.</param>
/// <param name="Line">The line of the <c>test</c> token of the case, starting with 1.</param>
/// <param name="Column">The column of the <c>test</c> token of the case, starting with 1.</param>
/// <param name="Test">The case.</param>
internal sealed record FilterCase(string Name, string Path, int Line, int Column, FilterTest Test);
