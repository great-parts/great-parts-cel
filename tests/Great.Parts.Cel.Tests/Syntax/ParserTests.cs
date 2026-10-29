using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

public sealed class ParserTests
{
    // "<expression> → <tree>": every node as "<node>@<offset>". A call is "<function>@<offset>(<arguments>)", a list
    // "[<elements>]@<offset>", a map "{<key> :@<offset> <value>, ...}@<offset>" with the offset of each entry after
    // its colon. Constants: int as is, uint with u, double with d, strings in double quotes, bytes as b"<hex>".
    private const string Trees = """""""
        x → x@0
        f_unknown(17) → f_unknown@9(17@10)
        f() → f@1()
        f(1, x, 'a') → f@1(1@2, x@5, "a"@8)
        g(f(1)) → g@1(f@3(1@4))
        1 + 1 → _+_@2(1@0, 1@4)
        1 + 2 + 3 → _+_@6(_+_@2(1@0, 2@4), 3@8)
        1 / 2 / 3 → _/_@6(_/_@2(1@0, 2@4), 3@8)
        1 + 2 + 3 + 4 → _+_@10(_+_@6(_+_@2(1@0, 2@4), 3@8), 4@12)
        1 / 2 / 3 / 4 → _/_@10(_/_@6(_/_@2(1@0, 2@4), 3@8), 4@12)
        1 + 2 / 3 → _+_@2(1@0, _/_@6(2@4, 3@8))
        1 / 2 + 3 → _+_@6(_/_@2(1@0, 2@4), 3@8)
        (1 + 2) / 3 → _/_@8(_+_@3(1@1, 2@5), 3@10)
        ((x)) → x@2
        a || b → _||_@2(a@0, b@5)
        a || b || c → _||_@7(_||_@2(a@0, b@5), c@10)
        a || b || c || d → _||_@7(_||_@2(a@0, b@5), _||_@12(c@10, d@15))
        a || b || c || d || e → _||_@12(_||_@7(_||_@2(a@0, b@5), c@10), _||_@17(d@15, e@20))
        a || b || c || d || e || f → _||_@12(_||_@7(_||_@2(a@0, b@5), c@10), _||_@22(_||_@17(d@15, e@20), f@25))
        a || (b || c) → _||_@2(a@0, _||_@8(b@6, c@11))
        x || true → _||_@2(x@0, true@5)
        a || b + c → _||_@2(a@0, _+_@7(b@5, c@9))
        a + b || c → _||_@6(_+_@2(a@0, b@4), c@9)
        -x → -_@0(x@1)
        -1 → -1@0
        - 1 → -1@0
        -2.5 → -2.5d@0
        -1u → -_@0(1u@1)
        -(1) → -_@0(1@2)
        -'a' → -_@0("a"@1)
        1 + -2 → _+_@2(1@0, -2@4)
        --x → x@2
        ---x → -_@0(x@3)
        --1 → 1@2
        ---1 → -_@0(1@3)
        - -1 → 1@3
        -(-1) → -_@0(-1@2)
        [] → []@0
        [-1] → [-1@1]@0
        [1, 2,] → [1@1, 2@4]@0
        [,] → []@0
        [17, 'pancakes'] → [17@1, "pancakes"@5]@0
        {} → {}@0
        {,} → {}@0
        {"k":"v"} → {"k"@1 :@4 "v"@5}@0
        {"k1":"v1","k":"v"} → {"k1"@1 :@5 "v1"@6, "k"@11 :@14 "v"@15}@0
        {1: 2,} → {1@1 :@2 2@4}@0
        {[x]: {}} → {[x@2]@1 :@4 {}@6}@0
        [0x10, 2u, 3.5, b'a', null, false, true] → [16@1, 2u@7, 3.5d@11, b"61"@16, null@22, false@28, true@35]@0
        """"""";

    public static TheoryData<string, string> TreeCases => CaseTable.Parse(Trees);

