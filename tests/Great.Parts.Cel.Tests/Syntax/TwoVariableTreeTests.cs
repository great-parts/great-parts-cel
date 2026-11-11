using System;
using System.Linq;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// Printing and comparing the trees of the macros of two variables: a comprehension is printed as the call it expands from, with
// both names, and the text parses into an equal tree; the second variable is part of the equality of two trees.
public sealed class TwoVariableTreeTests
{
    private static readonly SyntaxOptions WithTheSet = new(
        OptionalSyntax: false,
        MacroTable.Create([.. MacroTable.Standard.Macros, .. ExtensionMacros.TwoVariableComprehensions]));

    // "<expression> → <printed text>": every macro of the set, with and without a filter, and in the places of the grammar that
    // need parentheses or none. The printed text parses into the same tree as the expression.
    [Theory]
    [InlineData("xs.all(i, v, v > i)", "xs.all(i, v, v > i)")]
    [InlineData("xs.exists(i, v, v > i)", "xs.exists(i, v, v > i)")]
    [InlineData("xs.existsOne(i, v, v > i)", "xs.existsOne(i, v, v > i)")]
    [InlineData("xs.exists_one(i, v, v > i)", "xs.exists_one(i, v, v > i)")]
    [InlineData("xs.transformList(i, v, v + i)", "xs.transformList(i, v, v + i)")]
    [InlineData("xs.transformList(i, v, v > i, v)", "xs.transformList(i, v, v > i, v)")]
    [InlineData("xs.transformMap(k, v, v)", "xs.transformMap(k, v, v)")]
    [InlineData("xs.transformMap(k, v, k != 1, v)", "xs.transformMap(k, v, k != 1, v)")]
    [InlineData("xs.transformMapEntry(k, v, {v: k})", "xs.transformMapEntry(k, v, {v: k})")]
    [InlineData("xs.transformMapEntry(k, v, v > 0, {v: k})", "xs.transformMapEntry(k, v, v > 0, {v: k})")]
    [InlineData("(a + b).all(i, v, true)", "(a + b).all(i, v, true)")]
    [InlineData("f(x).transformList(i, v, i)", "f(x).transformList(i, v, i)")]
    [InlineData("xs.all( i ,v,( v ) )", "xs.all(i, v, v)")]
    [InlineData("-xs.all(i, v, true)", "-xs.all(i, v, true)")]
    [InlineData("xs.all(i, v, true) && xs.exists(i, v, true)", "xs.all(i, v, true) && xs.exists(i, v, true)")]
    [InlineData("xs.all(i, v, ys.exists(j, w, j < i))", "xs.all(i, v, ys.exists(j, w, j < i))")]
    [InlineData("xs.transformList(i, v, ys.transformList(j, w, w))", "xs.transformList(i, v, ys.transformList(j, w, w))")]
    [InlineData("xs.all(x, true).all(i, v, true)", "xs.all(x, true).all(i, v, true)")]
    public static void MacroCall_PrintsAsItIsWrittenAndParsesBack(string expression, string printed)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ParsedExpression parsed = Parse(expression);

        string text = Printer.Print(parsed);

        Assert.Equal(printed, text);
        Assert.Null(SyntaxTreeComparer.Difference(parsed.Root, Parse(text).Root));
    }

    // The same text parsed twice gives equal trees, and so do two spellings of one call.
    [Theory]
    [InlineData("xs.all(i, v, v > i)", "(xs).all(i,v,(v > i))")]
    [InlineData("xs.transformMap(k, v, v)", "xs . transformMap ( k , v , v )")]
    public static void TreesOfTheSameCall_HaveNoDifference(string left, string right)
    {
        Assert.Null(SyntaxTreeComparer.Difference(Parse(left).Root, Parse(right).Root));
    }

    // The second variable is compared after the first: a name that differs, or a comprehension of one variable against one of two.
    [Theory]
    [InlineData("xs.all(i, v, true)", "xs.all(i, w, true)", "root.iterVar2: \"v\" vs \"w\"")]
    [InlineData("xs.transformList(i, v, i)", "xs.transformList(i, w, i)", "root.iterVar2: \"v\" vs \"w\"")]
    [InlineData("xs.all(i, true)", "xs.all(i, v, true)", "root.iterVar2: \"null\" vs \"v\"")]
    [InlineData("xs.all(i, v, true)", "xs.all(i, true)", "root.iterVar2: \"v\" vs \"null\"")]
    [InlineData("xs.all(i, v, true)", "xs.all(j, v, true)", "root.iterVar: \"i\" vs \"j\"")]
    [InlineData("xs.all(i, v, true)", "xs.all(i, v, false)", "root.loopStep.arguments[1].value: true vs false")]
    public static void TreesThatDifferInAVariable_HaveThePathToIt(string left, string right, string difference)
    {
        Assert.Equal(difference, SyntaxTreeComparer.Difference(Parse(left).Root, Parse(right).Root));
    }

    private static ParsedExpression Parse(string expression)
    {
        ParseResult result = Parser.Parse(expression, WithTheSet);
        Assert.True(result.IsSuccess, expression + ": " + string.Join(" | ", result.Errors.Select(error => error.Code + " " + error.Message)));
        return result.Expression;
    }
}
