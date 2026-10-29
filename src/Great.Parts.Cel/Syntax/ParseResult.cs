using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The result of parsing: the parsed expression, or the parse errors in the order in which they were found.
/// </summary>
internal sealed class ParseResult
{
    internal ParseResult(ParsedExpression expression)
    {
        Expression = expression;
        Errors = [];
    }

    internal ParseResult(ImmutableArray<CelError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// <see langword="true"/> when the text parsed without errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Expression))]
    internal bool IsSuccess => Expression is not null;

    /// <summary>
    /// The parsed expression, or <see langword="null"/> when there are errors.
    /// </summary>
    internal ParsedExpression? Expression { get; }

    /// <summary>
    /// The errors of the stage <see cref="CelErrorStage.Parse"/>; empty on success.
    /// </summary>
    internal ImmutableArray<CelError> Errors { get; }
}
