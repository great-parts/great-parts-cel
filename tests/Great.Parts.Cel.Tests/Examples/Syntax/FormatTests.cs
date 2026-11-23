using System;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// CelSyntax.Format: the text of a tree, which parses in an environment with the same syntax into an equal tree. Operators have
// single spaces around them, parentheses stand only where the tree needs them, and a macro expansion is its recorded call; a tree
// without a text form is ArgumentException.
[Trait("Requirement", "F-21")]
[Trait("Requirement", "F-14")]
public sealed class FormatTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;

    [Theory]
    [InlineData("a+b*c", "a + b * c")]
    [InlineData("(a+b)*c", "(a + b) * c")]
    [InlineData("!(a&&b)", "!(a && b)")]
    [InlineData("items.all(v,v>0)", "items.all(v, v > 0)")]
    [InlineData("has(m.f)", "has(m.f)")]
    [InlineData("{\"a\":1}", "{\"a\": 1}")]
    [InlineData("m.`a-b`", "m.`a-b`")]
    [InlineData("cel.bind(t,1,t+1)", "cel.bind(t, 1, t + 1)")]
    public static void Format_PrintsTheTreeOfATextInTheNormalForm(string text, string expected)
    {
        ArgumentNullException.ThrowIfNull(text);

        Assert.Equal(expected, CelSyntax.Format(Env.Parse(text).GetParsedExprOrThrow()));
    }

    [Theory]
    [InlineData("a + b * c")]
    [InlineData("(a + b) * c")]
    [InlineData("a - (b - c)")]
    [InlineData("!(a && b) || c")]
    [InlineData("-(1) + -x")]
    [InlineData("items.map(v, v > 0, v * 2).filter(w, w > 1).size()")]
    [InlineData("[1, ?d, 3][?1]")]
    [InlineData("{\"a\": 1, ?\"b\": d}.size()")]
    [InlineData("m.?f.orValue(1)")]
    [InlineData("a ? b : c ? d : x")]
    [InlineData("has(m.f) && m.f in [1]")]
    [InlineData("cel.bind(t, 1, t + items.size())")]
    [InlineData("\"quote\\\" backslash\\\\ tab\\t\".size()")]
    [InlineData("2.5e10 + 1.0 + double(1u)")]
    public static void Format_OfAParsedText_ParsesIntoAnEqualTree(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        CelParsedExpr parsed = Env.Parse(text).GetParsedExprOrThrow();

        CelParsedExpr again = Env.Parse(CelSyntax.Format(parsed)).GetParsedExprOrThrow();

        Assert.Null(SyntaxTreeComparer.Difference(parsed.Root, again.Root));
    }

    [Fact]
    public static void Format_OfAnExpansionWithoutARecordedCall_IsArgumentException()
    {
        var f = new CelExprFactory();
        CelExprNode loop = f.Comprehension(
            "v",
            f.Ident("items"),
            "@result",
            f.Constant(CelValue.FromBoolean(true)),
            f.Constant(CelValue.FromBoolean(true)),
            f.Ident("@result"),
            f.Ident("@result"));
        var g = new CelExprFactory();
        CelExprNode presence = g.PresenceTest(g.Ident("m"), "f");

        Assert.Throws<ArgumentException>(() => CelSyntax.Format(f.Build(loop)));
        Assert.Throws<ArgumentException>(() => CelSyntax.Format(g.Build(presence)));
    }

    [Fact]
    public static void Format_OfATreeDeeperThanTheLimit_IsArgumentException()
    {
        var f = new CelExprFactory();
        CelExprNode node = f.Constant(CelValue.FromBoolean(true));
        for (int i = 0; i < 300; i++)
        {
            node = f.Call("!_", node);
        }

        CelParsedExpr tree = f.Build(node);

        Assert.Throws<ArgumentException>(() => CelSyntax.Format(tree));
    }

    [Fact]
    public static void Format_OfATreeAtTheLimit_IsPrinted()
    {
        var f = new CelExprFactory();
        CelExprNode node = f.Constant(CelValue.FromBoolean(true));
        for (int i = 0; i < 250; i++)
        {
            node = f.Call("!_", node);
        }

        string text = CelSyntax.Format(f.Build(node));

        Assert.StartsWith("!(!(", text, StringComparison.Ordinal);
        Assert.Contains("true", text, StringComparison.Ordinal);
    }

    [Fact]
    public static void Format_PrintsInternalNamesAsTheyAre()
    {
        var f = new CelExprFactory();
        CelExprNode tree = f.Call("cel.@block", f.List([f.Ident("x")]), f.Ident("@index0"));

        Assert.Equal("cel.@block([x], @index0)", CelSyntax.Format(f.Build(tree)));
    }

    [Fact]
    public static void Format_OfNull_IsArgumentNullException()
    {
        Assert.Equal("expression", Assert.Throws<ArgumentNullException>(() => CelSyntax.Format(null!)).ParamName);
    }
}
