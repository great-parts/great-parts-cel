#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The decimal literal in the parser, the printer and the comparison of trees: what the examples of the literal leave to the code of
// the syntax. The examples fix the values by string(x) and the places of the errors in one-line texts; here the member of the options,
// the constant in every construct that holds an expression, the places of errors after a line break and after a minus that is
// followed by white space, and the circle "parse, print, parse" with the parentheses that a numeric constant needs.
public sealed class DecimalLiteralSyntaxTests
{
    private static readonly SyntaxOptions WithDecimals = SyntaxOptions.Standard with { DecimalLiterals = true };

    // "<expression> → <printed text>": the text of the value as string() writes it with the suffix, the sign as a minus before the
    // magnitude, and the parentheses of a numeric constant where an int has them. The printed text parses, with the same options, into
    // an equal tree.
    private const string Texts = """
        100m → 100m
        1e2m → 1E+2m
        100.0m → 100.0m
        0m → 0m
        1.50m → 1.50m
        .5m → 0.5m
        007m → 7m
        5e-7m → 5E-7m
        0.000005m → 0.000005m
        1e39m → 1E+39m
        -0m → -0m
        -0.0m → -0.0m
        -1.50m → -1.50m
        1 - -2.5m → 1 - -2.5m
        -(1.5m) → -(1.5m)
        -(-1.5m) → -(-1.5m)
        !(1m) → !(1m)
        (1.5m).f() → (1.5m).f()
        (1.5m).x → (1.5m).x
        (1.5m)[0] → (1.5m)[0]
        [1.5m, 2m] → [1.5m, 2m]
        {1: 2.5m} → {1: 2.5m}
        f(1m + 2m) → f(1m + 2m)
        c ? 1m : 2m → c ? 1m : 2m
        """;

    [Fact]
    public static void SyntaxOptions_HasTheDecimalLiteralsMember_OffInStandardAndKeptByWith()
    {
        Assert.False(SyntaxOptions.Standard.DecimalLiterals);
        Assert.True(WithDecimals.DecimalLiterals);
        Assert.Same(SyntaxOptions.Standard.Macros, WithDecimals.Macros);
        Assert.NotEqual(SyntaxOptions.Standard, WithDecimals);

        SyntaxOptions both = WithDecimals with { OptionalSyntax = true };

        Assert.True(both.DecimalLiterals);
        Assert.True(both.OptionalSyntax);
    }

    // The option of the decimal literals and the optional syntax are two flags of the same options and do not depend on each other.
    [Fact]
    public static void DecimalOptionAndOptionalSyntax_AreIndependent()
    {
        Assert.True(Parser.Parse("a.?b", SyntaxOptions.Standard with { OptionalSyntax = true }).IsSuccess);
        Assert.False(Parser.Parse("1m", SyntaxOptions.Standard with { OptionalSyntax = true }).IsSuccess);
        Assert.False(Parser.Parse("a.?b", WithDecimals).IsSuccess);
    }

    // A constant of the kind Decimal is a node wherever an expression can stand: an argument, an element, a key, a value, the
    // branches of a conditional, the target of a call, the range and the step of a macro.
    [Theory]
    [InlineData("f(2m, 3e1m)", 2)]
    [InlineData("[1.5m, 2m, -3m]", 3)]
    [InlineData("{1.5m: 2m}", 2)]
    [InlineData("c ? 1m : 2m", 2)]
    [InlineData("(1.5m).f(2m)", 2)]
    [InlineData("a[1m]", 1)]
    [InlineData("[1.5m].exists(x, x > 1m)", 2)]
    [InlineData("[1m, 2m].map(x, x + 3m)", 3)]
    [InlineData("[1m].filter(x, x != 0m).all(y, y < 2m)", 3)]
    public static void LiteralInAnyConstruct_IsAConstantOfTheKindDecimal(string expression, int count)
    {
        ParseResult result = Parser.Parse(expression, WithDecimals);

        Assert.True(result.IsSuccess, expression);
        var constants = new List<CelValue>();
        Collect(result.Expression.Root, constants);
        Assert.Equal(count, constants.FindAll(constant => constant.Kind == CelValueKind.Decimal).Count);
    }

    // Without the option every literal of the text is an error and parsing goes on, also inside a macro.
    [Fact]
    public static void LiteralsInAMacroWithoutTheOption_AreEachSyntaxNotEnabled()
    {
        ParseResult result = Parser.Parse("[1m].exists(x, x > 2m)");

        Assert.Equal("SyntaxNotEnabled@1 | SyntaxNotEnabled@19", string.Join(" | ", result.Errors.Select(Place)));
    }

