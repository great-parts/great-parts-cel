using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The expansion of macros (docs/design/syntax.md, "Macros"): the shape of parser/macro.go of cel-go with the hidden
// accumulator, the record of the call, InvalidMacroCall, and macros turned off by the syntax options.
public sealed class MacroTests
{
    private static readonly SyntaxOptions WithoutMacros = new(OptionalSyntax: false, MacroTable.Empty);

    private static readonly SyntaxOptions WithOptionalSyntax = SyntaxOptions.Standard with { OptionalSyntax = true };

    // "<expression> → <tree>": every node as "<node>@<offset>". A comprehension is
    // "comprehension(<IterVar>, <IterRange>, <AccuVar>, <AccuInit>, <LoopCondition>, <LoopStep>, <Result>)", a presence
    // test "present(<operand>.<field>)", a call "<function>(<arguments>)" with "<target>." before it for a receiver call.
    private const string Expansions = """""""
        xs.all(e, e < 3) → comprehension(e, xs@0, @result, true@6, @not_strictly_false(@result@6)@6, _&&_(@result@6, _<_(e@10, 3@14)@12)@6, @result@6)@6
        m.exists(k, k == "a") → comprehension(k, m@0, @result, false@8, @not_strictly_false(!_(@result@8)@8)@8, _||_(@result@8, _==_(k@12, "a"@17)@14)@8, @result@8)@8
        xs.exists_one(e, e != 0) → comprehension(e, xs@0, @result, 0@13, true@13, _?_:_(_!=_(e@17, 0@22)@19, _+_(@result@13, 1@13)@13, @result@13)@13, _==_(@result@13, 1@13)@13)@13
        xs.map(e, e + 1) → comprehension(e, xs@0, @result, []@6, true@6, _+_(@result@6, [_+_(e@10, 1@14)@12]@6)@6, @result@6)@6
        xs.map(e, e > 1, e - 1) → comprehension(e, xs@0, @result, []@6, true@6, _?_:_(_>_(e@10, 1@14)@12, _+_(@result@6, [_-_(e@17, 1@21)@19]@6)@6, @result@6)@6, @result@6)@6
        xs.filter(e, e % 2 == 0) → comprehension(e, xs@0, @result, []@9, true@9, _?_:_(_==_(_%_(e@13, 2@17)@15, 0@22)@19, _+_(@result@9, [e@10]@9)@9, @result@9)@9, @result@9)@9
        has(msg.field) → present(msg@4.field)@3
        has(a.b.c) → present(a@4.b@5.c)@3
        has(m.f) || x → _||_(present(m@4.f)@3, x@12)@9
        f(x).map(e, e).size() → comprehension(e, f(x@2)@1, @result, []@8, true@8, _+_(@result@8, [e@12]@8)@8, @result@8)@8.size()@19
        xs.map(e, ys.filter(f, f == e)) → comprehension(e, xs@0, @result, []@6, true@6, _+_(@result@6, [comprehension(f, ys@10, @result, []@19, true@19, _?_:_(_==_(f@23, e@28)@25, _+_(@result@19, [f@20]@19)@19, @result@19)@19, @result@19)@19]@6)@6, @result@6)@6
        """"""";

    // Calls that differ from every macro in name, argument count or style stay calls.
    private const string Calls = """""""
        xs.all(e) → xs@0.all(e@7)@6
        xs.map(a, b, c, d) → xs@0.map(a@7, b@10, c@13, d@16)@6
        all(xs, e, true) → all(xs@4, e@8, true@11)@3
        has(a.b, c) → has(a@4.b@5, c@9)@3
        xs.has(a.b) → xs@0.has(a@7.b@8)@6
        .has(a.b) → .has(a@5.b@6)@4
        xs.exist(e, true) → xs@0.exist(e@9, true@12)@8
        """"""";

    public static TheoryData<string, string> ExpansionCases => CaseTable.Parse(Expansions);

    public static TheoryData<string, string> CallCases => CaseTable.Parse(Calls);

