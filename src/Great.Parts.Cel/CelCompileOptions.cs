using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// Options of a compilation: the explicit expected CEL type of the result and the position of the
/// expression in its source document.
/// </summary>
/// <remarks>
/// The options are immutable once created and can be shared between threads and calls. A
/// <see cref="CelCompilationCache"/> compares options by the values of their properties.
/// </remarks>
public sealed class CelCompileOptions
{
    private readonly CelSourceLocation? _sourceLocation;

    /// <summary>
    /// Creates options with no explicit expected type and no source position.
    /// </summary>
    public CelCompileOptions()
    {
    }

    /// <summary>
    /// The expected CEL type of the result, or <see langword="null"/> to take it from the .NET type of the
    /// result alone.
    /// </summary>
    /// <remarks>
    /// Set it when the .NET type says too little: for example, <c>list(int)</c> with the result type
    /// <see cref="CelValue"/>, which alone expects a result of any type. The type must be convertible to the .NET type of the result by the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
    /// otherwise compilation throws <see cref="ArgumentException"/>. An expression whose type is not assignable to it
    /// is the compilation error <see cref="CelErrorCode.ExpectedTypeMismatch"/>.
    /// </remarks>
    public CelType? ExpectedType
    {
        get => null;

        // The explicit expected type is not implemented yet; it is not ignored.
        init => throw new NotImplementedException();
    }

    /// <summary>
    /// The name of the source document that contains the expression and the position at which the
    /// expression starts in it, or <see langword="null"/> when the expression is not part of a document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, every error of the expression — at compilation and at evaluation of its program — reports,
    /// besides its position in the expression, the position in the document in
    /// <see cref="CelError.SourceLocation"/>: for example, an expression stored in a JSON or YAML file
    /// reports the line and column in that file.
    /// </para>
    /// <para>
    /// The position in the document is the start plus the position in the expression: the offsets add up; the
    /// lines add up, less one; on the first line of the expression the columns add up, less one, and on the
    /// other lines the column is that in the expression. This assumes that the text of the expression stands in
    /// the document character for character from the start; escape sequences of the document, such as those of
    /// a JSON string, shift the positions after them. A sum that would pass <see cref="int.MaxValue"/> stays at <see cref="int.MaxValue"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The value has no <see cref="CelSourceLocation.SourceName"/>, such as <c>default(CelSourceLocation)</c>.</exception>
    public CelSourceLocation? SourceLocation
    {
        get => _sourceLocation;
        init
        {
            if (value is { SourceName: null })
            {
                throw new ArgumentException(
                    "The start of an expression in a document must name the document; a position without a source name is a position in an expression.",
                    nameof(value));
            }

            _sourceLocation = value;
        }
    }
}
