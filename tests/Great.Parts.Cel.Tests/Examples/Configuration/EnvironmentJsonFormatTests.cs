using System.Collections.Immutable;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The rules of reading the environment description in JSON (the page of the description, "Reading"): the text
// is UTF-8 and the document is JSON as RFC 8259 defines it, with no comments and no trailing commas; an
// escaped lone surrogate in a string is an error; every schema error is reported and reading goes on, while
// malformed JSON ends reading; the builder changes only when the whole description is right. In the marked
// documents each '|' is the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-4")]
public sealed class EnvironmentJsonFormatTests
{
    // Text that is not UTF-8 is one error at the first byte that does not start a well-formed sequence: a lone
    // continuation byte, a byte that never occurs, an overlong form, a truncated sequence, a surrogate and a
    // value above U+10FFFF written in UTF-8. Nothing else is read, so the unknown key before the bad byte is
    // no second error, and the label has no name.
    [Fact]
    public void TextThatIsNotUtf8_IsOneErrorAtTheFirstBadByte()
    {
        var cases = new (string Prefix, byte[] Bad, string Suffix)[]
        {
            ("{\"description\": \"", [0x80], "\"}"),
            ("{\"description\": \"", [0xFF], "\"}"),
            ("{\"description\": \"", [0xC0, 0x80], "\"}"),
            ("{\"description\": \"", [0xE2, 0x82], "\"}"),
            ("{\"description\": \"", [0xED, 0xA0, 0x80], "\"}"),
            ("{\"description\": \"", [0xF4, 0x90, 0x80, 0x80], "\"}"),
            ("{\"description\": \"é\U0001F600", [0x80], "\"}"),
            ("{\"nme\": 1, \"description\": \"", [0xFF], "\"}"),
        };

        foreach ((string prefix, byte[] bad, string suffix) in cases)
        {
            var builder = JsonExample.BuilderWithState();
            byte[] text = [.. Encoding.UTF8.GetBytes(prefix), .. bad, .. Encoding.UTF8.GetBytes(suffix)];

            CelError error = Assert.Single(CelEnvironmentJson.Apply(builder, text));

            JsonExample.AssertInvalid(error, JsonExample.Label, prefix.Length, 1, prefix.Length + 1);
            JsonExample.AssertUnchanged(builder);
        }
    }

    [Fact]
    public void TextThatIsNotUtf8_ReportsTheLineOfTheBadByte()
    {
        var builder = JsonExample.NewBuilder();
        byte[] text = [.. Encoding.UTF8.GetBytes("{\n  \"description\": \""), 0x80, .. Encoding.UTF8.GetBytes("\"}")];

        CelError error = Assert.Single(CelEnvironmentJson.Apply(builder, text));

        JsonExample.AssertInvalid(error, JsonExample.Label, 20, 2, 19);
    }

    // A string, key or value, with an escaped lone surrogate is an error at the string: a high surrogate
    // without a low one, a low without a high, a pair in the reverse order.
    [Theory]
    [InlineData("""{"description": |"\uD800"}""")]
    [InlineData("""{"description": |"\uDC00"}""")]
    [InlineData("""{"description": |"\uDC00\uD800"}""")]
    [InlineData("""{"description": |"a\uD800b"}""")]
    [InlineData("""{"description": |"\uD800A"}""")]
    [InlineData("""{"name": |"\uDBFF"}""")]
    public void EscapedLoneSurrogateInAValue_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // At a key the error is at the key; the value of the key is skipped, so it is the only error.
    [Theory]
    [InlineData("""{|"\uD800": 1}""")]
    [InlineData("""{|"a\uDC00": {"x": []}}""")]
    [InlineData("""{"description": "d", |"\uDC00\uD800": "x"}""")]
    public void EscapedLoneSurrogateInAKey_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertError(marked);

    // Every such string is reported and reading goes on; a name that is skipped gives no name to the label.
    [Fact]
    public void EveryStringWithALoneSurrogate_IsReported() =>
        JsonExample.AssertErrors("""{"description": |"\uD800", |"\uDC00": 1, "name": |"\uDC00\uD800"}""");