    [Theory]
    [MemberData(nameof(ExpansionCases))]
    public static void MacroCall_ExpandsIntoTheShapeOfCelGo(string expression, string tree)
    {
        ParsedExpression parsed = Parsed(expression, null);

        Assert.Equal(tree, Format(parsed.Root, parsed));
    }

    [Theory]
    [MemberData(nameof(CallCases))]
    public static void CallThatIsNoMacro_StaysACall(string expression, string tree)
    {
        ParsedExpression parsed = Parsed(expression, null);

        Assert.Equal(tree, Format(parsed.Root, parsed));
    }

    // Without macros every macro call is a call.
    [Theory]
    [InlineData("has(a.b)", "has(a@4.b@5)@3")]
    [InlineData("xs.all(e, true)", "xs@0.all(e@7, true@10)@6")]
    [InlineData("xs.map(e, e, e)", "xs@0.map(e@7, e@10, e@13)@6")]
    public static void MacroCall_WithoutMacros_IsACall(string expression, string tree)
    {
        ParsedExpression parsed = Parsed(expression, WithoutMacros);

        Assert.Equal(tree, Format(parsed.Root, parsed));
    }

    // Every node the expansion creates is at the "(" of the call, also on another line than the name of the macro.
    [Fact]
    public static void Expansion_IsAtTheParenthesisOfTheCall()
    {
        ParsedExpression parsed = Parsed("[1]\n  .all(e,\n  true)", null);

        Assert.Equal(
            "comprehension(e, [1@1]@0, @result, true@10, @not_strictly_false(@result@10)@10, _&&_(@result@10, true@16)@10, @result@10)@10",
            Format(parsed.Root, parsed));
        Assert.Equal((2, 7), (parsed.Lines.GetLocation(10).Line, parsed.Lines.GetLocation(10).Column));
    }

    // The parts are created after the arguments, in the order of the columns of the table, each after its children;
    // the comprehension comes last. The identifier node of the first argument, id 2, is dropped and leaves a gap.
    [Fact]
    public static void Expansion_HasIdsInTheOrderOfCreation()
    {
        ParsedExpression parsed = Parsed("xs.all(e, e < 3)", null);

        Assert.Equal("12 1 6 8 7 10 9 5 3 4 11", string.Join(" ", Ids(parsed.Root)));
    }

    // The call is recorded as written under the id of its expansion; the target and the arguments are the nodes of
    // the call, the dropped identifier included.
    [Fact]
    public static void Expansion_RecordsTheCallAsWritten()
    {
        ParsedExpression parsed = Parsed("xs.exists(e, e > 0)", null);
        var comprehension = (ComprehensionNode)parsed.Root;

        MacroCall? call = parsed.GetMacroCall(comprehension.Id);

        Assert.NotNull(call);
        Assert.Equal("exists", call.Function);
        Assert.Same(comprehension.IterRange, call.Target);
        Assert.Equal(2, call.Arguments.Length);
        Assert.Equal((2, "e"), (call.Arguments[0].Id, ((IdentNode)call.Arguments[0]).Name));
        Assert.Same(((CallNode)comprehension.LoopStep).Arguments[1], call.Arguments[1]);
        Assert.Null(parsed.GetMacroCall(comprehension.IterRange.Id));
    }

    // has records its argument, the selection that the presence test replaces; a nested macro call is recorded under
    // the id of its own expansion, which is an argument of the outer record.
    [Fact]
    public static void NestedAndGlobalMacroCalls_AreEachRecorded()
    {
        ParsedExpression parsed = Parsed("xs.all(e, has(e.f))", null);
        var outer = (ComprehensionNode)parsed.Root;
        ExprNode presence = ((CallNode)outer.LoopStep).Arguments[1];

        MacroCall? outerCall = parsed.GetMacroCall(outer.Id);
        MacroCall? innerCall = parsed.GetMacroCall(presence.Id);

        Assert.NotNull(outerCall);
        Assert.NotNull(innerCall);
        Assert.Same(presence, outerCall.Arguments[1]);
        Assert.Equal(("has", (ExprNode?)null), (innerCall.Function, innerCall.Target));
        SelectNode written = Assert.IsType<SelectNode>(Assert.Single(innerCall.Arguments));
        Assert.False(written.IsPresenceTest);
        Assert.Same(written.Operand, ((SelectNode)presence).Operand);
    }

