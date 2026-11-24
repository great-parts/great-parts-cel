using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Library;
using Xunit;
using static Great.Parts.Cel.Filtering.Tests.LibraryEnvironments;

namespace Great.Parts.Cel.Filtering.Tests.Examples;

// The translation of a filter into CEL: the value by the type of its field, the guards of a path, the has operator, the stars of
// a string, the null, the function calls, the composite on the right and the value without a field. A filter and the CEL text that it
// becomes have equal trees; an error has its code and its place in the filter.
[Trait("Requirement", "F-32")]
public sealed class TranslationTests
{
    [Theory]
    [InlineData("title = \"Dune\"", "title == \"Dune\"")]
    [InlineData("title = Dune", "title == \"Dune\"")]
    [InlineData("pages > 100", "pages > 100")]
    [InlineData("pages > -5", "pages > -5")]
    [InlineData("pages = 007", "pages == 7")]
    [InlineData("copies = 3", "copies == 3u")]
    [InlineData("rating >= 4", "rating >= 4.0")]
    [InlineData("rating = 1e3", "rating == 1000.0")]
    [InlineData("rating < 4.25", "rating < 4.25")]
    [InlineData("available = true", "available == true")]
    public static void Value_IsReadByTheTypeOfTheField(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("pages > 1e2", 8, 3)]
    [InlineData("pages > 1.5", 8, 3)]
    [InlineData("pages > 99999999999999999999", 8, 20)]
    [InlineData("copies = -3", 9, 2)]
    [InlineData("available = yes", 12, 3)]
    [InlineData("state = Printed", 8, 7)]
    [InlineData("state = Draft", 8, 5, true)]
    [InlineData("published > \"2026-13-01\"", 12, 12)]
    [InlineData("pages = null", 8, 4)]
    [InlineData("pages = \"*1\"", 8, 4)]
    public static void ValueThatDoesNotFitTheField_IsInvalidFilterValueAtTheValue(string filter, int start, int length, bool withoutOptions = false)
    {
        CelFilterCompiler compiler = withoutOptions ? F0 : F;

        Assert.Equal((CelErrorCode.InvalidFilterValue, start, length), FirstError(compiler.Parse(filter).Errors));
        Assert.Equal((CelErrorCode.InvalidFilterValue, start, length), FirstError(compiler.Compile(filter).Errors));
    }

    [Fact]
    public void InvalidFilterValue_HasTheSubjectAndTheTypeOfTheField()
    {
        CelError error = F.Parse("pages > 1e2").Errors[0];

        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal("pages", error.Subject);
        Assert.Equal(CelType.Int, error.ExpectedType);
        Assert.Null(error.ActualType);
    }

    [Fact]
    public void InvalidFilterValue_OfATimestamp_NamesTheTimestampType()
    {
        CelError error = F.Parse("published > \"2026-13-01\"").Errors[0];

        Assert.Equal("published", error.Subject);
        Assert.Equal(CelType.Timestamp, error.ExpectedType);
    }

