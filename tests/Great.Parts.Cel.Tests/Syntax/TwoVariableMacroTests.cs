using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Library.Extensions.TwoVar;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The macros of two iteration variables as the parser creates them: the shape of each expansion, the offset and the order of the
// ids of its nodes, the record of the call, the key by which a call finds its macro, and the calls that are not these macros —
// the standard forms of two and three arguments, a call without a receiver and a call without the set.
public sealed class TwoVariableMacroTests
{
    private static readonly SyntaxOptions WithTheSet = new(
        OptionalSyntax: false,
        MacroTable.Create([.. MacroTable.Standard.Macros, .. ExtensionMacros.TwoVariableComprehensions]));

    // "text | the expansion": the iteration variables, the range, the accumulator, the initial value, the condition, the step and the
    // result of the comprehension, in this order. The names of the variables are checked by the macro and never read from the tree
    // by the step, except that transformMap reads the first one.
    private const string Shapes = """
        xs.all(i, v, v > i) | comprehension(i, v, xs, @result, true, @not_strictly_false(@result), _&&_(@result, _>_(v, i)), @result)
        xs.exists(i, v, v > i) | comprehension(i, v, xs, @result, false, @not_strictly_false(!_(@result)), _||_(@result, _>_(v, i)), @result)
        xs.existsOne(i, v, v > i) | comprehension(i, v, xs, @result, 0, true, _?_:_(_>_(v, i), _+_(@result, 1), @result), _==_(@result, 1))
        xs.exists_one(i, v, v > i) | comprehension(i, v, xs, @result, 0, true, _?_:_(_>_(v, i), _+_(@result, 1), @result), _==_(@result, 1))
        xs.transformList(i, v, t) | comprehension(i, v, xs, @result, [], true, _+_(@result, [t]), @result)
        xs.transformList(i, v, p, t) | comprehension(i, v, xs, @result, [], true, _?_:_(p, _+_(@result, [t]), @result), @result)
        xs.transformMap(i, v, t) | comprehension(i, v, xs, @result, {}, true, cel.@mapInsert(@result, i, t), @result)
        xs.transformMap(i, v, p, t) | comprehension(i, v, xs, @result, {}, true, _?_:_(p, cel.@mapInsert(@result, i, t), @result), @result)
        xs.transformMapEntry(i, v, t) | comprehension(i, v, xs, @result, {}, true, cel.@mapInsert(@result, t), @result)
        xs.transformMapEntry(i, v, p, t) | comprehension(i, v, xs, @result, {}, true, _?_:_(p, cel.@mapInsert(@result, t), @result), @result)
        """;

    public static TheoryData<string, string> ShapeRows => RowsOf(Shapes);

    [Theory]
    [MemberData(nameof(ShapeRows))]
    public static void Macro_ExpandsToAComprehensionOfTwoVariables(string text, string shape)
    {
        ParsedExpression parsed = Parse(text);

        Assert.Equal(shape, Shape(parsed.Root));
    }

