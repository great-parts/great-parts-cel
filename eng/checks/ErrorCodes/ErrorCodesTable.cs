using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The table of error codes of a Markdown page: the first table outside fenced code whose columns are <c>Code</c>, <c>Name</c>,
/// <c>Stage</c>, <c>When it occurs</c>, <c>Common cause</c> and <c>Fix</c> (<c>docs/design/eng-docs.md</c>, "error-codes"). It is read
/// from the lines of the page, compared with the members of <c>CelErrorCode</c> and written from them. The first four columns come from
/// the members; the last two are written by hand and kept when the table is written again.
/// </summary>
internal sealed class ErrorCodesTable
{
    private const string Separator = "| :- | :- | :- | :- | :- | :- |";
    private const int CauseColumn = 4;
    private const int FixColumn = 5;
    private static readonly string[] Columns = ["Code", "Name", "Stage", "When it occurs", "Common cause", "Fix"];

    private ErrorCodesTable(int header, List<(int Line, List<string> Cells)> rows)
    {
        Header = header;
        Rows = rows;
    }

    /// <summary>The index of the header line of the table in the lines of the page, starting with 0.</summary>
    internal int Header { get; }

    /// <summary>The rows of the table with the index of their line and their cells.</summary>
    internal IReadOnlyList<(int Line, List<string> Cells)> Rows { get; }

