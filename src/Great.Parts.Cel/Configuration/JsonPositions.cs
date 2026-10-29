using System;
using System.Collections.Immutable;
using System.Text;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Configuration;

/// <summary>
/// Positions in the JSON text of an environment description, in the units of positions in an expression: the offset,
/// the line and the column count UTF-16 code units of the decoded text, and the line and the column start at 1.
/// </summary>
/// <remarks>
/// The text the methods take is the JSON text without its byte order mark, so the mark is not counted.
/// </remarks>
internal static class JsonPositions
{
    /// <summary>
    /// Returns the byte index of a place that <see cref="System.Text.Json.Utf8JsonReader"/> reports as a line counted
    /// by <c>"\n"</c> and a byte position in that line, both starting at 0.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="line">The line of the place.</param>
    /// <param name="bytePositionInLine">The byte position of the place in its line.</param>
    /// <returns>The byte index of the place in <paramref name="json"/>.</returns>
    internal static int ByteIndex(ReadOnlySpan<byte> json, long line, long bytePositionInLine)
    {
        int lineStart = 0;
        for (long i = 0; i < line; i++)
        {
            lineStart += json[lineStart..].IndexOf((byte)'\n') + 1;
        }

        return lineStart + (int)bytePositionInLine;
    }

    /// <summary>
    /// Returns the positions of byte indexes in the JSON text, each in a source named <paramref name="sourceName"/>.
    /// </summary>
    /// <remarks>
    /// The text is decoded piece by piece, from one index to the next, so an index that falls inside the encoding of a
    /// character still gets a position.
    /// </remarks>
    /// <param name="json">The JSON text.</param>
    /// <param name="byteIndexes">The byte indexes, in ascending order, each at most the length of the text.</param>
    /// <param name="sourceName">The source name of the positions.</param>
    /// <returns>The position of each index, in the order of <paramref name="byteIndexes"/>.</returns>
    internal static ImmutableArray<CelSourceLocation> Locate(
        ReadOnlySpan<byte> json,
        ReadOnlySpan<int> byteIndexes,
        string sourceName)
    {
        StringBuilder text = new();
        int[] offsets = new int[byteIndexes.Length];
        int decoded = 0;
        for (int i = 0; i < byteIndexes.Length; i++)
        {
            text.Append(Encoding.UTF8.GetString(json[decoded..byteIndexes[i]]));
            decoded = byteIndexes[i];
            offsets[i] = text.Length;
        }

        text.Append(Encoding.UTF8.GetString(json[decoded..]));
        LineTable lines = new(text.ToString());
        ImmutableArray<CelSourceLocation>.Builder locations = ImmutableArray.CreateBuilder<CelSourceLocation>(offsets.Length);
        foreach (int offset in offsets)
        {
            CelSourceLocation location = lines.GetLocation(offset);
            locations.Add(new CelSourceLocation(sourceName, location.Offset, location.Line, location.Column));
        }

        return locations.MoveToImmutable();
    }
}
