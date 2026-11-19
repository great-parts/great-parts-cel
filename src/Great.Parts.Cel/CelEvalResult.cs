using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// The result of an evaluation: the value converted to <typeparamref name="T"/>, or the errors.
/// </summary>
/// <remarks>
/// <para>
/// Errors are returned, not thrown. An evaluation of a program has at most one error, of the stage
/// <see cref="CelErrorStage.Evaluate"/>; a one-step evaluation has either the compilation errors or that one
/// evaluation error, and <see cref="CelError.Stage"/> tells which. A successful evaluation allocates no
/// memory for the result: the structure holds the value itself.
/// </para>
/// <para>
/// Read
/// <see cref="Value"/> only after testing <see cref="IsSuccess"/>: for a reference type <typeparamref name="T"/> the compiler warns about a read without the test, and with warnings as errors the build fails. Reading <see cref="Value"/> of an unsuccessful result throws <see cref="CelException"/> with all errors. A CEL <c>null</c> result is <see langword="null"/> in <see cref="Value"/> even after the test; it occurs only when the expression can yield <c>null</c> and <typeparamref name="T"/> accepts it by the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
/// such as <see cref="object"/>. Code in which an error is a programming mistake can call
/// <see cref="GetValueOrThrow"/>.
/// </para>
/// <para>
/// <c>default(CelEvalResult&lt;T&gt;)</c> is not the result of any call: its <see cref="IsSuccess"/> is
/// <see langword="false"/> and its <see cref="Errors"/> is empty, and reading <see cref="Value"/>, <see cref="Cost"/> or
/// <see cref="GetValueOrThrow"/> throws <see cref="InvalidOperationException"/>. Use only results returned by the library.
/// The structure is immutable.
/// </para>
/// </remarks>
/// <typeparam name="T">The .NET type of the value.</typeparam>
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "A result is a value or errors returned by a call and is read, not compared; value equality would need equality of T and of error lists.")]
public readonly struct CelEvalResult<T>
{
    private readonly T? _value;
    private readonly ImmutableArray<CelError> _errors;
    private readonly ulong? _cost;

    // The errors of a result made by a call are never the default array (empty on success), so the default array tells a
    // default(CelEvalResult<T>) that no call returned.
    internal CelEvalResult(T? value, ulong? cost = null)
    {
        _value = value;
        _errors = [];
        _cost = cost;
    }

    internal CelEvalResult(ImmutableArray<CelError> errors, ulong? cost = null)
    {
        _errors = errors;
        _cost = cost;
    }

    /// <summary>
    /// Whether the evaluation succeeded: <see langword="true"/> when there are no errors; <see langword="false"/> for
    /// <c>default(CelEvalResult&lt;T&gt;)</c>, which no call returns.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => !_errors.IsDefault && _errors.IsEmpty;

    /// <summary>
    /// The value of the result converted to <typeparamref name="T"/>. Read it only when
    /// <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="CelException">
    /// <see cref="IsSuccess"/> is <see langword="false"/>; <see cref="CelException.Errors"/> holds the errors.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The structure is <c>default(CelEvalResult&lt;T&gt;)</c>, not a result returned by the library.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "Reading the value of an unsuccessful result throws CelException with all its errors.")]
    public T? Value =>
        IsSuccess ? _value : throw Failure();

    /// <summary>
    /// The errors: the compilation errors or the one evaluation error; empty when <see cref="IsSuccess"/> is
    /// <see langword="true"/>. Never a default <see cref="ImmutableArray{T}"/>: empty for
    /// <c>default(CelEvalResult&lt;T&gt;)</c> too.
    /// </summary>
    public ImmutableArray<CelError> Errors => _errors.IsDefault ? [] : _errors;

    /// <summary>
    /// Returns the value, for code in which an evaluation error is a programming mistake.
    /// </summary>
    /// <returns>The value converted to <typeparamref name="T"/>.</returns>
    /// <exception cref="CelException">
    /// <see cref="IsSuccess"/> is <see langword="false"/>; <see cref="CelException.Errors"/> holds the errors.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The structure is <c>default(CelEvalResult&lt;T&gt;)</c>, not a result returned by the library.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public T GetValueOrThrow() => IsSuccess ? _value! : throw Failure();

    /// <summary>
    /// The cost the evaluation spent, in the cost units of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md">cost model</see>, when a cost
    /// limit applied to it; <see langword="null"/> when no limit applied, because then the cost is not counted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A limit applies when the call passed <see cref="CelEvalOptions.CostLimit"/> or the environment has
    /// <see cref="CelEnvironment.DefaultCostLimit"/>; the simple entry <see cref="CelExpression"/> always has one. For a
    /// result with the error <see cref="Diagnostics.CelErrorCode.CostLimitExceeded"/>, the cost is the cost spent before the
    /// refused operation. For a one-step call that failed at compilation it is <see langword="null"/>.
    /// </para>
    /// <para>The value is held in the structure: reading it allocates nothing.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The structure is <c>default(CelEvalResult&lt;T&gt;)</c>, not a result returned by the library.
    /// </exception>
    public ulong? Cost => _errors.IsDefault ? throw NotReturnedByCall() : _cost;

    // The exception of an unsuccessful result: CelException with the errors, or, for a default structure, the state of the object.
    private Exception Failure() => _errors.IsDefault ? NotReturnedByCall() : CelException.FromErrors(_errors);

    private static InvalidOperationException NotReturnedByCall() =>
        new("The result was not returned by an evaluation: it is default(CelEvalResult<T>).");
}
