using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel;

/// <summary>
/// The result of a partial evaluation: the value converted to <typeparamref name="T"/>, the errors, or the unknown parts the
/// result depends on — <c>ExprValue</c> of <c>eval.proto</c> with its <c>UnknownSet</c> — and the residual expression.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one holds: <see cref="IsSuccess"/>, a non-empty <see cref="Errors"/>, or <see cref="IsUnknown"/> with a non-empty
/// <see cref="Unknowns"/> and no errors.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
/// <typeparam name="T">The .NET type of the result.</typeparam>
public sealed class CelPartialEvalResult<T>
{
    private readonly T? _value;
    private readonly ImmutableArray<CelError> _errors;
    private readonly ImmutableArray<CelUnknown> _unknowns;
    private readonly ulong? _cost;
    private readonly ResidualSource _residual;

    internal CelPartialEvalResult(T? value, ulong? cost, ResidualSource residual)
    {
        _value = value;
        _errors = [];
        _unknowns = [];
        _cost = cost;
        _residual = residual;
        IsSuccess = true;
    }

    internal CelPartialEvalResult(ImmutableArray<CelError> errors, ulong? cost, ResidualSource residual)
    {
        _errors = errors;
        _unknowns = [];
        _cost = cost;
        _residual = residual;
    }

    internal CelPartialEvalResult(ImmutableArray<CelUnknown> unknowns, ulong? cost, ResidualSource residual)
    {
        _errors = [];
        _unknowns = unknowns;
        _cost = cost;
        _residual = residual;
    }

    /// <summary>
    /// Whether the value is known: the unknown parts of the data, if any, do not affect it.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the result depends on unknown parts of the data, listed in <see cref="Unknowns"/>; then <see cref="Errors"/> is
    /// empty.
    /// </summary>
    public bool IsUnknown => !_unknowns.IsEmpty;

    /// <summary>
    /// The value when it is known.
    /// </summary>
    /// <exception cref="CelException">The result has errors; the exception holds them all.</exception>
    /// <exception cref="InvalidOperationException">The result is unknown.</exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "Reading the value of a result that has errors throws CelException with all its errors, as the value of CelEvalResult does.")]
    public T? Value => IsSuccess ? _value : throw Failure();

    /// <summary>
    /// The errors of the evaluation, of which there is at most one, as for <see cref="CelEvalResult{T}"/>; empty when the value
    /// is known or unknown.
    /// </summary>
    public ImmutableArray<CelError> Errors => _errors;

    /// <summary>
    /// The unknown parts the result depends on, ordered by <see cref="CelUnknown.ExprId"/>; empty unless
    /// <see cref="IsUnknown"/>.
    /// </summary>
    /// <remarks>
    /// The list follows the CEL language definition: in <c>a || b</c> with <c>a</c> unknown and <c>b</c> true the result is
    /// known; in <c>a + b</c> with both unknown the result depends on both.
    /// </remarks>
    public ImmutableArray<CelUnknown> Unknowns => _unknowns;

    /// <summary>
    /// The cost spent, in cost units, when a cost limit applied; otherwise <see langword="null"/>, as
    /// <see cref="CelEvalResult{T}.Cost"/>.
    /// </summary>
    public ulong? Cost => _cost;

    /// <summary>
    /// Returns the value when it is known.
    /// </summary>
    /// <returns>The value.</returns>
    /// <exception cref="CelException">The result has errors; the exception holds them all.</exception>
    /// <exception cref="InvalidOperationException">The result is unknown.</exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public T GetValueOrThrow() => IsSuccess ? _value! : throw Failure();

    /// <summary>
    /// Returns the residual expression: the parsed expression of the program in which each part whose value is known is
    /// replaced with that value, ready for <c>CelEnvironment.CompileParsed</c> and <c>CelSyntax.Format</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules are those of the residual of cel-go: a known node becomes a constant, a list or a map of constants, or a call
    /// that makes the value — <c>timestamp("…")</c>, <c>duration("…")</c>, <c>decimal("…")</c>, <c>double("NaN")</c>,
    /// <c>optional.of(…)</c>, <c>optional.none()</c>; a value of an object, a message or a type is not written, and its node
    /// stays as it is; <c>&amp;&amp;</c>, <c>||</c>, <c>?:</c>, <c>!</c> and <c>in</c> of an empty list shrink when an operand is
    /// known; of a comprehension, only the range is replaced. The nodes keep their positions in the text of the program, so the
    /// errors of compiling the residual point into that text; the ids are new.
    /// </para>
    /// <para>
    /// The residual compiled in the same environment and evaluated on the full data gives the result of the program on the
    /// full data, or an error with the same code, except an error in a part that the known data removed. When the value is known,
    /// the residual is its constant, or the tree of the program when the value has no written form.
    /// </para>
    /// </remarks>
    /// <returns>The residual expression.</returns>
    /// <exception cref="CelException">The result has errors; the exception holds them all.</exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method builds a new tree on each call and throws for a result with errors; a property getter must do neither.")]
    public CelParsedExpr GetResidual() => _errors.IsEmpty ? _residual.Build() : throw CelException.FromErrors(_errors);

    // The exception of a result that is not a value: CelException for errors, InvalidOperationException for an unknown.
    private Exception Failure() => _errors.IsEmpty
        ? new InvalidOperationException("The result is unknown: it depends on " + _unknowns.Length + " unknown part(s) of the data; read Unknowns.")
        : CelException.FromErrors(_errors);
}
