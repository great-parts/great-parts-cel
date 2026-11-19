using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The cost limit of an environment and the reported cost. The limit of the call replaces the limit of the environment; without
// both no cost is counted and Cost is null. With a limit, Cost is the sum charged by the evaluation: for CostLimitExceeded the
// sum before the refused operation. "x + 1" costs 2: the read of x and the addition; a literal costs nothing.
[Trait("Requirement", "R-8")]
public sealed class EnvironmentCostLimitTests
{
    private static readonly CelBindings Bindings = new CelBindings().Add("x", 1L);

    [Fact]
    public void WithoutALimit_CostIsNull()
    {
        CelEnvironment environment = Build(null);

        Assert.Null(environment.DefaultCostLimit);
        Assert.Null(Run(environment, "x + 1", null).Cost);
    }

    [Fact]
    public void LimitOfTheEnvironment_AppliesAndCostIsReported()
    {
        CelEnvironment environment = Build(10);

        Assert.Equal(10UL, environment.DefaultCostLimit);
        CelEvalResult<long> result = Run(environment, "x + 1", null);
        Assert.Equal(2L, result.Value);
        Assert.Equal(2UL, result.Cost);
    }

    [Fact]
    public void LimitOfTheCall_ReplacesTheLimitOfTheEnvironment()
    {
        CelEnvironment environment = Build(1);

        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Run(environment, "x + 1", null).Errors).Code);
        CelEvalResult<long> result = Run(environment, "x + 1", 100);
        Assert.True(result.IsSuccess);
        Assert.Equal(2UL, result.Cost);
    }

    // The read of x is charged (1), the addition is refused: Cost is 1, not more than the limit.
    [Fact]
    public void CostLimitExceeded_ReportsTheSumBeforeTheRefusedOperation()
    {
        CelEvalResult<long> result = Run(Build(null), "x + 1", 1);

        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(result.Errors).Code);
        Assert.Equal(1UL, result.Cost);
    }

    // Another evaluation error: the sum up to and including the operation that failed.
    [Fact]
    public void OtherError_ReportsTheSumIncludingTheFailedOperation()
    {
        CelEvalResult<long> result = Run(Build(null), "x / 0", 100);

        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(result.Errors).Code);
        Assert.Equal(2UL, result.Cost);
    }

    // The one-step call reports the cost of the evaluation only; a compilation error has no cost.
    [Fact]
    public void OneStep_ReportsTheCostOfTheEvaluation()
    {
        CelEnvironment environment = Build(100);

        Assert.Equal(2UL, environment.Evaluate<long>("x + 1", Bindings, cancellationToken: CancellationToken.None).Cost);
        Assert.Null(environment.Evaluate<long>("x +", Bindings, cancellationToken: CancellationToken.None).Cost);
    }

    [Fact]
    public void ToBuilder_KeepsTheLimit()
    {
        Assert.Equal(7UL, Build(7).ToBuilder().Build().GetEnvironmentOrThrow().DefaultCostLimit);
    }

    // The same program, data and limit give the same cost whatever the limit, when the limit is not reached.
    [Fact]
    public void Cost_DoesNotDependOnTheLimit()
    {
        CelEnvironment environment = Build(null);
        const string Text = "[1, 2, 3].map(i, i * x).filter(j, j > 1).size()";

        Assert.Equal(Run(environment, Text, 1_000).Cost, Run(environment, Text, ulong.MaxValue).Cost);
    }

    private static CelEnvironment Build(ulong? limit)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder().AddVariable<long>("x");
        if (limit is { } value)
        {
            builder.SetDefaultCostLimit(value);
        }

        return builder.Build().GetEnvironmentOrThrow();
    }

    private static CelEvalResult<long> Run(CelEnvironment environment, string text, ulong? limit) =>
        environment.Compile<long>(text).GetProgramOrThrow().Evaluate(Bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
}
