using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// The public declarations of functions and overloads: what the factories keep and the conditions of the calls.
public sealed class CelOverloadDeclTests
{
    private static readonly CelCostFunction Linear = arguments => (ulong)arguments.Length;

    [Fact]
    public void Global_KeepsTheIdentifierTheParametersAndTheResult()
    {
        CelOverloadDecl overload = CelOverloadDecl.Global("score_int_string", [CelType.Int, CelType.String], CelType.Double);

        Assert.Equal("score_int_string", overload.OverloadId);
        Assert.False(overload.IsReceiverStyle);
        Assert.Equal([CelType.Int, CelType.String], overload.ParameterTypes);
        Assert.Equal(CelType.Double, overload.ResultType);
        Assert.Null(overload.Cost);
    }

    [Fact]
    public void Receiver_PutsTheReceiverFirst()
    {
        CelOverloadDecl overload = CelOverloadDecl.Receiver("string_score_int", CelType.String, [CelType.Int], CelType.Bool);

        Assert.True(overload.IsReceiverStyle);
        Assert.Equal([CelType.String, CelType.Int], overload.ParameterTypes);
    }

    [Fact]
    public void WithCost_ReturnsACopyWithTheCostAndLeavesTheOriginal()
    {
        CelOverloadDecl original = CelOverloadDecl.Receiver("list_score", CelType.ListOf(CelType.Int), [], CelType.Int);

        CelOverloadDecl costed = original.WithCost(Linear);

        Assert.NotSame(original, costed);
        Assert.Same(Linear, costed.Cost);
        Assert.Null(original.Cost);
        Assert.Equal((original.OverloadId, original.IsReceiverStyle, original.ResultType), (costed.OverloadId, costed.IsReceiverStyle, costed.ResultType));
        Assert.Equal(original.ParameterTypes, costed.ParameterTypes);
    }

    [Fact]
    public void Factories_CheckTheirArguments()
    {
        Assert.Equal("overloadId", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Global(null!, [], CelType.Int)).ParamName);
        Assert.Equal("overloadId", Assert.Throws<ArgumentException>(() => CelOverloadDecl.Global(string.Empty, [], CelType.Int)).ParamName);
        Assert.Equal("resultType", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Global("f", [], null!)).ParamName);
        Assert.Equal("parameterTypes", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Global("f", [CelType.Int, null!], CelType.Int)).ParamName);
        Assert.Equal("receiverType", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Receiver("f", null!, [], CelType.Int)).ParamName);
        Assert.Equal("parameterTypes", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Receiver("f", CelType.Int, [null!], CelType.Int)).ParamName);
        Assert.Equal("resultType", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Receiver("f", CelType.Int, [], null!)).ParamName);
        Assert.Equal("cost", Assert.Throws<ArgumentNullException>(() => CelOverloadDecl.Global("f", [], CelType.Int).WithCost(null!)).ParamName);
    }

    // The same overload: identifier, call style, parameter and result types, and the same cost function or none.
    [Fact]
    public void SameAs_ComparesEveryPartAndTheCostByInstance()
    {
        CelOverloadDecl overload = CelOverloadDecl.Global("score_int", [CelType.Int], CelType.Int);

        Assert.True(overload.SameAs(CelOverloadDecl.Global("score_int", [CelType.Int], CelType.Int)));
        Assert.True(overload.WithCost(Linear).SameAs(overload.WithCost(Linear)));
        Assert.False(overload.SameAs(CelOverloadDecl.Global("score_uint", [CelType.Int], CelType.Int)));
        Assert.False(overload.SameAs(CelOverloadDecl.Receiver("score_int", CelType.Int, [], CelType.Int)));
        Assert.False(overload.SameAs(CelOverloadDecl.Global("score_int", [CelType.UInt], CelType.Int)));
        Assert.False(overload.SameAs(CelOverloadDecl.Global("score_int", [CelType.Int, CelType.Int], CelType.Int)));
        Assert.False(overload.SameAs(CelOverloadDecl.Global("score_int", [CelType.Int], CelType.Bool)));
        Assert.False(overload.SameAs(overload.WithCost(Linear)));
        Assert.False(overload.WithCost(Linear).SameAs(overload.WithCost(_ => 1)));
    }

    [Fact]
    public void FunctionDecl_KeepsTheNameAndTheOverloadsInTheirOrder()
    {
        CelOverloadDecl second = CelOverloadDecl.Global("score_string", [CelType.String], CelType.Int);
        CelOverloadDecl first = CelOverloadDecl.Global("score_int", [CelType.Int], CelType.Int);

        var function = new CelFunctionDecl("acme.score", second, first);

        Assert.Equal("acme.score", function.Name);
        Assert.Equal([second, first], function.Overloads);
    }

    [Fact]
    public void FunctionDecl_ChecksItsArguments()
    {
        CelOverloadDecl overload = CelOverloadDecl.Global("score_int", [CelType.Int], CelType.Int);

        Assert.Equal("name", Assert.Throws<ArgumentNullException>(() => new CelFunctionDecl(null!, overload)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentException>(() => new CelFunctionDecl(string.Empty, overload)).ParamName);
        Assert.Equal("overloads", Assert.Throws<ArgumentException>(() => new CelFunctionDecl("score")).ParamName);
        Assert.Equal("overloads", Assert.Throws<ArgumentNullException>(() => new CelFunctionDecl("score", overload, null!)).ParamName);
    }

    [Fact]
    public void VariableDecl_SameAs_ComparesTheNameAndTheType()
    {
        var variable = new CelVariableDecl("amount", CelType.ListOf(CelType.Int));

        Assert.True(variable.SameAs(new CelVariableDecl("amount", CelType.ListOf(CelType.Int))));
        Assert.False(variable.SameAs(new CelVariableDecl("amount", CelType.ListOf(CelType.UInt))));
        Assert.False(variable.SameAs(new CelVariableDecl("Amount", CelType.ListOf(CelType.Int))));
    }
}
