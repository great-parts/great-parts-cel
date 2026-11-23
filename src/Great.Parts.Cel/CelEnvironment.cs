using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library.Regex;
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
/// <see cref="Evaluate{T}(string, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/> does both steps in
/// one call, <see cref="Validate{T}(string, CelCompileOptions)"/> checks an expression without creating a program, and
/// <see cref="Create(Action{CelEnvironmentBuilder})"/> builds an environment in one call.
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
    // The references of a compilation that stopped before the check.
    private static readonly IReadOnlyDictionary<int, Reference> EmptyReferences = ImmutableDictionary<int, Reference>.Empty;

    private readonly CelStandardLibrary _standardLibrary;

    // The declarations the environment was built from, for ToBuilder.
    private readonly DeclarationSet _declarationSet;

    // The read-only description for the introspection members; null until the first read.
    private EnvironmentDescription? _description;

    internal CelEnvironment(CelStandardLibrary standardLibrary, DeclarationSet declarationSet, DeclarationIndex declarations, SyntaxOptions syntax, PatternCache patternCache)
    {
        _standardLibrary = standardLibrary;
        _declarationSet = declarationSet;
        Declarations = declarations;
        Syntax = syntax;
        PatternCache = patternCache;
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
        return ProgramOf<T>(CompileSteps(expression, expectedType, start), start);
    }

    // The program of a compilation that got as far as the plan, or its errors.
    private CelCompileResult<T> ProgramOf<T>(Compilation compiled, CelSourceLocation? start) =>
        compiled is { Plan: { } plan, Check.Expression: { } checkedExpression }
            ? new CelCompileResult<T>(new CelProgram<T>(plan, checkedExpression.ResultType, start, DefaultCostLimit, InputType))
            : new CelCompileResult<T>(compiled.Errors);

    // The steps that Compile and Validate share, so that both give the same errors: parse, check and planning. The plan exists
    // only when all three succeeded; the check result holds the references resolved before an error of the check.
    private Compilation CompileSteps(string expression, CelType expectedType, CelSourceLocation? start)
    {
        CelParseResult parsed = Parse(expression, syntax: null);
        return parsed.IsSuccess
            ? CheckAndPlan(parsed.ParsedExpr, expectedType, start)
            : new Compilation(InDocument(parsed.Errors, start), null, null, default);
    }

    // The steps for a tree of any source: the syntax that the environment accepts is checked unless its own parser made the tree,
    // then the tree is checked and planned.
    private Compilation CompileTree(CelParsedExpr tree, CelType expectedType, CelSourceLocation? start)
    {
        if (!ReferenceEquals(tree.ParsedWith, Syntax) && TreeInput.Check(tree, Syntax) is { IsEmpty: false } inputErrors)
        {
            return new Compilation(InDocument(inputErrors, start), null, null, default);
        }

        return CheckAndPlan(tree, expectedType, start);
    }

    private Compilation CheckAndPlan(CelParsedExpr parsed, CelType expectedType, CelSourceLocation? start)
    {
        ImmutableArray<CelMacroCall> macroCalls = parsed.MacroCalls;
        CheckResult checkedResult = Check(parsed, expectedType);
        if (!checkedResult.IsSuccess)
        {
            return new Compilation(InDocument(checkedResult.Errors, start), null, checkedResult, macroCalls);
        }

        if (!TryPlanWithinBoundary(Planner.For(checkedResult.Expression, Declarations, TimeZones), static planner => planner.Run(), out Plan? plan, out CelError? planningError))
        {
            return new Compilation(InDocument([planningError], start), null, checkedResult, macroCalls);
        }

        return new Compilation([], plan, checkedResult, macroCalls);
    }

    // The outcome of CompileSteps: the errors, and the plan, the check result and the macro calls of the text that exist when the
    // steps got that far.
    private readonly record struct Compilation(ImmutableArray<CelError> Errors, Plan? Plan, CheckResult? Check, ImmutableArray<CelMacroCall> MacroCalls);

    /// <summary>
    /// The compilation boundary of planning: runs a planning step of the planner, and turns an unexpected exception other than
    /// <see cref="OutOfMemoryException"/> into <see cref="CelErrorCode.InternalError"/> of the stage
    /// <see cref="CelErrorStage.Check"/> at the node being planned.
    /// </summary>
    /// <param name="planner">The planner of the checked expression.</param>
    /// <param name="plan">The planning step: <see cref="Compile{T}"/> runs the planner; a test can make the step throw.</param>
    /// <param name="result">The plan, when the step did not throw.</param>
    /// <param name="error">The error, when the step threw.</param>
    /// <returns><see langword="true"/> when the step gave the plan.</returns>
    internal static bool TryPlanWithinBoundary(
        Planner planner,
        Func<Planner, Plan> plan,
        [NotNullWhen(true)] out Plan? result,
        [NotNullWhen(false)] out CelError? error)
    {
        try
        {
            result = plan(planner);
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            result = null;
            error = planner.Interrupted(exception);
            return false;
        }
    }

    /// <summary>
    /// Compiles an expression and evaluates it on the bindings in one call, with the same result and the
    /// same errors as <see cref="Compile{T}"/> followed by
    /// <see cref="CelProgram{T}.Evaluate(CelBindings, CelEvalOptions, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both steps run the same code as the separate calls, so the result is the same by construction. The
    /// result holds either the compilation errors or the one evaluation error;
    /// <see cref="CelError.Stage"/> tells which step failed. The token is checked before compilation and
    /// between compilation and evaluation, and during evaluation as
    /// <see cref="CelProgram{T}.Evaluate(CelBindings, CelEvalOptions, CancellationToken)"/>
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

    // The declarations of the standard library version, of the extension sets and of the application, indexed for checking
    // and evaluation.
    internal DeclarationIndex Declarations { get; }

    // The syntax of the environment: SyntaxOptions.Standard unless an extension set brings macros or syntax. Every path that
    // parses a text, Compile and Evaluate included, uses it.
    internal SyntaxOptions Syntax { get; }

    // The programs of the patterns of matches that the programs of this environment have seen. Empty when the environment is built,
    // so a new environment and the one that ToBuilder().Build() gives each have a cache of their own; the same object that
    // Declarations hands to the checker and the planner.
    internal PatternCache PatternCache { get; }

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
    internal CelParseResult Parse(string expression, SyntaxOptions? syntax)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return ParseWithinBoundary(new Parser(expression, syntax ?? Syntax), static parser => parser.Run());
    }

    /// <summary>
    /// The compilation boundary of parsing: runs a parse step of the parser, and turns an unexpected exception other than
    /// <see cref="OutOfMemoryException"/> into <see cref="CelErrorCode.InternalError"/> of the stage
    /// <see cref="CelErrorStage.Parse"/>, after the errors the parser found before it.
    /// </summary>
    /// <param name="parser">The parser of the text.</param>
    /// <param name="parse">The parse step: <see cref="Parse(string, SyntaxOptions)"/> runs the parser; a test can make the step throw.</param>
    /// <returns>The result of the step, or the errors with <see cref="CelErrorCode.InternalError"/> last.</returns>
    internal static CelParseResult ParseWithinBoundary(Parser parser, Func<Parser, CelParseResult> parse)
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
    internal CheckResult Check(CelParsedExpr parsed, CelType? expectedType = null)
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
    internal CelEvalResult<CelValue> EvaluateWithoutTypeCheck(CelParsedExpr parsed, CelBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(bindings);
        return CelProgram<CelValue>.Run(Planner.PlanWithoutTypeCheck(parsed, Declarations, TimeZones), CelType.Dyn, bindings, null, null, CancellationToken.None);
    }

    /// <summary>
    /// Builds an environment with the standard library version 1 in one call: creates a builder, lets
    /// <paramref name="configure"/> declare variables, functions, types and extension sets, and builds the environment;
    /// build errors are thrown.
    /// </summary>
    /// <remarks>
    /// <c>CelEnvironment.Create(b =&gt; b.AddVariable&lt;long&gt;("x"))</c> is
    /// <c>new CelEnvironmentBuilder().AddVariable&lt;long&gt;("x").Build().GetEnvironmentOrThrow()</c>. Use it where a build
    /// error is a programming mistake, such as an environment declared once at startup; to receive build errors as a
    /// result, use <see cref="CelEnvironmentBuilder.Build"/>. An exception thrown by <paramref name="configure"/> reaches the
    /// caller unchanged.
    /// </remarks>
    /// <param name="configure">Declares the contents of the environment on the builder.</param>
    /// <returns>The environment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="CelException">The environment has build errors; <see cref="CelException.Errors"/> holds them all.</exception>
    public static CelEnvironment Create(Action<CelEnvironmentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return Create(CelStandardLibrary.V1, configure);
    }

    /// <summary>
    /// Builds an environment with the given version of the standard library in one call, as
    /// <see cref="Create(Action{CelEnvironmentBuilder})"/> does; build errors are thrown.
    /// </summary>
    /// <param name="standardLibrary">The version of the standard library, such as <see cref="CelStandardLibrary.V1"/>.</param>
    /// <param name="configure">Declares the contents of the environment on the builder.</param>
    /// <returns>The environment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="standardLibrary"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="CelException">The environment has build errors; <see cref="CelException.Errors"/> holds them all.</exception>
    public static CelEnvironment Create(CelStandardLibrary standardLibrary, Action<CelEnvironmentBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(standardLibrary);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new CelEnvironmentBuilder(standardLibrary);
        configure(builder);
        return builder.Build().GetEnvironmentOrThrow();
    }

    /// <summary>
    /// Compiles an expression and evaluates it on an instance of the input type of the environment and on the bindings of
    /// other variables in one call, with the same result and the same errors as <see cref="Compile{T}"/> followed by
    /// <see cref="CelProgram{T}.EvaluateInput{TInput}(TInput, CelBindings, CelEvalOptions, CancellationToken)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fields of the input are read from <paramref name="input"/> only, never from the bindings. The input is a parameter
    /// of the type <see cref="object"/> because <typeparamref name="T"/> is given explicitly and C# does not infer the rest of
    /// the type arguments; a structure is boxed. The path without boxing is a program compiled once and evaluated with
    /// <see cref="CelProgram{T}.EvaluateInput{TInput}(TInput, CelBindings, CelEvalOptions, CancellationToken)"/>. In an
    /// environment without an input type, use
    /// <see cref="Evaluate{T}(string, CelBindings, CelCompileOptions, CelEvalOptions, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// The result holds either the compilation errors or the one evaluation error; <see cref="CelError.Stage"/> tells which
    /// step failed. The token is checked before compilation and between compilation and evaluation, and during evaluation as
    /// the evaluation of a program describes. Each call compiles the text again; to evaluate the same texts repeatedly, keep
    /// the programs or use a <see cref="CelCompilationCache"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the result is returned, as for <see cref="Compile{T}"/>.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="input">The instance of <see cref="InputType"/>.</param>
    /// <param name="bindings">The values of the other variables, or <see langword="null"/> for none.</param>
    /// <param name="compileOptions">The compilation options, or <see langword="null"/> for none.</param>
    /// <param name="evalOptions">The evaluation options, such as the cost limit, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token that cancels the call.</param>
    /// <returns>The result converted to <typeparamref name="T"/>, or the errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The environment has no input type, <paramref name="input"/> is not an instance of the input type,
    /// <typeparamref name="T"/> is not in the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// or the expected type of <paramref name="compileOptions"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public CelEvalResult<T> EvaluateInput<T>(
        string expression,
        object input,
        CelBindings? bindings = null,
        CelCompileOptions? compileOptions = null,
        CelEvalOptions? evalOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(input);
        CheckInput(InputType, input);
        cancellationToken.ThrowIfCancellationRequested();
        return EvaluateInputCompiled(Compile<T>(expression, compileOptions), input, bindings, evalOptions, cancellationToken);
    }

    // The steps of the one-step call with an input after the compilation: the errors of a failed compilation are the result; otherwise
    // the token is checked again and the program evaluated on the input.
    internal static CelEvalResult<T> EvaluateInputCompiled<T>(
        CelCompileResult<T> compiled,
        object input,
        CelBindings? bindings,
        CelEvalOptions? evalOptions,
        CancellationToken cancellationToken)
    {
        if (!compiled.IsSuccess)
        {
            return new CelEvalResult<T>(compiled.Errors);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return compiled.Program.EvaluateInput(input, bindings, evalOptions, cancellationToken);
    }

    // The checks of an input that a call passes: the environment has an input type, and the instance belongs to it — an instance of
    // the .NET type of its description, or of the carrier class of a type described at run time. The parameter is named input.
    internal static void CheckInput(CelObjectType? inputType, object input)
    {
        if (inputType is null)
        {
            throw new ArgumentException("The environment has no input type: declare one with UseInput<TInput>().", nameof(input));
        }

        if (!inputType.Belongs(input))
        {
            throw NotTheInputType(inputType, input.GetType());
        }
    }

    // The same check for a structure, by its type: a structure of the input type, or one that implements an input interface. A type
    // described at run time has a class for its carrier, so no structure belongs to it. The parameter is named input, the name of the
    // argument that the exception names.
    internal static void CheckInputType(CelObjectType? inputType, Type input)
    {
        if (inputType is null)
        {
            throw new ArgumentException("The environment has no input type: declare one with UseInput<TInput>().", nameof(input));
        }

        if (inputType.ClrType?.IsAssignableFrom(input) != true)
        {
            throw NotTheInputType(inputType, input);
        }
    }

    private static ArgumentException NotTheInputType(CelObjectType inputType, Type input) =>
        new("The input of the type '" + input + "' is not an instance of the input type '" + (inputType.ClrType?.ToString() ?? inputType.Name) + "'.", nameof(input));

    /// <summary>
    /// Validates an expression without creating a program: compiles it as <see cref="Compile{T}"/> does and returns all
    /// errors, the CEL type of the result and the names of the variables and functions the expression refers to.
    /// </summary>
    /// <remarks>
    /// The expected type comes from <typeparamref name="T"/> as in <see cref="Compile{T}"/>, so <c>Validate&lt;bool&gt;</c>
    /// reports an expression whose type is not <c>bool</c> as the error <see cref="CelErrorCode.ExpectedTypeMismatch"/>.
    /// Errors are returned, not thrown, and the work is bounded by the limits of compilation, as for
    /// <see cref="Compile{T}"/>.
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the expression would return its result, as for <see cref="Compile{T}"/>.</typeparam>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The result of the validation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of
    /// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// or the expected type of <paramref name="options"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The generic and the non-generic validation take the same optional options; they differ by the expected type, not by the arguments.")]
    public CelValidationResult Validate<T>(string expression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        CelType expectedType = ConversionRules.ExpectedType<T>(options?.ExpectedType, nameof(options), Declarations.Types);
        return ValidationOf(CompileSteps(expression, expectedType, options?.SourceLocation));
    }

    // The result of the validation of a compilation: its errors, the type of the result and the names it referred to.
    private static CelValidationResult ValidationOf(Compilation compiled)
    {
        ReferencedNames.Collect(compiled.Check?.References ?? EmptyReferences, compiled.MacroCalls, out ImmutableArray<string> variables, out ImmutableArray<string> functions);
        if (compiled is { Plan: not null, Check.Expression: { } checkedExpression })
        {
            return new CelValidationResult(compiled.Errors, checkedExpression.ResultType, variables, functions, new CelCheckedExpr(checkedExpression));
        }

        return new CelValidationResult(compiled.Errors, null, variables, functions);
    }

    /// <summary>
    /// Validates an expression without creating a program and without an expected type of the result, as
    /// <see cref="Validate{T}(string, CelCompileOptions)"/> does with <see cref="CelValue"/>: any result type is accepted
    /// unless <see cref="CelCompileOptions.ExpectedType"/> sets one.
    /// </summary>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The result of the validation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The generic and the non-generic validation take the same optional options; they differ by the expected type, not by the arguments.")]
    public CelValidationResult Validate(string expression, CelCompileOptions? options = null) =>
        Validate<CelValue>(expression, options);

    /// <summary>
    /// Parses an expression with the syntax of this environment — its macros, the optional syntax of the <c>optional</c>
    /// extension, the <c>decimal</c> literal option — and returns the tree without type checking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parse is the first step of <see cref="Compile{T}(string, CelCompileOptions)"/>, with the same errors of the stage
    /// <see cref="CelErrorStage.Parse"/> and the same limits of length, nesting and number of errors.
    /// <see cref="CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/> of the result gives the same program and the same errors
    /// as <see cref="Compile{T}(string, CelCompileOptions)"/> of the text. The tree does not refer to the environment.
    /// </para>
    /// <para>
    /// Of the options only <see cref="CelCompileOptions.SourceLocation"/> is used: its source name becomes
    /// <see cref="CelParsedExpr.SourceName"/>, and parse errors report the position in the document, as at compilation.
    /// </para>
    /// </remarks>
    /// <param name="expression">The text of the expression.</param>
    /// <param name="options">The options, or <see langword="null"/> for none.</param>
    /// <returns>The parsed expression, or all parse errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    public CelParseResult Parse(string expression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelSourceLocation? start = options?.SourceLocation;
        CelParseResult parsed = Parse(expression, syntax: null);
        if (!parsed.IsSuccess)
        {
            return start is null ? parsed : new CelParseResult(InDocument(parsed.Errors, start));
        }

        return new CelParseResult(parsed.ParsedExpr.WithSource(start?.SourceName, Syntax));
    }

    /// <summary>
    /// Compiles a parsed expression — parsed from text, built by a <see cref="CelExprFactory"/> or loaded from protobuf — in this
    /// environment: type-checks and plans it, as <see cref="Compile{T}(string, CelCompileOptions)"/> does after parsing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree is checked first as an input: deeper than the nesting limit of parsing is <see cref="CelErrorCode.NestingTooDeep"/>;
    /// a <c>decimal</c> constant without the <c>decimal</c> literal option, and the optional syntax without the <c>optional</c>
    /// extension, are <see cref="CelErrorCode.SyntaxNotEnabled"/>, as for a text; these errors have the stage
    /// <see cref="CelErrorStage.Parse"/>. Then type checking resolves every name, function and type in the environment and
    /// reports all errors of the stage <see cref="CelErrorStage.Check"/> at the positions of the nodes. Only overloads that type
    /// checking found applicable are called at evaluation.
    /// </para>
    /// <para>
    /// Errors are returned, not thrown, and the time is linear in the number of nodes. With
    /// <see cref="CelCompileOptions.SourceLocation"/> errors also report the position in the document, as for a text.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type in which the result is returned, as for <see cref="Compile{T}(string, CelCompileOptions)"/>.</typeparam>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The program, or all errors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>, or the
    /// expected type of <paramref name="options"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    public CelCompileResult<T> CompileParsed<T>(CelParsedExpr expression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        CelType expectedType = ConversionRules.ExpectedType<T>(options?.ExpectedType, nameof(options), Declarations.Types);
        CelSourceLocation? start = options?.SourceLocation;
        return ProgramOf<T>(CompileTree(expression, expectedType, start), start);
    }

    /// <summary>
    /// Validates a parsed expression without creating a program: compiles it as
    /// <see cref="CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/> does and returns all errors, the CEL type of the result,
    /// the names it refers to and the checked expression.
    /// </summary>
    /// <typeparam name="T">The .NET type in which the expression would return its result.</typeparam>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The result of the validation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is not in the result column of the type mapping, or the expected type of
    /// <paramref name="options"/> cannot be converted to <typeparamref name="T"/>.
    /// </exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The generic and the non-generic validation take the same optional options; they differ by the expected type, not by the arguments.")]
    public CelValidationResult ValidateParsed<T>(CelParsedExpr expression, CelCompileOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        CelType expectedType = ConversionRules.ExpectedType<T>(options?.ExpectedType, nameof(options), Declarations.Types);
        return ValidationOf(CompileTree(expression, expectedType, options?.SourceLocation));
    }

    /// <summary>
    /// Validates a parsed expression without creating a program and without an expected type of the result, as
    /// <see cref="ValidateParsed{T}(CelParsedExpr, CelCompileOptions)"/> does with <see cref="Values.CelValue"/>.
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <param name="options">The compilation options, or <see langword="null"/> for none.</param>
    /// <returns>The result of the validation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> is <see langword="null"/>.</exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0026:Do not add multiple public overloads with optional parameters",
        Justification = "The generic and the non-generic validation take the same optional options; they differ by the expected type, not by the arguments.")]
    public CelValidationResult ValidateParsed(CelParsedExpr expression, CelCompileOptions? options = null) =>
        ValidateParsed<CelValue>(expression, options);

    /// <summary>
    /// The variables of the environment with their CEL types, including the fields of the input type, in the order of
    /// <see cref="string.CompareOrdinal(string, string)"/> by name.
    /// </summary>
    /// <remarks>The declarations are immutable; reading them does not change the environment.</remarks>
    public ImmutableArray<CelVariableDecl> Variables => Description.Variables;

    /// <summary>
    /// The functions of the environment with their overloads — of the standard library version, of the extension sets and of
    /// the application — that an expression calls by name, in the order of <see cref="string.CompareOrdinal(string, string)"/>
    /// by name. Operators of the language definition and internal functions, such as <c>_+_</c> and <c>@in</c>, are not
    /// included, as in <see cref="CelValidationResult.ReferencedFunctions"/>.
    /// </summary>
    /// <remarks>
    /// Each overload has its identifier, call style, parameter and result types and its
    /// <see cref="CelOverloadDecl.Description"/>: the description the application gave in <see cref="CelFunctionOptions"/>,
    /// or the one sentence that describes a function of the standard library or of an extension set.
    /// </remarks>
    public ImmutableArray<CelFunctionDecl> Functions => Description.Functions;

    /// <summary>
    /// The object types that the type sources of the environment describe, with their fields, in the order of
    /// <see cref="string.CompareOrdinal(string, string)"/> by CEL name.
    /// </summary>
    public ImmutableArray<CelObjectType> ObjectTypes => Description.ObjectTypes;

    /// <summary>
    /// The enumeration types that the type sources of the environment describe, in the order of
    /// <see cref="string.CompareOrdinal(string, string)"/> by CEL name.
    /// </summary>
    public ImmutableArray<CelEnumType> EnumTypes => Description.EnumTypes;

    // The description, made at the first read and once: an environment that nobody introspects pays nothing. Two threads can make
    // it at the same time; the first one published stays, and both are equal.
    private EnvironmentDescription Description =>
        _description ?? Interlocked.CompareExchange(ref _description, EnvironmentDescription.Create(Declarations), null) ?? _description!;

    /// <summary>
    /// The description of the input type declared with <see cref="CelEnvironmentBuilder.UseInput{TInput}"/>, or
    /// <see langword="null"/> when the environment has no input type.
    /// </summary>
    public CelObjectType? InputType => Declarations.InputType;

    /// <summary>
    /// The cost limit of evaluations that pass no limit of their own, set with
    /// <see cref="CelEnvironmentBuilder.SetDefaultCostLimit(ulong)"/>, in cost units; <see langword="null"/> when the
    /// environment has none, and then an evaluation without its own limit counts no cost.
    /// </summary>
    public ulong? DefaultCostLimit => _declarationSet.DefaultCostLimit;
}
