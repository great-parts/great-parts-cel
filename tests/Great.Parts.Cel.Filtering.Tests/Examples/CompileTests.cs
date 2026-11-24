using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;
using static Great.Parts.Cel.Filtering.Tests.LibraryEnvironments;

namespace Great.Parts.Cel.Filtering.Tests.Examples;

// The places of the nodes of a translated filter, the compilation of a filter into a program and its evaluation, and the errors
// of the compilation shown against the text of the filter.
[Trait("Requirement", "F-32")]
public sealed class CompileTests
{
    [Fact]
    public void Comparison_HasThePlacesOfItsParts()
    {
        CelParsedExpr tree = F.Parse("title = \"Dune\"").GetParsedExprOrThrow();
        CelCallNode root = Assert.IsType<CelCallNode>(tree.Root);

        Assert.Equal((6, 1), Span(tree, root));
        Assert.Equal((0, 5), Span(tree, root.Arguments[0]));
        Assert.Equal((8, 6), Span(tree, root.Arguments[1]));
    }

    [Fact]
    public void SelectionAndGuard_HaveThePlaceOfTheFieldPartAndOfTheWholeField()
    {
        CelParsedExpr tree = F.Parse("author.address.city = Paris").GetParsedExprOrThrow();

        CelSelectNode city = tree.Root.DescendantsAndSelf().OfType<CelSelectNode>().Single(select => select.Field == "city" && !select.IsPresenceTest);
        CelSelectNode presence = tree.Root.DescendantsAndSelf().OfType<CelSelectNode>().Single(select => select.IsPresenceTest && select.Field == "address");

        Assert.Equal((15, 4), Span(tree, city));
        Assert.Equal((0, 19), Span(tree, presence));
    }

    [Fact]
    public void KeywordAnd_IsThePlaceOfTheConjunction()
    {
        CelParsedExpr tree = F.Parse("title = A AND pages > 1").GetParsedExprOrThrow();

        Assert.Equal((10, 3), Span(tree, tree.Root));
    }

    [Fact]
    public void SequenceOfRestrictions_IsThePlaceFromTheStartOfTheLeftToTheEndOfTheRight()
    {
        CelParsedExpr tree = F.Parse("title = A pages > 1").GetParsedExprOrThrow();

        Assert.Equal((0, 19), Span(tree, tree.Root));
    }

    [Theory]
    [InlineData("title = Dune")]
    [InlineData("pages > 100 AND copies = 3 OR rating >= 4")]
    [InlineData("published > \"2026-01-01T00:00:00Z\"")]
    [InlineData("read_time < 20s")]
    [InlineData("subtitle = \"Part 1\"")]
    [InlineData("author.address.city = Paris")]
    [InlineData("state = Published AND state != Draft")]
    [InlineData("title = \"*un*\" OR title != \"Du*\"")]
    [InlineData("author:name OR author:*")]
    [InlineData("tags:sf AND tags:\"cl*\" AND tags:*")]
    [InlineData("labels:genre AND labels.genre = sf AND labels.genre:*")]
    [InlineData("editions.year:1965 AND editions.formats:epub AND editions.formats:\"ep*\"")]
    [InlineData("isRecent(published) AND lower(title) = dune")]
    [InlineData("pages = (1 OR NOT 2) AND meta = null AND meta:*")]
    [InlineData("Dune Herbert")]
    [InlineData("")]
    public static void PrintedTranslation_ParsesAndCompilesInTheEnvironment(string filter)
    {
        CelParsedExpr tree = F.Parse(filter).GetParsedExprOrThrow();

        string text = CelSyntax.Format(tree);
        CelParseResult parsed = Env.Parse(text);

        Assert.True(parsed.IsSuccess, filter + " as " + text + ": " + string.Join("; ", parsed.Errors));
        Assert.True(Env.Compile<bool>(text).IsSuccess, filter + " as " + text);
        Assert.True(F.Compile(filter).IsSuccess, filter + ": " + string.Join("; ", F.Compile(filter).Errors));
    }

