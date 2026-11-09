using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
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
    private readonly CelStandardLibrary _standardLibrary;

    // The declarations the environment was built from, for ToBuilder.
    private readonly DeclarationSet _declarationSet;

    internal CelEnvironment(CelStandardLibrary standardLibrary, DeclarationSet declarationSet, DeclarationIndex declarations)
    {
        _standardLibrary = standardLibrary;
        _declarationSet = declarationSet;
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

        // The expected type is derived before the text is read, so the same T and options throw for every text.
        CelType expectedType = ConversionRules.ExpectedType<T>(options?.ExpectedType, nameof(options), Declarations.Types);
        CelSourceLocation? start = options?.SourceLocation;
        ParseResult parsed = Parse(expression);
        if (!parsed.IsSuccess)
        {
            return new CelCompileResult<T>(InDocument(parsed.Errors, start));
        }

        CheckResult checkedResult = Check(parsed.Expression, expectedType);
        if (!checkedResult.IsSuccess)
        {
            return new CelCompileResult<T>(InDocument(checkedResult.Errors, start));
        }

        CheckedExpression checkedExpression = checkedResult.Expression;
        return new CelCompileResult<T>(new CelProgram<T>(Planner.Plan(checkedExpression, Declarations, TimeZones), checkedExpression.ResultType, start));
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
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(bindings);
        cancellationToken.ThrowIfCancellationRequested();
        return EvaluateCompiled(Compile<T>(expression, compileOptions), bindings, evalOptions, cancellationToken);
    }

    // The steps of the one-step call after the compilation: the errors of a failed compilation are the result; otherwise
    // the token is checked again and the program evaluated.
    internal static CelEvalResult<T> EvaluateCompiled<T>(
        CelCompileResult<T> compiled,
        CelBindings bindings,
        CelEvalOptions? evalOptions,
        CancellationToken cancellationToken)
    {
        if (!compiled.IsSuccess)
        {
            return new CelEvalResult<T>(compiled.Errors);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return compiled.Program.Evaluate(bindings, evalOptions, cancellationToken);
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
    public CelEnvironmentBuilder ToBuilder() => new(_standardLibrary, _declarationSet);

    // The declarations of variables as the builder collected them.
    internal ImmutableArray<CelVariableDecl> VariableDeclarations
    {
        get
        {
            ImmutableArray<CelVariableDecl>.Builder variables = ImmutableArray.CreateBuilder<CelVariableDecl>(_declarationSet.Variables.Length);
            foreach (SourcedDeclaration<CelVariableDecl> variable in _declarationSet.Variables)
            {
                variables.Add(variable.Value);
            }

            return variables.MoveToImmutable();
        }
    }

    // The declarations of the standard library version and of the application, indexed for checking
    // and evaluation.
    internal DeclarationIndex Declarations { get; }

    // The time zone database that the application gave, or null when the environment has none.
    internal CelTimeZoneDatabase? TimeZones => _declarationSet.TimeZones;

    /// <summary>
    /// Parses an expression: the parse step of <see cref="Compile{T}"/>, with the syntax of this environment unless
    /// <paramref name="syntax"/> replaces it. The parsed expression does not refer to the environment.
    /// </summary>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="syntax">
    /// The syntax options for this call, such as the optional syntax before its extension set; <see langword="null"/>
    /// for those of the environment.
    /// </param>
    /// <returns>The parsed expression, or the errors of the stage <see cref="CelErrorStage.Parse"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "An instance member of the environment by design (docs/design/facade.md, \"Internal entry points\"): parsing uses the syntax options of the environment. Every environment has SyntaxOptions.Standard until extension sets bring syntax (docs/design/syntax.md, \"Syntax options\"), and this suppression is removed with them.")]
    internal ParseResult Parse(string expression, SyntaxOptions? syntax = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ParseWithinBoundary(new Parser(expression, syntax ?? SyntaxOptions.Standard), static parser => parser.Run());
    }

    /// <summary>
    /// The compilation boundary of parsing: runs a parse step of the parser, and turns an unexpected exception other than
    /// <see cref="OutOfMemoryException"/> into <see cref="CelErrorCode.InternalError"/> of the stage
    /// <see cref="CelErrorStage.Parse"/>, after the errors the parser found before it.
    /// </summary>
    /// <param name="parser">The parser of the text.</param>
    /// <param name="parse">The parse step: <see cref="Parse"/> runs the parser; a test can make the step throw.</param>
    /// <returns>The result of the step, or the errors with <see cref="CelErrorCode.InternalError"/> last.</returns>
    internal static ParseResult ParseWithinBoundary(Parser parser, Func<Parser, ParseResult> parse)
    {
        try
        {
            return parse(parser);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return parser.Interrupted(exception);
        }
    }

    // The errors with their positions in the document of the expression, when the compile options give its start.
    private static ImmutableArray<CelError> InDocument(ImmutableArray<CelError> errors, CelSourceLocation? start)
    {
        if (start is not { } documentStart)
        {
            return errors;
        }

        ImmutableArray<CelError>.Builder mapped = ImmutableArray.CreateBuilder<CelError>(errors.Length);
        foreach (CelError error in errors)
        {
            mapped.Add(error.InDocument(documentStart));
        }

        return mapped.MoveToImmutable();
    }

    /// <summary>
    /// Checks the types of a parsed expression in this environment: the check step of <see cref="Compile{T}"/>, inside the
    /// compilation boundary of checking. An unexpected exception other than <see cref="OutOfMemoryException"/> becomes
    /// <see cref="CelErrorCode.InternalError"/> at the node being checked, with the errors found before it.
    /// </summary>
    /// <param name="parsed">The parsed expression.</param>
    /// <param name="expectedType">
    /// The expected type of the expression, which <see cref="Compile{T}"/> derives; <see langword="null"/> for none, as
    /// <c>dyn</c>.
    /// </param>
    /// <returns>
    /// The checked expression with the type of the expression, or every error of the stage <see cref="CelErrorStage.Check"/>
    /// ordered by offset.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="parsed"/> is <see langword="null"/>.</exception>
    internal CheckResult Check(ParsedExpression parsed, CelType? expectedType = null)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var checker = new Checker(parsed, Declarations, expectedType);
        try
        {
            return checker.Run();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return checker.Interrupted(exception);
        }
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
        return CelProgram<CelValue>.Run(Planner.PlanWithoutTypeCheck(parsed, Declarations, TimeZones), CelType.Dyn, bindings, null, null, CancellationToken.None);
    }
}
