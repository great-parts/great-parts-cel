using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

// Positions in a document for an expression whose text the document format changed. A separate part of the class, so that the
// options of other features do not meet it in one file.
public sealed partial class CelCompileOptions
{
    private ImmutableArray<CelSourceSegment> _sourceSegments;

    /// <summary>
    /// The correspondence between the text of the expression and the document that holds it, or the default (empty) array
    /// when the expression is not part of a document or <see cref="SourceLocation"/> describes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, every error of the expression — at parsing, at compilation and at evaluation of its program — reports,
    /// besides its position in the expression, the position of the same character in the document in
    /// <see cref="CelError.SourceLocation"/>, by the rule of <see cref="CelSourceSegment"/>. Unlike
    /// <see cref="SourceLocation"/>, which assumes that the text stands in the document character for character, segments
    /// describe a text whose indentation, line breaks or escape sequences the document format changed: a YAML block, a
    /// folded YAML scalar, a JSON string.
    /// </para>
    /// <para>
    /// The first segment starts at offset 0, the offsets grow strictly, and all segments name the same document. A segment
    /// that starts beyond the end of the expression is never used. A <see cref="CelCompilationCache"/> compares the segments
    /// element by element.
    /// </para>
    /// <para>
    /// A line feed belongs to the line that it ends, so the segment of a new line starts at the first character after the line
    /// feed, not at the line feed itself.
    /// </para>
    /// <example>
    /// A YAML literal block, read by a YAML library that removed the indentation of 4 spaces: the block starts on line 12
    /// of <c>rules.yaml</c>, at offset 310 of the document, and the document separates lines with <c>"\n"</c>.
    /// <code>
    /// using System.Collections.Immutable;
    /// using Great.Parts.Cel;
    /// using Great.Parts.Cel.Diagnostics;
    ///
    /// string expression = "first +\nsecond";
    /// var segments = ImmutableArray.CreateBuilder&lt;CelSourceSegment&gt;();
    /// int offsetInDocument = 310, line = 12, start = 0;
    /// foreach (string text in expression.Split('\n'))
    /// {
    ///     segments.Add(new CelSourceSegment(start, new CelSourceLocation("rules.yaml", offsetInDocument + 4, line, 5)));
    ///     start += text.Length + 1;
    ///     offsetInDocument += 4 + text.Length + 1;
    ///     line++;
    /// }
    ///
    /// var options = new CelCompileOptions { SourceSegments = segments.ToImmutable() };
    /// </code>
    /// </example>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// The first segment does not start at offset 0, the offsets do not grow strictly, the segments name different
    /// documents, a segment names no document (<c>default(CelSourceSegment)</c> names none), or <see cref="SourceLocation"/> is
    /// also set.
    /// </exception>
    public ImmutableArray<CelSourceSegment> SourceSegments
    {
        get => _sourceSegments;
        init
        {
            CheckSegments(value);
            _sourceSegments = value;
        }
    }

    private void CheckSegments(ImmutableArray<CelSourceSegment> segments)
    {
        if (segments.IsDefaultOrEmpty)
        {
            return;
        }

        if (_sourceLocation is not null)
        {
            throw new ArgumentException("The start of an expression and its source segments are exclusive: set one of them.", nameof(segments));
        }

        if (segments[0].ExpressionOffset != 0)
        {
            throw new ArgumentException("The first segment must start at offset 0 of the expression.", nameof(segments));
        }

        string? name = segments[0].DocumentLocation.SourceName;
        if (name is null)
        {
            throw new ArgumentException("A segment must name its document; the default segment names none.", nameof(segments));
        }

        for (int i = 1; i < segments.Length; i++)
        {
            if (segments[i].ExpressionOffset <= segments[i - 1].ExpressionOffset)
            {
                throw new ArgumentException("The offsets of the segments must grow strictly.", nameof(segments));
            }

            if (!string.Equals(segments[i].DocumentLocation.SourceName, name, StringComparison.Ordinal))
            {
                throw new ArgumentException("All segments must name the same document.", nameof(segments));
            }
        }
    }
}
