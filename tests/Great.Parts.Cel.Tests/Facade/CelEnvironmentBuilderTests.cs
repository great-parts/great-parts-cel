using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

public sealed class CelEnvironmentBuilderTests
{
    [Fact]
    public void StandardLibraryV1_IsOneInstance()
    {
        Assert.Same(CelStandardLibrary.V1, CelStandardLibrary.V1);
    }

    [Fact]
    public void Constructor_NullStandardLibrary_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CelEnvironmentBuilder(null!));

        Assert.Equal("standardLibrary", exception.ParamName);
    }

    [Fact]
    public void NewBuilder_HasNoDeclarations()
    {
        Assert.Empty(new CelEnvironmentBuilder(CelStandardLibrary.V1).VariableDeclarations);
    }

    [Fact]
    public void Build_GivesAnEnvironmentWithTheDeclaredVariables()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("x", CelType.Int)
            .AddVariable("request.auth", CelType.Dyn);

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        (string, CelType)[] expected = [("x", CelType.Int), ("request.auth", CelType.Dyn)];
        Assert.Equal(expected, Pairs(builder.VariableDeclarations));
        Assert.Equal(expected, Pairs(environment.VariableDeclarations));
    }

    [Fact]
    public void AddVariable_ReturnsTheSameBuilder()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

        Assert.Same(builder, builder.AddVariable("x", CelType.Bool));
    }

    [Fact]
    public void AddVariable_NullName_ThrowsAndAddsNothing()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.AddVariable(null!, CelType.Int));

        Assert.Equal("name", exception.ParamName);
        Assert.Empty(builder.VariableDeclarations);
    }

    [Fact]
    public void AddVariable_EmptyName_ThrowsAndAddsNothing()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => builder.AddVariable(string.Empty, CelType.Int));

        Assert.Equal("name", exception.ParamName);
        Assert.Empty(builder.VariableDeclarations);
    }

    [Fact]
    public void AddVariable_NullType_ThrowsAndAddsNothing()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => builder.AddVariable("x", null!));

        Assert.Equal("type", exception.ParamName);
        Assert.Empty(builder.VariableDeclarations);
    }

    [Fact]
    public void Environment_DoesNotChangeWhenTheBuilderDoes()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("x", CelType.Int);
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        builder.AddVariable("y", CelType.String);

        Assert.Equal([("x", CelType.Int)], Pairs(environment.VariableDeclarations));
        Assert.Equal([("x", CelType.Int), ("y", CelType.String)], Pairs(builder.VariableDeclarations));
    }

    [Fact]
    public void Build_DoesNotChangeTheBuilder()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("x", CelType.Int);

        CelEnvironment first = builder.Build().GetEnvironmentOrThrow();
        CelEnvironment second = builder.Build().GetEnvironmentOrThrow();

        Assert.NotSame(first, second);
        Assert.Equal([("x", CelType.Int)], Pairs(builder.VariableDeclarations));
        Assert.Equal([("x", CelType.Int)], Pairs(second.VariableDeclarations));
    }

    private static (string Name, CelType Type)[] Pairs(IEnumerable<CelVariableDecl> declarations) =>
        [.. declarations.Select(declaration => (declaration.Name, declaration.Type))];
}
