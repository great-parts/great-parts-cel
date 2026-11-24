using System;
using System.Collections.Generic;
using Xunit;

namespace Great.Parts.Cel.Filtering.Tests;

// The lexer of filters: every kind of token and every branch of the rules of the text, of the minus and of the strings, with the
// ranges of the tokens in UTF-16 code units.
[Trait("Requirement", "F-32")]
public sealed class FilterLexerTests
{
    private static readonly FilterComparator[] AllComparators =
    [
        FilterComparator.LessOrEqual,
        FilterComparator.Less,
        FilterComparator.GreaterOrEqual,
        FilterComparator.Greater,
        FilterComparator.NotEqual,
        FilterComparator.Equal,
        FilterComparator.Has,
    ];

    [Theory]
    [InlineData("a.b(c,d)", "T . T ( T , T ) $")]
    [InlineData("a<=1 b>=2 c!=3 d<4 e>5 f=6 g:7", "T <= T T >= T T != T T < T T > T T = T T : T $")]
    [InlineData("-a -5 a-b", "- T - T T $")]
    [InlineData("x=-1", "T = - T $")]
    [InlineData("(-a)", "( - T ) $")]
    [InlineData("f(a,-1)", "T ( T , - T ) $")]
    [InlineData("a.-b", "T . T $")]
    [InlineData("a ! b", "T ! T $")]
    [InlineData("a*b+c", "T $")]
    [InlineData("  ", "$")]
    public static void Tokens_AreOfTheKindsOfTheSpecification(string text, string expected) =>
        Assert.Equal(expected, string.Join(' ', Codes(text)));

    [Fact]
    public void WhiteSpace_IsASpaceATabAndALineBreak()
    {
        List<FilterToken> tokens = Tokens("a\t=\r\nb\n");

        Assert.Equal("T = T $", string.Join(' ', Codes("a\t=\r\nb\n")));
        Assert.False(tokens[0].SpaceBefore);
        Assert.True(tokens[1].SpaceBefore);
        Assert.True(tokens[2].SpaceBefore);
        Assert.True(tokens[3].SpaceBefore);
    }

    [Fact]
    public void Ranges_AreInUtf16CodeUnits()
    {
        // A name of Cyrillic letters and a pair of surrogates, which is one text of two code units.
        char[] letters = [(char)0x41F, (char)0x440, (char)0x438];
        string name = new(letters);
        List<FilterToken> tokens = Tokens(name + " = " + char.ConvertFromUtf32(0x1F600));

        Assert.Equal((0, 3), (tokens[0].Start, tokens[0].End));
        Assert.Equal((4, 5), (tokens[1].Start, tokens[1].End));
        Assert.Equal((6, 8), (tokens[2].Start, tokens[2].End));
        Assert.Equal(FilterTokenKind.Text, tokens[2].Kind);
        Assert.Equal((8, 8), (tokens[3].Start, tokens[3].End));
    }

    [Fact]
    public void Comparators_HaveTheirKinds()
    {
        List<FilterToken> tokens = Tokens("<= < >= > != = :");

        Assert.Equal<FilterComparator>(AllComparators, tokens[..7].ConvertAll(token => token.Comparator));
    }

    [Fact]
    public void String_InSingleQuotes_MayHoldDoubleQuotes_AndTheOtherWayRound()
    {
        Assert.Equal("a\"b", Tokens("'a\"b'")[0].Quoted!.Value);
        Assert.Equal("a'b", Tokens("\"a'b\"")[0].Quoted!.Value);
    }

    [Fact]
    public void String_WithEscapes_HasTheEscapedCharactersLiteral()
    {
        FilterToken token = Tokens("\"a\\\"b\\\\c\\*d\\n\"")[0];

        Assert.Equal(FilterTokenKind.String, token.Kind);
        Assert.Equal("a\"b\\c*dn", token.Quoted!.Value);
        Assert.Equal(5, Assert.Single(token.Quoted.EscapedStars));
    }

    [Fact]
    public void String_WithAnUnescapedStar_HasNoEscapedStars()
    {
        Assert.Empty(Tokens("'*a*'")[0].Quoted!.EscapedStars);
    }

    [Theory]
    [InlineData("\"abc")]
    [InlineData("'abc")]
    [InlineData("\"abc\\")]
    public static void String_NotClosed_IsInvalidToTheEndOfTheText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        FilterToken token = Tokens(text)[0];

        Assert.Equal(FilterTokenKind.Invalid, token.Kind);
        Assert.Equal((0, text.Length), (token.Start, token.End));
        Assert.Equal("the string is not closed", token.Detail);
    }

    [Fact]
    public void String_RightAfterAText_StartsAnotherToken()
    {
        Assert.Equal("T S $", string.Join(' ', Codes("ab'c'")));
    }

    private static List<FilterToken> Tokens(string text)
    {
        FilterLexer lexer = new(text);
        List<FilterToken> tokens = [];
        FilterToken token;
        do
        {
            token = lexer.Next();
            tokens.Add(token);
        }
        while (token.Kind != FilterTokenKind.End);

        return tokens;
    }

    // One code for each token: T text, S string, - minus, ! a token that is not of the language, $ the end, the others as written.
    private static IEnumerable<string> Codes(string text)
    {
        foreach (FilterToken token in Tokens(text))
        {
            yield return token.Kind switch
            {
                FilterTokenKind.Text => "T",
                FilterTokenKind.String => "S",
                FilterTokenKind.Dot => ".",
                FilterTokenKind.LeftParen => "(",
                FilterTokenKind.RightParen => ")",
                FilterTokenKind.Comma => ",",
                FilterTokenKind.Minus => "-",
                FilterTokenKind.Invalid => "!",
                FilterTokenKind.End => "$",
                _ => ComparatorCode(token.Comparator),
            };
        }
    }

    private static string ComparatorCode(FilterComparator comparator) =>
        comparator switch
        {
            FilterComparator.LessOrEqual => "<=",
            FilterComparator.Less => "<",
            FilterComparator.GreaterOrEqual => ">=",
            FilterComparator.Greater => ">",
            FilterComparator.NotEqual => "!=",
            FilterComparator.Has => ":",
            _ => "=",
        };
}
