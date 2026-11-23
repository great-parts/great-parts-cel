using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The option EnableCrossTypeNumericComparisons: the ordering operators between int, uint and double whose static types are known. By the
// language definition type checking accepts them only when an operand is dyn, so each row without the option is the type checking error
// NoMatchingOverload; with the option the same text checks and compares the numeric values exactly. Equality does not depend on the option,
// and ToBuilder keeps it.
[Trait("Requirement", "F-29")]
public sealed class CrossTypeNumericComparisonTests
{
    private static readonly CelEnvironment Plain = Create(false);

    private static readonly CelEnvironment WithOption = Create(true);

    [Theory]
    [InlineData("1 < 1.5")]
    [InlineData("2u >= 1")]
    [InlineData("-1 < 1u")]
    [InlineData("x > 0")]
    public static void WithoutTheOption_AnOrderingAcrossNumericTypes_IsNoMatchingOverloadAtCheck(string expression)
    {
        CelCompileResult<bool> result = Plain.Compile<bool>(expression);

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("1 < 1.5", true)]
    [InlineData("2u >= 1", true)]
    [InlineData("-1 < 1u", true)]
    [InlineData("1.0 / 0.0 > 9223372036854775807", true)]
    [InlineData("(0.0 / 0.0) < 1", false)]
    [InlineData("x > 0", true)]
    public static void WithTheOption_TheOrderingChecksAndCompares(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate(WithOption, expression));
    }

    [Theory]
    [InlineData("dyn(1) < 1.5", true)]
    [InlineData("dyn(1) == 1.0", true)]
    public static void WithoutTheOption_DynIsAsBefore(string expression, bool expected)
    {
        Assert.Equal(expected, Evaluate(Plain, expression));
    }

    // The option is for the ordering only: equality of an int and a double of known static types is the type checking error with and
    // without it, and with a dyn operand it is true with and without it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void EqualityAcrossNumericTypes_DoesNotDependOnTheOption(bool crossType)
    {
        CelEnvironment environment = crossType ? WithOption : Plain;

        CelCompileResult<bool> result = environment.Compile<bool>("1 == 1.0");

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(result.Errors).Code);
        Assert.True(Evaluate(environment, "dyn(1) == 1.0"));
    }

    [Fact]
    public static void ToBuilder_KeepsTheOption()
    {
        CelEnvironment copy = WithOption.ToBuilder().Build().GetEnvironmentOrThrow();

        Assert.True(Evaluate(copy, "1 < 1.5"));
        Assert.False(Plain.ToBuilder().Build().GetEnvironmentOrThrow().Compile<bool>("1 < 1.5").IsSuccess);
    }

    private static CelEnvironment Create(bool crossType)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("x", CelType.Double);
        if (crossType)
        {
            builder.EnableCrossTypeNumericComparisons();
        }

        return builder.Build().GetEnvironmentOrThrow();
    }

    private static bool Evaluate(CelEnvironment environment, string expression) =>
        environment.Compile<bool>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("x", 0.5), cancellationToken: CancellationToken.None).GetValueOrThrow();
}
