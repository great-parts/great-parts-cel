using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The cost limit of an evaluation. Each operation charges its cost before it runs, after its operands; a
// total equal to the limit is allowed, a total over it stops the evaluation before the operation with
// CostLimitExceeded at the node of the operation, as the single error. A read of a variable costs 1, a
// literal 0, int addition 1 (the cost page). The application function count(int) counts the calls of its
// implementation and of its cost function, which declares 5 units: a call of it costs 1 + 5.
public sealed class CostLimitTests
{
    private readonly Counters counters = new();
    private readonly CelEnvironment environment;
    private readonly CelBindings bindings = new CelBindings().Add("i", 41L).Add("j", 2L).Add("s", "abc");

    public CostLimitTests()
    {
        Counters calls = counters;
        environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("i")
            .AddVariable<long>("j")
            .AddVariable<string>("s")
            .AddFunction(new CelFunctionDecl(
                "count",
                CelOverloadDecl.Global("count_int", [CelType.Int], CelType.Int).WithCost(_ =>
                {
                    calls.CostFunction++;
                    return 5;
                })))
            .AddFunctionImplementation("count_int", arguments =>
            {
                calls.Implementation++;
                return arguments[0];
            })
            .AddFunction(new CelFunctionDecl(
                "huge",
                CelOverloadDecl.Global("huge_int", [CelType.Int], CelType.Int).WithCost(_ => ulong.MaxValue)))
            .AddFunctionImplementation("huge_int", arguments => arguments[0])
            .Build()
            .GetEnvironmentOrThrow();
    }

    // i + j: the reads of i and j, 1 each, then the addition, 1. The node that would go over the limit
    // is the place: the identifier i at 0, j at 4, the "+" at 2.
    [Theory]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "+R-9")]
    [InlineData(0UL, 0)]
    [InlineData(1UL, 4)]
    [InlineData(2UL, 2)]
    public void LimitBelowTheCost_IsCostLimitExceededAtTheOperationThatPassesIt(ulong limit, int offset)
    {
        CelError error = Assert.Single(Evaluate("i + j", limit).Errors);

        Assert.Equal((CelErrorCode.CostLimitExceeded, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal("evaluation cost limit of " + limit.ToString(CultureInfo.InvariantCulture) + " exceeded", error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    [Fact]
    [Trait("Requirement", "R-3")]
    public void LimitEqualToTheCost_IsAllowed()
    {
        Assert.True(Evaluate("i + j", 3).GetValueOrThrow().TryGetInt64(out long sum));
        Assert.Equal(43L, sum);
    }

    // count(i) costs 1 for i and 1 + 5 for the call. The cost function runs before the implementation, and
    // with 6 units the call does not run.
    [Fact]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "F-3")]
    public void CallOverTheLimit_DoesNotRunTheImplementation()
    {
        CelError error = Assert.Single(Evaluate("count(i)", 6).Errors);

        Assert.Equal((CelErrorCode.CostLimitExceeded, 5), (error.Code, error.Location.Offset));
        Assert.Equal((1, 0), (counters.CostFunction, counters.Implementation));
        Assert.True(Evaluate("count(i)", 7).IsSuccess);
        Assert.Equal((2, 1), (counters.CostFunction, counters.Implementation));
    }

    // Without a limit nothing is counted and no cost function is called.
    [Fact]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "F-3")]
    public void EvaluationWithoutALimit_DoesNotCallTheCostFunction()
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>("count(i)").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((0, 1), (counters.CostFunction, counters.Implementation));
    }

    // A total that does not fit the counter exceeds every limit, ulong.MaxValue included: 1 for i, then
    // 1 + ulong.MaxValue for the call.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void TotalThatOverflowsTheCounter_IsCostLimitExceeded()
    {
        CelError error = Assert.Single(Evaluate("huge(i)", ulong.MaxValue).Errors);

        Assert.Equal((CelErrorCode.CostLimitExceeded, 4), (error.Code, error.Location.Offset));
        Assert.Equal("evaluation cost limit of 18446744073709551615 exceeded", error.Message);
    }

    // An exceeded limit is not an error value: || does not absorb it. size(s) costs 2 for a string of 3
    // code units, and with the read of s the limit 2 is passed at the call. An operand that || never
    // evaluates costs nothing.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void ExceededLimit_IsNotAbsorbedByOr()
    {
        CelError error = Assert.Single(Evaluate("size(s) > 100 || true", 2).Errors);

        Assert.Equal((CelErrorCode.CostLimitExceeded, 4), (error.Code, error.Location.Offset));
        Assert.True(Evaluate("true || size(s) > 100", 0).IsSuccess);
    }

    private CelEvalResult<CelValue> Evaluate(string expression, ulong limit) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);

    private sealed class Counters
    {
        public int CostFunction { get; set; }

        public int Implementation { get; set; }
    }
}
