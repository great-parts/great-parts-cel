using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// ToBuilder gives a builder with the version, variables, functions, implementations and container of an
// environment: built with nothing added it gives an equal environment, and what is added changes only the new one.
public sealed class ToBuilderTests
{
    private static readonly CelEnvironment Original = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseContainer("shop")
        .AddVariable("price", CelType.Int)
        .AddVariable("rows", CelType.ListOf(CelType.TypeParameter("T")))
        .AddFunction<long, long>("twice", value => value * 2)
        .AddFunction(new CelFunctionDecl("halve", CelOverloadDecl.Global("halve_int", [CelType.Int], CelType.Int)))
        .AddFunctionImplementation<long, long>("halve_int", value => value / 2)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void BuilderWithNothingAdded_BuildsAnEqualEnvironment()
    {
        CelEnvironment copy = Original.ToBuilder().Build().GetEnvironmentOrThrow();

        Assert.NotSame(Original, copy);
        Assert.Equal(Original.VariableDeclarations, copy.VariableDeclarations);
        Assert.Equal(Original.Declarations.Container, copy.Declarations.Container);
        foreach ((string function, int count) in new[] { ("twice", 1), ("halve", 1), ("_+_", 2), ("_/_", 2) })
        {
            Assert.Equal(Original.Declarations.GetOverloads(function, count).Select(Id), copy.Declarations.GetOverloads(function, count).Select(Id));
        }

        Assert.True(copy.Declarations.TryGetVariable("rows", out CelVariableDecl? rows));
        Assert.Equal(CelType.ListOf(CelType.Dyn), rows.Type);
        foreach (CelEnvironment environment in new[] { Original, copy })
        {
            Assert.Equal(12L, Evaluate(environment, "twice(price) / 2 + halve(price)"));
        }
    }

    [Fact]
    public void DeclarationsAddedToTheNewBuilder_DoNotChangeTheOriginal()
    {
        CelProgram<long> before = Original.Compile<long>("twice(price)").GetProgramOrThrow();

        CelEnvironment extended = Original.ToBuilder()
            .AddVariable<long>("discount")
            .AddFunction<long, long>("negate", value => -value)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(-10L, Evaluate(extended, "negate(twice(price)) + discount + discount"));
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(Original.Compile<long>("negate(discount)").Errors).Code);
        Assert.Equal(2, Original.VariableDeclarations.Length);
        Assert.Equal(16L, before.Evaluate(Bindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The builder holds the declarations of the original, so a conflict with them is an error of the new build.
    [Fact]
    public void ConflictWithTheOriginal_IsAnErrorOfTheNewBuildOnly()
    {
        CelBuildResult conflicting = Original.ToBuilder()
            .UseContainer("store")
            .AddVariable("rows", CelType.ListOf(CelType.Int))
            .AddFunction<long, long>("twice", value => value + value)
            .Build();

        Assert.Equal(
            [CelErrorCode.DuplicateVariable, CelErrorCode.DuplicateImplementation, CelErrorCode.ContainerConflict],
            conflicting.Errors.Select(error => error.Code));
        Assert.True(Original.ToBuilder().Build().IsSuccess);
    }

    private static string Id(OverloadBinding overload) => overload.Declaration.OverloadId;

    private static CelBindings Bindings() => new CelBindings().Add("price", 8L).Add("discount", 3L);

    private static long Evaluate(CelEnvironment environment, string expression) =>
        environment.Compile<long>(expression).GetProgramOrThrow().Evaluate(Bindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}
