using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Library.Extensions.MathSet;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The macros math.greatest and math.least: the shape of the expansion, the checks of the arguments by the kind of the node with the
// text and the place of each error, and the calls that stay calls — without the set and on another target.
public sealed class MathMacroTests
{
    private static readonly CelEnvironment WithoutMath = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    [Fact]
    public static void TwoArguments_ExpandToACallOfMaxWithTheArguments()
    {
        const string Text = "math.greatest(1, x)";

        ParsedExpression parsed = Parse(Text);

        CallNode call = Assert.IsType<CallNode>(parsed.Root);
        Assert.Equal("math.@max", call.Function);
        Assert.Null(call.Target);
        Assert.Equal(2, call.Arguments.Length);
        Assert.IsType<ConstantNode>(call.Arguments[0]);
        Assert.Equal("x", Assert.IsType<IdentNode>(call.Arguments[1]).Name);
        Assert.Equal(Text.IndexOf('(', StringComparison.Ordinal), parsed.GetOffset(call.Id));
        MacroCall recorded = Assert.IsType<MacroCall>(parsed.GetMacroCall(call.Id));
        Assert.Equal(("greatest", "math"), (recorded.Function, Assert.IsType<IdentNode>(recorded.Target).Name));
    }

    [Fact]
    public static void Least_ExpandsToACallOfMin()
    {
        CallNode call = Assert.IsType<CallNode>(Parse("math.least(1, 2)").Root);

        Assert.Equal(("math.@min", 2), (call.Function, call.Arguments.Length));
    }

    [Fact]
    public static void OneArgument_ExpandsToACallOfMaxWithThatArgument()
    {
        CallNode call = Assert.IsType<CallNode>(Parse("math.greatest(x)").Root);

        Assert.Equal("math.@max", call.Function);
        Assert.Equal("x", Assert.IsType<IdentNode>(Assert.Single(call.Arguments)).Name);
    }

    // Three arguments and more become one argument, a list literal of them, at the offset of the call.
    [Theory]
    [InlineData("math.greatest(1, 2, 3)", "math.@max", 3)]
    [InlineData("math.least(1, 2, 3, 4, 5)", "math.@min", 5)]
    public static void ThreeArgumentsAndMore_ExpandToACallOfOneListLiteral(string text, string function, int count)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParsedExpression parsed = Parse(text);