    [Theory]
    [InlineData("xs.all(e.f, true)", "all", "argument must be a simple name", 6, 1, 7)]
    [InlineData("xs.exists(1, true)", "exists", "argument must be a simple name", 9, 1, 10)]
    [InlineData("xs.exists_one(f(e), true)", "exists_one", "argument must be a simple name", 13, 1, 14)]
    [InlineData("xs.map(\"e\", e)", "map", "argument must be a simple name", 6, 1, 7)]
    [InlineData("xs.map([e], true, e)", "map", "argument must be a simple name", 6, 1, 7)]
    [InlineData("xs.filter(-e, true)", "filter", "argument must be a simple name", 9, 1, 10)]
    [InlineData("xs\n  .map(__result__, 1)", "map", "iteration variable overwrites accumulator variable", 9, 2, 7)]
    [InlineData("xs.all(__result__, true)", "all", "iteration variable overwrites accumulator variable", 6, 1, 7)]
    [InlineData("has(x)", "has", "argument must be a field selection", 3, 1, 4)]
    [InlineData("1 +\r\nhas(x[0])", "has", "argument must be a field selection", 8, 2, 4)]
    [InlineData("has(f(x))", "has", "argument must be a field selection", 3, 1, 4)]
    [InlineData("has(x.f())", "has", "argument must be a field selection", 3, 1, 4)]
    public static void InvalidArguments_AreInvalidMacroCallAtTheParenthesis(
        string expression, string macro, string detail, int offset, int line, int column)
    {
        CelError error = Assert.Single(Parser.Parse(expression).Errors);

        Assert.Equal(
            (CelErrorCode.InvalidMacroCall, CelErrorStage.Parse, "invalid call of macro '" + macro + "': " + detail, offset, line, column),
            (error.Code, error.Stage, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // An optional selection is a call, not a selection.
    [Fact]
    public static void HasOfAnOptionalSelection_IsInvalidMacroCall()
    {
        CelError error = Assert.Single(Parser.Parse("has(x.?f)", WithOptionalSyntax).Errors);

        Assert.Equal("InvalidMacroCall@3 invalid call of macro 'has': argument must be a field selection", Format(error));
    }

    // InvalidMacroCall is found after the errors of the arguments, but is listed by its offset, before them; parsing
    // goes on after it.
    [Theory]
    [InlineData("has(x y)", "InvalidMacroCall@3 SyntaxError@6")]
    [InlineData("xs.all(if, x y) + has(z)", "ReservedIdentifier@7 SyntaxError@13 InvalidMacroCall@21")]
    [InlineData("xs.all(e.f, [1 2]) || ys.map(1, 2)", "InvalidMacroCall@6 SyntaxError@15 InvalidMacroCall@28")]
    public static void InvalidMacroCall_IsListedByItsOffset(string expression, string errors)
    {
        Assert.Equal(errors, string.Join(" ", Parser.Parse(expression).Errors.Select(error => error.Code + "@" + Offset(error))));
    }

    // A macro has the syntax tree depth of the call it is written as: a receiver macro counts, has does not.
    [Theory]
    [InlineData(249, ".all(e, true)", true)]
    [InlineData(250, ".all(e, true)", false)]
    public static void Macro_CountsAsItsCallInTheSyntaxTreeDepth(int selections, string tail, bool parses)
    {
        string text = "x" + string.Concat(Enumerable.Repeat(".a", selections)) + tail;
        ParseResult result = Parser.Parse(text);

        Assert.Equal(parses, result.IsSuccess);
        if (!parses)
        {
            Assert.Equal(
                "NestingTooDeep@" + (1 + (2 * selections) + 4).ToString(CultureInfo.InvariantCulture) + " syntax tree depth exceeds the limit of 250",
                Format(Assert.Single(result.Errors)));
        }
    }

    // has over a selection 250 deep stays within the limit: the global call adds no depth.
    [Fact]
    public static void HasOfTheDeepestSelection_IsParsed()
    {
        Assert.True(Parser.Parse("has(x" + string.Concat(Enumerable.Repeat(".a", 250)) + ")").IsSuccess);
    }

    // An error at the offset of an earlier one is not reported: the call left unexpanded by InvalidMacroCall is the
    // first node deeper than the limit, at the same "(".
    [Fact]
    public static void TooDeepInvalidMacroCall_IsReportedOnce()
    {
        string text = "x" + string.Concat(Enumerable.Repeat(".a", 250)) + ".all(1, true)";

        CelError error = Assert.Single(Parser.Parse(text).Errors);

        Assert.Equal("InvalidMacroCall@505 invalid call of macro 'all': argument must be a simple name", Format(error));
    }

    private static ParsedExpression Parsed(string expression, SyntaxOptions? syntax)
    {
        ParseResult result = Parser.Parse(expression, syntax);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(Format)));
        return result.Expression;
    }

    private static string Format(CelError error) => error.Code + "@" + Offset(error) + " " + error.Message;

    private static string Offset(CelError error) => error.Location.Offset.ToString(CultureInfo.InvariantCulture);

    private static string Format(ExprNode node, ParsedExpression parsed)
    {
        string text = node switch
        {
            ConstantNode constant => FormatValue(constant.Value),
            IdentNode ident => ident.Name,
            SelectNode { IsPresenceTest: true } select => "present(" + Format(select.Operand, parsed) + "." + select.Field + ")",
            SelectNode select => Format(select.Operand, parsed) + "." + select.Field,
            CallNode { Target: { } target } call => Format(target, parsed) + "." + call.Function + "(" + Join(call.Arguments, parsed) + ")",
            CallNode call => call.Function + "(" + Join(call.Arguments, parsed) + ")",
            ListNode list => "[" + Join(list.Elements, parsed) + "]",
            ComprehensionNode loop => "comprehension(" + string.Join(
                ", ",
                loop.IterVar,
                Format(loop.IterRange, parsed),
                loop.AccuVar,
                Format(loop.AccuInit, parsed),
                Format(loop.LoopCondition, parsed),
                Format(loop.LoopStep, parsed),
                Format(loop.Result, parsed)) + ")",
            _ => throw new ArgumentException(node.GetType().Name, nameof(node)),
        };
        return text + "@" + parsed.GetOffset(node.Id).ToString(CultureInfo.InvariantCulture);
    }

    private static string Join(ImmutableArray<ExprNode> nodes, ParsedExpression parsed) =>
        string.Join(", ", nodes.Select(node => Format(node, parsed)));

    // The ids in pre-order: a node, then its children; the parts of a comprehension in the order of the table.
    private static IEnumerable<int> Ids(ExprNode node)
    {
        IEnumerable<int> children = node switch
        {
            SelectNode select => Ids(select.Operand),
            CallNode call => (call.Target is null ? [] : Ids(call.Target)).Concat(call.Arguments.SelectMany(Ids)),
            ListNode list => list.Elements.SelectMany(Ids),
            ComprehensionNode loop => new[] { loop.IterRange, loop.AccuInit, loop.LoopCondition, loop.LoopStep, loop.Result }.SelectMany(Ids),
            _ => [],
        };
        return new[] { node.Id }.Concat(children);
    }

    private static string FormatValue(CelValue value) => value.Kind switch
    {
        CelValueKind.Bool => value.TryGetBoolean(out bool b) && b ? "true" : "false",
        CelValueKind.Int => (value.TryGetInt64(out long i) ? i : 0).ToString(CultureInfo.InvariantCulture),
        CelValueKind.String => "\"" + (value.TryGetString(out string? s) ? s : "") + "\"",
        _ => throw new ArgumentException(value.Kind.ToString(), nameof(value)),
    };
}
