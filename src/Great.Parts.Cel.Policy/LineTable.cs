using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Policy;

// The starts of the lines of a document. Lines are separated by "\n", "\r\n" and "\r", as the core separates the lines of an expression,
// so a position that is found here is the position the core reports for the same character. An offset and a column count UTF-16 code
// units; the line and the column start at 1.
internal sealed class LineTable
{
    private readonly int[] _starts;

    internal LineTable(string text)
    {
        var starts = new List<int>(16) { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n' || (c == '\r' && !(i + 1 < text.Length && text[i + 1] == '\n')))
            {
                starts.Add(i + 1);
            }
        }

        _starts = [.. starts];
    }

    // The line and the column of an offset.
    internal (int Line, int Column) Locate(int offset)
    {
        int index = _starts.AsSpan().BinarySearch(offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return (index + 1, offset - _starts[index] + 1);
    }
}
