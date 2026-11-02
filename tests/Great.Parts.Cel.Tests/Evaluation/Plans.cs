using System;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Plans built through the internal entry points Parse and Check of an environment, or through the
// checker with an index of test overloads, and the helpers of the evaluation tests.
internal static class Plans
{
    // The variables n: int, flag: bool, a to d: bool, and any and s: dyn.
    internal static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("n", CelType.Int)
        .AddVariable("flag", CelType.Bool)
        .AddVariable("a", CelType.Bool)
        .AddVariable("b", CelType.Bool)
        .AddVariable("c", CelType.Bool)
        .AddVariable("d", CelType.Bool)
        .AddVariable("any", CelType.Dyn)
        .AddVariable("s", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    internal static Plan Checked(string expression)
    {
        CheckResult checkedResult = Environment.Check(Parse(expression));
        Assert.True(checkedResult.IsSuccess, expression);
        return Planner.Plan(checkedResult.Expression);
    }

    // Plans an expression checked against the standard library and the test overloads, with the
    // variables n: int and any: dyn.
    internal static Plan CheckedWith(string expression, params OverloadBinding[] overloads)
    {
        DeclarationIndex index = DeclarationIndex.Create(
            [.. StandardLibraryV1.Overloads, .. overloads],
            [new CelVariableDecl("n", CelType.Int), new CelVariableDecl("any", CelType.Dyn)]);
        CheckResult checkedResult = Checker.Check(Parse(expression), index);
        Assert.True(checkedResult.IsSuccess, expression);
        return Planner.Plan(checkedResult.Expression);
    }

    internal static Plan WithoutTypeCheck(string expression) =>
        Planner.PlanWithoutTypeCheck(Parse(expression), Environment.Declarations);

    // A global overload of one parameter for the tests.
    internal static OverloadBinding Overload(string function, string overloadId, CelType parameterType, OverloadImplementation implementation) =>
        new(function, new CelOverloadDecl(overloadId, false, [parameterType], CelType.Dyn), implementation, 1, null, "test");

    // An implementation that returns 42 whatever its arguments, so a test sees whether the node checked
    // their kinds before the call.
    internal static readonly OverloadImplementation Constant = (ReadOnlySpan<CelValue> _, in CallSite _) => CelValue.FromInt64(42);

    internal static CelError Error(CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error), value.Kind.ToString());
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Null(error.SourceLocation);
        return error;
    }

    private static ParsedExpression Parse(string expression)
    {
        ParseResult parsed = Environment.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        return parsed.Expression;
    }
}
