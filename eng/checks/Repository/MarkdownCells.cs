using System.Collections.Generic;
using System.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The cells of a row of a Markdown table, read the same way by the checks of the error code page and of the lists.</summary>
internal static class MarkdownCells
{
    /// <summary>Splits a row into its cells: the leading and the trailing bar are not cells, and <c>\|</c> is a bar inside a cell.</summary>
    /// <param name="line">The row of the table.</param>
    /// <returns>The cells, trimmed; an empty last cell is dropped.</returns>
    internal static List<string> Split(string line)
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
