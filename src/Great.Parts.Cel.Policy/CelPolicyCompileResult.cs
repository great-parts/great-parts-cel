using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Policy;

/// <summary>
/// The result of compiling a policy in an environment: the program and the composed expression, or all errors found.
/// </summary>
/// <remarks>
/// <para>
/// Errors are returned, not thrown: the parse errors of the expressions, the type errors, the errors of the checks of the
/// policy and the errors of building the environment of its imports, each with its position in the document in
/// <see cref="CelError.SourceLocation"/>. The program is an ordinary program of the environment and evaluates as the
/// program of an expression does; its evaluation errors report their position in the document.
/// </para>
/// <para>The result is immutable and can be shared between threads.</para>
/// </remarks>
/// <typeparam name="T">The .NET type in which the program returns the result of the policy.</typeparam>
public sealed class CelPolicyCompileResult<T>
{
    internal CelPolicyCompileResult(CelProgram<T> program, CelParsedExpr expression)
    {
        Program = program;
        Expression = expression;
        Errors = [];
    }

    internal CelPolicyCompileResult(ImmutableArray<CelError> errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// <see langword="true"/> when the policy compiled: there are no errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Program), nameof(Expression))]
    public bool IsSuccess => Program is not null;

    /// <summary>
    /// The program of the policy, or <see langword="null"/> when the result is not successful.
    /// </summary>
    public CelProgram<T>? Program { get; }

    /// <summary>
    /// The policy composed into one parsed expression, with the positions of its nodes in the document, or
    /// <see langword="null"/> when the result is not successful.
    /// </summary>
    /// <remarks>
    /// The expression uses <c>cel.@block</c> and the slots <c>@index0</c>, <c>@index1</c> and so on, as cel-go composes a policy;
    /// <see cref="CelSyntax.Format(CelParsedExpr)"/> prints it, and
    /// <see cref="CelEnvironment.CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/> compiles it again in an environment
    /// with the same abbreviations as the imports of the policy. The expression has no source name of its own
    /// (<see cref="CelParsedExpr.SourceName"/> is <see langword="null"/>): the name of the document is
    /// <see cref="CelPolicy.SourceName"/>, and the place that compiling it again needs is
    /// <c>new CelSourceLocation(policy.SourceName, 0, 1, 1)</c>, which the positions of its nodes are relative to.
    /// </remarks>
    public CelParsedExpr? Expression { get; }

    /// <summary>
    /// The errors, ordered by position in the document; empty when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public ImmutableArray<CelError> Errors { get; }

    /// <summary>
    /// Returns the program, or throws all errors.
    /// </summary>
    /// <returns>The program.</returns>
    /// <exception cref="CelException">The result is not successful; the exception carries all errors.</exception>
    [SuppressMessage(
        "Design",
        "CA1024:Use properties where appropriate",
        Justification = "The method throws for an unsuccessful result; a property getter must not throw, and the name tells the caller that it can.")]
    public CelProgram<T> GetProgramOrThrow() => Program ?? throw new CelException(Errors);
}
