using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Library.Extensions.MathSet;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The macro cel.bind: a comprehension over the empty list whose accumulator is the bound name, the error for a first argument that is
// not an identifier with its place, and the calls that stay calls — without the set, on another target and with another number of
// arguments.
public sealed class BindMacroTests
{
    private static readonly CelEnvironment WithoutBindings = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    // The range is the empty list, the variable of iteration is #unused, the accumulator is the bound name with the first value as its
    // start, the condition is false, the step reads the name and the result is the third argument. The nodes the expansion creates
    // have the offset of the "(" of the call.
    [Fact]
    public static void Bind_ExpandsToAComprehensionOverTheEmptyList()
    {
        const string Text = "cel.bind(x, 1, x + 1)";

        ParsedExpression parsed = Parse(Text);

        ComprehensionNode comprehension = Assert.IsType<ComprehensionNode>(parsed.Root);
        Assert.Equal(("#unused", "x"), (comprehension.IterVar, comprehension.AccuVar));
        Assert.Empty(Assert.IsType<ListNode>(comprehension.IterRange).Elements);
        Assert.True(Assert.IsType<ConstantNode>(comprehension.AccuInit).Value.TryGetInt64(out long start));
        Assert.Equal(1L, start);
        Assert.True(Assert.IsType<ConstantNode>(comprehension.LoopCondition).Value.TryGetBoolean(out bool condition) && !condition);
        Assert.Equal("x", Assert.IsType<IdentNode>(comprehension.LoopStep).Name);
        Assert.Equal("_+_", Assert.IsType<CallNode>(comprehension.Result).Function);

        int call = Text.IndexOf('(', StringComparison.Ordinal);
        Assert.Equal(call, parsed.GetOffset(comprehension.Id));
        Assert.Equal(call, parsed.GetOffset(comprehension.IterRange.Id));
        Assert.Equal(call, parsed.GetOffset(comprehension.LoopCondition.Id));
        Assert.Equal(call, parsed.GetOffset(comprehension.LoopStep.Id));
        Assert.Equal(Text.IndexOf('1', StringComparison.Ordinal), parsed.GetOffset(comprehension.AccuInit.Id));

        MacroCall recorded = Assert.IsType<MacroCall>(parsed.GetMacroCall(comprehension.Id));
        Assert.Equal(("bind", "cel", 3), (recorded.Function, Assert.IsType<IdentNode>(recorded.Target).Name, recorded.Arguments.Length));
    }

    // The nodes are created in the order of cel-go: the range, the condition, the step, then the comprehension, after the arguments.
    [Fact]
    public static void Bind_CreatesTheNodesInTheOrderOfCelGo()
    {
        ComprehensionNode comprehension = Assert.IsType<ComprehensionNode>(Parse("cel.bind(x, 1, 2)").Root);

        Assert.True(comprehension.AccuInit.Id < comprehension.Result.Id);
        Assert.True(comprehension.Result.Id < comprehension.IterRange.Id);
        Assert.True(comprehension.IterRange.Id < comprehension.LoopCondition.Id);
        Assert.True(comprehension.LoopCondition.Id < comprehension.LoopStep.Id);
        Assert.True(comprehension.LoopStep.Id < comprehension.Id);
    }

    // "text | the piece at which the error is": the first argument is not a simple identifier. The selection a.b is at its dot; the piece ".b," is not found in "bind".
    [Theory]
    [InlineData("cel.bind(f(a), 2, 3)", "(a)")]
    [InlineData("cel.bind([a], 2, 3)", "[a]")]
    public static void FirstArgumentThatIsNotAnIdentifier_IsInvalidMacroCall(string text, string place)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParseResult parsed = MathEnvironment.Environment.Parse(text);

        Assert.False(parsed.IsSuccess);
        CelError error = Assert.Single(parsed.Errors);
        Assert.Equal(CelErrorCode.InvalidMacroCall, error.Code);
        Assert.Equal("invalid call of macro 'bind': cel.bind() variable names must be simple identifiers", error.Message);
        Assert.Equal(text.IndexOf(place, StringComparison.Ordinal), error.Location.Offset);
    }

    // A call with two or four arguments is no macro: bind is not declared, and the call stays a call.
    [Theory]
    [InlineData("cel.bind(x, 1)", 2)]
    [InlineData("cel.bind(x, 1, 2, 3)", 4)]
    [InlineData("cel.bind()", 0)]
    public static void OtherNumberOfArguments_StaysACall(string text, int count)
    {
        ParsedExpression parsed = Parse(text);

        CallNode call = Assert.IsType<CallNode>(parsed.Root);
        Assert.Equal(("bind", count), (call.Function, call.Arguments.Length));
        Assert.Equal("cel", Assert.IsType<IdentNode>(call.Target).Name);
        Assert.Null(parsed.GetMacroCall(call.Id));
    }

    [Fact]
    public static void WithoutTheSet_TheCallStaysACall()
    {
        ParseResult parsed = WithoutBindings.Parse("cel.bind(x, 1, x)");

        Assert.True(parsed.IsSuccess);
        CallNode call = Assert.IsType<CallNode>(parsed.Expression!.Root);
        Assert.Equal(("bind", 3), (call.Function, call.Arguments.Length));
    }

    // The target has to be the identifier cel.
    [Theory]
    [InlineData("foo.bind(x, 1, 2)")]
    [InlineData("a.cel.bind(x, 1, 2)")]
    [InlineData("cel2.bind(x, 1, 2)")]
    public static void OtherTarget_LeavesTheCall(string text)
    {
        ParsedExpression parsed = Parse(text);

        CallNode call = Assert.IsType<CallNode>(parsed.Root);
        Assert.Equal("bind", call.Function);
        Assert.Null(parsed.GetMacroCall(call.Id));
    }

    // The expansion has the depth of the call: a bind nested in the result is a comprehension in a comprehension.
    [Fact]
    public static void NestedBind_IsAComprehensionInTheResult()
    {
        ComprehensionNode outer = Assert.IsType<ComprehensionNode>(Parse("cel.bind(a, 1, cel.bind(b, a, b))").Root);

        ComprehensionNode inner = Assert.IsType<ComprehensionNode>(outer.Result);
        Assert.Equal(("a", "b"), (outer.AccuVar, inner.AccuVar));
        Assert.Equal("a", Assert.IsType<IdentNode>(inner.AccuInit).Name);
    }

    private static ParsedExpression Parse(string text)
    {
        ParseResult parsed = MathEnvironment.Environment.Parse(text);
        Assert.True(parsed.IsSuccess, text + ": " + string.Join("; ", parsed.Errors));
        return parsed.Expression!;
    }
}
