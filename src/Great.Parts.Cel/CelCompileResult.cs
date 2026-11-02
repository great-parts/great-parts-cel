using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel;

/// <summary>
/// The result of a compilation: a program ready to evaluate, or all compilation errors.
/// </summary>
/// <remarks>
/// <para>
/// Compilation errors are returned, not thrown. Each has the stage <see cref="CelErrorStage.Parse"/> or
/// <see cref="CelErrorStage.Check"/> — or, for the codes 9000–9999, the stage at which it occurred — and
/// its own position in the expression. Compilation reports every error it finds, not only the first.
/// </para>
/// <para>
/// Read <see cref="Program"/> only after testing <see cref="IsSuccess"/>: the compiler warns about a read
/// without the test, and with warnings as errors the build fails. Code in which a compilation error is a
/// programming mistake, such as an expression fixed in the source code, can call
/// <see cref="GetProgramOrThrow"/>.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
/// <typeparam name="T">The .NET type in which the program returns its result.</typeparam>
public sealed class CelCompileResult<T>
{
    private readonly CelProgram<T>? _program;

    internal CelCompileResult(CelProgram<T> program)
    {
        _program = program;
        Errors = [];
    }

    internal CelCompileResult(ImmutableArray<CelError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Whether the expression compiled: <see langword="true"/> when there are no errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Program))]
    public bool IsSuccess => _program is not null;

    /// <summary>
    /// The compiled program. Read it only when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="IsSuccess"/> is <see langword="false"/>.</exception>
    public CelProgram<T>? Program =>
        _program ?? throw new InvalidOperationException("The program was not compiled: the result has compilation errors.");

    /// <summary>
    /// All compilation errors found; empty when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public ImmutableArray<CelError> Errors { get; }

    /// <summary>
    /// Returns the compiled program, for code in which a compilation error is a programming mistake.
    /// </summary>
    /// <returns>The program.</returns>
    /// <exception cref="CelException">
    /// <see cref="IsSuccess"/> is <see langword="false"/>; <see cref="CelException.Errors"/> holds the compilation errors.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public CelProgram<T> GetProgramOrThrow() => _program ?? throw new CelException(Errors);
}
