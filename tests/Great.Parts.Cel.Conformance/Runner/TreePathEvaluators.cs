using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Runs a checked case through one way of getting a program: from the text, from the parsed expression, from the bytes of a parsed
/// expression, or from the bytes of a checked expression. All ways share the setup of <see cref="LibraryEvaluator"/>; the outcome
/// keeps what the ways must agree on: the stage, the code, the place of each error, the result type and the value. An error is
/// written as its code and place without the message and without the length of the fragment, which a loaded node does not keep.
/// </summary>
internal abstract class TreePathEvaluator : ICaseEvaluator
{
    /// <inheritdoc/>
    public CaseOutcome Evaluate(ConformanceCase testCase)
    {
        CaseStage stage = CaseStage.Environment;
        try
        {
            CelBuildResult built = LibraryEvaluator.Setup(testCase.Environment).Build();
            if (!built.IsSuccess)
            {
                return CaseOutcome.Failed(CaseStage.Environment, Facts(built.Errors));
            }

            stage = CaseStage.Check;
            CelCompileResult<CelValue> compiled = Compile(built.Environment, testCase.Expression);
            if (!compiled.IsSuccess)
            {
                CaseStage failed = compiled.Errors.Any(error => error.Stage == CelErrorStage.Parse) ? CaseStage.Parse : CaseStage.Check;
                return CaseOutcome.Failed(failed, Facts(compiled.Errors));
            }

            Type resultType = TypeConversion.ToExprType(compiled.Program.ResultType);
            stage = CaseStage.Evaluate;
            CelEvalResult<CelValue> result = compiled.Program.Evaluate(LibraryEvaluator.Bindings(testCase.Bindings), null, CancellationToken.None);
            return result.IsSuccess
                ? CaseOutcome.Evaluated(resultType, ValueConversion.ToExprValue(result.Value))
                : CaseOutcome.EvaluationFailed(resultType, Facts(result.Errors));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CaseOutcome.Threw(stage, exception);
        }
    }

    /// <summary>Gets the program of the expression by this way.</summary>
    /// <param name="environment">The environment of the case.</param>
    /// <param name="expression">The text of the expression.</param>
    /// <returns>The program or the errors.</returns>
    protected abstract CelCompileResult<CelValue> Compile(CelEnvironment environment, string expression);

    /// <summary>The program of a tree that the library parsed: the errors of the parse, or the compilation of the tree.</summary>
    /// <param name="environment">The environment of the case.</param>
    /// <param name="parsed">The result of the parse.</param>
    /// <param name="reload">Turns the parsed expression into the tree to compile, or <see langword="null"/> to compile it as it is.</param>
    protected static CelCompileResult<CelValue> CompileTree(CelEnvironment environment, CelParseResult parsed, Func<CelParsedExpr, CelParseResult>? reload)
    {
        if (!parsed.IsSuccess)
        {
            return new CelCompileResult<CelValue>(parsed.Errors);
        }

        if (reload is null)
        {
            return environment.CompileParsed<CelValue>(parsed.ParsedExpr);
        }

        CelParseResult loaded = reload(parsed.ParsedExpr);
        return loaded.IsSuccess
            ? environment.CompileParsed<CelValue>(loaded.ParsedExpr)
            : new CelCompileResult<CelValue>(loaded.Errors);
    }

    private static string[] Facts(ImmutableArray<CelError> errors) =>
    [
        .. errors.Select(error => string.Create(
            CultureInfo.InvariantCulture,
            $"{error.Code} at {error.Location.Offset} (line {error.Location.Line}, column {error.Location.Column})")),
    ];
}

/// <summary>The text path: <c>Compile&lt;CelValue&gt;</c> of the text; the outcome that the other paths are compared with.</summary>
internal sealed class TextEvaluator : TreePathEvaluator
{
    /// <inheritdoc/>
    protected override CelCompileResult<CelValue> Compile(CelEnvironment environment, string expression) =>
        environment.Compile<CelValue>(expression);
}

/// <summary>The path of the parsed expression: <c>Parse</c>, then <c>CompileParsed&lt;CelValue&gt;</c> of the tree.</summary>
internal sealed class ParsedEvaluator : TreePathEvaluator
{
    /// <inheritdoc/>
    protected override CelCompileResult<CelValue> Compile(CelEnvironment environment, string expression) =>
        CompileTree(environment, environment.Parse(expression), reload: null);
}

/// <summary>The path of the bytes of a parsed expression: <c>Parse</c>, <c>ToProtobuf</c>, <c>FromProtobuf</c> with the text, <c>CompileParsed</c>.</summary>
internal sealed class ProtobufEvaluator : TreePathEvaluator
{
    /// <inheritdoc/>
    protected override CelCompileResult<CelValue> Compile(CelEnvironment environment, string expression) =>
        CompileTree(environment, environment.Parse(expression), parsed => CelParsedExpr.FromProtobuf(parsed.ToProtobuf(), expression));
}

/// <summary>
/// The path of the bytes of a checked expression: <c>Validate&lt;CelValue&gt;</c>, <c>CheckedExpr.ToProtobuf</c>,
/// <c>FromCheckedExprProtobuf</c> with the text, <c>CompileParsed</c>. A text that does not validate gives the errors of the validation.
/// </summary>
internal sealed class CheckedExprEvaluator : TreePathEvaluator
{
    /// <inheritdoc/>
    protected override CelCompileResult<CelValue> Compile(CelEnvironment environment, string expression)
    {
        CelValidationResult validated = environment.Validate<CelValue>(expression);
        if (validated.CheckedExpr is not { } checkedExpr)
        {
            return new CelCompileResult<CelValue>(validated.Errors);
        }

        CelParseResult loaded = CelParsedExpr.FromCheckedExprProtobuf(checkedExpr.ToProtobuf(), expression);
        return loaded.IsSuccess
            ? environment.CompileParsed<CelValue>(loaded.ParsedExpr)
            : new CelCompileResult<CelValue>(loaded.Errors);
    }
}
