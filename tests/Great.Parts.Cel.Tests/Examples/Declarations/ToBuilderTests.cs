using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// An environment does not change. ToBuilder gives a new builder with its version, variables, functions,
// implementations and container; what is added there makes a new environment, and the first one and its
// programs give the same results as before.
public sealed class ToBuilderTests
{
    private readonly CelEnvironment first = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseContainer("acme")
        .AddVariable<long>("acme.x")
        .AddFunction<string, bool>("isInn", text => text.Length == 10)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("acme.x", 41L).Add("y", "text");

    [Theory]
    [Trait("Requirement", "+R-7")]
    [InlineData("x + 1")]
    [InlineData("isInn('0123456789') && !isInn('x')")]
    [InlineData("[x].map(e, e * 2)")]
    public void BuilderOfTheEnvironmentWithNothingAdded_BuildsAnEqualEnvironment(string expression)
    {
        CelEnvironment second = first.ToBuilder().Build().GetEnvironmentOrThrow();

        CelProgram<CelValue> before = first.Compile<CelValue>(expression).GetProgramOrThrow();
        CelProgram<CelValue> after = second.Compile<CelValue>(expression).GetProgramOrThrow();

        Assert.Equal(before.ResultType, after.ResultType);
        Assert.True(Bool(CelValue.CelEquals(Run(before), Run(after))));
    }

    [Fact]
    [Trait("Requirement", "+R-7")]
    public void DeclarationAddedToTheNewBuilder_DoesNotChangeTheFirstEnvironment()
    {
        CelProgram<long> program = first.Compile<long>("x + 1").GetProgramOrThrow();

        CelEnvironment second = first.ToBuilder().AddVariable<string>("y").Build().GetEnvironmentOrThrow();

        Assert.Equal("text", second.Compile<string>("y").GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(first.Compile<CelValue>("y").Errors).Code);
        Assert.Equal(42L, program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // A conflict with a declaration of the first environment is an error of the new build only.
    [Fact]
    [Trait("Requirement", "+R-7")]
    [Trait("Requirement", "+R-8")]
    public void ConflictAddedToTheNewBuilder_IsAnErrorOfTheNewBuildOnly()
    {
        CelBuildResult conflicting = first.ToBuilder().AddVariable("acme.x", CelType.String).UseContainer("other").Build();

        Assert.False(conflicting.IsSuccess);
        Assert.Equal(
            new[] { CelErrorCode.DuplicateVariable, CelErrorCode.ContainerConflict },
            conflicting.Errors.Select(error => error.Code));
        Assert.True(first.ToBuilder().Build().IsSuccess);
        Assert.Equal(CelType.Int, first.Compile<CelValue>("x").GetProgramOrThrow().ResultType);
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void StandardLibraryV1_HasVersion1()
    {
        Assert.Equal(1u, CelStandardLibrary.V1.Version);
    }

    private static bool Bool(CelValue value)
    {
        Assert.True(value.TryGetBoolean(out bool result));
        return result;
    }

    private CelValue Run(CelProgram<CelValue> program) =>
        program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
