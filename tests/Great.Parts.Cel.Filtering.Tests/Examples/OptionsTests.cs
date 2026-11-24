using System;
using System.Collections.Immutable;
using Xunit;
using static Great.Parts.Cel.Filtering.Tests.LibraryEnvironments;

namespace Great.Parts.Cel.Filtering.Tests.Examples;

// The options of the compiler: which integer fields are enumerations, in which fields a value without a field is searched, and what
// the compiler refuses when it is created.
[Trait("Requirement", "F-32")]
public sealed class OptionsTests
{
    [Fact]
    public void EnumerationOfAnIntegerField_IsReadByTheNamesOfItsConstants()
    {
        CelFilterCompiler compiler = new(Env, Options("pages", "Library.State"));

        AssertCel("pages = Draft", "pages == 1", compiler);
    }

    [Fact]
    public void EnumerationOfAFieldOfAnObjectType_IsNamedByTheObjectTypeAndTheField()
    {
        CelFilterCompiler compiler = new(Env, Options("Library.Author.born", "Library.State"));

        AssertCel("author.born = Draft", "author != null && author.born == 1", compiler);
    }

    [Theory]
    [InlineData("title", "Library.State")]
    [InlineData("state", "Library.Color")]
    [InlineData("isbn", "Library.State")]
    public static void EnumerationThatTheEnvironmentDoesNotFit_IsRefusedWhenTheCompilerIsCreated(string field, string enumType)
    {
        Assert.Throws<ArgumentException>(() => new CelFilterCompiler(Env, Options(field, enumType)));
    }

    [Theory]
    [InlineData("pages")]
    [InlineData("author.nickname")]
    public static void SearchFieldThatIsNotAStringField_IsRefusedWhenTheCompilerIsCreated(string field)
    {
        CelFilterOptions options = new() { SearchFields = [field] };

        Assert.Throws<ArgumentException>(() => new CelFilterCompiler(Env, options));
    }

    [Fact]
    public void SearchFieldOfTheWrapperOfAString_IsAccepted()
    {
        CelFilterOptions options = new() { SearchFields = ["subtitle"] };

        CelFilterCompiler compiler = new(Env, options);

        AssertCel("Part", "subtitle != null && subtitle.contains(\"Part\")", compiler);
    }

    [Fact]
    public void CompilerWithoutOptions_HasEmptyOptions()
    {
        CelFilterCompiler compiler = new(Env);

        Assert.Empty(compiler.Options.EnumTypes);
        Assert.False(compiler.Options.SearchFields.IsDefault);
        Assert.True(compiler.Options.SearchFields.IsEmpty);
    }

    // The options with one enumeration field; the dictionary is built with Add, which an immutable dictionary needs.
    private static CelFilterOptions Options(string field, string enumType) => new()
    {
        EnumTypes = ImmutableDictionary<string, string>.Empty.Add(field, enumType),
    };
}
