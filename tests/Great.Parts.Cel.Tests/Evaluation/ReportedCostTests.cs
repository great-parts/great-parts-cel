using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// What the examples of the limit and the reported cost (EnvironmentCostLimitTests) leave out: the cost of macros and of the functions of an
// extension set, the limit at the greatest value and the exact limit of an evaluation.
[Trait("Requirement", "R-8")]
public sealed class ReportedCostTests
{
    private static readonly CelEnvironment Environment = CelEnvironment.Create(builder => builder
        .AddVariable<long>("x")
        .AddExtension(CelExtensions.Strings(5)));

    private static readonly CelBindings Bindings = new CelBindings().Add("x", 3L);

    // The cost is the same at the greatest limit and at a small one, and it is what the evaluation needs: a limit of exactly the cost
    // lets it run, a limit one less refuses it, and the cost of the refused evaluation is below the limit, not the cost of the whole.
    [Theory]
    [InlineData("[1, 2, 3].map(i, i * x).size()")]
    [InlineData("[1, 2, 3].all(i, i > x)")]
    [InlineData("[1, 2, 3].filter(i, i < x).map(j, j + 1)")]
    [InlineData("'abc'.upperAscii().size() + x")]
    public static void Cost_OfMacrosAndSetFunctions_IsTheExactLimit(string text)
    {
        ulong cost = Run(text, ulong.MaxValue).Cost!.Value;

        Assert.True(cost > 0);
        Assert.Equal(cost, Run(text, 100_000).Cost);
        Assert.True(Run(text, cost).IsSuccess);

        CelEvalResult<CelValue> refused = Run(text, cost - 1);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(refused.Errors).Code);
        Assert.True(refused.Cost <= cost - 1);
    }

    // A limit of 0 is a limit: nothing may be charged, and the first read of a variable is refused with a cost of 0.
    [Fact]
    public void LimitOfZero_IsALimit()
    {
        CelEvalResult<CelValue> result = Run("x > 1", 0);

        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(result.Errors).Code);
        Assert.Equal(0UL, result.Cost);
        Assert.Equal(0UL, Run("true", 0).Cost);
    }

    private static CelEvalResult<CelValue> Run(string text, ulong limit) =>
        Environment.Compile<CelValue>(text).GetProgramOrThrow().Evaluate(Bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
}
