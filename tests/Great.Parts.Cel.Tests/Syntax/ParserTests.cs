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
#if NET11_0_OR_GREATER
    private const string DecimalNotEnabled =
        "decimal literal is not enabled: enable decimal literals in the environment builder";
#else
    private const string DecimalNotEnabled = "decimal literal is not enabled: decimal requires .NET 11";
#endif

    private const string OptionalNotEnabled = " is not enabled: add the extension set optional";

    private static readonly SyntaxOptions WithOptionalSyntax = SyntaxOptions.Standard with { OptionalSyntax = true };

    // "<expression> → <tree>": every node as "<node>@<offset>". A call is "<function>@<offset>(<arguments>)", with
    // "<target>." before it for a receiver call; a selection "<operand>.<field>@<offset>"; a list
    // "[<elements>]@<offset>"; a map or a message "<name>{<key or field> :@<offset> <value>, ...}@<offset>" with the
    // offset of each entry after its colon. An optional element or entry has "?" before it. Constants: int as is, uint
    // with u, double with d, strings in double quotes, bytes as b"<hex>".
    private const string Trees = """""""
        x → x@0
        f_unknown(17) → f_unknown@9(17@10)
        f() → f@1()
        f(1, x, 'a') → f@1(1@2, x@5, "a"@8)
        g(f(1)) → g@1(f@3(1@4))
        As → As@0
        iff → iff@0
        a ? b : c → _?_:_@2(a@0, b@4, c@8)
        a ? b : c ? d : e → _?_:_@2(a@0, b@4, _?_:_@10(c@8, d@12, e@16))
        (a ? b : c) ? d : e → _?_:_@12(_?_:_@3(a@1, b@5, c@9), d@14, e@18)
        a || b ? c : d → _?_:_@7(_||_@2(a@0, b@5), c@9, d@13)
        a ? b || c : d && e → _?_:_@2(a@0, _||_@6(b@4, c@9), _&&_@15(d@13, e@18))
        a || b → _||_@2(a@0, b@5)
        a || b || c → _||_@7(_||_@2(a@0, b@5), c@10)
        a || b || c || d → _||_@7(_||_@2(a@0, b@5), _||_@12(c@10, d@15))
        a || b || c || d || e → _||_@12(_||_@7(_||_@2(a@0, b@5), c@10), _||_@17(d@15, e@20))
        a || b || c || d || e || f → _||_@12(_||_@7(_||_@2(a@0, b@5), c@10), _||_@22(_||_@17(d@15, e@20), f@25))
        a || (b || c) → _||_@2(a@0, _||_@8(b@6, c@11))
        x || true → _||_@2(x@0, true@5)
        a && b → _&&_@2(a@0, b@5)
        a && b && c → _&&_@7(_&&_@2(a@0, b@5), c@10)
        a && b && c && d → _&&_@7(_&&_@2(a@0, b@5), _&&_@12(c@10, d@15))
        a || b && c → _||_@2(a@0, _&&_@7(b@5, c@10))
        a && b || c → _||_@7(_&&_@2(a@0, b@5), c@10)
        a < b → _<_@2(a@0, b@4)
        a <= b → _<=_@2(a@0, b@5)
        a >= b → _>=_@2(a@0, b@5)
        a > b → _>_@2(a@0, b@4)
        a == b → _==_@2(a@0, b@5)
        a != b → _!=_@2(a@0, b@5)
        a in b → @in@2(a@0, b@5)
        x in [1] → @in@2(x@0, [1@6]@5)
        a < b == c → _==_@6(_<_@2(a@0, b@4), c@9)
        a in b in c → @in@7(@in@2(a@0, b@5), c@10)
        a == b && c → _&&_@7(_==_@2(a@0, b@5), c@10)
        a || b == c → _||_@2(a@0, _==_@7(b@5, c@10))
        a + b < c → _<_@6(_+_@2(a@0, b@4), c@8)
        a < b + c → _<_@2(a@0, _+_@6(b@4, c@8))
        1 + 1 → _+_@2(1@0, 1@4)
        1 + 2 + 3 → _+_@6(_+_@2(1@0, 2@4), 3@8)
        1 / 2 / 3 → _/_@6(_/_@2(1@0, 2@4), 3@8)
        1 + 2 + 3 + 4 → _+_@10(_+_@6(_+_@2(1@0, 2@4), 3@8), 4@12)
        1 / 2 / 3 / 4 → _/_@10(_/_@6(_/_@2(1@0, 2@4), 3@8), 4@12)
        1 + 2 / 3 → _+_@2(1@0, _/_@6(2@4, 3@8))
        1 / 2 + 3 → _+_@6(_/_@2(1@0, 2@4), 3@8)
        (1 + 2) / 3 → _/_@8(_+_@3(1@1, 2@5), 3@10)
        1 - 2 → _-_@2(1@0, 2@4)
        1 -2 → _-_@2(1@0, 2@3)
        1 - 2 - 3 → _-_@6(_-_@2(1@0, 2@4), 3@8)
        1 - 2 + 3 → _+_@6(_-_@2(1@0, 2@4), 3@8)
        1 + 2 - 3 → _-_@6(_+_@2(1@0, 2@4), 3@8)
        1 - (2 - 3) → _-_@2(1@0, _-_@7(2@5, 3@9))
        1 - -2 → _-_@2(1@0, -2@4)
        2 * 3 → _*_@2(2@0, 3@4)
        7 % 3 → _%_@2(7@0, 3@4)
        1 * 2 / 3 % 4 → _%_@10(_/_@6(_*_@2(1@0, 2@4), 3@8), 4@12)
        1 + 2 * 3 - 4 → _-_@10(_+_@2(1@0, _*_@6(2@4, 3@8)), 4@12)
        1 * 2 - 3 % 4 → _-_@6(_*_@2(1@0, 2@4), _%_@10(3@8, 4@12))
        ((x)) → x@2
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
        -a * b → _*_@3(-_@0(a@1), b@5)
        -x.y → -_@0(x@1.y@2)
        -1.f → -1@0.f@2
        -x[0] → -_@0(_[_]@2(x@1, 0@3))
        !x → !_@0(x@1)
        !!x → x@2
        !!!x → !_@0(x@3)
        ! ! x → x@4
        !-1 → !_@0(-1@1)
        !!-1 → -1@2
        !-1.5 → !_@0(-1.5d@1)
        !x.y → !_@0(x@1.y@2)
        !a == b → _==_@3(!_@0(a@1), b@6)
        !(-x) → !_@0(-_@2(x@3))
        x.y → x@0.y@1
        x . y → x@0.y@2
        x.y.z → x@0.y@1.z@3
        x.if → x@0.if@1
        x.package.while → x@0.package@1.while@9
        x.`a-b` → x@0.a-b@1
        x.`a b.c/d_1` → x@0.a b.c/d_1@1
        x.f() → x@0.f@3()
        x.f ( ) → x@0.f@4()
        x.f(1, y) → x@0.f@3(1@4, y@7)
        x.y.f(1) → x@0.y@1.f@5(1@6)
        f(x).g(y) → f@1(x@2).g@6(y@7)
        x.package() → x@0.package@9()
        a.b.package(1) → a@0.b@1.package@11(1@12)
        x.f().g → x@0.f@3().g@5
        "abc".size() → "abc"@0.size@10()
        (x).y → x@1.y@3
        (x).f() → x@1.f@5()
        x[0] → _[_]@1(x@0, 0@2)
        x[0][1] → _[_]@4(_[_]@1(x@0, 0@2), 1@5)
        x[a + 1] → _[_]@1(x@0, _+_@4(a@2, 1@6))
        x.y[0].z → _[_]@3(x@0.y@1, 0@4).z@6
        x[0].f(1) → _[_]@1(x@0, 0@2).f@6(1@7)
        [1, 2][0] → _[_]@6([1@1, 2@4]@0, 0@7)
        {1: 2}[1] → _[_]@6({1@1 :@2 2@4}@0, 1@7)
        .x → .x@1
        .x.y → .x@1.y@2
        . x → .x@2
        .f(1) → .f@2(1@3)
        .a.f(1) → .a@1.f@4(1@5)
        .a.b{} → .a.b{}@4
        x{} → x{}@1
        x {} → x{}@2
        a.b.C{} → a.b.C{}@5
        a . b . C {} → a.b.C{}@10
        A{f: 1} → A{f :@3 1@5}@1
        A{f: 1, g: x,} → A{f :@3 1@5, g :@9 x@11}@1
        A{,} → A{}@1
        A{`a-b`: 1} → A{a-b :@7 1@9}@1
        A{if: 1, in2: 2} → A{if :@4 1@6, in2 :@12 2@14}@1
        if{} → if{}@2
        package.while{} → package.while{}@13
        A{b: B{}} → A{b :@3 B{}@6}@1
        A{}.f → A{}@1.f@3
        a.b{c: 1}.d → a.b{c :@5 1@7}@3.d@9
        A{}[0] → _[_]@3(A{}@1, 0@4)
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
        {a ? b : c: d} → {_?_:_@3(a@1, b@5, c@9) :@10 d@12}@0
        [0x10, 2u, 3.5, b'a', null, false, true] → [16@1, 2u@7, 3.5d@11, b"61"@16, null@22, false@28, true@35]@0
        """"""";

    public static TheoryData<string, string> TreeCases => CaseTable.Parse(Trees);

    // The optional syntax, parsed with it enabled.
    private const string OptionalTrees = """""""
        a.?b → _?._@1(a@0, "b"@3)
        a . ? b → _?._@2(a@0, "b"@6)
        a.?`b-c` → _?._@1(a@0, "b-c"@3)
        a.?if → _?._@1(a@0, "if"@3)
        a.?b.c → _?._@1(a@0, "b"@3).c@4
        a.b.?c → _?._@3(a@0.b@1, "c"@5)
        a[?0] → _[?_]@1(a@0, 0@3)
        a[?0][?1] → _[?_]@5(_[?_]@1(a@0, 0@3), 1@7)
        [?a] → [?a@2]@0
        [?a, b, ?c] → [?a@2, b@5, ?c@9]@0
        [a, ?b,] → [a@1, ?b@5]@0
        {?1: a, 2: b} → {?1@2 :@3 a@5, 2@8 :@9 b@11}@0
        A{?f: x} → A{?f :@4 x@6}@1
        A{f: x, ?`g-h`: y} → A{f :@3 x@5, ?g-h :@14 y@16}@1
        a ? [?b] : c → _?_:_@2(a@0, [?b@6]@4, c@11)
        """"""";

    public static TheoryData<string, string> OptionalTreeCases => CaseTable.Parse(OptionalTrees);

    // "<expression> → <errors>": every error as "<code>@<offset> <message>", separated by " | ".
    private const string Errors = """""""
        1 + → SyntaxError@3 syntax error: expected an operand, found the end of the text
        (1 + 2 → SyntaxError@6 syntax error: expected ')', found the end of the text
        1 2 → SyntaxError@2 syntax error: expected the end of the text, found '2'
        1 # 2 → SyntaxError@2 syntax error: unexpected character '#'
        'abc → SyntaxError@0 syntax error: unterminated string literal
        [1, 2 → SyntaxError@5 syntax error: expected ']', found the end of the text
        a || → SyntaxError@4 syntax error: expected an operand, found the end of the text
        a && → SyntaxError@4 syntax error: expected an operand, found the end of the text
        a in → SyntaxError@4 syntax error: expected an operand, found the end of the text
        a < < b → SyntaxError@4 syntax error: expected an operand, found '<'
        1 +* 2 → SyntaxError@3 syntax error: expected an operand, found '*'
        a ? b → SyntaxError@5 syntax error: expected ':', found the end of the text
        a ? b ? c : d : e → SyntaxError@6 syntax error: expected ':', found '?'
        a ? b : → SyntaxError@7 syntax error: expected an operand, found the end of the text
        ) → SyntaxError@0 syntax error: expected an operand, found ')'
        in → SyntaxError@0 syntax error: expected an operand, found 'in'
        true(1) → SyntaxError@4 syntax error: expected the end of the text, found '('
        f(x)(1) → SyntaxError@4 syntax error: expected the end of the text, found '('
        f(1,) → SyntaxError@4 syntax error: expected an operand, found ')'
        f(,) → SyntaxError@2 syntax error: expected an operand, found ',' | SyntaxError@3 syntax error: expected an operand, found ')'
        f(1 2) → SyntaxError@4 syntax error: expected ')', found '2'
        f(1 # 2) → SyntaxError@4 syntax error: unexpected character '#'
        a.f(1,) → SyntaxError@6 syntax error: expected an operand, found ')'
        -!x → SyntaxError@1 syntax error: expected an operand, found '!'
        --!x → SyntaxError@2 syntax error: expected an operand, found '!'
        !-x → SyntaxError@1 syntax error: expected an operand, found '-'
        !--1 → SyntaxError@1 syntax error: expected an operand, found '-'
        !-1u → SyntaxError@1 syntax error: expected an operand, found '-'
        a. → SyntaxError@2 syntax error: expected a field name, found the end of the text
        a.true → SyntaxError@2 syntax error: expected a field name, found 'true'
        a.in → SyntaxError@2 syntax error: expected a field name, found 'in'
        a.1 → SyntaxError@1 syntax error: expected the end of the text, found '.1'
        a.(b) → SyntaxError@2 syntax error: expected a field name, found '('
        a.`b`() → SyntaxError@5 syntax error: expected the end of the text, found '('
        a.`b → SyntaxError@2 syntax error: unterminated escaped identifier
        a.`` → SyntaxError@2 syntax error: empty escaped identifier
        a.`b+c` → SyntaxError@2 syntax error: unexpected character in escaped identifier
        `a` → SyntaxError@0 syntax error: expected an operand, found '`a`'
        . → SyntaxError@1 syntax error: expected an identifier, found the end of the text
        . (x) → SyntaxError@2 syntax error: expected an identifier, found '('
        .`a` → SyntaxError@1 syntax error: expected an identifier, found '`a`'
        x[1 → SyntaxError@3 syntax error: expected ']', found the end of the text
        x[] → SyntaxError@2 syntax error: expected an operand, found ']'
        x.y(1)(2) → SyntaxError@6 syntax error: expected the end of the text, found '('
        A{1: 2} → SyntaxError@2 syntax error: expected a field name, found '1'
        A{f 1} → SyntaxError@4 syntax error: expected ':', found '1'
        A{f:} → SyntaxError@4 syntax error: expected an operand, found '}'
        A{f: 1 g: 2} → SyntaxError@7 syntax error: expected '}', found 'g'
        A{true: 1} → SyntaxError@2 syntax error: expected a field name, found 'true'
        A{,f: 1} → SyntaxError@3 syntax error: expected '}', found 'f'
        (x){} → SyntaxError@3 syntax error: expected the end of the text, found '{'
        x.y(){} → SyntaxError@5 syntax error: expected the end of the text, found '{'
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
        [1 +, 99999999999999999999] → SyntaxError@4 syntax error: expected an operand, found ',' | InvalidLiteral@6 invalid int literal: out of range
        f(1 +, 99999999999999999999) → SyntaxError@5 syntax error: expected an operand, found ',' | InvalidLiteral@7 invalid int literal: out of range
        {1 +: 99999999999999999999} → SyntaxError@4 syntax error: expected an operand, found ':'
        99999999999999999999 + 1e999 → InvalidLiteral@0 invalid int literal: out of range | InvalidLiteral@23 invalid double literal: the value overflows to infinity
        [99999999999999999999, 1 2] → InvalidLiteral@1 invalid int literal: out of range | SyntaxError@25 syntax error: expected ']', found '2'
        '\q' 2 → InvalidLiteral@0 invalid string literal: unknown escape sequence '\q' | SyntaxError@5 syntax error: expected the end of the text, found '2'
        -99999999999999999999 → InvalidLiteral@0 invalid int literal: out of range
        --9223372036854775808 → InvalidLiteral@2 invalid int literal: out of range
        if → ReservedIdentifier@0 reserved identifier 'if'
        .package → ReservedIdentifier@1 reserved identifier 'package'
        while(1) → ReservedIdentifier@0 reserved identifier 'while'
        .for(1) → ReservedIdentifier@1 reserved identifier 'for'
        a + let → ReservedIdentifier@4 reserved identifier 'let'
        [as, break] → ReservedIdentifier@1 reserved identifier 'as' | ReservedIdentifier@5 reserved identifier 'break'
        if.x → ReservedIdentifier@0 reserved identifier 'if'
        var.f() → ReservedIdentifier@0 reserved identifier 'var'
        x[void] → ReservedIdentifier@2 reserved identifier 'void'
        if 1 → ReservedIdentifier@0 reserved identifier 'if' | SyntaxError@3 syntax error: expected the end of the text, found '1'
        if + 99999999999999999999 → ReservedIdentifier@0 reserved identifier 'if' | InvalidLiteral@5 invalid int literal: out of range
        a.?b → SyntaxNotEnabled@2 '.?' is not enabled: add the extension set optional
        a[?0] → SyntaxNotEnabled@2 '[?' is not enabled: add the extension set optional
        [?a] → SyntaxNotEnabled@1 '?' is not enabled: add the extension set optional
        {?1: a} → SyntaxNotEnabled@1 '?' is not enabled: add the extension set optional
        A{?f: x} → SyntaxNotEnabled@2 '?' is not enabled: add the extension set optional
        a.?b + [?c] → SyntaxNotEnabled@2 '.?' is not enabled: add the extension set optional | SyntaxNotEnabled@8 '?' is not enabled: add the extension set optional
        a.?b 1 → SyntaxNotEnabled@2 '.?' is not enabled: add the extension set optional | SyntaxError@5 syntax error: expected the end of the text, found '1'
        a.?if(1) → SyntaxNotEnabled@2 '.?' is not enabled: add the extension set optional | SyntaxError@5 syntax error: expected the end of the text, found '('
        """"""";

    public static TheoryData<string, string> ErrorCases => CaseTable.Parse(Errors);

    // The words of RESERVED of the language definition other than the keywords true, false, null and in.
    public static TheoryData<string> ReservedWords =>
    [
        "as", "break", "const", "continue", "else", "for", "function", "if", "import", "let", "loop", "package",
        "namespace", "return", "var", "void", "while",
    ];

    [Theory]
    [MemberData(nameof(TreeCases))]
    public static void Expression_ParsesIntoItsTree(string expression, string tree)
    {
        ParseResult result = Parser.Parse(expression);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(tree, Format(result.Expression.Root, result.Expression));
    }

    [Theory]
    [MemberData(nameof(OptionalTreeCases))]
    public static void OptionalSyntax_ParsesIntoItsTreeWhenEnabled(string expression, string tree)
    {
        ParseResult result = Parser.Parse(expression, WithOptionalSyntax);

        Assert.True(result.IsSuccess);
        Assert.Equal(tree, Format(result.Expression.Root, result.Expression));
    }

    // Ids run from 1 in the order of creation: operands before the call, keys and values before their entry, the
    // target of a receiver call before its arguments.
    [Theory]
    [InlineData("x", "1")]
    [InlineData("1 + 2 + 3", "5 3 1 2 4")]
    [InlineData("a || b || c || d", "7 5 1 2 6 3 4")]
    [InlineData("f(1, -x)", "4 1 3 2")]
    [InlineData("{\"k\": [1]}", "5 4 1 3 2")]
    [InlineData("a.b.f(x)", "4 2 1 3")]
    [InlineData("a ? b : c[0]", "6 1 2 5 3 4")]
    [InlineData("A{f: 1, g: x.y}", "6 2 1 5 4 3")]
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

    [Theory]
    [InlineData("1 +\r\n  if", CelErrorCode.ReservedIdentifier, "reserved identifier 'if'", 7, 2, 3)]
    [InlineData("x +\n  a.?b", CelErrorCode.SyntaxNotEnabled, "'.?'" + OptionalNotEnabled, 8, 2, 5)]
    [InlineData("[x,\n\n?1]", CelErrorCode.SyntaxNotEnabled, "'?'" + OptionalNotEnabled, 5, 3, 1)]
    [InlineData("[1,\r 2.5m]", CelErrorCode.SyntaxNotEnabled, DecimalNotEnabled, 5, 2, 2)]
    public static void ReservedIdentifierAndSyntaxNotEnabled_HaveTheirTextAndPlace(
        string expression, CelErrorCode code, string message, int offset, int line, int column)
    {
        ParseResult result = Parser.Parse(expression);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (code, CelErrorStage.Parse, message, offset, line, column, (string?)null, (CelSourceLocation?)null, (Exception?)null),
            (error.Code, error.Stage, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column, error.Location.SourceName, error.SourceLocation, error.Exception));
    }

    [Theory]
    [MemberData(nameof(ReservedWords))]
    public static void ReservedWord_IsReservedOnlyAsAnIdentifierOrAGlobalFunction(string word)
    {
        Assert.Equal(
            "ReservedIdentifier@0 reserved identifier '" + word + "'",
            FormatError(Assert.Single(Parser.Parse(word).Errors)));
        Assert.Equal(
            "ReservedIdentifier@0 reserved identifier '" + word + "'",
            FormatError(Assert.Single(Parser.Parse(word + "()").Errors)));

        // A field, a receiver function, a message field and a segment of a message name are SELECTORs.
        Assert.True(Parser.Parse("a." + word).IsSuccess);
        Assert.True(Parser.Parse("a." + word + "()").IsSuccess);
        Assert.True(Parser.Parse("A{" + word + ": 1}").IsSuccess);
        Assert.True(Parser.Parse(word + "." + word + "{}").IsSuccess);
        Assert.True(Parser.Parse(word + "x").IsSuccess);
    }

    // A decimal literal of either form is recognized and is SyntaxNotEnabled at the literal, its minus included.
    [Theory]
    [InlineData("1.5m", 0)]
    [InlineData("100M", 0)]
    [InlineData(".5m", 0)]
    [InlineData("1e3m", 0)]
    [InlineData("-2.5m", 0)]
    [InlineData("!-2m", 1)]
    [InlineData("--1m", 2)]
    [InlineData("x + 3m", 4)]
    public static void DecimalLiteral_IsNotEnabled(string expression, int offset)
    {
        ParseResult result = Parser.Parse(expression);

        Assert.Equal("SyntaxNotEnabled@" + offset.ToString(CultureInfo.InvariantCulture) + " " + DecimalNotEnabled, FormatError(Assert.Single(result.Errors)));
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
    public static void SyntaxOptions_Standard_HasNoOptionalSyntaxAndTheStandardMacros()
    {
        Assert.Equal((false, MacroTable.Standard), (SyntaxOptions.Standard.OptionalSyntax, SyntaxOptions.Standard.Macros));
        Assert.NotSame(MacroTable.Standard, MacroTable.Empty);
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

    // Without syntax options the environment parses with its own, which are the standard ones; options replace them for
    // one call.
    [Fact]
    public static void EnvironmentParse_UsesTheSyntaxOptionsOfTheCall()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        ParseResult standard = environment.Parse("a.?b");
        ParseResult optional = environment.Parse("a.?b", WithOptionalSyntax);

        Assert.Equal(
            "SyntaxNotEnabled@2 '.?'" + OptionalNotEnabled,
            FormatError(Assert.Single(standard.Errors)));
        Assert.True(optional.IsSuccess);
        Assert.Equal("_?._@1(a@0, \"b\"@3)", Format(optional.Expression.Root, optional.Expression));
        Assert.Single(environment.Parse("a.?b", SyntaxOptions.Standard).Errors);
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
        string at = "@" + Offset(node.Id, parsed);
        switch (node)
        {
            case ConstantNode constant:
                return FormatValue(constant.Value) + at;
            case IdentNode ident:
                return ident.Name + at;
            case SelectNode select:
                Assert.False(select.IsPresenceTest);
                return Format(select.Operand, parsed) + "." + select.Field + at;
            case CallNode call:
                string target = call.Target is null ? string.Empty : Format(call.Target, parsed) + ".";
                return target + call.Function + at + "(" + string.Join(", ", call.Arguments.Select(a => Format(a, parsed))) + ")";
            case ListNode list:
                IEnumerable<string> elements = list.Elements.Select((e, i) => (list.OptionalIndices.Contains(i) ? "?" : "") + Format(e, parsed));
                return "[" + string.Join(", ", elements) + "]" + at;
            default:
                var map = (StructNode)node;
                IEnumerable<string> entries = map.Entries.Select(e =>
                    (e.IsOptional ? "?" : "") + (e.FieldName ?? Format(e.Key, parsed)) + " :@" + Offset(e.Id, parsed) + " " + Format(e.Value, parsed));
                return map.MessageName + "{" + string.Join(", ", entries) + "}" + at;
        }
    }

    private static string Offset(int id, ParsedExpression parsed) => parsed.GetOffset(id).ToString(CultureInfo.InvariantCulture);

    // The ids of the nodes and entries in pre-order: a node, then its children in the order of the text.
    private static IEnumerable<int> Ids(ExprNode node)
    {
        IEnumerable<int> children = node switch
        {
            SelectNode select => Ids(select.Operand),
            CallNode call => (call.Target is null ? Enumerable.Empty<int>() : Ids(call.Target)).Concat(call.Arguments.SelectMany(Ids)),
            ListNode list => list.Elements.SelectMany(Ids),
            StructNode map => map.Entries.SelectMany(e => new[] { e.Id }.Concat(e.FieldName is null ? Ids(e.Key) : Enumerable.Empty<int>()).Concat(Ids(e.Value))),
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
