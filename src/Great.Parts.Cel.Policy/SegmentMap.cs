using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Policy;

// The map of an expression to its document: from an offset in the text of an expression to the offset of the same character in the
// document, by the segments the reader of the format gave. The offset of the segment that starts at or before a position, plus the
// distance, as the core maps a position of an error; the end of the expression, one past its last character, maps with its last
// segment. An offset that would fall behind the document is cut to its end, so a copy of a tree never fails on a position.
internal sealed class SegmentMap
{
    private readonly int[] _expression;
    private readonly int[] _document;
    private readonly int _documentLength;

    internal SegmentMap(ImmutableArray<CelSourceSegment> segments, int documentLength)
    {
        _expression = new int[segments.Length];
        _document = new int[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            _expression[i] = segments[i].ExpressionOffset;
            _document[i] = segments[i].DocumentLocation.Offset;
        }

        _documentLength = documentLength;
    }

    // The offset in the document of an offset in the text of the expression.
    internal int ToDocument(int offset)
    {
        int index = _expression.AsSpan().BinarySearch(offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        long mapped = (long)_document[index] + (offset - _expression[index]);
        return (int)Math.Clamp(mapped, 0, _documentLength);
    }
}
