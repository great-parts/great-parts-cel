using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The nodes of the evaluator: selection and presence, &&, ?:, @not_strictly_false, comprehensions with their
// accumulator, and the checks of arguments that the planner marks.
public sealed class NodeTests
{
    // m holds {'p': 1}, d holds 3 and xs [1, 2, 3, 4].
    private static readonly IReadOnlyDictionary<string, CelValue> Bound = new Dictionary<string, CelValue>
    {
        ["m"] = CelValue.FromMap(CelMap.Create([new(CelValue.FromString("p"), CelValue.FromInt64(1))])),
        ["d"] = CelValue.FromInt64(3),
        ["xs"] = MacroPlans.Integers(4),
        ["n"] = CelValue.FromInt64(2),
    };

    [Theory]
    [InlineData("m.p", 1L)]
    [InlineData("has(m.p) ? 10 : 20", 10L)]
    [InlineData("has(m.q) ? 10 : 20", 20L)]
    [InlineData("xs.exists_one(v, v == 2) ? 1 : 0", 1L)]
    [InlineData("xs.all(v, true) && xs.exists(v, v == 4) ? 1 : 0", 1L)]
    [InlineData("xs.all(v, v == 1) ? 1 : 0", 0L)]
    public static void Expression_HasItsValue(string expression, long expected)
    {
        Assert.Equal(CelValue.FromInt64(expected), MacroPlans.Checked(expression).EvaluateOn(Bound));
    }

    [Theory]
    [InlineData("xs.map(v, v + n)", "3 4 5 6")]
    [InlineData("xs.filter(v, v == 1 || v == 3)", "1 3")]
    [InlineData("xs.map(v, v == 2 || v == 4, v + 10)", "12 14")]
    [InlineData("xs.filter(v, xs.exists(w, w == v + 1))", "1 2 3")]
    [InlineData("m.map(k, 7)", "7")]
    public static void MapAndFilter_GiveTheirLists(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        Assert.True(MacroPlans.Checked(expression).EvaluateOn(Bound).TryGetList(out CelList? list), expression);

        Assert.Equal(expected, string.Join(' ', list.Select(item => item.TryGetInt64(out long value) ? value : -1)));
    }

    // "<expression> → <code> <offset> <message>"
    [Theory]
    [InlineData("m.q", "3001 1 no such field 'q' in map")]
    [InlineData("d.f", "3025 1 value of type 'int' does not support field selection of 'f'")]
    [InlineData("has(d.f)", "3025 3 value of type 'int' does not support field selection of 'f'")]
    [InlineData("d ? 1 : 2", "3000 2 no such overload: _?_:_(int)")]
    [InlineData("d && true", "3000 2 no such overload: _&&_(int, bool)")]
    [InlineData("d.all(v, true)", "3026 0 value of type 'int' cannot be the range of a comprehension (must be list or map)")]
    [InlineData("xs.map(v, v / 0)", "3004 12 division by zero")]
    public static void Error_HasItsCodePositionAndMessage(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        CelError error = Plans.Error(MacroPlans.Checked(expression).EvaluateOn(Bound));

        Assert.Equal(expected, $"{(int)error.Code} {error.Location.Offset} {error.Message}");
    }

    // && decides at the first false and absorbs the errors of the other operands; || at the first true.
    [Theory]
    [InlineData("m.q == 1 && false", false)]
    [InlineData("false && m.q == 1", false)]
    [InlineData("m.q == 1 || true", true)]
    [InlineData("true && true && true", true)]
    public static void LogicalChain_IsDecidedByItsDecisiveOperand(string expression, bool expected)
    {
        Assert.Equal(CelValue.FromBoolean(expected), MacroPlans.Checked(expression).EvaluateOn(Bound));
    }

    // all absorbs an error of the predicate when another element gives false.
    [Fact]
    public static void All_AbsorbsAnErrorThroughFalse()
    {
        Assert.Equal(CelValue.FromBoolean(false), MacroPlans.Checked("xs.all(v, 4 / (v + -1) == 2)").EvaluateOn(Bound));
    }

    // A library call whose static argument types cover the parameters is direct; dyn is checked.
    [Theory]
    [InlineData("n + 1", false)]
    [InlineData("d + 1", true)]
    public static void LibraryCall_IsCheckedOnlyWhenTheStaticTypeDoesNotCoverTheParameter(string expression, bool checks)
    {
        Assert.Equal(checks, Assert.IsType<CallPlanNode>(MacroPlans.Checked(expression).Root).Checks);
    }

    // An application function gets only values that fit its declared parameter types at any depth.
    [Fact]
    public static void ApplicationArgument_IsCheckedAtDepth()
    {
        int calls = 0;
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("total", CelOverloadDecl.Global("total_list", [CelType.ListOf(CelType.Int)], CelType.Int)))
            .AddFunctionImplementation("total_list", _ => CelValue.FromInt64(++calls))
            .AddVariable("items", CelType.ListOf(CelType.Dyn))
            .Build()
            .GetEnvironmentOrThrow();
        Plan plan = environment.Compile<CelValue>("total(items)").GetProgramOrThrow().Plan;
        var bindings = new Dictionary<string, CelValue>
        {
            ["items"] = CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromBoolean(false))),
        };

        CelError error = Plans.Error(plan.EvaluateOn(bindings));

        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, "no such overload: total(list)", 0), (error.Code, error.Message, calls));
    }

    // Without type checking a name resolves in the bindings by the candidates of the container, the
    // longest first, and a selection chain may be one qualified name.
    [Theory]
    [InlineData("a.b", 5L)]
    [InlineData("v", 6L)]
    [InlineData("m.p", 1L)]
    public static void WithoutTypeCheck_NameResolvesInTheBindings(string expression, long expected)
    {
        var bindings = new Dictionary<string, CelValue>(Bound)
        {
            ["a.b"] = CelValue.FromInt64(5),
            ["v"] = CelValue.FromInt64(6),
        };

        Assert.Equal(CelValue.FromInt64(expected), MacroPlans.WithoutTypeCheck(expression).EvaluateOn(bindings));
    }

    // Without type checking a call with a target passes the target first; comprehensions run the same.
    [Fact]
    public static void WithoutTypeCheck_MacrosAndCallsRun()
    {
        Assert.Equal(CelValue.FromBoolean(true), MacroPlans.WithoutTypeCheck("xs.exists(v, v == n)").EvaluateOn(Bound));
    }
}