    // Every node of the expansion has the offset of the "(" of the call, the arguments that the author wrote keep their own, and
    // the parts of the comprehension are created after those arguments in the order of the table: the initial value, the condition,
    // the step, the result, then the comprehension.
    [Theory]
    [InlineData("xs.all(i, v, v > i)")]
    [InlineData("xs.transformList(i, v, v > 1, i)")]
    [InlineData("xs.transformMap(i, v, v > 1, i)")]
    [InlineData("xs.transformMapEntry(i, v, x)")]
    public static void Expansion_IsAtTheParenthesisAndIsCreatedAfterItsArguments(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParsedExpression parsed = Parse(text);
        ComprehensionNode loop = Assert.IsType<ComprehensionNode>(parsed.Root);
        int parenthesis = text.IndexOf('(', StringComparison.Ordinal);

        Assert.Equal(parenthesis, parsed.GetOffset(loop.Id));
        Assert.Equal(parenthesis, parsed.GetOffset(loop.AccuInit.Id));
        Assert.Equal(parenthesis, parsed.GetOffset(loop.LoopCondition.Id));
        Assert.Equal(parenthesis, parsed.GetOffset(loop.LoopStep.Id));
        Assert.Equal(parenthesis, parsed.GetOffset(loop.Result.Id));
        Assert.True(Ids(loop.IterRange).Max() < loop.AccuInit.Id);
        if (loop.LoopStep is CallNode { Function: "_?_:_" } conditional)
        {
            Assert.True(Ids(conditional.Arguments[0]).Max() < loop.AccuInit.Id);
        }

        Assert.True(loop.AccuInit.Id < loop.LoopCondition.Id);
        Assert.True(loop.LoopCondition.Id < loop.LoopStep.Id);
        Assert.True(loop.LoopStep.Id < loop.Result.Id);
        Assert.True(loop.Result.Id < loop.Id);
    }

    // The first variable that transformMap puts into the call of cel.@mapInsert is an identifier of its own, created by the
    // expansion at the "(", not the node of the argument that the author wrote, which the tree does not hold.
    [Fact]
    public static void TransformMap_ReadsTheFirstVariableThroughAnIdentifierOfItsOwn()
    {
        const string Text = "xs.transformMap(k, v, v)";
        ParsedExpression parsed = Parse(Text);
        ComprehensionNode loop = Assert.IsType<ComprehensionNode>(parsed.Root);
        MacroCall recorded = Assert.IsType<MacroCall>(parsed.GetMacroCall(loop.Id));

        CallNode insert = Assert.IsType<CallNode>(loop.LoopStep);
        IdentNode key = Assert.IsType<IdentNode>(insert.Arguments[1]);

        Assert.Equal("k", key.Name);
        Assert.NotSame(recorded.Arguments[0], key);
        Assert.Equal(Text.IndexOf('(', StringComparison.Ordinal), parsed.GetOffset(key.Id));
        Assert.Equal(Text.Length - 2, parsed.GetOffset(insert.Arguments[2].Id));
    }

    // The call is recorded as written, under the id of the comprehension, with its name, its receiver and every argument.
    [Theory]
    [InlineData("xs.all(i, v, true)", "all", 3)]
    [InlineData("xs.exists_one(i, v, true)", "exists_one", 3)]
    [InlineData("xs.transformList(i, v, i)", "transformList", 3)]
    [InlineData("xs.transformList(i, v, true, i)", "transformList", 4)]
    [InlineData("xs.transformMapEntry(i, v, x)", "transformMapEntry", 3)]
    [InlineData("xs.transformMapEntry(i, v, true, x)", "transformMapEntry", 4)]
    public static void Call_IsRecordedAsWrittenUnderTheIdOfTheComprehension(string text, string function, int count)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParsedExpression parsed = Parse(text);
        ComprehensionNode loop = Assert.IsType<ComprehensionNode>(parsed.Root);

        MacroCall recorded = Assert.IsType<MacroCall>(parsed.GetMacroCall(loop.Id));