    // A pair, written as two escapes or as the character itself, is not an error.
    [Theory]
    [InlineData("""{"name": "😀", "description": "😀 😀"}""")]
    [InlineData("""{"name": "\uD83D\uDE00", "description": "\uD83D\uDE00 \uD83D\uDE00"}""")]
    [InlineData("""{"name": "\ud83d\ude00", "description": "a\uD83D\uDE00\uD83D\uDE00b"}""")]
    [InlineData("{\"name\": \"\U0001F600\", \"description\": \"é\U0001F600\"}")]
    public void EscapedSurrogatePair_IsNotAnError(string json)
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, json));
        Assert.Empty(JsonExample.Variables(builder));
    }

    // An escaped pair in a key is no lone surrogate: the key is read as the character, so it is an unknown key
    // at the key and the text of the error names the character, not a lone surrogate.
    [Fact]
    public void EscapedSurrogatePairInAKey_IsAnUnknownKeyAtTheKey() =>
        JsonExample.AssertError("""{|"\uD83D\uDE00": 1}""", mentions: "\U0001F600");

    // The document is an object.
    [Theory]
    [InlineData("|[]")]
    [InlineData("  |\"text\"")]
    [InlineData("|null")]
    [InlineData("|1")]
    public void DocumentThatIsNotAnObject_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // JSON as RFC 8259 defines it: a comment, a trailing comma, a single-quoted string, a second value, a
    // missing colon, a document cut short and no document at all end reading with one error. The place is where
    // Utf8JsonReader stopped, which the description of the format does not fix more closely than "not before
    // the mark and within the text"; the mark is the earliest place.
    [Theory]
    [InlineData("""{"description": "d"|,}""", JsonExample.Label)]
    [InlineData("""{"name": "m"|,}""", "environment JSON 'm'")]
    [InlineData("""{"variables": []|,}""", JsonExample.Label)]
    [InlineData("""{"description": "d"| /* comment */}""", JsonExample.Label)]
    [InlineData("""{"description": "d"}| // comment""", JsonExample.Label)]
    [InlineData("""{"description":| 'd'}""", JsonExample.Label)]
    [InlineData("""{"description": "d"}| {}""", JsonExample.Label)]
    [InlineData("""{"description"| "d"}""", JsonExample.Label)]
    [InlineData("""{"variables":| []""", JsonExample.Label)]
    [InlineData("|", JsonExample.Label)]
    public void MalformedJson_IsOneErrorWhereTheReaderStopped(string marked, string label)
    {
        (string json, int[] offsets) = JsonExample.Mark(marked);
        var builder = JsonExample.BuilderWithState();

        CelError error = Assert.Single(JsonExample.Apply(builder, json));

        Assert.Equal(CelErrorCode.InvalidConfiguration, error.Code);
        Assert.Equal(CelErrorStage.Build, error.Stage);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(label, place.SourceName);
        Assert.InRange(place.Offset, offsets[0], json.Length);
        Assert.Equal((1, place.Offset + 1), (place.Line, place.Column));
        JsonExample.AssertUnchanged(builder);
    }

    // Reading ends at the first malformed token, the schema errors before it stay: the unknown key and then
    // the trailing comma.
    [Fact]
    public void MalformedJson_KeepsTheSchemaErrorsBeforeIt()
    {
        const string Json = """{"nme": 1, "description": "d",}""";
        var builder = JsonExample.BuilderWithState();

        ImmutableArray<CelError> errors = JsonExample.Apply(builder, Json);

        Assert.Equal(2, errors.Length);
        JsonExample.AssertInvalid(errors[0], JsonExample.Label, 1, 1, 2);
        Assert.Equal(CelErrorCode.InvalidConfiguration, errors[1].Code);
        Assert.InRange(errors[1].SourceLocation!.Value.Offset, Json.IndexOf(',', 20), Json.Length);
        JsonExample.AssertUnchanged(builder);
    }

    // Nesting deeper than 64 levels ends reading, wherever it is: here inside the value of an unknown key.
    [Fact]
    public void NestingDeeperThan64Levels_IsAnError()
    {
        string json = "{\"nme\": " + new string('[', 70) + new string(']', 70) + "}";
        var builder = JsonExample.BuilderWithState();

        ImmutableArray<CelError> errors = JsonExample.Apply(builder, json);

        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Equal((CelErrorCode.InvalidConfiguration, CelErrorStage.Build), (error.Code, error.Stage)));
        Assert.InRange(errors[^1].SourceLocation!.Value.Offset, 8 + 60, json.Length);
        JsonExample.AssertUnchanged(builder);
    }

    // The line and the column start at 1, the line breaks are \n, \r\n and \r (each is one break), a tab is one
    // column, and a character outside the Basic Multilingual Plane is two units.
    [Theory]
    [InlineData("{\n  \"nme\": 1}", 4, 2, 3)]
    [InlineData("{\r\n  \"nme\": 1}", 5, 2, 3)]
    [InlineData("{\r  \"nme\": 1}", 4, 2, 3)]
    [InlineData("{\n\r\n\r  \"nme\": 1}", 7, 4, 3)]
    [InlineData("{\t\"nme\": 1}", 2, 1, 3)]
    [InlineData("{\"description\": \"\U0001F600\",\n \"nme\": 1}", 23, 2, 2)]
    public void Positions_CountUtf16UnitsAndLineBreaks(string json, int offset, int line, int column)
    {
        CelError error = Assert.Single(JsonExample.Apply(JsonExample.NewBuilder(), json));

        JsonExample.AssertInvalid(error, JsonExample.Label, offset, line, column);
    }

    // A description is applied whole or not at all; two right descriptions add up, and a wrong one after them
    // leaves what they declared.
    [Fact]
    public void RightDescriptions_AddUp_AndAWrongOneChangesNothing()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, """{"variables": [{"name": "a", "type": "int"}]}"""));
        Assert.Empty(JsonExample.Apply(builder, """{"variables": [{"name": "b", "type": "string"}]}"""));
        Assert.NotEmpty(JsonExample.Apply(builder, """{"variables": [{"name": "c", "type": "bool"}], "nme": 1}"""));

        Assert.Equal(new[] { ("a", CelType.Int), ("b", CelType.String) }, JsonExample.Variables(builder));
    }

    // The container and the type sources of a wrong description are not changed either.
    [Fact]
    public void WrongDescription_LeavesTheContainerFunctionsAndTypeSourcesAsTheyWere()
    {
        var builder = JsonExample.BuilderWithState();
        const string Json = """
            {
              "container": "acme",
              "variables": [{ "name": "x", "type": "int" }],
              "functions": [{ "name": "f", "overloads": [{ "id": "f_int", "args": ["int"], "return": "int" }] }],
              "greatPartsCel": { "objectTypes": [{ "name": "acme.Deal", "fields": [{ "name": "amount", "type": "int" }] }] },
              "nme": 1
            }
            """;

        Assert.NotEmpty(JsonExample.Apply(builder, Json));

        JsonExample.AssertUnchanged(builder);
    }
}