    // "<expression> → <errors>": every error as "<code>@<offset> <message>", separated by " | ".
    private const string Errors = """""""
        1 + → SyntaxError@3 syntax error: expected an operand, found the end of the text
        (1 + 2 → SyntaxError@6 syntax error: expected ')', found the end of the text
        1 2 → SyntaxError@2 syntax error: expected the end of the text, found '2'
        1 # 2 → SyntaxError@2 syntax error: unexpected character '#'
        'abc → SyntaxError@0 syntax error: unterminated string literal
        [1, 2 → SyntaxError@5 syntax error: expected ']', found the end of the text
        a || → SyntaxError@4 syntax error: expected an operand, found the end of the text
        x.y → SyntaxError@1 syntax error: expected the end of the text, found '.'
        1 - 2 → SyntaxError@2 syntax error: expected the end of the text, found '-'
        ) → SyntaxError@0 syntax error: expected an operand, found ')'
        in → SyntaxError@0 syntax error: expected an operand, found 'in'
        true(1) → SyntaxError@4 syntax error: expected the end of the text, found '('
        f(x)(1) → SyntaxError@4 syntax error: expected the end of the text, found '('
        x{} → SyntaxError@1 syntax error: expected the end of the text, found '{'
        f(1,) → SyntaxError@4 syntax error: expected an operand, found ')'
        f(,) → SyntaxError@2 syntax error: expected an operand, found ','
        f(1 2) → SyntaxError@4 syntax error: expected ')', found '2'
        f(1 # 2) → SyntaxError@4 syntax error: unexpected character '#'
        [1 2] → SyntaxError@3 syntax error: expected ']', found '2'
        [1,,] → SyntaxError@3 syntax error: expected an operand, found ','
        [,1] → SyntaxError@2 syntax error: expected ']', found '1'
        [,,] → SyntaxError@2 syntax error: expected ']', found ','
        {1 2} → SyntaxError@3 syntax error: expected ':', found '2'
        {1} → SyntaxError@2 syntax error: expected ':', found '}'
        {1:} → SyntaxError@3 syntax error: expected an operand, found '}'
        {1: 2 3} → SyntaxError@6 syntax error: expected '}', found '3'
        {,1: 2} → SyntaxError@2 syntax error: expected '}', found '1'
        [(1 2] → SyntaxError@4 syntax error: expected ')', found '2'
        [1 +, 99999999999999999999] → SyntaxError@4 syntax error: expected an operand, found ','
        f(1 +, 99999999999999999999) → SyntaxError@5 syntax error: expected an operand, found ','
        {1 +: 99999999999999999999} → SyntaxError@4 syntax error: expected an operand, found ':'
        99999999999999999999 + 1e999 → InvalidLiteral@0 invalid int literal: out of range | InvalidLiteral@23 invalid double literal: the value overflows to infinity
        [99999999999999999999, 1 2] → InvalidLiteral@1 invalid int literal: out of range | SyntaxError@25 syntax error: expected ']', found '2'
        '\q' 2 → InvalidLiteral@0 invalid string literal: unknown escape sequence '\q' | SyntaxError@5 syntax error: expected the end of the text, found '2'
        -99999999999999999999 → InvalidLiteral@0 invalid int literal: out of range
        --9223372036854775808 → InvalidLiteral@2 invalid int literal: out of range
        """"""";

    public static TheoryData<string, string> ErrorCases => CaseTable.Parse(Errors);

    [Theory]
    [MemberData(nameof(TreeCases))]
    public static void Expression_ParsesIntoItsTree(string expression, string tree)
    {
        ParseResult result = Parser.Parse(expression);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(tree, Format(result.Expression.Root, result.Expression));
    }

    // Ids run from 1 in the order of creation: operands before the call, keys and values before their entry.
    [Theory]
    [InlineData("x", "1")]
    [InlineData("1 + 2 + 3", "5 4 1 2 3")]
    [InlineData("a || b || c || d", "7 5 1 2 6 3 4")]
    [InlineData("f(1, -x)", "4 1 3 2")]
    [InlineData("{\"k\": [1]}", "5 4 1 3 2")]
    public static void Nodes_HaveIdsInTheOrderOfCreation(string expression, string ids)
    {
        ParsedExpression parsed = Parsed(expression);

        Assert.Equal(ids, string.Join(" ", Ids(parsed.Root)));
    }

    [Fact]
    public static void Positions_AreRecordedById()
    {
        ParsedExpression parsed = Parsed("{\"k\": [1]}");

        Assert.Equal("1 7 6 4 0", string.Join(" ", Enumerable.Range(1, 5).Select(parsed.GetOffset)));
    }

    [Fact]
    public static void LineTable_GivesTheLinesAndColumnsOfNodes()
    {
        ParsedExpression parsed = Parsed("1 +\r\n  f(\n x)");
        var call = (CallNode)parsed.Root;

        CelSourceLocation location = parsed.Lines.GetLocation(parsed.GetOffset(call.Arguments[1].Id));

        Assert.Equal((8, 2, 4), (location.Offset, location.Line, location.Column));
        location = parsed.Lines.GetLocation(parsed.GetOffset(((CallNode)call.Arguments[1]).Arguments[0].Id));
        Assert.Equal((11, 3, 2), (location.Offset, location.Line, location.Column));
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_IsReportedInTheOrderFound(string expression, string errors)
    {
        ParseResult result = Parser.Parse(expression);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Expression);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
        Assert.Equal(errors, string.Join(" | ", result.Errors.Select(FormatError)));
    }

    [Theory]
    [InlineData("", 0, 1, 1)]
    [InlineData("  ", 2, 1, 3)]
    [InlineData("[1,\n2", 5, 2, 2)]
    [InlineData("1 +\r\n)", 5, 2, 1)]
    public static void SyntaxError_HasItsLineAndColumn(string expression, int offset, int line, int column)
    {
        ParseResult result = Parser.Parse(expression);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.SyntaxError, offset, line, column, (string?)null, (CelSourceLocation?)null, (Exception?)null),
            (error.Code, error.Location.Offset, error.Location.Line, error.Location.Column, error.Location.SourceName, error.SourceLocation, error.Exception));
    }

    // A lone surrogate is an invalid literal, and parsing goes on after it.
    [Fact]
    public static void LoneSurrogate_IsInvalidLiteralAndParsingGoesOn()
    {
        ParseResult result = Parser.Parse("['\uD800', b'\uDC00', 99999999999999999999]");

        Assert.Equal(
            "InvalidLiteral@1 invalid string literal: the text has a lone surrogate"
                + " | InvalidLiteral@6 invalid bytes literal: the text has a lone surrogate"
                + " | InvalidLiteral@12 invalid int literal: out of range",
            string.Join(" | ", result.Errors.Select(FormatError)));
    }

    [Fact]
    public static void EnvironmentParse_GivesTheTreeOrTheErrors()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        ParseResult success = environment.Parse("f(x) + 1");
        ParseResult failure = environment.Parse("1 +");

        Assert.True(success.IsSuccess);
        Assert.Equal("_+_@5(f@1(x@2), 1@7)", Format(success.Expression.Root, success.Expression));
        Assert.Equal(
            "SyntaxError@3 syntax error: expected an operand, found the end of the text",
            FormatError(Assert.Single(failure.Errors)));
    }

