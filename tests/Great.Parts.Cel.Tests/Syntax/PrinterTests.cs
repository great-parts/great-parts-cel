using System;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// Printing of syntax trees (docs/design/syntax.md, "Printing and tree equality"): the text parses, with the same
// options, into an equal tree, with parentheses only where the tree needs them.
public sealed class PrinterTests
{
    private static readonly SyntaxOptions WithOptionalSyntax = SyntaxOptions.Standard with { OptionalSyntax = true };

    // "<expression> → <printed text>": every node class, every level of the grammar table with an operand of a lower
    // level, of the same level on either side and of a higher level, and the macros. The printed text parses into the
    // same tree as the expression.
    private const string Texts = """""""
        null → null
        true → true
        false → false
        0 → 0
        -9223372036854775808 → -9223372036854775808
        0x1F → 31
        18446744073709551615u → 18446744073709551615u
        1.5 → 1.5
        2.0 → 2.0
        1e2 → 100.0
        -0.0 → -0.0
        .25e1 → 2.5
        1e300 → 1E+300
        1e-7 → 1E-07
        'it\'s' → "it's"
        "q\"b\\s" → "q\"b\\s"
        '\n\r\t\a\x7fé \U0001F600' → "\n\r\t\u0007\u007Fé 😀"
        r'raw\n' → "raw\\n"
        b'a"\\\x00\xff' → b"a\x22\x5C\x00\xFF"
        x → x
        .x → .x
        a.b → a.b
        .a.b → .a.b
        a.if → a.if
        a.`b-c` → a.`b-c`
        a.`in` → a.`in`
        a.`x.y z/w` → a.`x.y z/w`
        f() → f()
        f(1, x) → f(1, x)
        .f(1) → .f(1)
        a.f() → a.f()
        a.b.f(1, 2) → a.b.f(1, 2)
        a.package() → a.package()
        [] → []
        [1, 2,] → [1, 2]
        {} → {}
        {1: 'a', 'b': 2,} → {1: "a", "b": 2}
        A{} → A{}
        .a.B{f: 1, `g-h`: x} → .a.B{f: 1, `g-h`: x}
        x[0] → x[0]
        x[0][a + 1] → x[0][a + 1]
        a ? b : c → a ? b : c
        a ? b : c ? d : e → a ? b : c ? d : e
        (a ? b : c) ? d : e → (a ? b : c) ? d : e
        a ? (b ? c : d) : e → a ? (b ? c : d) : e
        a || b ? c || d : e || f → a || b ? c || d : e || f
        (a ? b : c) || d → (a ? b : c) || d
        a || b → a || b
        a || b || c → (a || b) || c
        a || b || c || d → (a || b) || (c || d)
        a || (b || c) → a || (b || c)
        a || b && c → a || b && c
        (a || b) && c → (a || b) && c
        a && b && c && d && e → ((a && b) && c) && (d && e)
        a && b == c → a && b == c
        (a && b) == c → (a && b) == c
        a < b == c → a < b == c
        a < (b == c) → a < (b == c)
        a in b in c → a in b in c
        a in (b in c) → a in (b in c)
        a <= b + c → a <= b + c
        (a <= b) + c → (a <= b) + c
        a - b - c → a - b - c
        a - (b - c) → a - (b - c)
        a + b * c → a + b * c
        (a + b) * c → (a + b) * c
        a * b / c % d → a * b / c % d
        a * (b % c) → a * (b % c)
        a * -b → a * -b
        a - -1 → a - -1
        -a * b → -a * b
        -(a * b) → -(a * b)
        !x → !x
        -x → -x
        !(!x) → !(!x)
        -(-x) → -(-x)
        !(-x) → !(-x)
        -(1) → -(1)
        -(1u) → -(1u)
        -(1.5) → -(1.5)
        -(-1) → -(-1)
        -'a' → -"a"
        !(a || b) → !(a || b)
        !a.b → !a.b
        !f(x)[0] → !f(x)[0]
        (a + b).c → (a + b).c
        (-a).b → (-a).b
        (!a).f() → (!a).f()
        (1).b → (1).b
        (-1).f() → (-1).f()
        (1u)[0] → (1u)[0]
        (a ? b : c)[0] → (a ? b : c)[0]
        "s".size() → "s".size()
        [1][0] → [1][0]
        {1: 2}.f → {1: 2}.f
        A{}.f → A{}.f
        has(a.b) → has(a.b)
        has(a.b).c → has(a.b).c
        !has(a.b) → !has(a.b)
        has(a.`b c`) → has(a.`b c`)
        xs.all(e, e > 0) → xs.all(e, e > 0)
        xs.exists(e, e) → xs.exists(e, e)
        xs.exists_one(e, e) → xs.exists_one(e, e)
        xs.map(e, e * 2) → xs.map(e, e * 2)
        xs.map(e, e > 0, -e) → xs.map(e, e > 0, -e)
        xs.filter(e, has(e.f)) → xs.filter(e, has(e.f))
        (xs.map(e, e)).size() → xs.map(e, e).size()
        (a + b).all(e, e) → (a + b).all(e, e)
        xs.all(x, ys.exists(y, x == y)) ? 1 : 2 → xs.all(x, ys.exists(y, x == y)) ? 1 : 2
        """"""";

