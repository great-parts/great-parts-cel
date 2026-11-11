using System.Globalization;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The key extensions: a name that the catalog does not have is an error at the value of name, and the version is checked on its
// own. A part that brings a set adds a test of its name.
public sealed class EnvironmentJsonExtensionTests
{
    [Theory]
    [InlineData("nosuchset")]
    [InlineData("regex")]
    [InlineData("block")]
    [InlineData("cel.lib.ext.strings")]
    [InlineData("Strings")]
    public static void NameOutsideTheCatalog_IsAnErrorAtTheValueThatNamesIt(string name) =>
        AssertError($$"""{"extensions": [{"name": ^"{{name}}", "version": "1"}]}""", $"'{name}'");

    [Theory]
    [InlineData("strings", "5")]
    [InlineData("sets", "0")]
    public static void NameOfASetOfTheLibrary_AddsTheSetOfItsVersion(string name, string version)
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, $$"""{"extensions": [{"name": "{{name}}", "version": "{{version}}"}]}"""));

        Assert.Equal(new[] { CelExtensions.Create(name, uint.Parse(version, CultureInfo.InvariantCulture)) }, builder.Extensions);
    }

    [Theory]
    [InlineData("\"latest\"")]
    [InlineData("\"\"")]
    [InlineData("2")]
    [InlineData("null")]
    [InlineData("[\"1\"]")]
    [InlineData("\"v2\"")]
    [InlineData("\"-1\"")]
    [InlineData("\"+1\"")]
    [InlineData("\"1.5\"")]
    [InlineData("\" 1\"")]
    [InlineData("\"0x1\"")]
    [InlineData("\"4294967296\"")]
    public static void VersionThatIsNotADecimalString_IsAnErrorAtTheValue(string version) =>
        AssertErrors("""{"extensions": [{"name": ^"x", "version": ^""" + version + "}]}", Label, "'x'");

    [Theory]
    [InlineData("0")]
    [InlineData("007")]
    [InlineData("4294967295")]
    public static void VersionInTheRangeOfUint_LeavesOnlyTheErrorOfTheName(string version) =>
        AssertError("{\"extensions\": [{\"name\": ^\"x\", \"version\": \"" + version + "\"}]}", "'x'");

    [Fact]
    public static void MissingKeys_AreErrorsAtTheElement()
    {
        AssertError("""{"extensions": [^{"version": "1"}]}""", "has no 'name'");
        AssertErrors("""{"extensions": [^{"name": ^"x"}]}""", Label, "has no 'version'", "'x'");
    }

    [Theory]
    [InlineData("""{"extensions": [{"name": ^"x", "version": "1", ^"flags": []}]}""")]
    [InlineData("""{"extensions": [{"name": ^"x", "version": "1", ^"version": "2"}]}""")]
    public static void UnknownOrRepeatedKey_IsAnErrorAtTheKey(string marked) =>
        AssertErrors(marked, Label, "'x'");

    [Theory]
    [InlineData("""{"extensions": ^{}}""", "'extensions' must be an array")]
    [InlineData("""{"extensions": [^"strings"]}""", "an element of 'extensions' must be an object")]
    [InlineData("""{"extensions": [{"name": ^1, "version": "1"}]}""", "'name' must be a string")]
    public static void ValueOfTheWrongKind_IsAnErrorAtTheValue(string marked, string detail) =>
        AssertError(marked, detail);

    [Fact]
    public static void EveryElement_IsChecked() =>
        AssertErrors(
            """{"extensions": [{"name": ^"a", "version": ^"latest"}, {"name": ^"b", "version": "1"}, {"name": ^"c", "version": ^""}]}""",
            Label,
            "'a'",
            "'b'",
            "'c'");
}
