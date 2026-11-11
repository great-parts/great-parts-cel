using System.Collections.Generic;
using Great.Parts.Cel.Library.Extensions.Strings;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The parser of the format string, step by step: pieces of text, the %% that stands for one %, the clauses with their letter, precision
// and number, and the first failure, in the order that the page of the function gives: no unit after the %, no element for the
// clause, the precision, the letter.
public sealed class FormatParserTests
{
    [Fact]
    public static void Steps_OfATextWithPercentsAndClauses_AreTextClauseAndTheSingleFormTheDoublePercentStandsFor()
    {
        Assert.Equal(
            [
                "text:a",
                "text:%",
                "text:b ",
                "clause:Decimal/6/0",
                "text:-",
                "clause:HexUpper/6/1",
                "text:%",
            ],
            Steps("a%%b %d-%X%%", 2));
    }

    [Theory]
    [InlineData("%s", "String")]
    [InlineData("%d", "Decimal")]
    [InlineData("%f", "Fixed")]
    [InlineData("%e", "Scientific")]
    [InlineData("%b", "Binary")]
    [InlineData("%o", "Octal")]
    [InlineData("%x", "Hex")]
    [InlineData("%X", "HexUpper")]
    public static void Letter_IsOneOfTheEight(string format, string specifier)
    {
        Assert.Equal(["clause:" + specifier + "/6/0"], Steps(format, 1));
    }

    [Theory]
    [InlineData("%.0f", 0)]
    [InlineData("%.1f", 1)]
    [InlineData("%.007f", 7)]
    [InlineData("%.99f", 99)]
    [InlineData("%.100f", 100)]
    [InlineData("%.0000000000000000000000000100f", 100)]
    public static void Precision_UpToTheLimit_IsReadWithItsDigits(string format, int precision)
    {
        Assert.Equal(["clause:Fixed/" + precision + "/0"], Steps(format, 1));
    }

    // The precision is read after any letter; whether the letter uses it is for the formatting.
    [Fact]
    public static void Precision_IsReadForEveryLetter()
    {
        Assert.Equal(["clause:String/3/0", "clause:Decimal/5/1"], Steps("%.3s%.5d", 2));
    }

    [Theory]
    [InlineData("%.101f", "PrecisionTooLarge/101")]
    [InlineData("%.1000s", "PrecisionTooLarge/1000")]
    [InlineData("%.9223372036854775807d", "PrecisionTooLarge/9223372036854775807")]
    [InlineData("%.9223372036854775808d", "PrecisionOutOfRange/0")]
    [InlineData("%.99999999999999999999999999f", "PrecisionOutOfRange/0")]
    [InlineData("%.f", "PrecisionEmpty/0")]
    [InlineData("%.s", "PrecisionEmpty/0")]
    [InlineData("%.", "PrecisionUnterminated/0")]
    [InlineData("%.5", "PrecisionUnterminated/0")]
    [InlineData("%.123456", "PrecisionUnterminated/0")]
    [InlineData("%", "UnexpectedEnd/0")]
    [InlineData("%z", "UnknownClause/122")]
    [InlineData("%5d", "UnknownClause/53")]
    [InlineData("% d", "UnknownClause/32")]
    [InlineData("%-d", "UnknownClause/45")]
    [InlineData("%😀", "UnknownClause/128512")]
    public static void Failure_OfAClause_IsTheReasonWithItsNumber(string format, string failure)
    {
        Assert.Equal(["failure:" + failure], Steps(format, 1));
    }

    // A letter that is a lone surrogate is its own code point.
    [Fact]
    public static void UnknownLetter_OfALoneSurrogate_IsItsUnit()
    {
        Assert.Equal(["failure:UnknownClause/" + 0xD83D], Steps("%" + (char)0xD83D, 1));
        Assert.Equal(["failure:UnknownClause/" + 0xDE00], Steps("%" + (char)0xDE00 + "x", 1));
    }

    // The clause has no element when its number reaches the count; the end of the string is looked at first.
    [Theory]
    [InlineData("%d", 0, "IndexOutOfRange/0")]
    [InlineData("%d %d", 1, "IndexOutOfRange/1")]
    [InlineData("%s%s%s", 2, "IndexOutOfRange/2")]
    [InlineData("%z", 0, "IndexOutOfRange/0")]
    [InlineData("%.101f", 0, "IndexOutOfRange/0")]
    [InlineData("%", 0, "UnexpectedEnd/0")]
    public static void Failure_OfAMissingElement_IsTheNumberOfTheClause(string format, int count, string failure)
    {
        List<string> steps = Steps(format, count);

        Assert.Equal("failure:" + failure, steps[^1]);
    }

    // The text before a % without a unit after it is given first, then the failure.
    [Fact]
    public static void Failure_OfAPercentAtTheEnd_FollowsTheTextBeforeIt()
    {
        Assert.Equal(["text:abc", "failure:UnexpectedEnd/0"], Steps("abc%", 1));
    }

    // The first error ends the call: the text before it was given, and nothing follows.
    [Fact]
    public static void Failure_EndsTheSteps()
    {
        Assert.Equal(["text:a", "clause:Decimal/6/0", "text:b", "failure:UnknownClause/122"], Steps("a%db%zc%d", 3));
    }

    // At a precision above the limit the error is that of the precision, not of the letter after it.
    [Fact]
    public static void Failure_OfThePrecision_ComesBeforeTheLetter()
    {
        Assert.Equal(["failure:PrecisionTooLarge/101"], Steps("%.101z", 1));
    }

    [Fact]
    public static void EmptyFormat_HasNoSteps()
    {
        Assert.Empty(Steps(string.Empty, 0));
    }

    [Fact]
    public static void ClauseCount_IsTheNumberOfClausesRead()
    {
        var parser = new FormatParser("%d%%%s", 2);
        while (parser.Next() != FormatStep.End)
        {
        }

        Assert.Equal(2, parser.ClauseCount);
    }

    // The steps as text: "text:…", "clause:Letter/precision/index" and "failure:Reason/number".
    private static List<string> Steps(string format, int elementCount)
    {
        var parser = new FormatParser(format, elementCount);
        List<string> steps = [];
        for (FormatStep step = parser.Next(); step != FormatStep.End; step = parser.Next())
        {
            steps.Add(step switch
            {
                FormatStep.Text => "text:" + parser.Text.ToString(),
                FormatStep.Clause => "clause:" + parser.Clause.Specifier + "/" + parser.Clause.Precision + "/" + parser.Clause.Index,
                _ => "failure:" + parser.Failure.Kind + "/" + parser.Failure.Number,
            });
        }

        return steps;
    }
}
