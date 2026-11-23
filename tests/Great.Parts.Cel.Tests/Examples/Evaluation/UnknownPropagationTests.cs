using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Unknown values propagate as the language definition has errors propagate, with two differences: the error of an argument of a
// strict call takes precedence over an unknown one, and in && and || an unknown operand takes precedence over an error. The
// variables named in "unknowns" are unknown; otherwise x = 1, y = 2 and l = [1, 2].
public sealed class UnknownPropagationTests
{
    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("x > 0 && false", "x", "false")]
    [InlineData("x > 0 || true", "x", "true")]
    [InlineData("true ? 1 : x", "x", "1")]
    [InlineData("l.exists(i, i == 1 || i == x)", "x", "true")]
    [InlineData("x + 1", "", "2")]
    public static void KnownResult_OfAnExpressionWhoseUnknownPartDoesNotDecideIt(string expression, string unknowns, string expected)
    {
        ArgumentNullException.ThrowIfNull(unknowns);
        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate(expression, PartialTestSupport.Patterns(unknowns));

        Assert.True(result.IsSuccess);
        Assert.False(result.IsUnknown);
        Assert.Equal(expected, result.GetValueOrThrow().ToString());
    }

    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("x + 1", "x", "x")]
    [InlineData("x + y", "x,y", "x,y")]
    [InlineData("x > 0 && y > 0", "x,y", "x,y")]
    [InlineData("x > 0 && (1 / 0 > 0)", "x", "x")]
    [InlineData("x > 0 ? 1 : 2", "x", "x")]
    [InlineData("[x, 1]", "x", "x")]
    [InlineData("{\"a\": x}", "x", "x")]
    [InlineData("l.exists(i, i == x)", "x", "x")]
    [InlineData("l.all(i, i > 0)", "l", "l")]
    [InlineData("l.map(i, i + x)", "x", "x")]
    [InlineData("cel.bind(t, x + 1, t * 2)", "x", "x")]
    [InlineData("string(x)", "x", "x")]
    [InlineData("optional.of(x).orValue(0)", "x", "x")]
    [InlineData("optional.none().orValue(x)", "x", "x")]
    [InlineData("!(x > 0)", "x", "x")]
    [InlineData("[?optional.of(x), y]", "x", "x")]
    [InlineData("[?optional.of(x), ?optional.of(y)]", "x,y", "x,y")]
    [InlineData("{?\"a\": optional.of(x), ?\"b\": optional.of(y)}", "x,y", "x,y")]
    [InlineData("{?\"a\": optional.of(x), \"b\": y}", "x", "x")]
    [InlineData("{?x: optional.of(1), ?y: optional.of(2)}", "x,y", "x,y")]
    public static void UnknownResult_DependsOnTheUnknownPartsOnly(string expression, string unknowns, string trails)
    {
        ArgumentNullException.ThrowIfNull(unknowns);
        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate(expression, PartialTestSupport.Patterns(unknowns));

        Assert.True(result.IsUnknown);
        Assert.False(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(trails, PartialTestSupport.Trails(result));
        Assert.Equal(result.Unknowns.OrderBy(unknown => unknown.ExprId).Select(unknown => unknown.ExprId), result.Unknowns.Select(unknown => unknown.ExprId));
    }

    [Theory]
    [Trait("Requirement", "F-22")]
    [InlineData("x + (1 / 0)")]
    [InlineData("(1 / 0) + x")]
    [InlineData("[x, 1 / 0]")]
    [InlineData("{\"a\": x, \"b\": 1 / 0}")]
    [InlineData("[?optional.of(x), 1 / 0]")]
    [InlineData("{?\"a\": optional.of(x), \"b\": 1 / 0}")]
    [InlineData("{?x: optional.of(1), y: 1 / 0}")]
    public static void ErrorOfAnOperandOfAStrictCall_TakesPrecedenceOverTheUnknownOne(string expression)
    {
        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate(expression, PartialTestSupport.Patterns("x"));

        Assert.False(result.IsUnknown);
        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(result.Errors).Code);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownOperand_HasTheIdAndThePlaceOfItsNode()
    {
        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate("x + 1", PartialTestSupport.Patterns("x"));

        CelUnknown unknown = Assert.Single(result.Unknowns);
        var root = Assert.IsType<CelCallNode>(PartialTestSupport.Env.Parse("x + 1").GetParsedExprOrThrow().Root);
        Assert.Equal(root.Arguments[0].Id, unknown.ExprId);
        Assert.Equal(0, unknown.Span.Start.Offset);
        Assert.Equal(1, unknown.Span.Length);
        Assert.Null(unknown.Function);
        Assert.Empty(unknown.Arguments);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void UnknownSets_AreMergedInTheOrderOfTheIds()
    {
        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate("y + x", PartialTestSupport.Patterns("x,y"));

        Assert.Equal("y,x", PartialTestSupport.Trails(result));
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void CostLimit_ChargesAnUnknownAttributeOne()
    {
        var options = new CelEvalOptions { CostLimit = 1_000 };

        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate("x", PartialTestSupport.Patterns("x"), options: options);

        Assert.True(result.IsUnknown);
        Assert.Equal(1UL, result.Cost);
    }

    [Fact]
    [Trait("Requirement", "F-22")]
    public void SynchronousEvaluation_OfAPartialProgram_HasNoUnknowns()
    {
        CelProgram<long> program = PartialTestSupport.Env.Compile<long>("x + y", PartialTestSupport.Partial).GetProgramOrThrow();
        CelProgram<long> plain = PartialTestSupport.Env.Compile<long>("x + y").GetProgramOrThrow();
        var options = new CelEvalOptions { CostLimit = 1_000 };

        CelEvalResult<long> partial = program.Evaluate(PartialTestSupport.Bindings(), options, CancellationToken.None);
        CelEvalResult<long> expected = plain.Evaluate(PartialTestSupport.Bindings(), options, CancellationToken.None);

        Assert.Equal(expected.GetValueOrThrow(), partial.GetValueOrThrow());
        Assert.Equal(expected.Cost, partial.Cost);
    }
}
