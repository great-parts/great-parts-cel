using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The result of parsing an expression or of loading a parsed expression from protobuf: the parsed expression, or all
/// errors found.
/// </summary>
/// <remarks>
/// <para>
/// Errors are returned, not thrown. Each has the stage <see cref="CelErrorStage.Parse"/>: the parse errors of a text with
/// their places, or, for a loaded message, <see cref="CelErrorCode.InvalidProtobuf"/>, <see cref="CelErrorCode.InvalidSyntaxTree"/>
/// or <see cref="CelErrorCode.NestingTooDeep"/>.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelParseResult
{
    private readonly CelParsedExpr? _parsed;

    internal CelParseResult(CelParsedExpr parsed)
    {
        _parsed = parsed;
        Errors = [];
    }

    internal CelParseResult(ImmutableArray<CelError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// <see langword="true"/> when the text parsed or the message loaded without errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ParsedExpr))]
    public bool IsSuccess => _parsed is not null;

    /// <summary>
    /// The parsed expression.
    /// </summary>
    /// <exception cref="CelException">The result is not successful; the exception carries all errors.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "Reading the parsed expression of an unsuccessful result throws CelException with all its errors.")]
    public CelParsedExpr? ParsedExpr => _parsed ?? throw CelException.FromErrors(Errors);

    /// <summary>
    /// The errors, ordered by place; empty when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public ImmutableArray<CelError> Errors { get; }

    /// <summary>
    /// Returns the parsed expression, or throws all errors.
    /// </summary>
    /// <returns>The parsed expression.</returns>
    /// <exception cref="CelException">The result is not successful; the exception carries all errors.</exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public CelParsedExpr GetParsedExprOrThrow() => _parsed ?? throw CelException.FromErrors(Errors);
}