        CallNode call = Assert.IsType<CallNode>(parsed.Root);
        Assert.Equal(function, call.Function);
        ListNode list = Assert.IsType<ListNode>(Assert.Single(call.Arguments));
        Assert.Equal(count, list.Elements.Length);
        Assert.Empty(list.OptionalIndices);
        Assert.Equal(parsed.GetOffset(call.Id), parsed.GetOffset(list.Id));
        Assert.Equal(text.IndexOf('(', StringComparison.Ordinal), parsed.GetOffset(list.Id));
    }

    // The arguments are expanded before the call, so a macro among them is already its expansion.
    [Fact]
    public static void MacroAmongTheArguments_IsExpandedFirst()
    {
        CallNode call = Assert.IsType<CallNode>(Parse("math.greatest(math.least(1, 2), 3)").Root);

        CallNode inner = Assert.IsType<CallNode>(call.Arguments[0]);
        Assert.Equal(("math.@max", "math.@min"), (call.Function, inner.Function));
    }

    // A numeric argument is a constant int, uint or double, or a node that is not a constant and not a literal of a list, a map or a
    // message: the decision is by the kind of the node, not by its type.
    [Theory]
    [InlineData("math.greatest(1)")]
    [InlineData("math.greatest(1u)")]
    [InlineData("math.greatest(1.5)")]
    [InlineData("math.greatest(x)")]
    [InlineData("math.greatest(x.y)")]
    [InlineData("math.greatest(dyn(5.4))")]
    [InlineData("math.greatest(1 + 2)")]
    [InlineData("math.greatest(true ? 1 : 2)")]
    [InlineData("math.greatest(1, x, x.y, dyn(2), -3, 1 + 2, 2.5, 3u)")]
    [InlineData("math.greatest([1, 2])")]
    [InlineData("math.greatest([dyn(1), 2.5, x])")]
    [InlineData("math.least([dyn(5.4), dyn(10), dyn(3u), dyn(-5.0), dyn(3.5)])")]
    [InlineData("math.least('a'.size())")]
    public static void NumericArguments_Expand(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParseResult parsed = Parse2(text);

        Assert.True(parsed.IsSuccess, text + ": " + string.Join("; ", parsed.Errors));
        Assert.Equal(text.Contains("least", StringComparison.Ordinal) ? "math.@min" : "math.@max", Assert.IsType<CallNode>(parsed.Expression!.Root).Function);
    }

    // The first argument that is not numeric is the place, though a later one is not either.
    [Fact]
    public static void SeveralBadArguments_ReportTheFirst()
    {
        ParseResult parsed = Parse2("math.greatest(1, 'a', [2])");

        CelError error = Assert.Single(parsed.Errors);
        Assert.Equal(17, error.Location.Offset);
    }

    // The macros exist only in an environment that has the set: without it the call stays a call, and the target is the identifier.
    [Fact]
    public static void WithoutTheSet_TheCallStaysACall()
    {
        ParseResult parsed = WithoutMath.Parse("math.greatest(1, 2)");

        Assert.True(parsed.IsSuccess);
        CallNode call = Assert.IsType<CallNode>(parsed.Expression!.Root);
        Assert.Equal("greatest", call.Function);
        Assert.Equal("math", Assert.IsType<IdentNode>(call.Target).Name);
        Assert.Null(parsed.Expression.GetMacroCall(call.Id));
    }

    // The target has to be the identifier math: a selection that ends in math, another name and a call result are not it.
    [Theory]
    [InlineData("foo.math.greatest(1)", "greatest")]
    [InlineData("math2.greatest(1)", "greatest")]
    [InlineData("x.greatest(1)", "greatest")]
    [InlineData("math.greatest.least(1)", "least")]
    public static void OtherTarget_LeavesTheCall(string text, string function)
    {
        ParsedExpression parsed = Parse(text);

        CallNode call = Assert.IsType<CallNode>(parsed.Root);
        Assert.NotNull(call.Target);
        Assert.Equal(function, call.Function);
        Assert.Null(parsed.GetMacroCall(call.Id));
    }

    // Without a target the function name is the whole name: greatest(1) is a call of a function that no set declares.
    [Fact]
    public static void CallWithoutATarget_IsNotAMacro()
    {
        CallNode call = Assert.IsType<CallNode>(Parse("greatest(1, 2)").Root);

        Assert.Equal("greatest", call.Function);
        Assert.Null(call.Target);
    }

    // Both macros have a key of their own, apart from the standard ones and from cel.bind: the table of an environment has them.
    [Fact]
    public static void Table_FindsTheMacrosForAnyNumberOfArguments()
    {
        SyntaxOptions syntax = MathEnvironment.Environment.Syntax;

        foreach (int count in new[] { 0, 1, 2, 3, 9 })
        {
            Assert.Equal("greatest", syntax.Macros.Find("greatest", count, isReceiver: true)!.Name);
            Assert.Equal("least", syntax.Macros.Find("least", count, isReceiver: true)!.Name);
        }

        Assert.Null(syntax.Macros.Find("greatest", 1, isReceiver: false));
        Assert.Equal("bind", syntax.Macros.Find("bind", 3, isReceiver: true)!.Name);
        Assert.Null(syntax.Macros.Find("bind", 2, isReceiver: true));
    }

    private static ParsedExpression Parse(string text)
    {
        ParseResult parsed = Parse2(text);
        Assert.True(parsed.IsSuccess, text + ": " + string.Join("; ", parsed.Errors));
        return parsed.Expression!;
    }

    private static ParseResult Parse2(string text) => MathEnvironment.Environment.Parse(text);
}
