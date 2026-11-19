using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// The starts of the lines of a text, which turn an offset in UTF-16 code units into a line and a column.
/// Line breaks are <c>"\n"</c>, <c>"\r\n"</c> and <c>"\r"</c>, as in the CEL language definition.
/// </summary>
internal sealed class LineTable
{
    // The offset right after each line break: the starts of lines 2, 3 and so on, in ascending order.
    private readonly ImmutableArray<int> _lineStarts;

    // The offset and the end of each node of the syntax tree, by id − 1, which the parser records; empty for a table without
    // nodes. The end of the node that has an offset is found only when an error asks for its fragment.
    private readonly ImmutableArray<int> _nodeOffsets;
    private readonly ImmutableArray<int> _nodeEnds;
    private Dictionary<int, int>? _endsByOffset;

    private LineTable(ImmutableArray<int> lineStarts, ImmutableArray<int> nodeOffsets, ImmutableArray<int> nodeEnds)
    {
        _lineStarts = lineStarts;
        _nodeOffsets = nodeOffsets;
        _nodeEnds = nodeEnds;
    }

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
    /// Gives a table of the same text that also knows where the nodes of its syntax tree end.
    /// </summary>
    /// <param name="offsets">The offset of the node with the id i + 1 at the index i.</param>
    /// <param name="ends">The offset just after the last token of that node, at the same index.</param>
    /// <returns>The table.</returns>
    internal LineTable WithNodes(ImmutableArray<int> offsets, ImmutableArray<int> ends) => new(_lineStarts, offsets, ends);

    /// <summary>
    /// Returns the end of the node whose place is an offset: the offset just after its last token. Of several nodes with
    /// one place, such as the nodes of an expanded macro, the end of the longest is returned. An offset that is the place of
    /// no recorded node ends where it starts.
    /// </summary>
    /// <param name="offset">The offset of the place of a node in UTF-16 code units.</param>
    /// <returns>The end offset, not less than <paramref name="offset"/>.</returns>
    internal int GetEnd(int offset)
    {
        if (_nodeOffsets.IsDefaultOrEmpty)
        {
            return offset;
        }

        Dictionary<int, int>? ends = Volatile.Read(ref _endsByOffset);
        if (ends is null)
        {
            ends = new Dictionary<int, int>(_nodeOffsets.Length);
            for (int i = 0; i < _nodeOffsets.Length; i++)
            {
                ref int end = ref CollectionsMarshal.GetValueRefOrAddDefault(ends, _nodeOffsets[i], out _);
                end = Math.Max(end, _nodeEnds[i]);
            }

            ends = Interlocked.CompareExchange(ref _endsByOffset, ends, null) ?? ends;
        }

        return ends.TryGetValue(offset, out int found) ? Math.Max(found, offset) : offset;
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
