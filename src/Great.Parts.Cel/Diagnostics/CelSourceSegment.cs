using System;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// A segment of the correspondence between the text of an expression and the document that holds it: from
/// <see cref="ExpressionOffset"/> on, the characters of the expression stand in the document one after another, starting at
/// <see cref="DocumentLocation"/>, up to the next segment.
/// </summary>
/// <remarks>
/// <para>
/// A document format may change the text of an expression on its way into the document: YAML removes the indentation of a
/// block and folds the lines of a plain or quoted scalar, JSON and quoted YAML scalars write escape sequences. The
/// application that read the expression from the document knows where each part of the text stands; it passes that knowledge
/// as segments in <see cref="CelCompileOptions.SourceSegments"/>, and every error of the expression then reports, besides its
/// position in the expression, the position of the same character in the document in <see cref="CelError.SourceLocation"/>.
/// </para>
/// <para>
/// The position in the document of a position in the expression is taken from the last segment that starts at or before it:
/// the offset is the offset of the segment plus the distance; when no line break of the expression lies between the start
/// of the segment and the position, the line is that of the segment and the column is the column of the segment plus the
/// distance; otherwise the line is that of the segment plus the line breaks between them, and the column is the column in
/// the expression. A single segment at offset 0 gives the rule of <see cref="CelCompileOptions.SourceLocation"/>.
/// </para>
/// <para>
/// For a YAML literal block whose indentation the reader removed, a segment for the start of every line of the expression
/// gives exact positions: the offset in the expression where the line starts, and the position of the first character
/// after the indentation of that line in the document.
/// </para>
/// <para>The structure is immutable and can be shared between threads.</para>
/// </remarks>
public readonly struct CelSourceSegment : IEquatable<CelSourceSegment>
{
    /// <summary>
    /// Creates a segment.
    /// </summary>
    /// <param name="expressionOffset">The offset in the expression text, in UTF-16 code units, 0 or more.</param>
    /// <param name="documentLocation">
    /// The position in the document of the character at <paramref name="expressionOffset"/>; it names the document.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expressionOffset"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="documentLocation"/> has no <see cref="CelSourceLocation.SourceName"/>, such as
    /// <c>default(CelSourceLocation)</c>.
    /// </exception>
    public CelSourceSegment(int expressionOffset, CelSourceLocation documentLocation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expressionOffset);
        if (documentLocation.SourceName is null)
        {
            throw new ArgumentException(
                "The position of a segment is a position in a document and must name the document.",
                nameof(documentLocation));
        }

        ExpressionOffset = expressionOffset;
        DocumentLocation = documentLocation;
    }

    /// <summary>
    /// The offset in the expression text, in UTF-16 code units, at which the segment starts.
    /// </summary>
    public int ExpressionOffset { get; }

    /// <summary>
    /// The position in the document of the character at <see cref="ExpressionOffset"/>.
    /// </summary>
    public CelSourceLocation DocumentLocation { get; }

    /// <summary>
    /// Compares two segments: they are equal when their offsets are equal and their document positions are equal by
    /// <see cref="CelSourceLocation.Equals(CelSourceLocation)"/>.
    /// </summary>
    /// <param name="other">The segment to compare with.</param>
    /// <returns><see langword="true"/> when the segments are equal.</returns>
    public bool Equals(CelSourceSegment other) =>
        ExpressionOffset == other.ExpressionOffset && DocumentLocation.Equals(other.DocumentLocation);

    /// <summary>
    /// Compares this segment with an object, as <see cref="Equals(CelSourceSegment)"/> does when the object is a
    /// <see cref="CelSourceSegment"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal segment.</returns>
    public override bool Equals(object? obj) => obj is CelSourceSegment other && Equals(other);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelSourceSegment)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(ExpressionOffset, DocumentLocation);

    /// <summary>
    /// Tells whether two segments are equal, as <see cref="Equals(CelSourceSegment)"/> does.
    /// </summary>
    /// <param name="left">The first segment.</param>
    /// <param name="right">The second segment.</param>
    /// <returns><see langword="true"/> when the segments are equal.</returns>
    public static bool operator ==(CelSourceSegment left, CelSourceSegment right) => left.Equals(right);

    /// <summary>
    /// Tells whether two segments differ, as the negation of <see cref="Equals(CelSourceSegment)"/>.
    /// </summary>
    /// <param name="left">The first segment.</param>
    /// <param name="right">The second segment.</param>
    /// <returns><see langword="true"/> when the segments differ.</returns>
    public static bool operator !=(CelSourceSegment left, CelSourceSegment right) => !left.Equals(right);
}
