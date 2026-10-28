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
    /// <summary>
    /// Creates options with no explicit expected type and no source position.
    /// </summary>
    public CelCompileOptions()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The expected CEL type of the result, or <see langword="null"/> to take it from the .NET type of the
    /// result alone.
    /// </summary>
    /// <remarks>
    /// Set it when the .NET type says too little: for example, <c>list(int)</c> with the result type
    /// <see cref="CelValue"/>, which alone expects a result of any type. The type must be convertible to the
    /// .NET type of the result by the type mapping table of the documentation; otherwise compilation throws
    /// <see cref="ArgumentException"/>. An expression whose type is not assignable to it is the compilation
    /// error <see cref="CelErrorCode.ExpectedTypeMismatch"/>.
    /// </remarks>
    public CelType? ExpectedType
    {
        get => throw new NotImplementedException();
        init => throw new NotImplementedException();
    }

    /// <summary>
    /// The name of the source document that contains the expression and the position at which the
    /// expression starts in it, or <see langword="null"/> when the expression is not part of a document.
    /// </summary>
    /// <remarks>
    /// When set, every error of the expression — at compilation and at evaluation of its program — reports,
    /// besides its position in the expression, the position in the document in
    /// <see cref="CelError.SourceLocation"/>: for example, an expression stored in a JSON or YAML file
    /// reports the line and column in that file.
    /// </remarks>
    /// <exception cref="ArgumentException">The value has no <see cref="CelSourceLocation.SourceName"/>, such as <c>default(CelSourceLocation)</c>.</exception>
    public CelSourceLocation? SourceLocation
    {
        get => throw new NotImplementedException();
        init => throw new NotImplementedException();
    }
}
