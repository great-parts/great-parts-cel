using System;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// The starts of the lines of a text, which turn an offset in UTF-16 code units into a line and a column.
/// Line breaks are <c>"\n"</c>, <c>"\r\n"</c> and <c>"\r"</c>, as in the CEL language definition.
/// </summary>
internal sealed class LineTable
{
    // The offset right after each line break: the starts of lines 2, 3 and so on, in ascending order.
    private readonly ImmutableArray<int> _lineStarts;

    internal LineTable(ReadOnlySpan<char> text)
    {
        ImmutableArray<int>.Builder lineStarts = ImmutableArray.CreateBuilder<int>();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }

            if (c is '\r' or '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        _lineStarts = lineStarts.ToImmutable();
    }

    /// <summary>
    /// Returns the position of an offset: the line and the column start at 1. The offset equal to the length of
    /// the text, the end of the text, is a position too.
    /// </summary>
    /// <param name="offset">The offset from the start of the text in UTF-16 code units.</param>
    /// <returns>The position, without a source name.</returns>
    internal CelSourceLocation GetLocation(int offset)
    {
        int index = _lineStarts.AsSpan().BinarySearch(offset);
        int lineStartsBefore = index >= 0 ? index + 1 : ~index;
        int lineStart = lineStartsBefore == 0 ? 0 : _lineStarts[lineStartsBefore - 1];
        return new CelSourceLocation(offset, lineStartsBefore + 1, offset - lineStart + 1);
    }
}
