using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cel.Expr;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
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

    /// <inheritdoc/>
    public CaseOutcome Evaluate(ConformanceCase testCase)
    {
        CaseStage stage = CaseStage.Environment;
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

    // A checked case: Compile<T> parses, checks and plans the expression, and the program is evaluated.
    private static CaseOutcome Compiled(CelEnvironment environment, ConformanceCase testCase, ref CaseStage stage)
    {
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
    // cel-go; a check_only case is parsed and checked.
    private static CaseOutcome WithInternalEntryPoints(CelEnvironment environment, ConformanceCase testCase, ref CaseStage stage)
    {
        stage = CaseStage.Parse;
        ParseResult parsed = environment.Parse(testCase.Expression);
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

    // The setup of all cases and what the case declares: the container, the variables and the functions of type_env.
    private static CelEnvironmentBuilder Setup(CaseEnvironment declared)
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        if (declared.Container.Length > 0)
        {
            builder.UseContainer(declared.Container);
        }

        foreach (Decl declaration in declared.Declarations.Where(declaration => !ReservedNames.Contains(declaration.Name)))
        {
            if (declaration.DeclKindCase == Decl.DeclKindOneofCase.Function)
            {
                builder.AddFunction(new CelFunctionDecl(declaration.Name, [.. declaration.Function.Overloads.Select(Overload)]));
            }
            else
            {
                builder.AddVariable(declaration.Name, TypeConversion.ToCelType(declaration.Ident.Type));
            }
        }

        return builder;
    }

    private static CelOverloadDecl Overload(Decl.Types.FunctionDecl.Types.Overload overload)
    {
        CelType[] parameters = [.. overload.Params.Select(TypeConversion.ToCelType)];
        CelType result = TypeConversion.ToCelType(overload.ResultType);
        return overload.IsInstanceFunction
            ? CelOverloadDecl.Receiver(overload.OverloadId, parameters[0], parameters.AsSpan(1), result)
            : CelOverloadDecl.Global(overload.OverloadId, parameters, result);
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
