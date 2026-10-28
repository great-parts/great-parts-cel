using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>A row of a Markdown table: its line in the file and its cells, trimmed and with <c>\|</c> turned into <c>|</c>.</summary>
/// <param name="Line">The line of the row in the file, starting with 1.</param>
/// <param name="Cells">The cells in the order of the columns.</param>
internal sealed record MarkdownRow(int Line, IReadOnlyList<string> Cells);

/// <summary>A problem of the form of a Markdown table at a line of the file.</summary>
/// <param name="Line">The line, starting with 1; a file without the table has the problem at line 1.</param>
/// <param name="Message">The problem: English, one line.</param>
internal sealed record MarkdownTableProblem(int Line, string Message);

/// <summary>
/// The one table of a Markdown file with fixed columns. The first line that starts with <c>|</c> is the header, the next line
/// is the separator, and every later line that starts with <c>|</c> is a row. Cells are separated by <c>|</c>, a <c>|</c> inside
/// a cell is written <c>\|</c>, and white space at the edges of a cell does not count. Other text of the file is for the reader.
/// </summary>
internal sealed class MarkdownTable
{
    private MarkdownTable(IReadOnlyList<MarkdownRow> rows, IReadOnlyList<MarkdownTableProblem> problems)
    {
        Rows = rows;
        Problems = problems;
    }

    /// <summary>The rows that have one cell for each column, in the order of the file.</summary>
    internal IReadOnlyList<MarkdownRow> Rows { get; }

    /// <summary>The problems of the form, in the order of the file. A row with another number of cells is a problem and not a row.</summary>
    internal IReadOnlyList<MarkdownTableProblem> Problems { get; }

    /// <summary>Reads the table of a Markdown file.</summary>
    /// <param name="text">The text of the file; a line ends with <c>\n</c> or <c>\r\n</c>.</param>
    /// <param name="columns">The names of the columns in their order.</param>
    internal static MarkdownTable Read(string text, IReadOnlyList<string> columns)
    {
        string[] lines = text.Split('\n');
        int header = Array.FindIndex(lines, IsTableLine);
        string expected = string.Join(" | ", columns);
        if (header < 0)
        {
            return Failed(1, $"the file has no table with the columns '{expected}'");
        }

        int headerLine = header + 1;
        string actual = string.Join(" | ", Cells(lines[header]));
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            return Failed(headerLine, $"the columns of the table are '{actual}', not '{expected}'");
        }

        if (header + 1 == lines.Length)
        {
            return Failed(headerLine, "the header is the last line: the separator row is missing");
        }

        if (!IsSeparator(lines[header + 1], columns.Count))
        {
            return Failed(headerLine + 1, $"the line after the header is not the separator row of {Count(columns.Count)}");
        }

        List<MarkdownRow> rows = [];
        List<MarkdownTableProblem> problems = [];
        for (int index = header + 2; index < lines.Length; index++)
        {
            if (!IsTableLine(lines[index]))
            {
                continue;
            }

            List<string> cells = Cells(lines[index]);
            if (cells.Count == columns.Count)
            {
                rows.Add(new MarkdownRow(index + 1, cells));
            }
            else
            {
                problems.Add(new MarkdownTableProblem(index + 1, $"the row has {Count(cells.Count)}, not {Count(columns.Count)}"));
            }
        }

        return new MarkdownTable(rows, problems);
    }

    private static bool IsTableLine(string line) => line.Length > 0 && line[0] == '|';

    private static MarkdownTable Failed(int line, string message) => new([], [new MarkdownTableProblem(line, message)]);

    private static string Count(int cells) => string.Create(CultureInfo.InvariantCulture, $"{cells} {(cells == 1 ? "cell" : "cells")}");

    // A separator cell is one or more '-', with an optional ':' at either edge for the alignment.
    private static bool IsSeparator(string line, int columns)
    {
        List<string> cells = Cells(line);
        if (!IsTableLine(line) || cells.Count != columns)
        {
            return false;
        }

        foreach (string cell in cells)
        {
            ReadOnlySpan<char> dashes = cell.AsSpan();
            dashes = dashes.StartsWith(':') ? dashes[1..] : dashes;
            dashes = dashes.EndsWith(':') ? dashes[..^1] : dashes;
            if (dashes.IsEmpty || dashes.ContainsAnyExcept('-'))
            {
                return false;
            }
        }

        return true;
    }

    // The leading '|' starts the row and a final unescaped '|' ends it; the text between them splits at every unescaped '|'.
    private static List<string> Cells(string line)
    {
        string row = line.TrimEnd();
        List<string> cells = [];
        StringBuilder cell = new();
        bool closed = false;
        for (int index = 1; index < row.Length; index++)
        {
            closed = row[index] == '|';
            if (closed)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else if (row[index] == '\\' && index + 1 < row.Length && row[index + 1] == '|')
            {
                cell.Append('|');
                index++;
            }
            else
            {
                cell.Append(row[index]);
            }
        }

        if (!closed)
        {
            cells.Add(cell.ToString().Trim());
        }

        return cells;
    }
}
