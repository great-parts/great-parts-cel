using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

// The map from a position in the text of an expression to the position of the same character in the document that holds it. It is made
// once per compilation from the compile options, either from the start of the expression (CelCompileOptions.SourceLocation) or from the
// segments (CelCompileOptions.SourceSegments): the start is the map of one segment. The segments stay in arrays, so that a position is
// found by a binary search; the line of each segment in the expression is read from the line table once, when the map is made. The
// default value is the map of an expression that is not part of a document: it changes nothing.
internal readonly struct DocumentMap
{
    // The offset in the expression where each segment starts, ascending; null for no map.
    private readonly int[]? _offsets;

    // The line in the expression of each segment start, and the place in the document of its first character.
    private readonly int[]? _lines;
    private readonly CelSourceLocation[]? _places;

    private DocumentMap(int[] offsets, int[] lines, CelSourceLocation[] places)
    {
        _offsets = offsets;
        _lines = lines;
        _places = places;
    }

    // Whether the expression is part of a document.
    internal bool IsNone => _offsets is null;

    // The name of the document; null for no map.
    internal string? SourceName => _places?[0].SourceName;

    // The map of the options: the segments when there are some, else the start of the expression, else no map. The line table is that of
    // the expression, the table the parser built; it is read only to find the line of each segment start.
    internal static DocumentMap Create(CelCompileOptions? options, LineTable lines) => Create(options, lines, null);

    // The same for an expression that did not parse, which has no line table: the table is made from the text, and only for segments.
    internal static DocumentMap ForText(CelCompileOptions? options, string text) => Create(options, null, text);

    // The name of the document of the options, or null for none.
    internal static string? SourceNameOf(CelCompileOptions? options) =>
        options is null ? null : options.SourceSegments.IsDefaultOrEmpty ? options.SourceLocation?.SourceName : options.SourceSegments[0].DocumentLocation.SourceName;

    private static DocumentMap Create(CelCompileOptions? options, LineTable? lines, string? text)
    {
        if (options is null)
        {
            return default;
        }

        ImmutableArray<CelSourceSegment> segments = options.SourceSegments;
        if (segments.IsDefaultOrEmpty)
        {
            return options.SourceLocation is { } start ? FromStart(start) : default;
        }

        lines ??= new LineTable(text!);
        int[] offsets = new int[segments.Length];
        int[] segmentLines = new int[segments.Length];
        var places = new CelSourceLocation[segments.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            CelSourceSegment segment = segments[i];
            offsets[i] = segment.ExpressionOffset;
            segmentLines[i] = lines.GetLocation(segment.ExpressionOffset).Line;
            places[i] = segment.DocumentLocation;
        }

        return new DocumentMap(offsets, segmentLines, places);
    }

    // The map of an expression that starts at a place of the document and stands there character for character.
    internal static DocumentMap FromStart(CelSourceLocation start) => new([0], [1], [start]);

    // The errors with their positions in the document, when there is a map.
    internal ImmutableArray<CelError> Map(ImmutableArray<CelError> errors)
    {
        if (IsNone)
        {
            return errors;
        }

        ImmutableArray<CelError>.Builder mapped = ImmutableArray.CreateBuilder<CelError>(errors.Length);
        foreach (CelError error in errors)
        {
            mapped.Add(Map(error));
        }

        return mapped.MoveToImmutable();
    }

    // The error with its position in the document: from the last segment that starts at or before the position, the offset is the
    // offset of the segment plus the distance; with no line break between the start of the segment and the position the line is the
    // line of the segment and the column the column of the segment plus the distance, otherwise the line is the line of the segment
    // plus the line breaks and the column is the column in the expression. An error without a position stays as it is.
    internal CelError Map(CelError error)
    {
        CelSourceLocation location = error.Location;
        if (_offsets is null || location.Line == 0)
        {
            return error;
        }

        int index = _offsets.AsSpan().BinarySearch(location.Offset);
        if (index < 0)
        {
            index = ~index - 1;
        }

        CelSourceLocation segment = _places![index];
        int distance = location.Offset - _offsets[index];
        int lineBreaks = location.Line - _lines![index];
        // CheckSegments of the options guarantees the name of every segment.
        var place = new CelSourceLocation(
            segment.SourceName!,
            Add(segment.Offset, distance),
            Add(segment.Line, lineBreaks),
            lineBreaks == 0 ? Add(segment.Column, distance) : location.Column);
        return error.AtDocument(place);
    }

    // The start is any position the application gives; a sum past int.MaxValue stays at int.MaxValue.
    private static int Add(int start, int value) => (int)Math.Min((long)start + value, int.MaxValue);
}
