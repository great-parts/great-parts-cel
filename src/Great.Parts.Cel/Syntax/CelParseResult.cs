using System;
using System.Collections.Generic;
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
    /// Creates a successful result of a parser of another language, such as a filter, from the tree it built with a
    /// <see cref="CelExprFactory"/>.
    /// </summary>
    /// <param name="parsedExpr">The parsed expression.</param>
    /// <returns>The result with the parsed expression and no errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parsedExpr"/> is <see langword="null"/>.</exception>
    public static CelParseResult FromParsedExpr(CelParsedExpr parsedExpr)
    {
        ArgumentNullException.ThrowIfNull(parsedExpr);
        return new CelParseResult(parsedExpr);
    }

    /// <summary>
    /// Creates an unsuccessful result of a parser of another language from the errors it found, such as the errors that
    /// <see cref="CelExprFactory.CreateError"/> created.
    /// </summary>
    /// <remarks>
    /// The errors keep their order. Their stages are not checked: a parser of another language can report an error of the
    /// stage <see cref="CelErrorStage.Check"/>, such as a value that does not fit the type of a field, before the tree is
    /// type-checked.
    /// </remarks>
    /// <param name="errors">The errors; at least one.</param>
    /// <returns>The result with the errors and no parsed expression.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> or one of its elements is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public static CelParseResult FromErrors(IEnumerable<CelError> errors) => new(CopyErrors(errors));

    // The errors of a result of a parser of another language: copied once, in order, at least one and none null.
    internal static ImmutableArray<CelError> CopyErrors(IEnumerable<CelError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ImmutableArray<CelError>.Builder copy = ImmutableArray.CreateBuilder<CelError>();
        foreach (CelError error in errors)
        {
            ArgumentNullException.ThrowIfNull(error, nameof(errors));
            copy.Add(error);
        }

        if (copy.Count == 0)
        {
            throw new ArgumentException("A result without a success needs at least one error.", nameof(errors));
        }

        return copy.DrainToImmutable();
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