    private const string OptionalTexts = """""""
        a.?b → a.?b
        a.?`b-c` → a.?`b-c`
        (-a).?b → (-a).?b
        a[?0] → a[?0]
        (1)[?0] → (1)[?0]
        [?a, b, ?c] → [?a, b, ?c]
        {?1: 2, 3: 4} → {?1: 2, 3: 4}
        A{?f: 1, g: 2} → A{?f: 1, g: 2}
        a.?b.c[?d].e() → a.?b.c[?d].e()
        """"""";

    public static TheoryData<string, string> TextCases => CaseTable.Parse(Texts);

    public static TheoryData<string, string> OptionalTextCases => CaseTable.Parse(OptionalTexts);

    [Theory]
    [MemberData(nameof(TextCases))]
    public static void Tree_PrintsIntoTextThatParsesIntoTheSameTree(string expression, string printed)
    {
        AssertPrintsAndParsesBack(expression, printed, SyntaxOptions.Standard);
    }

    [Theory]
    [MemberData(nameof(OptionalTextCases))]
    public static void OptionalSyntax_PrintsIntoTextThatParsesIntoTheSameTree(string expression, string printed)
    {
        AssertPrintsAndParsesBack(expression, printed, WithOptionalSyntax);
    }

    // Without macros a macro call is a call and prints as one.
    [Theory]
    [InlineData("has(a.b)")]
    [InlineData("xs.all(e, true)")]
    public static void CallWithoutMacros_PrintsAsACall(string expression)
    {
        AssertPrintsAndParsesBack(expression, expression, new SyntaxOptions(OptionalSyntax: false, MacroTable.Empty));
    }

    // A comprehension or a presence test that no macro call created cannot be printed.
    [Fact]
    public static void ExpansionWithoutRecordedCall_Throws()
    {
        var nodes = new NodeFactory();
        IdentNode range = nodes.Ident("xs", 0);
        ConstantNode init = nodes.Constant(CelValue.FromBoolean(true), 1);
        IdentNode accumulator = nodes.Ident(Macro.AccumulatorName, 1);
        ComprehensionNode loop = nodes.Comprehension("e", range, Macro.AccumulatorName, init, init, accumulator, accumulator, 1);
        SelectNode presence = nodes.Select(range, "f", isPresenceTest: true, 2);
        var lines = new LineTable("xs");

        Assert.Throws<InvalidOperationException>(() => Printer.Print(nodes.Build(loop, lines)));
        Assert.Throws<InvalidOperationException>(() => Printer.Print(nodes.Build(presence, lines)));
    }

    // The text of 2^-25 and 2^-958 in the form "R" of .NET 10 reads back to the double below them, whose gap is half as
    // wide as the gap above.
    [Theory]
    [InlineData(-25)]
    [InlineData(-958)]
    public static void Double_OfAPowerOfTwoWhoseNetTextReadsBelow_PrintsTextThatReadsBack(int exponent)
    {
        AssertDoubleReadsBack(Math.ScaleB(1.0, exponent));
    }

    // Every one of the 2098 positive powers of two, from the least subnormal number to the largest power.
    [Fact]
    public static void Double_OfEveryPowerOfTwo_PrintsTextThatReadsBack()
    {
        for (int exponent = -1074; exponent <= 1023; exponent++)
        {
            AssertDoubleReadsBack(Math.ScaleB(1.0, exponent));
            AssertDoubleReadsBack(-Math.ScaleB(1.0, exponent));
        }
    }

    // 20,000 doubles of any exponent and sign, spread over the bit patterns.
    [Fact]
    public static void Double_OfRandomBitPatterns_PrintsTextThatReadsBack()
    {
        for (int i = 0; i < 20_000; i++)
        {
            double value = BitConverter.UInt64BitsToDouble(unchecked((ulong)(i + 1) * 0x9E3779B97F4A7C15UL));
            if (double.IsFinite(value))
            {
                AssertDoubleReadsBack(value);
            }
        }
    }

    private static void AssertDoubleReadsBack(double value)
    {
        var text = new StringBuilder();
        Printer.AppendConstant(text, CelValue.FromDouble(value));
        string printed = text.ToString();

        ParsedExpression parsed = Parse(printed, SyntaxOptions.Standard);

        var constant = Assert.IsType<ConstantNode>(parsed.Root);
        Assert.True(constant.Value.TryGetDouble(out double readBack), printed);
        Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(readBack));
    }

    private static void AssertPrintsAndParsesBack(string expression, string printed, SyntaxOptions syntax)
    {
        ParsedExpression parsed = Parse(expression, syntax);

        string text = Printer.Print(parsed);

        Assert.Equal(printed, text);
        Assert.Null(SyntaxTreeComparer.Difference(parsed.Root, Parse(text, syntax).Root));
    }

    private static ParsedExpression Parse(string expression, SyntaxOptions syntax)
    {
        ParseResult result = Parser.Parse(expression, syntax);
        Assert.True(result.IsSuccess, expression + ": " + string.Join(" | ", result.Errors.Select(Format)));
        return result.Expression;
    }

    private static string Format(CelError error) => error.Code + " " + error.Message;
}
