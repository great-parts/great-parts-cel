using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cel.Expr;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Runs a case on the library. All cases share one setup, <c>new CelEnvironmentBuilder(CelStandardLibrary.V1)</c>; each case adds
/// what it declares and builds its environment. A checked case is compiled to <c>CelProgram&lt;CelValue&gt;</c> and evaluated.
/// </summary>
/// <remarks>
/// A <c>disable_check</c> case is parsed and evaluated through the internal entry points <c>Parse</c> and
/// <c>EvaluateWithoutTypeCheck</c>, with the result type <c>dyn</c>; a <c>check_only</c> case is parsed and checked through
/// <c>Parse</c> and <c>Check</c>, and nothing is evaluated.
/// </remarks>
internal sealed class LibraryEvaluator : ICaseEvaluator
{
    // Keywords of CEL, never identifiers: the library rejects them as variable names, so the runner neither declares nor binds them.
    private static readonly string[] ReservedNames = ["true", "false", "null", "in"];

    // The syntax options of the environment without macros. Every environment has SyntaxOptions.Standard until extension sets
    // bring syntax.
    private static readonly SyntaxOptions WithoutMacros = SyntaxOptions.Standard with { Macros = MacroTable.Empty };

    /// <summary>The reason why a checked case with <c>disable_macros</c> fails: only the internal <c>Parse</c> takes syntax options.</summary>
    internal const string DisableMacrosOnCheckedPath = "disable_macros needs an internal compile entry point";

    /// <summary>
    /// The reason why a case that declares functions in <c>type_env</c> fails: <c>Build</c> requires their implementations, and the
    /// part that brings the first such file decides how the runner supplies them.
    /// </summary>
    internal const string FunctionDeclarationsInALaterPart = "function declarations need a later part";

    /// <inheritdoc/>
    public CaseOutcome Evaluate(ConformanceCase testCase)
    {
        CaseStage stage = CaseStage.Environment;
        if (testCase.Environment.Declarations.Any(declaration => declaration.DeclKindCase == Decl.DeclKindOneofCase.Function))
        {
            return CaseOutcome.Failed(CaseStage.Environment, FunctionDeclarationsInALaterPart);
        }

        try
        {
            CelBuildResult built = Setup(testCase.Environment).Build();
            if (!built.IsSuccess)
            {
                return CaseOutcome.Failed(CaseStage.Environment, Messages(built.Errors));
            }

            return testCase.Check == CheckMode.Check
                ? Compiled(built.Environment, testCase, ref stage)
                : WithInternalEntryPoints(built.Environment, testCase, ref stage);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // An exception of the library is part of the outcome; running out of memory is a failure of the process and ends the run.
            return CaseOutcome.Threw(stage, exception);
        }
    }

    // A checked case: Compile<T> parses, checks and plans the expression, and the program is evaluated. Compile<T> takes no syntax
    // options, so a checked case with disable_macros fails.
    private static CaseOutcome Compiled(CelEnvironment environment, ConformanceCase testCase, ref CaseStage stage)
    {
        if (testCase.Environment.DisableMacros)
        {
            return CaseOutcome.Failed(CaseStage.Parse, DisableMacrosOnCheckedPath);
        }

        stage = CaseStage.Check;
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(testCase.Expression);
        if (!compiled.IsSuccess)
        {
            CaseStage failed = compiled.Errors.Any(error => error.Stage == CelErrorStage.Parse) ? CaseStage.Parse : CaseStage.Check;
            return CaseOutcome.Failed(failed, Messages(compiled.Errors));
        }

        Type resultType = TypeConversion.ToExprType(compiled.Program.ResultType);
        stage = CaseStage.Evaluate;
        CelEvalResult<CelValue> result = compiled.Program.Evaluate(Bindings(testCase.Bindings), null, CancellationToken.None);
        return Evaluated(resultType, result);
    }

    // A disable_check case is parsed and evaluated without a check, with the result type dyn as for an unchecked expression in
    // cel-go; a check_only case is parsed and checked. A case with disable_macros is parsed without macros, as the cel-go runner
    // clears them.
    private static CaseOutcome WithInternalEntryPoints(CelEnvironment environment, ConformanceCase testCase, ref CaseStage stage)
    {
        stage = CaseStage.Parse;
        ParseResult parsed = environment.Parse(testCase.Expression, testCase.Environment.DisableMacros ? WithoutMacros : null);
        if (!parsed.IsSuccess)
        {
            return CaseOutcome.Failed(CaseStage.Parse, Messages(parsed.Errors));
        }

        if (testCase.Check == CheckMode.CheckOnly)
        {
            stage = CaseStage.Check;
            CheckResult checkedResult = environment.Check(parsed.Expression);
            return checkedResult.IsSuccess
                ? CaseOutcome.Checked(TypeConversion.ToExprType(checkedResult.Expression.ResultType))
                : CaseOutcome.Failed(CaseStage.Check, Messages(checkedResult.Errors));
        }

        stage = CaseStage.Evaluate;
        return Evaluated(TypeConversion.Dyn, environment.EvaluateWithoutTypeCheck(parsed.Expression, Bindings(testCase.Bindings)));
    }

    private static CaseOutcome Evaluated(Type resultType, CelEvalResult<CelValue> result) =>
        result.IsSuccess
            ? CaseOutcome.Evaluated(resultType, ValueConversion.ToExprValue(result.Value))
            : CaseOutcome.EvaluationFailed(resultType, Messages(result.Errors));

    // The setup of all cases and what the case declares: the container and the variables of type_env.
    private static CelEnvironmentBuilder Setup(CaseEnvironment declared)
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        if (declared.Container.Length > 0)
        {
            builder.UseContainer(declared.Container);
        }

        foreach (Decl declaration in declared.Declarations.Where(declaration => !ReservedNames.Contains(declaration.Name)))
        {
            builder.AddVariable(declaration.Name, TypeConversion.ToCelType(declaration.Ident.Type));
        }

        return builder;
    }

    // A binding of the kind value only: the rule fails a case with other bindings.
    private static CelBindings Bindings(IReadOnlyDictionary<string, ExprValue> bindings)
    {
        CelBindings values = new();
        foreach ((string name, ExprValue binding) in bindings)
        {
            if (binding.KindCase == ExprValue.KindOneofCase.Value && !ReservedNames.Contains(name))
            {
                values.Add(name, ValueConversion.ToCelValue(binding.Value));
            }
        }

        return values;
    }

    private static string[] Messages(ImmutableArray<CelError> errors) => [.. errors.Select(error => error.Message)];
}
