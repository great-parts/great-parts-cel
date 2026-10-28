using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A place in a text: line and column, both from 1. The column counts UTF-16 code units of the line.</summary>
/// <param name="Line">The line, from 1.</param>
/// <param name="Column">The column, from 1.</param>
internal readonly record struct Position(int Line, int Column);

/// <summary>
/// The line starts of a text, for the line and column of an offset. A line ends with a line feed, a carriage return and
/// line feed, or a carriage return; the offset and the column count UTF-16 code units, so a character outside the basic
/// plane takes two columns.
/// </summary>
internal sealed class SourceLines
{
    private readonly int[] _starts;
    private readonly int _length;

    /// <summary>Indexes the lines of the text.</summary>
    /// <param name="text">The text, as read from a file.</param>
    internal SourceLines(string text)
    {
        _length = text.Length;
        List<int> starts = [0];
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            if (current is '\r' or '\n')
            {
                starts.Add(index + 1);
            }
        }

        _starts = [.. starts];
    }

    /// <summary>Returns the line and the column of an offset.</summary>
    /// <param name="offset">The offset in UTF-16 code units, from 0 to the length of the text.</param>
    internal Position Locate(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, _length);
        int found = Array.BinarySearch(_starts, offset);
        int line = found >= 0 ? found : ~found - 1;
        return new Position(line + 1, offset - _starts[line] + 1);
    }
}
