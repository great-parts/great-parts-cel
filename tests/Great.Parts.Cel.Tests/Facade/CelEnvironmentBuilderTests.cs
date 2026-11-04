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

    [Fact]
    public void FunctionMembers_ReturnTheSameBuilder()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        CelFunctionImplementation implementation = arguments => arguments[0];

        Assert.Same(builder, builder.AddFunction<long>("zero", () => 0));
        Assert.Same(builder, builder.AddFunction<long, long>("one", value => value));
        Assert.Same(builder, builder.AddFunction<long, long, long>("two", (value, _) => value));
        Assert.Same(builder, builder.AddFunction<long, long, long, long>("three", (value, _, _) => value));
        Assert.Same(builder, builder.AddFunction<long, long, long, long, long>("four", (value, _, _, _) => value));
        Assert.Same(builder, builder.AddFunction(new CelFunctionDecl("five", CelOverloadDecl.Global("five_int", [CelType.Int], CelType.Int))));
        Assert.Same(builder, builder.AddFunctionImplementation("five_int", implementation));
        Assert.Same(builder, builder.AddVariable(new CelVariableDecl("v", CelType.Int)));
        Assert.Same(builder, builder.UseContainer("acme"));
        Assert.True(builder.Build().IsSuccess);
    }

    [Fact]
    public void FunctionMembers_CheckTheirArguments()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

        Assert.Equal("declaration", Assert.Throws<ArgumentNullException>(() => builder.AddVariable((CelVariableDecl)null!)).ParamName);
        Assert.Equal("declaration", Assert.Throws<ArgumentNullException>(() => builder.AddFunction(null!)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => builder.AddVariable<long>(null!)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => builder.AddFunction<long>(string.Empty, () => 0)).ParamName);
        Assert.Equal("implementation", Assert.Throws<ArgumentNullException>(() => builder.AddFunction<long, long>("f", null!)).ParamName);
        Assert.Equal("overloadId", Assert.Throws<ArgumentException>(() => builder.AddFunctionImplementation(string.Empty, arguments => arguments[0])).ParamName);
        Assert.Equal("implementation", Assert.Throws<ArgumentNullException>(() => builder.AddFunctionImplementation("f", (CelFunctionImplementation)null!)).ParamName);
        Assert.Equal("overloadId", Assert.Throws<ArgumentNullException>(() => builder.AddFunctionImplementation<long, long>(null!, value => value)).ParamName);
        Assert.Equal("implementation", Assert.Throws<ArgumentNullException>(() => builder.AddFunctionImplementation<long, long, long, long, long>("f", null!)).ParamName);
        Assert.True(builder.Build().IsSuccess);
    }

    private static (string Name, CelType Type)[] Pairs(IEnumerable<CelVariableDecl> declarations) =>
        [.. declarations.Select(declaration => (declaration.Name, declaration.Type))];
}
