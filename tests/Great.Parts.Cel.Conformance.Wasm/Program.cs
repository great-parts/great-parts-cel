using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Great.Parts.Cel.Conformance.CelPolicy;
using Great.Parts.Cel.Conformance.CelSpec;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Conformance;

/// <summary>
/// The cel-spec suite and the CEL Policy suite on browser-wasm. It runs every case of <c>tests/simple/testdata</c> of the pinned commit
/// of cel-spec and every test of the categories of the pinned commit of cel-policy on the library, as the suite project does, in the cell
/// of the run (<c>jit, browser-wasm</c> and the target of the build), and prints the summary line of each suite, the skipped cases of the
/// cell with their reasons and the findings. Then it checks the database of the system without zones (<see cref="SystemZones"/>) and runs
/// the control example of asynchronous evaluation (<see cref="AsyncControl"/>). The exit code is 0 when there is no finding with the
/// severity error and no problem of those checks, 1 when there is one, and 2 when the run is not a run on browser-wasm or a suite has
/// no cases.
/// </summary>
internal static class Program
{
    private const string SkipColumns = "Case | Scope | Reason | Reference | Issue";
    private const string CelSpecSkipList = "tests/Great.Parts.Cel.Conformance/CelSpec/skips.md";
    private const string PolicySkipList = "tests/Great.Parts.Cel.Conformance/CelPolicy/skips.md";

    private static async Task<int> Main()
    {
        RunCell cell = RunCell.Current;
        if (cell.OperatingSystem != "browser-wasm")
        {
            Console.WriteLine($"This is the runner of browser-wasm; the cell of the run is {cell}.");
            return 2;
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        SuiteResult celSpec = CelSpecSuite.Run(new LibraryEvaluator(), cell);
        Console.WriteLine(celSpec.Summary(CelSpecSuite.Source, today));
        Skipped(cell, CelSpecSkipList, SkipList.ReadCelSpec(CelSpecSkipList, EmbeddedData.ReadText(CelSpecSkipList), CelSpecSuite.Commit));

        SuiteResult policy = PolicySuite.Run(cell);
        Console.WriteLine(policy.Summary(PolicySuite.Source, today));
        Skipped(cell, PolicySkipList, SkipList.ReadPolicy(PolicySkipList, EmbeddedData.ReadText(PolicySkipList)));

        string[] errors = [.. celSpec.Findings.Concat(policy.Findings).Where(finding => finding.Severity == FindingSeverity.Error).Select(finding => finding.ToString())];
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"findings with the severity error: {errors.Length}"));
        foreach (string error in errors)
        {
            Console.WriteLine(error);
        }

        if (celSpec.Total == 0 || policy.Total == 0)
        {
            Console.WriteLine($"A suite has no cases: {celSpec.Total} cases of cel-spec, {policy.Total} of cel-policy.");
            return 2;
        }

        // The database of the system on a runtime without zones (the invariant time zone mode): its problems are errors of the run, like a finding.
        string[] zoneProblems = SystemZones.Problems();
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"problems of the database of the system without zones: {zoneProblems.Length}"));
        foreach (string problem in zoneProblems)
        {
            Console.WriteLine(problem);
        }

        // The control example of asynchronous evaluation: the driver on the one thread of the runtime.
        string[] asyncProblems = await AsyncControl.ProblemsAsync().ConfigureAwait(false);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"problems of the asynchronous control example: {asyncProblems.Length}"));
        foreach (string problem in asyncProblems)
        {
            Console.WriteLine(problem);
        }

        return errors.Length == 0 && zoneProblems.Length == 0 && asyncProblems.Length == 0 ? 0 : 1;
    }

    // Prints the entries of a skip list whose scope contains the cell: each is a case that fails in the cell (a case that passes is ENG802),
    // with its reason and, where there is one, its issue.
    private static void Skipped(RunCell cell, string path, SkipList list)
    {
        Dictionary<int, MarkdownRow> rows = MarkdownTable.Read(EmbeddedData.ReadText(path), SkipColumns.Split(" | ")).Rows.ToDictionary(row => row.Line);
        string[] skipped =
        [
            .. list.Entries
                .Where(entry => entry.Scope.Contains(cell))
                .Select(entry => rows[entry.Line])
                .Select(row => $"  {row.Cells[0]} (issue {row.Cells[4]}): {row.Cells[2]}"),
        ];
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"skipped in this cell of {path}: {skipped.Length}"));
        foreach (string line in skipped)
        {
            Console.WriteLine(line);
        }
    }
}
