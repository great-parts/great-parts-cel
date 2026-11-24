using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;
using static Great.Parts.Cel.Filtering.Tests.LibraryEnvironments;

namespace Great.Parts.Cel.Filtering.Tests.Examples;

// The syntax of filters: the grammar of AIP-160 with the details of the lexer, the place of each syntax error, the empty filter and
// the limits of an untrusted text. A filter and the CEL text that it becomes have equal trees.
[Trait("Requirement", "F-32")]
public sealed class FilterSyntaxTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public static void EmptyFilter_IsTrue(string filter) => AssertCel(filter, "true");

    [Theory]
    [InlineData("title = ", 8, 0)]
    [InlineData("(title = A", 10, 0)]
    [InlineData("title = \"Dune", 8, 5)]
    [InlineData("AND title = A", 0, 3)]
    [InlineData("title = A AND", 13, 0)]
    [InlineData("title == A", 7, 1)]
    [InlineData("title = A)", 9, 1)]
    [InlineData("()", 1, 1)]
    [InlineData("author . name = X", 7, 1)]
    public static void SyntaxError_IsReportedAtTheTokenWhereParsingStopped(string filter, int start, int length)
    {
        Assert.Equal((CelErrorCode.FilterSyntaxError, start, length), FirstError(F.Parse(filter).Errors));
        Assert.Equal((CelErrorCode.FilterSyntaxError, start, length), FirstError(F.Compile(filter).Errors));
    }

    [Fact]
    public void SyntaxError_HasTheStageParseAndNoParts()
    {
        CelError error = F.Parse("title = ").Errors[0];

        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Null(error.Subject);
        Assert.Null(error.ExpectedType);
        Assert.Null(error.ActualType);
        Assert.Equal("expected a value but found the end of the filter\ntitle = \n        ^", error.ToDisplayString("title = "));
    }

    [Fact]
    public void SyntaxError_OfAnUnclosedCall_IsReported()
    {
        CelParseResult result = F.Parse("lower(title = \"a\"");

        Assert.Equal(CelErrorCode.FilterSyntaxError, result.Errors[0].Code);
    }

    [Fact]
    public void KeywordFollowedByText_IsATextNotAKeyword()
    {
        AssertCel("title = A ANDpages = 1", "title == \"A\" && ANDpages == 1");
        AssertCel("NOTavailable = true", "NOTavailable == true");

        CelCompileResult<bool> andPages = F.Compile("title = A ANDpages = 1");
        CelCompileResult<bool> notAvailable = F.Compile("NOTavailable = true");

        Assert.Equal((CelErrorCode.UndeclaredReference, 10, 8), FirstError(andPages.Errors));
        Assert.Equal((CelErrorCode.UndeclaredReference, 0, 12), FirstError(notAvailable.Errors));
    }

    [Fact]
    public void Strings_AreInSingleOrDoubleQuotesWithEscapes()
    {
        AssertCel("title = 'Dune'", "title == \"Dune\"");
        AssertCel("title = \"Du\\\"ne\"", "title == \"Du\\\"ne\"");
        AssertCel("title = \"\\*Dune\"", "title == \"*Dune\"");
    }

    [Fact]
    public void OrBindsStrongerThanASequence_AndASequenceStrongerThanAnd()
    {
        AssertCel("title = A OR title = B AND pages > 10", "(title == \"A\" || title == \"B\") && pages > 10");
        AssertCel("title = A title = B OR title = C", "title == \"A\" && (title == \"B\" || title == \"C\")");
    }

    [Theory]
    [InlineData("NOT available = true")]
    [InlineData("-available = true")]
    public static void NegationOfATerm_IsNot(string filter) => AssertCel(filter, "!(available == true)");

    [Fact]
    public void ValueWithoutAField_StartingWithAMinus_IsTheValue()
    {
        AssertCel("-5", "title.contains(\"-5\") || author != null && author.name.contains(\"-5\")");
    }

    [Fact]
    public void FilterLongerThanTheLimit_IsExpressionTooLong()
    {
        string filter = new('a', 65_537);

        CelError error = Assert.Single(F.Parse(filter).Errors);

        Assert.Equal(CelErrorCode.ExpressionTooLong, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
    }

    [Fact]
    public void FilterOfTheLongestLength_IsNotTooLong()
    {
        string filter = new(' ', 65_536);

        AssertCel(filter, "true");
    }

    [Fact]
    public void FilterLongerThanTheLimitInUtf8_IsExpressionTooLong()
    {
        // 21 846 characters of three bytes each: 65 538 bytes and 21 846 UTF-16 code units.
        string filter = new('\u20AC', 21_846);

        Assert.Equal(CelErrorCode.ExpressionTooLong, F.Parse(filter).Errors[0].Code);
        Assert.True(F.Parse(new string('\u20AC', 21_845)).IsSuccess);
    }

    [Fact]
    public void ParenthesesDeeperThan250_AreNestingTooDeepAtThe251stOpening()
    {
        string filter = new string('(', 251) + "pages = 1" + new string(')', 251);

        CelError error = Assert.Single(F.Parse(filter).Errors);

        Assert.Equal(CelErrorCode.NestingTooDeep, error.Code);
        Assert.Equal(250, error.Span.Start.Offset);
        Assert.Equal(1, error.Span.Length);
        AssertCel(new string('(', 250) + "pages = 1" + new string(')', 250), "pages == 1");
    }

    [Fact]
    public void OpenParenthesesToTheLimitOfTheLength_AreNestingTooDeepWithoutAStackOverflow()
    {
        // The length is checked first: a text of 100 000 parentheses is longer than the limit and is not read.
        Assert.Equal(CelErrorCode.ExpressionTooLong, F.Parse(new string('(', 100_000)).Errors[0].Code);

        CelError error = Assert.Single(F.Parse(new string('(', 65_536)).Errors);

        Assert.Equal(CelErrorCode.NestingTooDeep, error.Code);
        Assert.Equal(250, error.Span.Start.Offset);
    }

    [Fact]
    public void ManyRestrictionsJoinedByAnd_CompileToAShallowTree()
    {
        string filter = string.Join(" AND ", Enumerable.Repeat("pages = 1", 4_600));

        CelCompileResult<bool> result = F.Compile(filter);

        Assert.True(filter.Length < 65_536);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.True(Depth(F.Parse(filter).GetParsedExprOrThrow().Root) <= 20);
    }

    // The depth of a tree, counted with an explicit stack.
    private static int Depth(CelExprNode root)
    {
        int deepest = 0;
        Stack<(CelExprNode Node, int Depth)> pending = new();
        pending.Push((root, 1));
        while (pending.Count > 0)
        {
            (CelExprNode node, int depth) = pending.Pop();
            deepest = Math.Max(deepest, depth);
            foreach (CelExprNode child in node.Children)
            {
                pending.Push((child, depth + 1));
            }
        }

        return deepest;
    }
}
