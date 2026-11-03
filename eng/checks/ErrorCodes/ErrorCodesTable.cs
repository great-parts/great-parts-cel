using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The table of error codes of a Markdown page: the first table outside fenced code whose columns are <c>Code</c>, <c>Name</c>,
/// <c>Stage</c> and <c>When it occurs</c> (<c>docs/design/eng-docs.md</c>, "error-codes"). It is read from the lines of the page, compared
/// with the members of <c>CelErrorCode</c> and written from them.
/// </summary>
internal sealed class ErrorCodesTable
{
    private const string Separator = "| :- | :- | :- | :- |";
    private static readonly string[] Columns = ["Code", "Name", "Stage", "When it occurs"];

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
                    rows.Add((row, SplitCells(lines[row])));
                }

                return new ErrorCodesTable(index, rows);
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the lines of the page with the table rewritten from the members, in the order of numbers; the other lines stay as they are.
    /// </summary>
    /// <param name="lines">The lines of the page.</param>
    /// <param name="members">The members of <c>CelErrorCode</c> sorted by number.</param>
    internal List<string> Rewrite(IReadOnlyList<string> lines, IReadOnlyList<ErrorCodeMember> members)
    {
        List<string> result = [.. lines.Take(Header)];
        result.Add("| " + string.Join(" | ", Columns) + " |");
        result.Add(Separator);
        result.AddRange(members.Select(member => string.Create(
            CultureInfo.InvariantCulture,
            $"| {member.Number} | `{member.Name}` | {member.Stage} | {member.Summary} |")));
        int end = Rows.Count == 0 ? Header + 2 : Rows[^1].Line + 1;
        result.AddRange(lines.Skip(end));
        return result;
    }

    /// <summary>
    /// Compares the rows with the members by name: a member without a row, and a row without a member, with another number, another stage
    /// or another summary, is <c>ENG303</c>.
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
        }

        foreach (ErrorCodeMember member in members.Where(member => !seen.Contains(member.Name)))
        {
            yield return Error(path, Header, $"member '{member.Name}' of CelErrorCode has no row in the table");
        }
    }

    private static Finding Error(string path, int index, string message) =>
        new(path, index + 1, 1, FindingSeverity.Error, EngCodes.Eng303, message);

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
        && SplitCells(lines[index]).SequenceEqual(Columns)
        && SplitCells(lines[index + 1]).All(IsSeparatorCell);

    private static bool IsSeparatorCell(string cell) =>
        cell.Trim(':').Length > 0 && cell.Trim(':').All(character => character == '-');

    // The cells of a row of a table; a pipe in a cell is written \|.
    private static List<string> SplitCells(string line)
    {
        List<string> cells = [];
        StringBuilder cell = new();
        string text = line.Trim();
        for (int index = 1; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '\\' && index + 1 < text.Length && text[index + 1] == '|')
            {
                cell.Append('|');
                index++;
            }
            else if (character == '|')
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(character);
            }
        }

        if (cell.ToString().Trim().Length > 0)
        {
            cells.Add(cell.ToString().Trim());
        }

        return cells;
    }
}
