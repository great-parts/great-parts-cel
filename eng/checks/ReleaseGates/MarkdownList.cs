using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One row of a list of the repository: its line in the file and its cells by the name of the column.</summary>
/// <param name="Line">The line of the row, starting with 1.</param>
/// <param name="Cells">The text of each cell by the name of its column, trimmed.</param>
internal sealed record ListRecord(int Line, IReadOnlyDictionary<string, string> Cells)
{
    /// <summary>The text of the cell of the column.</summary>
    /// <param name="column">The name of the column.</param>
    internal string this[string column] => Cells[column];
}

/// <summary>
/// Reads the table of a skip list (<c>docs/design/conformance.md</c>): the first table of a Markdown file, with its columns found by the
/// names of the header row. It does not share code with the runner of the suites, which is another assembly.
/// </summary>
internal static class MarkdownList
{
    /// <summary>Reads the rows of the first table of the file.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="relativePath">The path of the file relative to the root, with <c>/</c>.</param>
    /// <param name="columns">The columns that the table has to have.</param>
    /// <exception cref="CheckInputException">The file does not exist, has no table or lacks a column.</exception>
    internal static IReadOnlyList<ListRecord> Read(string root, string relativePath, params string[] columns)
    {
        string path = Path.Combine(root, relativePath);
        if (!File.Exists(path))
        {
            throw new CheckInputException($"the list {relativePath} does not exist");
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int header = Array.FindIndex(lines, line => line.StartsWith('|'));
        if (header < 0 || header + 1 >= lines.Length || !IsSeparator(lines[header + 1]))
        {
            throw new CheckInputException($"the list {relativePath} has no table");
        }

        List<string> names = MarkdownCells.Split(lines[header]);
        foreach (string column in columns)
        {
            if (!names.Contains(column))
            {
                throw new CheckInputException($"the table of the list {relativePath} has no column {column}");
            }
        }

        List<ListRecord> records = [];
        for (int index = header + 2; index < lines.Length && lines[index].StartsWith('|'); index++)
        {
            List<string> cells = MarkdownCells.Split(lines[index]);
            Dictionary<string, string> byName = new(StringComparer.Ordinal);
            for (int column = 0; column < names.Count; column++)
            {
                byName[names[column]] = column < cells.Count ? cells[column] : string.Empty;
            }

            records.Add(new ListRecord(index + 1, byName));
        }

        return records;
    }

    private static bool IsSeparator(string line)
    {
        foreach (string cell in MarkdownCells.Split(line))
        {
            if (cell.Length == 0 || cell.Trim(':', '-').Length > 0)
            {
                return false;
            }
        }

        return true;
    }
}