    [Fact]
    public void InvalidFilterValue_OfAnEnumeration_NamesTheConstants()
    {
        CelError error = F.Parse("state = Printed").Errors[0];

        Assert.Contains("Unspecified, Draft, Published", error.Message, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("available > false", 10, 1, "available", "bool")]
    [InlineData("state > Draft", 6, 1, "state", "int")]
    [InlineData("editions.year = 1965", 14, 1, "editions.year", "list(Library.Edition)")]
    [InlineData("tags = sf", 5, 1, "tags", "list(string)")]
    [InlineData("author = X", 7, 1, "author", "Library.Author")]
    [InlineData("meta:abc", 4, 1, "meta", "dyn")]
    [InlineData("subtitle < null", 9, 1, "subtitle", "string")]
    public static void ComparatorThatTheFieldDoesNotAllow_IsInvalidFilterRestrictionAtTheComparator(
        string filter,
        int start,
        int length,
        string subject,
        string actualType)
    {
        CelError error = Assert.Single(F.Parse(filter).Errors);

        Assert.Equal((CelErrorCode.InvalidFilterRestriction, start, length), (error.Code, error.Span.Start.Offset, error.Span.Length));
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal(subject, error.Subject);
        Assert.Equal(actualType, error.ActualType!.ToString());
        Assert.Null(error.ExpectedType);
    }

    [Theory]
    [InlineData("state = Published", "state == 2")]
    [InlineData("state = \"Published\"", "state == 2")]
    [InlineData("state != Draft", "state != 1")]
    [InlineData("state = 2", "state == 2")]
    [InlineData("state = Library.State.Draft", "state == 1")]
    public static void EnumerationField_IsComparedWithTheNumberOfTheConstant(string filter, string cel) => AssertCel(filter, cel);

    [Fact]
    public void IntegerWithoutAMapping_TakesTheFullNameOfAConstant()
    {
        AssertCel("state = Library.State.Draft", "state == 1", F0);
        Assert.Equal((CelErrorCode.InvalidFilterValue, 8, 5), FirstError(F0.Parse("state = Draft").Errors));
        Assert.Equal(CelType.Int, F0.Parse("state = Draft").Errors[0].ExpectedType);
    }

    [Theory]
    [InlineData("published > \"2026-01-01T00:00:00Z\"", "published > timestamp(\"2026-01-01T00:00:00Z\")")]
    [InlineData("read_time < 20s", "read_time < duration(\"20s\")")]
    [InlineData("read_time < 1.5s", "read_time < duration(\"1.5s\")")]
    [InlineData("read_time < \"1h30m\"", "read_time < duration(\"1h30m\")")]
    [InlineData("read_time < \"-1h30m\"", "read_time < duration(\"-1h30m\")")]
    public static void TimeValue_IsAConversionOfItsText(string filter, string cel) => AssertCel(filter, cel);

    [Fact]
    public void TimeWithoutQuotes_IsASyntaxError()
    {
        Assert.Equal(CelErrorCode.FilterSyntaxError, F.Parse("published > 2026-01-01T00:00:00Z").Errors[0].Code);
        Assert.Equal(CelErrorCode.InvalidFilterValue, F.Parse("published > 2026-01-01").Errors[0].Code);
    }

    [Theory]
    [InlineData("subtitle = \"Part 1\"", "subtitle != null && subtitle == \"Part 1\"")]
    [InlineData("author.name = \"Frank Herbert\"", "author != null && author.name == \"Frank Herbert\"")]
    [InlineData("author.name != \"X\"", "author != null && author.name != \"X\"")]
    [InlineData("author.address.city = Paris", "author != null && has(author.address) && author.address.city == \"Paris\"")]
    [InlineData("author.born > 1900", "author != null && author.born > 1900")]
    public static void CompositeFieldOnThePath_IsGuardedAgainstBeingNotSet(string filter, string cel) => AssertCel(filter, cel);

    [Fact]
    public void NotEqual_OnAnItemWithoutTheCompositeField_DoesNotMatch()
    {
        Assert.False(Matches("author.name != \"X\"", Anon));
        Assert.True(Matches("author.name != \"X\"", Dune));
        Assert.False(Matches("author.address.city = Paris", Dune));
    }

    [Theory]
    [InlineData("title = author.name", "title == \"author.name\"")]
    [InlineData("subtitle = null", "subtitle == null")]
    [InlineData("subtitle != null", "subtitle != null")]
    [InlineData("title = null", "title == \"null\"")]
    [InlineData("meta = null", "meta == null")]
    [InlineData("author.address = null", "author != null && !has(author.address)")]
    [InlineData("meta = 5", "meta == 5")]
    [InlineData("meta = abc", "meta == \"abc\"")]
    [InlineData("meta = true", "meta == true")]
    [InlineData("meta = 1.5", "meta == 1.5")]
    [InlineData("meta = \"5\"", "meta == \"5\"")]
    public static void ValueIsAValue_AndNullIsTheAbsenceOfAWrapperObjectOrDynamicField(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("title = \"*une\"", "title.endsWith(\"une\")")]
    [InlineData("title = \"Du*\"", "title.startsWith(\"Du\")")]
    [InlineData("title = \"*un*\"", "title.contains(\"un\")")]
    [InlineData("title != \"Du*\"", "!title.startsWith(\"Du\")")]
    [InlineData("title = \"*\"", "true")]
    [InlineData("subtitle = \"*\"", "subtitle != null")]
    [InlineData("subtitle = \"Pa*\"", "subtitle != null && subtitle.startsWith(\"Pa\")")]
    [InlineData("title = \"D*e\"", "title == \"D*e\"")]
    [InlineData("title = \"\\*une\"", "title == \"*une\"")]
    public static void StarOfAString_IsAnEndOrAStartOrAContainsTest(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("author:*", "author != null")]
    [InlineData("tags:*", "size(tags) > 0")]
    [InlineData("pages:*", "true")]
    [InlineData("labels:*", "size(labels) > 0")]
    [InlineData("author.address:*", "author != null && has(author.address)")]
    [InlineData("author.name:*", "author != null && has(author.name)")]
    [InlineData("meta:*", "meta != null")]
    [InlineData("subtitle:*", "subtitle != null")]
    [InlineData("labels.genre:*", "\"genre\" in labels")]
    [InlineData("editions.year:*", "editions.exists(e, has(e.year))")]
    public static void LoneStarAfterTheHasOperator_IsThePresenceOfTheField(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("tags:sf", "\"sf\" in tags")]
    [InlineData("tags:\"cl*\"", "tags.exists(e, e.startsWith(\"cl\"))")]
    [InlineData("labels:genre", "\"genre\" in labels")]
    [InlineData("labels.genre:sf", "\"genre\" in labels && labels[\"genre\"] == \"sf\"")]
    [InlineData("labels.genre = sf", "\"genre\" in labels && labels[\"genre\"] == \"sf\"")]
    [InlineData("author:name", "author != null && has(author.name)")]
    [InlineData("author.address:city", "author != null && has(author.address) && has(author.address.city)")]
    [InlineData("title:Dune", "title == \"Dune\"")]
    [InlineData("state:Published", "state == 2")]
    public static void HasOperatorWithAValue_LooksTheValueUpByTheTypeOfTheField(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("editions.year:1965", "editions.exists(e, e.year == 1965)")]
    [InlineData("editions.formats:epub", "editions.exists(e, \"epub\" in e.formats)")]
    [InlineData("editions.formats:\"ep*\"", "editions.exists(e, e.formats.exists(e1, e1.startsWith(\"ep\")))")]
    public static void RepeatedFieldOnThePath_IsWalkedWithAnExistsLoop(string filter, string cel) => AssertCel(filter, cel);

    [Fact]
    public void LoopVariable_SkipsTheNamesOfTheEnvironment()
    {
        CelEnvironment env = CelEnvironment.Create(builder => builder
            .AddTypes(LibraryContext.Default)
            .UseInput<Book>()
            .AddVariable<int>("e"));
        CelFilterCompiler compiler = new(env);

        CelParseResult translated = compiler.Parse("editions.year:1965");
        CelParseResult expected = env.Parse("editions.exists(e1, e1.year == 1965)");

        Assert.Null(SyntaxTreeComparer.Difference(translated.GetParsedExprOrThrow().Root, expected.GetParsedExprOrThrow().Root));
    }

    [Fact]
    public void AuthorNickname_IsNoSuchFieldAtTheNameOfTheField()
    {
        AssertCel("author:nickname", "author != null && has(author.nickname)");

        CelCompileResult<bool> result = F.Compile("author:nickname");

        Assert.Equal((CelErrorCode.NoSuchField, 7, 8), FirstError(result.Errors));
    }

    [Theory]
    [InlineData("isRecent(published)", "isRecent(published)")]
    [InlineData("lower(title) = \"dune\"", "lower(title) == \"dune\"")]
    [InlineData("lower(abc) = \"x\"", "lower(\"abc\") == \"x\"")]
    [InlineData("lower(\"Abc\") = \"x\"", "lower(\"Abc\") == \"x\"")]
    [InlineData("lower(title) = \"d*\"", "lower(title).startsWith(\"d\")")]
    [InlineData("lower(title) != null", "lower(title) != \"null\"")]
    public static void FunctionOfTheEnvironment_IsOneGlobalCall(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("pages <= size(title)", 9, 4)]
    [InlineData("pages = (1 OR size(title))", 14, 4)]
    public static void FunctionOnTheRight_IsInvalidFilterRestrictionAtTheNameOfTheFunction(string filter, int start, int length)
    {
        Assert.Equal((CelErrorCode.InvalidFilterRestriction, start, length), FirstError(F.Parse(filter).Errors));
    }

    [Theory]
    [InlineData("pages = (1 OR 2)", "pages == 1 || pages == 2")]
    [InlineData("pages = (1 OR NOT 2)", "pages == 1 || !(pages == 2)")]
    [InlineData("pages > (1 AND 5)", "pages > 1 && pages > 5")]
    [InlineData("author.name = (\"A\" OR \"B\")", "author != null && author.name == \"A\" || author != null && author.name == \"B\"")]
    [InlineData("lower(title) = (a OR b)", "lower(title) == \"a\" || lower(title) == \"b\"")]
    public static void CompositeOnTheRight_IsAComparisonWithEachValue(string filter, string cel) => AssertCel(filter, cel);

    [Theory]
    [InlineData("Dune", "title.contains(\"Dune\") || author != null && author.name.contains(\"Dune\")")]
    [InlineData("\"Dune Herbert\"", "title.contains(\"Dune Herbert\") || author != null && author.name.contains(\"Dune Herbert\")")]
    public static void ValueWithoutAField_IsSearchedInTheSearchFields(string filter, string cel) => AssertCel(filter, cel);

    [Fact]
    public void ValuesWithoutFields_AreAConjunction()
    {
        AssertCel(
            "Dune Herbert",
            "(title.contains(\"Dune\") || author != null && author.name.contains(\"Dune\"))"
            + " && (title.contains(\"Herbert\") || author != null && author.name.contains(\"Herbert\"))");
    }

    [Fact]
    public void ValueWithoutAField_WithoutSearchFields_IsInvalidFilterRestriction()
    {
        CelError error = Assert.Single(F0.Parse("Dune").Errors);

        Assert.Equal((CelErrorCode.InvalidFilterRestriction, 0, 4), (error.Code, error.Span.Start.Offset, error.Span.Length));
    }

    [Fact]
    public void UndeclaredField_IsTranslatedAsWritten_AndReportedByTheCheck()
    {
        AssertCel("isbn = 1", "isbn == 1");

        Assert.Equal((CelErrorCode.UndeclaredReference, 0, 4), FirstError(F.Compile("isbn = 1").Errors));
    }

    [Fact]
    public void FunctionThatTheArgumentsDoNotFit_IsNoMatchingOverloadAtTheName()
    {
        AssertCel("lower(pages) = \"x\"", "lower(pages) == \"x\"");

        Assert.Equal((CelErrorCode.NoMatchingOverload, 0, 5), FirstError(F.Compile("lower(pages) = \"x\"").Errors));
    }

    [Fact]
    public void ErrorsOfTheTranslation_AreAllReported_AndTheTypeCheckIsSkipped()
    {
        ImmutableArray<CelError> errors = F.Compile("pages > 1.5 AND state = Printed AND isbn = 1").Errors;

        Assert.Equal(2, errors.Length);
        Assert.Equal((CelErrorCode.InvalidFilterValue, 8, 3), (errors[0].Code, errors[0].Span.Start.Offset, errors[0].Span.Length));
        Assert.Equal((CelErrorCode.InvalidFilterValue, 24, 7), (errors[1].Code, errors[1].Span.Start.Offset, errors[1].Span.Length));
    }

    [Fact]
    public void MoreThan100Errors_EndWithTooManyErrors()
    {
        string filter = string.Join(" AND ", System.Linq.Enumerable.Repeat("pages = x", 150));

        ImmutableArray<CelError> errors = F.Parse(filter).Errors;

        Assert.Equal(101, errors.Length);
        Assert.Equal(CelErrorCode.TooManyErrors, errors[100].Code);
    }

    [Theory]
    [InlineData("\"\" = x")]
    [InlineData("author.\"\" = x")]
    [InlineData("author:\"\"")]
    public static void EmptyNameOfAField_IsAnError_NotAnException(string filter)
    {
        Assert.False(F.Compile(filter).IsSuccess);
    }

    [Fact]
    public void FieldOfMoreThan250Parts_IsNestingTooDeep()
    {
        string filter = string.Join('.', System.Linq.Enumerable.Repeat("a", 251)) + " = 1";

        Assert.Equal(CelErrorCode.NestingTooDeep, F.Parse(filter).Errors[0].Code);
    }

    [Fact]
    public void MapWithIntegerKeys_ReadsTheKeyByTheTypeOfTheKeys()
    {
        CelEnvironment env = CelEnvironment.Create(builder => builder.AddVariable("counts", CelType.MapOf(CelType.Int, CelType.String)));
        CelFilterCompiler compiler = new(env);

        CelParseResult translated = compiler.Parse("counts.5 = x");
        CelParseResult expected = env.Parse("5 in counts && counts[5] == \"x\"");

        Assert.Null(SyntaxTreeComparer.Difference(translated.GetParsedExprOrThrow().Root, expected.GetParsedExprOrThrow().Root));
        Assert.Equal(CelErrorCode.InvalidFilterValue, compiler.Parse("counts.five = x").Errors[0].Code);
    }

    [Fact]
    public void ColumnsAndLinesOfAnError_AreThoseOfTheFilter()
    {
        CelError error = F.Parse("title = A\nAND pages > 1.5").Errors[0];

        Assert.Equal(2, error.Span.Start.Line);
        Assert.Equal(22, error.Span.Start.Offset);
    }
}
