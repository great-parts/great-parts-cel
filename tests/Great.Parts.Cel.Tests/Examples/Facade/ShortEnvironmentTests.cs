using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The short environment: the builder without an argument is the standard library version 1; CelEnvironment.Create builds in
// one call and throws CelException for build errors; an exception of the configuring delegate reaches the caller unchanged.
[Trait("Requirement", "F-3")]
public sealed class ShortEnvironmentTests
{
    [Fact]
    public void BuilderWithoutArgument_IsTheStandardLibraryVersion1()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddVariable<long>("x").Build().GetEnvironmentOrThrow();

        Assert.Equal(43L, Evaluate(environment, "x + 1", new CelBindings().Add("x", 42L)));
        Assert.Equal(3L, Evaluate(environment, "size('abc')", new CelBindings()));
    }

    [Fact]
    public void Create_BuildsInOneCall()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddVariable<long>("x"));
        CelEnvironment versioned = CelEnvironment.Create(CelStandardLibrary.V1, builder => builder.AddVariable<long>("x"));

        Assert.Equal(2L, Evaluate(environment, "x * 2", new CelBindings().Add("x", 1L)));
        Assert.Equal(2L, Evaluate(versioned, "x * 2", new CelBindings().Add("x", 1L)));
    }

    [Fact]
    public void Create_WithBuildErrors_ThrowsCelExceptionWithAllOfThem()
    {
        CelException exception = Assert.Throws<CelException>(() => CelEnvironment.Create(builder => builder
            .AddVariable<long>("x").AddVariable<string>("x")
            .AddVariable<long>("y").AddVariable<bool>("y")));

        Assert.Equal(2, exception.Errors.Length);
        Assert.All(exception.Errors, error => Assert.Equal(CelErrorCode.DuplicateVariable, error.Code));
    }

    [Fact]
    public void Create_LetsTheExceptionOfTheDelegatePass()
    {
        var thrown = new FormatException("bad configuration");

        Assert.Same(thrown, Assert.Throws<FormatException>(() => CelEnvironment.Create(_ => throw thrown)));
        Assert.Throws<ArgumentNullException>("configure", () => CelEnvironment.Create(null!));
    }

    private static long Evaluate(CelEnvironment environment, string text, CelBindings bindings) =>
        environment.Compile<long>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
