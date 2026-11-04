using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The cost limit: a total equal to the limit is allowed, and the operation whose charge would exceed it
// does not run; the evaluation ends with CostLimitExceeded at that node, which no operator absorbs.
// Without a limit nothing is counted.
public sealed class CostLimitTests
{
    private static readonly IReadOnlyDictionary<string, CelValue> Five = new Dictionary<string, CelValue> { ["n"] = CelValue.FromInt64(5) };

    // n + n costs a read of 1 for each identifier and 1 for the call: 3.
    [Fact]
    public static void TotalEqualToTheLimit_IsAllowed()
    {
        Plan plan = Plans.Checked("n + n");
        Frame frame = plan.CreateFrame(Five, 3, CancellationToken.None);

        Assert.Equal((CelValue.FromInt64(10), 3UL), (plan.Run(frame), frame.ChargedUnits));
    }

    [Fact]
    public static void TotalAboveTheLimit_IsCostLimitExceededAtTheNodeOfTheOperation()
    {
        CelError error = Plans.Error(Plans.Checked("n + n").Evaluate(Five, 2, CancellationToken.None));

        Assert.Equal(
            (CelErrorCode.CostLimitExceeded, 2, "evaluation cost limit of 2 exceeded"),
            (error.Code, error.Location.Offset, error.Message));
    }

    // The call of 1 unit exceeds the limit 0, so the implementation does not run.
    [Fact]
    public static void ExceededCharge_StopsBeforeTheOperation()
    {
        int calls = 0;
        CelProgram<CelValue> program = Program(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("tick", () => ++calls > 0),
            "tick()");

        CelError error = Plans.Error(program.Plan.Evaluate(new Dictionary<string, CelValue>(), 0, CancellationToken.None));

        Assert.Equal((CelErrorCode.CostLimitExceeded, 0), (error.Code, calls));
    }

    // An exceeded limit is not an error value: a true operand after it does not absorb it.
    [Fact]
    public static void ExceededLimit_IsNotAbsorbedByOr()
    {
        CelProgram<CelValue> program = Program(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("tick", () => true),
            "tick() || true");

        CelError error = Plans.Error(program.Plan.Evaluate(new Dictionary<string, CelValue>(), 0, CancellationToken.None));

        Assert.Equal((CelErrorCode.CostLimitExceeded, 4), (error.Code, error.Location.Offset));
    }

    // 1 for the call plus the value of the cost function does not fit the counter.
    [Fact]
    public static void CostThatDoesNotFitTheCounter_IsCostLimitExceeded()
    {
        CelProgram<CelValue> program = Program(
            new CelEnvironmentBuilder(CelStandardLibrary.V1)
                .AddFunction(new CelFunctionDecl("huge", CelOverloadDecl.Global("huge", [], CelType.Int).WithCost(_ => ulong.MaxValue)))
                .AddFunctionImplementation("huge", () => 1L),
            "huge()");

        CelError error = Plans.Error(program.Plan.Evaluate(new Dictionary<string, CelValue>(), ulong.MaxValue, CancellationToken.None));

        Assert.Equal(CelErrorCode.CostLimitExceeded, error.Code);
    }

    // The application function costs 1 plus its cost function, called with the arguments.
    [Fact]
    public static void ApplicationFunction_CostsOnePlusItsCostFunction()
    {
        CelProgram<CelValue> program = Program(
            new CelEnvironmentBuilder(CelStandardLibrary.V1)
                .AddFunction(new CelFunctionDecl("weigh", CelOverloadDecl.Global("weigh_int", [CelType.Int], CelType.Int)
                    .WithCost(arguments => arguments[0].TryGetInt64(out long units) ? (ulong)units : 0)))
                .AddFunctionImplementation("weigh_int", (long value) => value),
            "weigh(7)");
        Frame frame = program.Plan.CreateFrame(new Dictionary<string, CelValue>(), 100, CancellationToken.None);

        Assert.Equal((CelValue.FromInt64(7), 8UL), (program.Plan.Run(frame), frame.ChargedUnits));
    }