        Assert.Equal((function, count), (recorded.Function, recorded.Arguments.Length));
        Assert.Same(loop.IterRange, recorded.Target);
        Assert.Equal(("i", "v"), (Assert.IsType<IdentNode>(recorded.Arguments[0]).Name, Assert.IsType<IdentNode>(recorded.Arguments[1]).Name));
    }

    // The key of a macro is its name, whether it has a receiver and the number of its arguments: all, exists, exists_one, map and
    // filter of two or three arguments stay the standard macros, and all, exists and exists_one of three arguments, with the
    // transformations of three and four, are those of the set.
    [Theory]
    [InlineData("all", 2, true, 2)]
    [InlineData("all", 3, true, 3)]
    [InlineData("exists", 2, true, 2)]
    [InlineData("exists", 3, true, 3)]
    [InlineData("exists_one", 2, true, 2)]
    [InlineData("exists_one", 3, true, 3)]
    [InlineData("existsOne", 3, true, 3)]
    [InlineData("map", 2, true, 2)]
    [InlineData("map", 3, true, 3)]
    [InlineData("filter", 2, true, 2)]
    [InlineData("transformList", 3, true, 3)]
    [InlineData("transformList", 4, true, 4)]
    [InlineData("transformMap", 3, true, 3)]
    [InlineData("transformMap", 4, true, 4)]
    [InlineData("transformMapEntry", 3, true, 3)]
    [InlineData("transformMapEntry", 4, true, 4)]
    public static void Table_FindsTheMacroOfTheNameTheReceiverAndTheCount(string function, int count, bool isReceiver, int found)
    {
        Macro? macro = WithTheSet.Macros.Find(function, count, isReceiver);

        Assert.NotNull(macro);
        Assert.Equal((function, found), (macro.Name, macro.ArgumentCount));
    }

    [Theory]
    [InlineData("filter", 3, true)]
    [InlineData("all", 4, true)]
    [InlineData("all", 3, false)]
    [InlineData("transformList", 2, true)]
    [InlineData("transformList", 5, true)]
    [InlineData("transformList", 3, false)]
    [InlineData("transformMap", 2, true)]
    [InlineData("transformMapEntry", 5, true)]
    public static void Table_HasNoMacroForAnotherCountOrAGlobalCall(string function, int count, bool isReceiver)
    {
        Assert.Null(WithTheSet.Macros.Find(function, count, isReceiver));
    }

    // The set adds ten entries and no key of them is a key of a standard macro or of another entry: the table of the union is made.
    [Fact]
    public static void MacrosOfTheSet_HaveKeysThatDifferFromEachOtherAndFromTheStandardOnes()
    {
        Assert.Equal(10, ExtensionMacros.TwoVariableComprehensions.Length);
        Assert.Equal(MacroTable.Standard.Macros.Length + 10, WithTheSet.Macros.Macros.Length);
        Assert.All(ExtensionMacros.TwoVariableComprehensions, macro => Assert.True(macro.IsReceiver && macro.Namespace is null));
    }

    // The standard forms keep one variable: the second name of their comprehension is null, and so does the tree of the macros
    // of one variable in an environment that has the set.
    [Theory]
    [InlineData("xs.all(x, true)")]
    [InlineData("xs.exists(x, true)")]
    [InlineData("xs.exists_one(x, true)")]
    [InlineData("xs.map(x, x)")]
    [InlineData("xs.map(x, true, x)")]
    [InlineData("xs.filter(x, true)")]
    public static void StandardMacro_KeepsOneVariable(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParsedExpression parsed = Parse(text);

        ComprehensionNode loop = Assert.IsType<ComprehensionNode>(parsed.Root);

        Assert.Equal("x", loop.IterVar);
        Assert.Null(loop.IterVar2);
    }

    // The macros of two variables apply to a receiver of any kind: a literal, a selection, a call, a nested comprehension.
    [Theory]
    [InlineData("[1, 2].all(i, v, true)")]
    [InlineData("{'a': 1}.transformMap(i, v, v)")]
    [InlineData("a.b.transformList(i, v, v)")]
    [InlineData("f(x).exists(i, v, true)")]
    [InlineData("xs.map(y, y).transformMapEntry(i, v, x)")]
    public static void Receiver_OfAnyKind_ExpandsTheMacro(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParsedExpression parsed = Parse(text);

        ComprehensionNode loop = Assert.IsType<ComprehensionNode>(parsed.Root);

        Assert.Equal(("i", "v"), (loop.IterVar, loop.IterVar2));
    }

    // A call without a receiver is no macro, and without the set the calls of three arguments are calls of functions.
    [Theory]
    [InlineData("transformList(xs, i, v)")]
    [InlineData("all(xs, i, v)")]
    public static void CallWithoutAReceiver_StaysACall(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParsedExpression parsed = Parse(text);

        CallNode call = Assert.IsType<CallNode>(parsed.Root);

        Assert.Null(call.Target);
        Assert.Null(parsed.GetMacroCall(call.Id));
    }

    [Theory]
    [InlineData("xs.all(i, v, true)", "all", 3)]
    [InlineData("xs.transformList(i, v, i)", "transformList", 3)]
    [InlineData("xs.transformMap(i, v, true, i)", "transformMap", 4)]
    public static void WithoutTheSet_TheCallStaysACall(string text, string function, int count)
    {
        ArgumentNullException.ThrowIfNull(text);
        ParseResult result = TwoVarEnvironment.WithoutTheSet.Parse(text);

        Assert.True(result.IsSuccess);
        CallNode call = Assert.IsType<CallNode>(result.Expression!.Root);
        Assert.Equal((function, count), (call.Function, call.Arguments.Length));
        Assert.NotNull(call.Target);
        Assert.Null(result.Expression.GetMacroCall(call.Id));
    }

    // An error of the names stops the expansion of that call alone: the other macro calls of the text are expanded, and the one that
    // failed is reported once, the first fault of its names.
    [Fact]
    public static void CallWithBadNames_IsOneErrorAndOthersAreStillExpanded()
    {
        ParseResult result = TwoVarEnvironment.Environment.Parse("xs.all(1, v, true) && ys.all(i, i, true)");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.Errors.Length);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorCode.InvalidMacroCall, error.Code));
    }

    private static ParsedExpression Parse(string text)
    {
        ParseResult result = Parser.Parse(text, WithTheSet);
        Assert.True(result.IsSuccess, text + ": " + string.Join("; ", result.Errors));
        return result.Expression;
    }

    // The comprehension as "comprehension(<variables>, <range>, <accumulator>, <init>, <condition>, <step>, <result>)", and the other nodes
    // as the text of the checks of the shapes writes them.
    private static string Shape(ExprNode node) => node switch
    {
        ConstantNode { Value.Kind: CelValueKind.Bool } constant => constant.Value.TryGetBoolean(out bool flag) && flag ? "true" : "false",
        ConstantNode constant => constant.Value.TryGetInt64(out long number) ? number.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("constant"),
        IdentNode ident => ident.Name,
        CallNode call => call.Function + "(" + string.Join(", ", call.Arguments.Select(Shape)) + ")",
        ListNode list => "[" + string.Join(", ", list.Elements.Select(Shape)) + "]",
        StructNode { Entries.Length: 0 } => "{}",
        ComprehensionNode loop => "comprehension(" + string.Join(
            ", ",
            loop.IterVar,
            loop.IterVar2 ?? "-",
            Shape(loop.IterRange),
            loop.AccuVar,
            Shape(loop.AccuInit),
            Shape(loop.LoopCondition),
            Shape(loop.LoopStep),
            Shape(loop.Result)) + ")",
        _ => throw new ArgumentException(node.GetType().Name, nameof(node)),
    };

    // The ids of a node and of all the nodes below it.
    private static IEnumerable<int> Ids(ExprNode node)
    {
        IEnumerable<int> below = node switch
        {
            CallNode call => call.Arguments.SelectMany(Ids),
            ListNode list => list.Elements.SelectMany(Ids),
            SelectNode select => Ids(select.Operand),
            ComprehensionNode loop => new[] { loop.IterRange, loop.AccuInit, loop.LoopCondition, loop.LoopStep, loop.Result }.SelectMany(Ids),
            _ => [],
        };
        return new[] { node.Id }.Concat(below);
    }

    private static TheoryData<string, string> RowsOf(string rows)
    {
        TheoryData<string, string> data = [];
        foreach (string row in rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0].Trim(), cells[1].Trim());
        }

        return data;
    }
}
