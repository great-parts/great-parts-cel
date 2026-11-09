using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The key extensions of the environment description in JSON (the page of the description, "Extension sets"):
// an element has a name and a version, both required; the name is looked up in the catalog of the sets that the
// library implements, and the version is a string of decimal digits that fits an unsigned 32-bit integer
// (latest, the empty string and a JSON number are errors). The name and the version are checked independently,
// so both can be errors.
//
// The examples use names that no part adds to the catalog: nosuchset, which is no set at all, and the names of
// cel-go that the catalog does not take, regex, block and the full names cel.lib.ext.*. An example of a name
// that the catalog takes, with the call of AddExtension it makes, comes with the part that brings the first set.
// In the marked documents each '|' is the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-3")]
public sealed class EnvironmentJsonExtensionTests
{
    // A name outside the catalog is an error at the value of name, which mentions the name; the names of cel-go
    // outside the catalog, the full names and the internal set of the runner are among them.
    [Theory]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": "1"}]}""", "nosuchset")]
    [InlineData("""{"extensions": [{"name": |"regex", "version": "1"}]}""", "regex")]
    [InlineData("""{"extensions": [{"name": |"block", "version": "1"}]}""", "block")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.strings", "version": "5"}]}""", "cel.lib.ext.strings")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.cel.bindings", "version": "1"}]}""", "cel.lib.ext.cel.bindings")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.comprev2", "version": "2"}]}""", "cel.lib.ext.comprev2")]
    [InlineData("""{"extensions": [{"name": |"Strings", "version": "5"}]}""", "Strings")]
    [InlineData("""{"extensions": [{"version": "1", "name": |"nosuchset"}]}""", "nosuchset")]
    public void NameOutsideTheCatalog_IsAnErrorAtTheValue(string marked, string name) =>
        JsonExample.AssertError(marked, mentions: name);

    // The version of a set is a string of decimal digits in the range of uint: any other value is an error at
    // the value, besides the error of the unknown name.
    [Theory]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"latest"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |""}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |2}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"v2"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"-1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"+1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"1.5"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |" 1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"0x1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |"4294967296"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |null}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": |["1"]}]}""")]
    public void VersionThatIsNotADecimalString_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertErrors(marked);

    // A version that is right leaves only the error of the name, so the range is 0 to 4294967295 and leading
    // zeros do no harm.
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("007")]
    [InlineData("4294967295")]
    public void VersionThatIsRight_LeavesOnlyTheErrorOfTheName(string version) =>
        JsonExample.AssertError("{\"extensions\": [{\"name\": |\"nosuchset\", \"version\": \"" + version + "\"}]}");

    // A missing key is an error at the "{" of the element; the "{" comes before the value of the name.
    [Fact]
    public void MissingNameOrVersion_IsAnErrorAtTheElement()
    {
        JsonExample.AssertError("""{"extensions": [|{"version": "1"}]}""");
        JsonExample.AssertErrors("""{"extensions": [|{"name": |"nosuchset"}]}""", ordered: false);
    }

    [Theory]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": "1", |"flags": []}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": "1", |"description": "d"}]}""")]
    [InlineData("""{"extensions": [{"name": |"nosuchset", "version": "1", |"version": "2"}]}""")]
    public void UnknownOrRepeatedKeyOfAnElement_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertErrors(marked);

    [Fact]
    public void NameOfTheWrongKind_IsAnErrorAtTheValue()
    {
        JsonExample.AssertError("""{"extensions": [{"name": |1, "version": "1"}]}""");
        JsonExample.AssertError("""{"extensions": [{"name": |["strings"], "version": "1"}]}""");
    }

    // Every element is checked and every error is reported, in the order of the text.
    [Fact]
    public void EveryElement_IsChecked() =>
        JsonExample.AssertErrors(
            """{"extensions": [{"name": |"nosuchset", "version": |"latest"}, {"name": |"regex", "version": "1"}, {"name": |"block", "version": |""}]}""");
}
