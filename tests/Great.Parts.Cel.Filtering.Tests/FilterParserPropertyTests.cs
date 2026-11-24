using System.Text;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Filtering.Tests;

// The parser on untrusted text: a random text of any length up to the limit gives a tree or one error, never an exception; a filter
// that the grammar generates parses without an error.
[Trait("Requirement", "R-15")]
[Trait("Requirement", "F-32")]
public sealed class FilterParserPropertyTests
{
    private const int Cases = 2_000;
    private const int Limit = 65_536;

    [Fact]
    public void RandomText_GivesATreeOrOneErrorInsideTheText()
    {
        var random = new FilterGenerator.Source(20261125);
        for (int i = 0; i < Cases; i++)
        {
            string text = FilterGenerator.RandomText(random, random.Next(0, 3) == 0 ? random.Next(0, Limit + 1) : random.Next(0, 60));

            FilterParseResult result = FilterParser.Parse(text);

            Assert.True((result.Root is null) != (result.Error is null), "one result: " + text);
            if (result.Error is { } error)
            {
                Assert.True(0 <= error.Start && error.Start <= error.End && error.End <= text.Length, "range: " + text);
                Assert.True(error.Code is CelErrorCode.FilterSyntaxError or CelErrorCode.NestingTooDeep, "code: " + text);
            }
        }
    }

    [Fact]
    public void FilterOfTheGrammar_ParsesWithoutAnError()
    {
        var random = new FilterGenerator.Source(20261126);
        for (int i = 0; i < Cases; i++)
        {
            StringBuilder filter = new();
            FilterGenerator.Expression(random, filter, 4);

            FilterParseResult result = FilterParser.Parse(filter.ToString());

            Assert.True(result.Error is null, filter + ": " + result.Error?.Message);
        }
    }

    [Fact]
    public void FilterOfTheGrammarOfTheLimitOfTheLength_ParsesWithoutAnError()
    {
        var random = new FilterGenerator.Source(20261127);
        StringBuilder filter = new();
        while (filter.Length < Limit - 200)
        {
            if (filter.Length > 0)
            {
                filter.Append(" AND ");
            }

            FilterGenerator.Restriction(random, filter);
        }

        FilterParseResult result = FilterParser.Parse(filter.ToString());

        Assert.True(filter.Length <= Limit);
        Assert.Null(result.Error);
    }

    [Fact]
    public void RandomParentheses_NeverOverflowTheStack()
    {
        var random = new FilterGenerator.Source(20261128);
        for (int i = 0; i < 50; i++)
        {
            StringBuilder text = new();
            for (int length = random.Next(1, Limit); length > 0; length--)
            {
                text.Append(random.Next(0, 10) < 8 ? '(' : "xf, "[random.Next(0, 4)]);
            }

            FilterParseResult result = FilterParser.Parse(text.ToString());

            Assert.True(result.Error is not null);
        }
    }
}
