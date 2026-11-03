using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// An immutable CEL environment: the declarations against which expressions are compiled — variables,
/// functions with their implementations, types, extension sets, the standard library version, the time
/// zone database and the name resolution container. Build one with <see cref="CelEnvironmentBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// One environment compiles any number of expressions. Compilation parses the expression, checks its types
/// and plans it, and returns a program that is evaluated any number of times on different bindings.
/// <see cref="Evaluate{T}"/> does both steps in one call.
/// </para>
/// <para>
/// The environment does not change: no member changes its behavior. <see cref="ToBuilder"/> gives a
/// builder for a new environment with more declarations; this environment and the programs compiled in it
/// stay as they are. All members can be called from any number of threads at once. The environment keeps
/// compiled <c>matches</c> patterns in a cache bounded in size and shared by its programs; the cache never
/// changes a result or a cost. It does not cache compilations of expressions: an application that
/// evaluates the same texts again creates a <see cref="CelCompilationCache"/> or keeps the programs.
/// </para>
/// </remarks>
public sealed class CelEnvironment
{
    internal CelEnvironment(ImmutableArray<CelVariableDecl> variableDeclarations, DeclarationIndex declarations)
    {
        VariableDeclarations = variableDeclarations;
        Declarations = declarations;
    }

    /// <summary>
    /// Compiles an expression: parses it, checks its types in this environment and plans its evaluation.
    /// The result holds a program ready to evaluate, or all errors found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The expected CEL type of the result comes from <typeparamref name="T"/> by
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// for example <c>bool</c> for <see cref="bool"/>; <see cref="CelValue"/> and <see cref="object"/> accept a result
    /// of any type. An expression whose type is not assignable to the expected type is the compilation error
    /// <see cref="CelErrorCode.ExpectedTypeMismatch"/>. <see cref="CelCompileOptions.ExpectedType"/> sets the expected
    /// type explicitly when <typeparamref name="T"/> says too little, such as <c>list(int)</c> for
    /// <see cref="CelValue"/>.
    /// </para>
    /// <para>
    /// Errors are returned, not thrown: parse and check errors, every one found, each with its position in the
    /// expression. The text is untrusted input: an expression longer than the length limit or nested deeper than the
    /// nesting limit
    /// (page <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/limits.md">Limits</see>) is a
    /// compilation error, not an exception, and compilation of any text up to the length limit ends within a bounded
    /// time. Compilation takes no cancellation token: its work is bounded by these limits.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">
    /// The .NET type in which evaluation returns the result: a type of the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// <see cref="CelValue"/> or <see cref="object"/>.
    /// </typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">
    /// The compilation options: the explicit expected type and the position of the expression in its source
    /// document; <see langword="null"/> for none.
    /// </param>
    /// <returns>The program or the compilation errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// or <see cref="CelCompileOptions.ExpectedType"/> of <paramref name="options"/> cannot be converted to
    /// <typeparamref name="T"/>.
    /// </exception>
    public CelCompileResult<T> Compile<T>(string expression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (options is not null || !ConversionRules.IsResultType<T>())
        {
            // Compile options and the result types outside the rows implemented so far are not
            // implemented yet; they are not ignored.
            throw new NotImplementedException();
        }

        ParseResult parsed = Parse(expression);
        if (!parsed.IsSuccess)
        {
            return new CelCompileResult<T>(parsed.Errors);
        }

        CheckResult checkedResult = Check(parsed.Expression);
        if (!checkedResult.IsSuccess)
        {
            return new CelCompileResult<T>(checkedResult.Errors);
        }

        CheckedExpression checkedExpression = checkedResult.Expression;
        return new CelCompileResult<T>(new CelProgram<T>(Planner.Plan(checkedExpression), checkedExpression.ResultType));
    }

    /// <summary>
    /// Compiles an expression and evaluates it on the bindings in one call, with the same result and the
    /// same errors as <see cref="Compile{T}"/> followed by <see cref="CelProgram{T}.Evaluate"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both steps run the same code as the separate calls, so the result is the same by construction. The
    /// result holds either the compilation errors or the one evaluation error;
    /// <see cref="CelError.Stage"/> tells which step failed. The token is checked before compilation and
    /// between compilation and evaluation, and during evaluation as <see cref="CelProgram{T}.Evaluate"/>
    /// describes.
    /// </para>
    /// <para>
    /// Each call compiles the text again. To evaluate the same texts repeatedly, keep the programs or use a
    /// <see cref="CelCompilationCache"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the result is returned, as for <see cref="Compile{T}"/>.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <param name="compileOptions">The compilation options, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// or the expected type of <paramref name="compileOptions"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelEvalResult<T> Evaluate<T>(
        string expression,
        CelBindings bindings,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a new builder with all declarations of this environment, to build an environment that adds
    /// declarations to them.
    /// </summary>
    /// <remarks>
    /// Building from the returned builder creates a new environment; this environment and the programs
    /// compiled in it do not change and give the same results as before.
    /// </remarks>
    /// <returns>The builder.</returns>
    public CelEnvironmentBuilder ToBuilder()
    {
        throw new NotImplementedException();
    }

    internal ImmutableArray<CelVariableDecl> VariableDeclarations { get; }

    // The declarations of the standard library version and of the application, indexed for checking
    // and evaluation.
    internal DeclarationIndex Declarations { get; }

    /// <summary>
    /// Parses an expression: the parse step of <see cref="Compile{T}"/>, with the syntax of this environment. The
    /// parsed expression does not refer to the environment.
    /// </summary>
    /// <param name="expression">The text of the expression.</param>
    /// <returns>The parsed expression, or the errors of the stage <see cref="CelErrorStage.Parse"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "An instance member of the environment by design (docs/design/facade.md, \"Internal entry points\"): parsing uses the syntax of the environment. The minimal environment has no syntax settings yet; they arrive with the complete grammar (macros, the length and depth limits, docs/design/syntax.md), and this suppression is removed with them.")]
    internal ParseResult Parse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return Parser.Parse(expression);
    }

    /// <summary>
    /// Checks the types of a parsed expression in this environment: the check step of <see cref="Compile{T}"/>.
    /// </summary>
    /// <param name="parsed">The parsed expression.</param>
    /// <returns>
    /// The checked expression with the type of the expression, or every error of the stage <see cref="CelErrorStage.Check"/>
    /// in the order of the traversal.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="parsed"/> is <see langword="null"/>.</exception>
    internal CheckResult Check(ParsedExpression parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        return Checker.Check(parsed, Declarations);
    }

    /// <summary>
    /// Evaluates a parsed expression without type checking: names are looked up in the bindings when they are
    /// evaluated, and a function is chosen among all overloads of its name and argument count by the kinds of
    /// the argument values.
    /// </summary>
    /// <param name="parsed">The parsed expression.</param>
    /// <param name="bindings">The values of the variables.</param>
    /// <returns>The value as it is, without conversion, or one error of the stage <see cref="CelErrorStage.Evaluate"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parsed"/> or <paramref name="bindings"/> is <see langword="null"/>.</exception>
    internal CelEvalResult<CelValue> EvaluateWithoutTypeCheck(ParsedExpression parsed, CelBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(bindings);
        return CelProgram<CelValue>.Run(Planner.PlanWithoutTypeCheck(parsed, Declarations), bindings);
    }
}
