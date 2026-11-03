using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// Equality of syntax trees (docs/design/syntax.md, "Printing and tree equality"): the path to the first difference
// with both values, or null; ids, positions and macro calls are not compared.
public sealed class SyntaxTreeComparerTests
{
    private static readonly SyntaxOptions WithOptionalSyntax = SyntaxOptions.Standard with { OptionalSyntax = true };

    [Theory]
    [InlineData("a+b", " a  +  b ")]
    [InlineData("f(x, [1, 2])", "f(x,[1,2,])")]
    [InlineData("A{f: 1}", "A{`f`: 1}")]
    [InlineData("{'k': b'v'}", "{\"k\": b\"v\"}")]
    [InlineData("xs.all(e, e)", "(xs).all(e,(e))")]
    [InlineData("has(a.b)", "has((a).b)")]
    [InlineData("1.5", "15e-1")]
    public static void TreesOfTheSameShape_HaveNoDifference(string left, string right)
    {
        Assert.Null(SyntaxTreeComparer.Difference(Parse(left), Parse(right)));
    }

    // Two trees differ in a single place; the difference names it from the root.
    [Theory]
    [InlineData("x", "1", "root: IdentNode vs ConstantNode")]
    [InlineData("x", "y", "root.name: \"x\" vs \"y\"")]
    [InlineData("1", "1u", "root.kind: Int vs UInt")]
    [InlineData("1.0", "2.0", "root.value: 1.0 vs 2.0")]
    [InlineData("0.0", "-0.0", "root.value: 0.0 vs -0.0")]
    [InlineData("'a'", "'b'", "root.value: \"a\" vs \"b\"")]
    [InlineData("b'a'", "b'b'", "root.value: b\"a\" vs b\"b\"")]
    [InlineData("b'a'", "'a'", "root.kind: Bytes vs String")]
    [InlineData("true", "false", "root.value: true vs false")]
    [InlineData("a.b", "a.c", "root.field: \"b\" vs \"c\"")]
    [InlineData("a.b.c", "a.d.c", "root.operand.field: \"b\" vs \"d\"")]
    [InlineData("has(a.b)", "a.b", "root.isPresenceTest: true vs false")]
    [InlineData("f(1, a + b)", "f(1, a - b)", "root.arguments[1].function: \"_+_\" vs \"_-_\"")]
    [InlineData("f(1, 2)", "f(1)", "root.arguments: 2 items vs 1 items")]
    [InlineData("a.f()", "f(a)", "root.target: a target vs null")]
    [InlineData("a.f()", "b.f()", "root.target.name: \"a\" vs \"b\"")]
    [InlineData("[1, 2]", "[1, 3]", "root.elements[1].value: 2 vs 3")]
    [InlineData("x{}", "{}", "root.messageName: \"x\" vs \"\"")]
    [InlineData("x{f: 1}", "x{g: 1}", "root.entries[0].fieldName: \"f\" vs \"g\"")]
    [InlineData("x{f: 1}", "x{f: 1, g: 2}", "root.entries: 1 items vs 2 items")]
    [InlineData("{1: 2}", "{1: 3}", "root.entries[0].value.value: 2 vs 3")]
    [InlineData("{1: 2}", "{0: 2}", "root.entries[0].key.value: 1 vs 0")]
    [InlineData("xs.all(e, true)", "xs.all(f, true)", "root.iterVar: \"e\" vs \"f\"")]
    [InlineData("xs.all(e, true)", "ys.all(e, true)", "root.iterRange.name: \"xs\" vs \"ys\"")]
    [InlineData("xs.all(e, true)", "xs.exists(e, true)", "root.accuInit.value: true vs false")]
    [InlineData("xs.map(e, e)", "xs.filter(e, e)", "root.loopStep.function: \"_+_\" vs \"_?_:_\"")]
    [InlineData("xs.exists(e, e)", "xs.exists_one(e, e)", "root.accuInit.kind: Bool vs Int")]
    public static void TreesThatDiffer_HaveThePathToTheFirstDifference(string left, string right, string difference)
    {
        Assert.Equal(difference, SyntaxTreeComparer.Difference(Parse(left), Parse(right)));
    }

    [Theory]
    [InlineData("[?a]", "[a]", "root.optionalIndices: [0] vs []")]
    [InlineData("[a, ?b, ?c]", "[?a, b, ?c]", "root.optionalIndices: [1, 2] vs [0, 2]")]
    [InlineData("{?1: 2}", "{1: 2}", "root.entries[0].isOptional: true vs false")]
    [InlineData("x{?f: 1}", "x{f: 1}", "root.entries[0].isOptional: true vs false")]
    [InlineData("a.?b", "a.?c", "root.arguments[1].value: \"b\" vs \"c\"")]
    [InlineData("a[?0]", "a[0]", "root.function: \"_[?_]\" vs \"_[_]\"")]
    public static void TreesWithOptionalSyntaxThatDiffer_HaveThePathToTheFirstDifference(string left, string right, string difference)
    {
        Assert.Equal(difference, SyntaxTreeComparer.Difference(Parse(left, WithOptionalSyntax), Parse(right, WithOptionalSyntax)));
    }

    // The accumulator variable is compared too, though every macro names it the same.
    [Fact]
    public static void Comprehensions_DifferInTheAccumulatorVariable()
    {
        var nodes = new NodeFactory();
        IdentNode range = nodes.Ident("xs", 0);
        ConstantNode init = nodes.Constant(CelValue.FromBoolean(true), 0);

        ComprehensionNode left = nodes.Comprehension("e", range, "@result", init, init, init, init, 0);
        ComprehensionNode right = nodes.Comprehension("e", range, "acc", init, init, init, init, 0);

        Assert.Equal("root.accuVar: \"@result\" vs \"acc\"", SyntaxTreeComparer.Difference(left, right));
    }

    private static ExprNode Parse(string expression, SyntaxOptions? syntax = null)
    {
        ParseResult result = Parser.Parse(expression, syntax);
        Assert.True(result.IsSuccess, expression + ": " + string.Join(" | ", result.Errors.Select(Format)));
        return result.Expression.Root;
    }

    private static string Format(CelError error) => error.Code + " " + error.Message;
}