    // Without a limit the cost function is not called.
    [Fact]
    public static void WithoutALimit_TheCostFunctionIsNotCalled()
    {
        int costCalls = 0;
        CelProgram<CelValue> program = Program(
            new CelEnvironmentBuilder(CelStandardLibrary.V1)
                .AddFunction(new CelFunctionDecl("weigh", CelOverloadDecl.Global("weigh_int", [CelType.Int], CelType.Int)
                    .WithCost(_ => (ulong)++costCalls)))
                .AddFunctionImplementation("weigh_int", (long value) => value),
            "weigh(7)");
        Frame frame = program.Plan.CreateFrame(new Dictionary<string, CelValue>(), null, CancellationToken.None);

        Assert.Equal((CelValue.FromInt64(7), 0UL, 0), (program.Plan.Run(frame), frame.ChargedUnits, costCalls));
    }

    // The first read of a variable checks the bound value at depth and charges n units before it descends
    // into n entries; a parameter dyn is not descended into.
    [Theory]
    [InlineData("m", 3UL)]
    [InlineData("xs", 1UL)]
    public static void CheckOfTheBinding_ChargesTheDescent(string expression, ulong cost)
    {
        Plan plan = MacroPlans.Checked(expression);
        var bindings = new Dictionary<string, CelValue>
        {
            ["m"] = CelValue.FromMap(CelMap.Create([new(CelValue.FromString("p"), CelValue.FromInt64(1)), new(CelValue.FromString("q"), CelValue.FromInt64(2))])),
            ["xs"] = MacroPlans.Integers(6),
        };
        Frame frame = plan.CreateFrame(bindings, 1000, CancellationToken.None);

        _ = plan.Run(frame);

        Assert.Equal(cost, frame.ChargedUnits);
    }

    // The costs of the standard macros over a range of 4 elements: the read of the range 1, a step 1, the
    // initializer [] of map and filter 1 and each element they add 1, the expressions of the author by
    // their rows, and nothing for what the expansion adds.
    [Theory]
    [InlineData("xs.all(v, true)", 5UL)]
    [InlineData("xs.exists(v, false)", 5UL)]
    [InlineData("xs.exists_one(v, true)", 5UL)]
    [InlineData("xs.map(v, v)", 14UL)]
    [InlineData("xs.map(v, true, 0)", 10UL)]
    [InlineData("xs.filter(v, false)", 6UL)]
    [InlineData("xs.filter(v, true)", 10UL)]
    [InlineData("xs.all(v, v == v)", 17UL)]
    public static void StandardMacro_CostsTheRowOfItsTable(string expression, ulong cost)
    {
        Plan plan = MacroPlans.Checked(expression);
        Frame frame = plan.CreateFrame(new Dictionary<string, CelValue> { ["xs"] = MacroPlans.Integers(4) }, 1000, CancellationToken.None);

        Assert.NotEqual(CelValueKind.Error, plan.Run(frame).Kind);
        Assert.Equal(cost, frame.ChargedUnits);
    }

    // A type identifier costs 1 as a name read, a literal nothing, a list of n elements 1 + n, and a map
    // of n entries 1 + 2n with ⌈L/10⌉ for a string key of length L.
    [Theory]
    [InlineData("int", 1UL)]
    [InlineData("7", 0UL)]
    [InlineData("[1, 2]", 3UL)]
    [InlineData("{'abcdefghijk': 1}", 5UL)]
    public static void Literal_AndTypeIdentifier_CostTheirRows(string expression, ulong cost)
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        Plan plan = environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(new Dictionary<string, CelValue>(), 1000, CancellationToken.None);

        _ = plan.Run(frame);

        Assert.Equal(cost, frame.ChargedUnits);
    }

    private static CelProgram<CelValue> Program(CelEnvironmentBuilder builder, string expression) =>
        builder.Build().GetEnvironmentOrThrow().Compile<CelValue>(expression).GetProgramOrThrow();
}
