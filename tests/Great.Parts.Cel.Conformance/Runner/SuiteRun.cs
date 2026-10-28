using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>The result of a run of a suite in one cell.</summary>
/// <param name="Findings">
/// The findings: <c>ENG800</c> of the skip list, then <c>ENG801</c> and <c>ENG802</c> in the order of the cases, then <c>ENG804</c>.
/// The regression corpus puts the <c>ENG800</c> findings of its registry of known differences before them.
/// </param>
/// <param name="Passed">The number of the cases that passed.</param>
/// <param name="Total">The number of all cases of the suite.</param>
/// <param name="Cell">The cell of the run.</param>
internal sealed record SuiteResult(IReadOnlyList<Finding> Findings, int Passed, int Total, RunCell Cell)
{
    /// <summary>
    /// The summary line: the passed cases out of all cases, the source, the date of the run in UTC and the cell, such as
    /// <c>cel-spec 8bef98f: 47/2592 passed, 2026-11-20 (jit, linux-x64, net11.0)</c>. A failed case on the skip list counts as
    /// not passed.
    /// </summary>
    /// <param name="source">The source of the suite, such as <c>cel-spec 8bef98f</c> or <c>regression corpus</c>.</param>
    /// <param name="date">The date of the run in UTC.</param>
    internal string Summary(string source, DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"{source}: {Passed}/{Total} passed, {date:yyyy-MM-dd} ({Cell})");
}

/// <summary>
/// Runs every case of a suite, the cases of the skip list too, judges each by the <see cref="Rule"/> and matches the result with
/// the entries of the skip list whose scope contains the cell of the run.
/// </summary>
internal static class SuiteRun
{
    /// <summary>Runs a suite.</summary>
    /// <param name="cases">The cases of the suite.</param>
    /// <param name="skipList">The skip list of the suite.</param>
    /// <param name="evaluator">Runs a case.</param>
    /// <param name="cell">The cell of the run.</param>
    internal static SuiteResult Run(IReadOnlyList<ConformanceCase> cases, SkipList skipList, ICaseEvaluator evaluator, RunCell cell)
    {
        ILookup<string, SkipEntry> entries = skipList.Entries.ToLookup(entry => entry.Case, StringComparer.Ordinal);
        List<Finding> findings = [.. skipList.Findings];
        int passed = 0;
        foreach (ConformanceCase testCase in cases)
        {
            Judgement judgement = Rule.Judge(testCase, evaluator.Evaluate(testCase));
            SkipEntry? entry = entries[testCase.Name].FirstOrDefault(candidate => candidate.Scope.Contains(cell));
            if (judgement.Passed)
            {
                passed++;
            }

            if (judgement.Passed && entry is not null)
            {
                findings.Add(Error(skipList.Path, entry.Line, 1, EngCodes.Eng802, $"case '{testCase.Name}' now passes; remove it from the skip list"));
            }
            else if (!judgement.Passed && entry is null)
            {
                findings.Add(Error(testCase.Path, testCase.Line, testCase.Column, EngCodes.Eng801, $"case '{testCase.Name}' failed: {judgement.Reason} ({cell})"));
            }
        }

        HashSet<string> names = cases.Select(testCase => testCase.Name).ToHashSet(StringComparer.Ordinal);
        findings.AddRange(skipList.Entries
            .Where(entry => !names.Contains(entry.Case))
            .Select(entry => Error(skipList.Path, entry.Line, 1, EngCodes.Eng804, $"case '{entry.Case}' is not in the suite; remove it from the skip list")));
        return new SuiteResult(findings, passed, cases.Count, cell);
    }

    private static Finding Error(string path, int line, int column, string code, string message) =>
        new(path, line, column, FindingSeverity.Error, code, message);
}