    /// <summary>Finds the table in the lines of the page, or returns <see langword="null"/> when the page has none.</summary>
    /// <param name="lines">The lines of the page.</param>
    internal static ErrorCodesTable? Find(IReadOnlyList<string> lines)
    {
        bool fenced = false;
        for (int index = 0; index < lines.Count; index++)
        {
            string line = lines[index].TrimStart();
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;
            }
            else if (!fenced && IsTable(lines, index))
            {
                List<(int Line, List<string> Cells)> rows = [];
                for (int row = index + 2; row < lines.Count && lines[row].TrimStart().StartsWith('|'); row++)
                {
                    rows.Add((row, MarkdownCells.Split(lines[row])));
                }

                return new ErrorCodesTable(index, rows);
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the lines of the page with the table rewritten from the members, in the order of numbers; the other lines stay as they are.
    /// The cells <c>Common cause</c> and <c>Fix</c> of a member are taken from its row of the page, and are empty for a member that has none.
    /// </summary>
    /// <param name="lines">The lines of the page.</param>
    /// <param name="members">The members of <c>CelErrorCode</c> sorted by number.</param>
    internal List<string> Rewrite(IReadOnlyList<string> lines, IReadOnlyList<ErrorCodeMember> members)
    {
        Dictionary<string, List<string>> written = new(StringComparer.Ordinal);
        foreach (List<string> row in Rows.Select(candidate => candidate.Cells).Where(candidate => candidate.Count > 1))
        {
            written.TryAdd(row[1].Trim('`'), row);
        }

        List<string> result = [.. lines.Take(Header)];
        result.Add("| " + string.Join(" | ", Columns) + " |");
        result.Add(Separator);
        result.AddRange(members.Select(member =>
        {
            written.TryGetValue(member.Name, out List<string>? existing);
            return string.Create(
                CultureInfo.InvariantCulture,
                $"| {member.Number} | `{member.Name}` | {member.Stage} | {member.Summary} | {Kept(existing, CauseColumn)} | {Kept(existing, FixColumn)} |");
        }));
        int end = Rows.Count == 0 ? Header + 2 : Rows[^1].Line + 1;
        result.AddRange(lines.Skip(end));
        return result;
    }

    /// <summary>
    /// Compares the rows with the members by name: a member without a row, and a row without a member, with another number, another stage
    /// or another summary, is <c>ENG303</c>; a row of a member with an empty <c>Common cause</c> or an empty <c>Fix</c> is <c>ENG306</c>,
    /// one finding for each empty cell.
    /// </summary>
    /// <param name="path">The path of the page relative to the repository root.</param>
    /// <param name="members">The members of <c>CelErrorCode</c>.</param>
    internal IEnumerable<Finding> Compare(string path, IReadOnlyList<ErrorCodeMember> members)
    {
        Dictionary<string, ErrorCodeMember> byName = members.ToDictionary(member => member.Name, StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach ((int line, List<string> cells) in Rows)
        {
            string? problem = Problem(cells, byName, seen);
            if (problem is not null)
            {
                yield return Error(path, line, problem);
            }

            foreach (string column in EmptyColumns(cells, byName))
            {
                yield return Gap(path, line, $"row '{cells[1].Trim('`')}' has an empty column '{column}'");
            }
        }

        foreach (ErrorCodeMember member in members.Where(member => !seen.Contains(member.Name)))
        {
            yield return Error(path, Header, $"member '{member.Name}' of CelErrorCode has no row in the table");
        }
    }

    private static Finding Error(string path, int index, string message) =>
        new(path, index + 1, 1, FindingSeverity.Error, EngCodes.Eng303, message);

    private static Finding Gap(string path, int index, string message) =>
        new(path, index + 1, 1, FindingSeverity.Error, EngCodes.Eng306, message);

    // The cell of a written column as it is written back: the cell is read with \| as |, so the bar is escaped again.
    private static string Kept(List<string>? cells, int column) =>
        cells is not null && cells.Count > column ? cells[column].Replace("|", "\\|", StringComparison.Ordinal) : string.Empty;

    // The written columns that are empty in the row of a member. A row of six cells whose name is not a member, and a row of another
    // number of cells, have only ENG303.
    private static IEnumerable<string> EmptyColumns(List<string> cells, Dictionary<string, ErrorCodeMember> byName)
    {
        if (cells.Count != Columns.Length || !byName.ContainsKey(cells[1].Trim('`')))
        {
            return [];
        }

        return Columns.Skip(CauseColumn).Where((_, offset) => cells[CauseColumn + offset].Length == 0);
    }

    // What is wrong with the row, or null when it matches its member. A matching row is recorded in seen.
    private static string? Problem(List<string> cells, Dictionary<string, ErrorCodeMember> byName, HashSet<string> seen)
    {
        if (cells.Count != Columns.Length)
        {
            return $"row has {cells.Count} cells, expected {Columns.Length}";
        }

        string name = cells[1].Trim('`');
        if (!byName.TryGetValue(name, out ErrorCodeMember? member))
        {
            return $"row '{name}' has no member in CelErrorCode";
        }

        if (!seen.Add(name))
        {
            return $"row '{name}' repeats an earlier row";
        }

        List<string> differences = [];
        string number = member.Number.ToString(CultureInfo.InvariantCulture);
        if (cells[0] != number)
        {
            differences.Add($"number {cells[0]}, expected {number}");
        }

        if (cells[2] != member.Stage)
        {
            differences.Add($"stage {cells[2]}, expected {member.Stage}");
        }

        // The cell is read with \| as |, the summary keeps the escape of --write: both are compared without it.
        if (cells[3] != member.Summary.Replace("\\|", "|", StringComparison.Ordinal))
        {
            differences.Add("summary differs from the XML documentation, rebuild the page with --write");
        }

        return differences.Count == 0 ? null : $"row '{name}' differs from CelErrorCode: {string.Join("; ", differences)}";
    }

    // The header with the four columns and, right after it, the separator row.
    private static bool IsTable(IReadOnlyList<string> lines, int index) =>
        index + 1 < lines.Count
        && lines[index].TrimStart().StartsWith('|')
        && MarkdownCells.Split(lines[index]).SequenceEqual(Columns)
        && MarkdownCells.Split(lines[index + 1]).All(IsSeparatorCell);

    private static bool IsSeparatorCell(string cell) =>
        cell.Trim(':').Length > 0 && cell.Trim(':').All(character => character == '-');
}
