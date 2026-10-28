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
/// Read <see cref="Value"/> only after testing <see cref="IsSuccess"/>: for a reference type
/// <typeparamref name="T"/> the compiler warns about a read without the test, and with warnings as errors
/// the build fails. A CEL <c>null</c> result is <see langword="null"/> in <see cref="Value"/> even after the
/// test; it occurs only when the expression can yield <c>null</c> and <typeparamref name="T"/> accepts it
/// by the type mapping table of the documentation, such as <see cref="object"/>. Code in which an error is
/// a programming mistake can call <see cref="GetValueOrThrow"/>.
/// </para>
/// <para>
/// <c>default(CelEvalResult&lt;T&gt;)</c> is not the result of any call: use only results returned by
/// the library. The structure is immutable.
/// </para>
/// </remarks>
/// <typeparam name="T">The .NET type of the value.</typeparam>
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "A result is a value or errors returned by a call and is read, not compared; value equality would need equality of T and of error lists.")]
public readonly struct CelEvalResult<T>
{
    /// <summary>
    /// Whether the evaluation succeeded: <see langword="true"/> when there are no errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => throw new NotImplementedException();

    /// <summary>
    /// The value of the result converted to <typeparamref name="T"/>. Read it only when
    /// <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="IsSuccess"/> is <see langword="false"/>.</exception>
    public T? Value => throw new NotImplementedException();

    /// <summary>
    /// The errors: the compilation errors or the one evaluation error; empty when <see cref="IsSuccess"/> is
    /// <see langword="true"/>.
    /// </summary>
    public ImmutableArray<CelError> Errors => throw new NotImplementedException();

    /// <summary>
    /// Returns the value, for code in which an evaluation error is a programming mistake.
    /// </summary>
    /// <returns>The value converted to <typeparamref name="T"/>.</returns>
    /// <exception cref="CelException">
    /// <see cref="IsSuccess"/> is <see langword="false"/>; <see cref="CelException.Errors"/> holds the errors.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public T GetValueOrThrow()
    {
        throw new NotImplementedException();
    }
}