    [Fact]
    public void ProgramOfAConjunction_IsTrueForTheBookThatHasTheAuthorAndFalseWithout()
    {
        const string Filter = "author.name = \"Frank Herbert\" AND published < \"1970-01-01T00:00:00Z\"";

        Assert.True(Matches(Filter, Dune));
        Assert.False(Matches(Filter, Anon));
    }

    [Fact]
    public void ProgramOfSeveralKindsOfRestrictions_IsTrueForTheBook()
    {
        Assert.True(Matches("editions.formats:epub AND tags:sf AND labels.genre = sf AND state = Published", Dune));
        Assert.False(Matches("editions.formats:pdf", Dune));
        Assert.False(Matches("editions.year:1999", Dune));
        Assert.True(Matches("editions.year:2005 AND tags:\"cl*\" AND NOT tags:xyz", Dune));
    }

    [Fact]
    public void ProgramOfNumbersAndTimes_IsFalseWhenTheDurationIsLonger()
    {
        Assert.False(Matches("rating >= 4 AND read_time < 20s", Dune));
        Assert.True(Matches("rating >= 4 AND read_time > 20s", Dune));
        Assert.True(Matches("pages > 400 AND copies = 0", Dune));
    }

    [Fact]
    public void ProgramOfPresenceAndNull_TellsWhetherTheFieldIsSet()
    {
        Assert.True(Matches("author:*", Dune));
        Assert.False(Matches("author:*", Anon));
        Assert.False(Matches("author.address:*", Dune));
        Assert.True(Matches("subtitle = null", Dune));
        Assert.False(Matches("subtitle:*", Dune));
        Assert.True(Matches("pages:*", Dune));
        Assert.True(Matches("meta = null", Dune));
        Assert.True(Matches("labels.genre:*", Dune));
        Assert.False(Matches("labels.mood:*", Dune));
    }

    [Fact]
    public void ProgramOfValuesWithoutFields_SearchesTheTitleAndTheAuthor()
    {
        Assert.True(Matches("Dune", Dune));
        Assert.True(Matches("Herbert", Dune));
        Assert.False(Matches("Herbert", Anon));
        Assert.True(Matches("Dune Herbert", Dune));
        Assert.False(Matches("Dune Asimov", Dune));
    }

    [Fact]
    public void ProgramOfFunctions_CallsTheFunctionsOfTheEnvironment()
    {
        Assert.False(Matches("isRecent(published)", Dune));
        Assert.True(Matches("lower(title) = dune", Dune));
        Assert.True(Matches("lower(title) = \"du*\"", Dune));
    }

    [Fact]
    public void NullFilterOrEnvironment_IsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => F.Compile(null!));
        Assert.Throws<ArgumentNullException>(() => F.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => new CelFilterCompiler(null!));
    }

    [Fact]
    public void ErrorOfTheCompilation_ShowsTheFilterAndAPointerUnderTheValue()
    {
        const string Filter = "pages > 1e2";

        string display = F.Compile(Filter).Errors[0].ToDisplayString(Filter);

        string[] lines = display.Split('\n');
        Assert.Contains(Filter, lines[1], StringComparison.Ordinal);
        Assert.Equal(8, lines[2].IndexOf('^', StringComparison.Ordinal));
    }

    [Fact]
    public void ErrorOfTheTypeCheck_ShowsThePlaceInTheFilter()
    {
        const string Filter = "author:nickname";

        string display = F.Compile(Filter).Errors[0].ToDisplayString(Filter);

        Assert.Contains(Filter, display, StringComparison.Ordinal);
    }

    private static (int Start, int Length) Span(CelParsedExpr tree, CelExprNode node)
    {
        CelSourceSpan span = tree.GetSpan(node.Id);
        return (span.Start.Offset, span.Length);
    }
}
