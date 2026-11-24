using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;

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
    /// Creates an unsuccessful compilation result from errors found before compilation, for a parser of another language
    /// that compiles its tree with <see cref="CelEnvironment.CompileParsed{T}(Syntax.CelParsedExpr, CelCompileOptions)"/>
    /// and reports its own errors, such as a syntax error of a filter, in the same result type.
    /// </summary>
    /// <remarks>
    /// The errors keep their order. <see cref="GetProgramOrThrow"/> of the result throws <see cref="CelException"/> with them.
    /// </remarks>
    /// <param name="errors">The errors; at least one.</param>
    /// <returns>The result with the errors and no program.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> or one of its elements is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    [SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "The result type is generic in the type of the program; a call CelCompileResult<bool>.FromErrors(errors) is unambiguous and the type has no non-generic counterpart.")]
    public static CelCompileResult<T> FromErrors(IEnumerable<CelError> errors) => new(CelParseResult.CopyErrors(errors));

    /// <summary>
    /// Whether the expression compiled: <see langword="true"/> when there are no errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Program))]
    public bool IsSuccess => _program is not null;

    /// <summary>
    /// The compiled program. Read it only when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    /// <exception cref="CelException">
    /// <see cref="IsSuccess"/> is <see langword="false"/>; <see cref="CelException.Errors"/> holds the compilation errors.
    /// </exception>
    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "Reading the program of an unsuccessful result throws CelException with all its errors.")]
    public CelProgram<T>? Program =>
        _program ?? throw CelException.FromErrors(Errors);

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
    public CelProgram<T> GetProgramOrThrow() => _program ?? throw CelException.FromErrors(Errors);
}