    // The place of an error of a literal is its first character, or the minus that belongs to it, also when a line break or white
    // space comes between them; the line and the column follow the line table.
    [Theory]
    [InlineData("1 +\n  1e6112m", 6, 2, 3)]
    [InlineData("1 +\r\n  1e6112m", 7, 2, 3)]
    [InlineData("- 1e6112m", 0, 1, 1)]
    [InlineData("-\n1e6112m", 0, 1, 1)]
    [InlineData("x +\n  - 1e6112m", 6, 2, 3)]
    [InlineData("\n\n1e-6177m", 2, 3, 1)]
    public static void InvalidLiteral_IsAtTheLiteralOrItsMinus_WithTheLineAndColumn(string expression, int offset, int line, int column)
    {
        ParseResult result = Parser.Parse(expression, WithDecimals);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.InvalidLiteral, "invalid decimal literal: exponent out of the range of decimal128", offset, line, column),
            (error.Code, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    [Theory]
    [InlineData("- 0m", 0, 1, 1)]
    [InlineData("-\n0m", 0, 1, 1)]
    [InlineData("1 +\n  0m", 6, 2, 3)]
    public static void SyntaxNotEnabled_IsAtTheLiteralOrItsMinus_WithTheLineAndColumn(string expression, int offset, int line, int column)
    {
        ParseResult result = Parser.Parse(expression);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.SyntaxNotEnabled, offset, line, column),
            (error.Code, error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private static string Place(CelError error) =>
        error.Code + "@" + error.Location.Offset.ToString(CultureInfo.InvariantCulture);

    [Theory]
    [MemberData(nameof(PrintedTexts))]
    public static void Printer_WritesTheValueAndTheTextParsesBackIntoAnEqualTree(string expression, string printed)
    {
        ParseResult first = Parser.Parse(expression, WithDecimals);
        Assert.True(first.IsSuccess, expression);

        string text = Printer.Print(first.Expression);
        ParseResult second = Parser.Parse(text, WithDecimals);

        Assert.Equal(printed, text);
        Assert.True(second.IsSuccess, text);
        Assert.Null(SyntaxTreeComparer.Difference(first.Expression.Root, second.Expression.Root));
    }

    public static TheoryData<string, string> PrintedTexts()
    {
        var data = new TheoryData<string, string>();
        foreach (string line in Texts.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] sides = line.Split(" → ");
            data.Add(sides[0], sides[1]);
        }

        return data;
    }

    // Two literals of one number that are written differently are two trees: the cohort and the sign of a zero are part of the
    // constant, so the circle "parse, print, parse" keeps them.
    [Theory]
    [InlineData("100m", "1e2m", "root.value: 100m vs 1E+2m")]
    [InlineData("1.5m", "1.50m", "root.value: 1.5m vs 1.50m")]
    [InlineData("0m", "-0m", "root.value: 0m vs -0m")]
    [InlineData("0.0m", "0m", "root.value: 0.0m vs 0m")]
    [InlineData("1m", "2m", "root.value: 1m vs 2m")]
    public static void Comparer_ComparesDecimalsByCoefficientExponentAndSign(string left, string right, string difference)
    {
        Assert.Equal(difference, SyntaxTreeComparer.Difference(Root(left), Root(right)));
    }

    [Theory]
    [InlineData("100m")]
    [InlineData("1e2m")]
    [InlineData("-0.0m")]
    [InlineData("[1.5m, f(2m)]")]
    public static void Comparer_SameDecimalsAreEqual(string expression)
    {
        Assert.Null(SyntaxTreeComparer.Difference(Root(expression), Root(expression)));
    }

    [Fact]
    public static void Comparer_DecimalAndIntOfOneNumber_DifferByTheirKind()
    {
        Assert.Equal("root.kind: Decimal vs Int", SyntaxTreeComparer.Difference(Root("1m"), Root("1")));
        Assert.Equal("root.kind: Decimal vs Double", SyntaxTreeComparer.Difference(Root("1.5m"), Root("1.5")));
    }

    private static ExprNode Root(string expression)
    {
        ParseResult result = Parser.Parse(expression, WithDecimals);
        Assert.True(result.IsSuccess, expression);
        return result.Expression.Root;
    }

    // The constants of a tree, in the order of the text; a comprehension has its range, its initial value, its condition, its step
    // and its result.
    private static void Collect(ExprNode node, List<CelValue> constants)
    {
        switch (node)
        {
            case ConstantNode constant:
                constants.Add(constant.Value);
                break;
            case SelectNode select:
                Collect(select.Operand, constants);
                break;
            case CallNode call:
                if (call.Target is not null)
                {
                    Collect(call.Target, constants);
                }

                foreach (ExprNode argument in call.Arguments)
                {
                    Collect(argument, constants);
                }

                break;
            case ListNode list:
                foreach (ExprNode element in list.Elements)
                {
                    Collect(element, constants);
                }

                break;
            case StructNode structure:
                foreach (StructEntry entry in structure.Entries)
                {
                    if (entry.Key is not null)
                    {
                        Collect(entry.Key, constants);
                    }

                    Collect(entry.Value, constants);
                }

                break;
            case ComprehensionNode comprehension:
                Collect(comprehension.IterRange, constants);
                Collect(comprehension.AccuInit, constants);
                Collect(comprehension.LoopCondition, constants);
                Collect(comprehension.LoopStep, constants);
                Collect(comprehension.Result, constants);
                break;
            default:
                break;
        }
    }
}
#endif