    [Fact]
    public static void EnvironmentParse_OfNull_Throws()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => environment.Parse(null!));

        Assert.Equal("expression", exception.ParamName);
    }

    private static ParsedExpression Parsed(string expression)
    {
        ParseResult result = Parser.Parse(expression);
        Assert.True(result.IsSuccess);
        return result.Expression;
    }

    private static string FormatError(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{error.Code}@{error.Location.Offset} {error.Message}");

    private static string Format(ExprNode node, ParsedExpression parsed)
    {
        string at = "@" + parsed.GetOffset(node.Id).ToString(CultureInfo.InvariantCulture);
        switch (node)
        {
            case ConstantNode constant:
                return FormatValue(constant.Value) + at;
            case IdentNode ident:
                return ident.Name + at;
            case CallNode call:
                Assert.Null(call.Target);
                return call.Function + at + "(" + string.Join(", ", call.Arguments.Select(a => Format(a, parsed))) + ")";
            case ListNode list:
                return "[" + string.Join(", ", list.Elements.Select(e => Format(e, parsed))) + "]" + at;
            default:
                var map = (StructNode)node;
                IEnumerable<string> entries = map.Entries.Select(e =>
                    Format(e.Key, parsed) + " :@" + parsed.GetOffset(e.Id).ToString(CultureInfo.InvariantCulture) + " " + Format(e.Value, parsed));
                return map.MessageName + "{" + string.Join(", ", entries) + "}" + at;
        }
    }

    // The ids of the nodes and entries in pre-order: a node, then its children in the order of the text.
    private static IEnumerable<int> Ids(ExprNode node)
    {
        IEnumerable<int> children = node switch
        {
            CallNode call => call.Arguments.SelectMany(Ids),
            ListNode list => list.Elements.SelectMany(Ids),
            StructNode map => map.Entries.SelectMany(e => new[] { e.Id }.Concat(Ids(e.Key)).Concat(Ids(e.Value))),
            _ => [],
        };
        return new[] { node.Id }.Concat(children);
    }

    private static string FormatValue(CelValue value) => value.Kind switch
    {
        CelValueKind.Null => "null",
        CelValueKind.Bool => value.TryGetBoolean(out bool b) && b ? "true" : "false",
        CelValueKind.Int => string.Create(CultureInfo.InvariantCulture, $"{(value.TryGetInt64(out long i) ? i : 0)}"),
        CelValueKind.UInt => string.Create(CultureInfo.InvariantCulture, $"{(value.TryGetUInt64(out ulong u) ? u : 0)}u"),
        CelValueKind.Double => (value.TryGetDouble(out double d) ? d : 0).ToString("R", CultureInfo.InvariantCulture) + "d",
        CelValueKind.String => "\"" + (value.TryGetString(out string? s) ? s : "") + "\"",
        _ => "b\"" + Convert.ToHexString((value.TryGetBytes(out ReadOnlyMemory<byte> m) ? m : default).Span) + "\"",
    };
}
