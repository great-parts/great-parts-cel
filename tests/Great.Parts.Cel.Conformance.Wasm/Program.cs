using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Conformance.CelSpec;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Conformance;

/// <summary>
/// The cel-spec suite on browser-wasm. It runs every case of <c>tests/simple/testdata</c> of the pinned commit on the library,
/// as the suite project does, in the cell of the run (<c>jit, browser-wasm</c> and the target of the build), and prints the summary line, the
/// skipped cases of the cell with their reasons and the findings. Then it checks the database of the system without zones
/// (<see cref="SystemZones"/>). The exit code is 0 when there is no finding with the severity error and no problem of that check,
/// 1 when there is one, and 2 when the run is not a run on browser-wasm or the suite has no cases.
/// </summary>
internal static class Program
{
    private const string CelSpecColumns = "Case | Scope | Reason | Reference | Issue";

    private static int Main()
    {
        RunCell cell = RunCell.Current;
        if (cell.OperatingSystem != "browser-wasm")
        {
            Console.WriteLine($"This is the runner of browser-wasm; the cell of the run is {cell}.");
            return 2;
        }

        SuiteResult result = CelSpecSuite.Run(new LibraryEvaluator(), cell);
        Console.WriteLine(result.Summary(CelSpecSuite.Source, DateOnly.FromDateTime(DateTime.UtcNow)));

        string[] skipped = SkippedCases(cell);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"skipped in this cell: {skipped.Length}"));
        foreach (string line in skipped)
        {
            Console.WriteLine(line);
        }

        string[] errors = [.. result.Findings.Where(finding => finding.Severity == FindingSeverity.Error).Select(finding => finding.ToString())];
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"findings with the severity error: {errors.Length}"));
        foreach (string error in errors)
        {
            Console.WriteLine(error);
        }

        if (result.Total == 0)
        {
            Console.WriteLine($"The suite has {result.Total} cases.");
            return 2;
        }

        // The database of the system on a runtime without zones (the invariant time zone mode): its problems are errors of the run, like a finding.
        string[] zoneProblems = SystemZones.Problems();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"problems of the database of the system without zones: {zoneProblems.Length}"));
        foreach (string problem in zoneProblems)
        {
            Console.WriteLine(problem);
        }

        return errors.Length == 0 && zoneProblems.Length == 0 ? 0 : 1;
    }

    // The entries of the skip list whose scope contains the cell: each is a case that fails in the cell (a case that passes is ENG802), with
    // its reason and, where there is one, its issue.
    private static string[] SkippedCases(RunCell cell)
    {
        string path = "tests/Great.Parts.Cel.Conformance/CelSpec/skips.md";
        string text = EmbeddedData.ReadText(path);
        SkipList list = SkipList.ReadCelSpec(path, text, CelSpecSuite.Commit);
        Dictionary<int, MarkdownRow> rows = MarkdownTable.Read(text, CelSpecColumns.Split(" | ")).Rows.ToDictionary(row => row.Line);
        return
        [
            .. list.Entries
                .Where(entry => entry.Scope.Contains(cell))
                .Select(entry => rows[entry.Line])
                .Select(row => $"  {row.Cells[0]} (issue {row.Cells[4]}): {row.Cells[2]}"),
        ];
    }
}
