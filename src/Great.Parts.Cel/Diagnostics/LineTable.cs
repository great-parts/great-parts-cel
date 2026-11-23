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

    // The offsets in UTF-16 code units of the high surrogate of each surrogate pair of the text, in ascending order, and the code
    // point index of each such character (the offset minus the number of pairs before it): they convert between UTF-16 offsets and
    // code point offsets. Empty for a text without supplementary characters and for a table made from line offsets.
    private readonly ImmutableArray<int> _pairStarts;
    private readonly ImmutableArray<int> _pairCodePoints;

    // The length of the text in UTF-16 code units; 0 for a table that does not know the text.
    private readonly int _length;

    private LineTable(
        ImmutableArray<int> lineStarts,
        ImmutableArray<int> nodeOffsets,
        ImmutableArray<int> nodeEnds,
        ImmutableArray<int> pairStarts,
        ImmutableArray<int> pairCodePoints,
        int length)
    {
        _lineStarts = lineStarts;
        _nodeOffsets = nodeOffsets;
        _nodeEnds = nodeEnds;
        _pairStarts = pairStarts;
        _pairCodePoints = pairCodePoints;
        _length = length;
    }

    internal LineTable(ReadOnlySpan<char> text)
    {
        ImmutableArray<int>.Builder lineStarts = ImmutableArray.CreateBuilder<int>();
        ImmutableArray<int>.Builder pairStarts = ImmutableArray.CreateBuilder<int>();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }
            else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                pairStarts.Add(i);
                i++;
                continue;
            }

            if (c is '\r' or '\n')
            {
                lineStarts.Add(i + 1);
            }
        }

        _lineStarts = lineStarts.ToImmutable();
        _pairStarts = pairStarts.ToImmutable();
        ImmutableArray<int>.Builder pairCodePoints = ImmutableArray.CreateBuilder<int>(_pairStarts.Length);
        for (int k = 0; k < _pairStarts.Length; k++)
        {
            pairCodePoints.Add(_pairStarts[k] - k);
        }

        _pairCodePoints = pairCodePoints.MoveToImmutable();
        _length = text.Length;
    }

    /// <summary>
    /// Makes the table of a loaded message, which has the offsets of its lines and no text: every offset is taken as an offset in
    /// UTF-16 code units.
    /// </summary>
    /// <param name="lineStarts">The start of each line after the first.</param>
    /// <param name="length">The length of the text, or 0 when it is not known.</param>
    /// <returns>The table.</returns>
    internal static LineTable FromLineStarts(ImmutableArray<int> lineStarts, int length) =>
        new(lineStarts, default, default, [], [], length);

    /// <summary>
    /// The length of the text in UTF-16 code units; 0 when the table does not know the text.
    /// </summary>
    internal int Length => _length;

    /// <summary>
    /// The offset just after each line break after the first line, in UTF-16 code units: the starts of the lines after the first.
    /// </summary>
    internal ImmutableArray<int> LineStarts => _lineStarts;

    /// <summary>
    /// The length of the text in code points: a surrogate pair is one.
    /// </summary>
    internal int CodePointLength => _length - _pairStarts.Length;

    /// <summary>
    /// Converts an offset in UTF-16 code units to an offset in code points, which the protobuf form counts.
    /// </summary>
    /// <param name="offset">The offset in UTF-16 code units.</param>
    /// <returns>The offset in code points.</returns>
    internal int ToCodePoints(int offset)
    {
        if (_pairStarts.IsEmpty)
        {
            return offset;
        }

        int index = _pairStarts.AsSpan().BinarySearch(offset);
        return offset - (index >= 0 ? index : ~index);
    }

    /// <summary>
    /// Converts an offset in code points to an offset in UTF-16 code units of the text.
    /// </summary>
    /// <param name="codePoint">The offset in code points, from 0 to <see cref="CodePointLength"/>.</param>
    /// <returns>The offset in UTF-16 code units.</returns>
    internal int ToUtf16(int codePoint)
    {
        if (_pairCodePoints.IsEmpty)
        {
            return codePoint;
        }

        int index = _pairCodePoints.AsSpan().BinarySearch(codePoint);
        return codePoint + (index >= 0 ? index : ~index);
    }

    /// <summary>
    /// Gives a table of the same text that also knows where the nodes of its syntax tree end.
    /// </summary>
    /// <param name="offsets">The offset of the node with the id i + 1 at the index i.</param>
    /// <param name="ends">The offset just after the last token of that node, at the same index.</param>
    /// <returns>The table.</returns>
    internal LineTable WithNodes(ImmutableArray<int> offsets, ImmutableArray<int> ends) =>
        new(_lineStarts, offsets, ends, _pairStarts, _pairCodePoints, _length);

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
    /// <param name="offset">The offset from the start of the text in UTF-16 code units; a negative one is no position.</param>
    /// <returns>The position, without a source name; <c>default</c> for no position.</returns>
    internal CelSourceLocation GetLocation(int offset)
    {
        if (offset < 0)
        {
            return default;
        }

        int index = _lineStarts.AsSpan().BinarySearch(offset);
        int lineStartsBefore = index >= 0 ? index + 1 : ~index;
        int lineStart = lineStartsBefore == 0 ? 0 : _lineStarts[lineStartsBefore - 1];
        return new CelSourceLocation(offset, lineStartsBefore + 1, offset - lineStart + 1);
    }
}
