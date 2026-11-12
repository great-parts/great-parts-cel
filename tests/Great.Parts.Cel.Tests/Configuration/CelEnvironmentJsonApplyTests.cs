using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Configuration;

public sealed class CelEnvironmentJsonApplyTests
{
    private readonly CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

    [Fact]
    public void NullBuilder_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => CelEnvironmentJson.Apply(null!, "{}"u8));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void Variables_AreAddedAfterTheExistingOnesInTheOrderOfTheArray()
    {
        builder.AddVariable("before", CelType.Bool);

        Assert.Empty(Apply("""{"variables": [{"name": "z", "type": "int"}, {"name": "a", "type": "string"}, {"name": "m", "type": "dyn"}]}"""));

        Assert.Equal(
            new[] { ("before", CelType.Bool), ("z", CelType.Int), ("a", CelType.String), ("m", CelType.Dyn) },
            Declarations());
    }

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": " \t\r\nuint\n"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": "uint"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": "uint", "params": []}]}""")]
    [InlineData("""{"name": "n", "description": "d", "variables": [{"description": "d", "type": "uint", "name": "x"}]}""")]
    public void VariableOfASimpleType_IsDeclared(string json)
    {
        Assert.Empty(Apply(json));
        Assert.Equal(new[] { ("x", CelType.UInt) }, Declarations());
    }

    [Fact]
    public void EscapedKeys_AreReadAsTheirText()
    {
        string json = "{\"n" + Escape('a') + "me\": \"m\", \"variables\": [{\"n" + Escape('a') + "me\": \"x\", \"typ" + Escape('e') + "\": \"uint\"}]}";

        Assert.Empty(Apply(json));
        Assert.Equal(new[] { ("x", CelType.UInt) }, Declarations());
    }

    [Fact]
    public void EverySimpleTypeAndDyn_IsMappedByTypeAndByTypeName()
    {
        const string json = """
            {"variables": [
              {"name": "i", "type": "int"}, {"name": "u", "type": "uint"}, {"name": "d", "type": "double"},
              {"name": "b", "type": "bool"}, {"name": "s", "type": "string"}, {"name": "y", "type": "bytes"},
              {"name": "z", "type": "dyn"},
              {"name": "i2", "type_name": "int"}, {"name": "u2", "type_name": "uint"}, {"name": "d2", "type_name": "double"},
              {"name": "b2", "type_name": "bool"}, {"name": "s2", "type_name": "string"}, {"name": "y2", "type_name": "bytes"},
              {"name": "z2", "type_name": "dyn"}
            ]}
            """;
        CelType[] types = [CelType.Int, CelType.UInt, CelType.Double, CelType.Bool, CelType.String, CelType.Bytes, CelType.Dyn];

        Assert.Empty(Apply(json));

        CelType[] expected = [.. types, .. types];
        Assert.Equal(expected, Declarations().Select(declaration => declaration.Type));
    }

    [Fact]
    public void ByteOrderMark_IsSkipped()
    {
        byte[] json = [0xEF, 0xBB, 0xBF, .. """{"variables": [{"name": "x", "type": "int"}]}"""u8];

        Assert.Empty(CelEnvironmentJson.Apply(builder, json));
        Assert.Equal(new[] { ("x", CelType.Int) }, Declarations());
    }

    [Fact]
    public void ValueOfAKeyWithAnError_IsSkippedWhole()
    {
        string[] details =
        [
            "the key 'stdlib' is not supported",
            "'variables' must be an array",
            "the key 'variables' is repeated",
            "unknown key 'c'",
        ];

        ImmutableArray<CelError> errors = Apply("""{"stdlib": {"a": [1]}, "variables": {"b": {}}, "variables": {"d": 1}, "c": 1}""");

        Assert.Equal(details, errors.Select(Detail));
    }

    // In the tests below "^" marks the place of the error in the JSON text and is removed before reading.
    [Theory]
    [InlineData("^[]", "the description must be an object")]
    [InlineData("""{"name": ^1}""", "'name' must be a string")]
    [InlineData("""{"description": ^null}""", "'description' must be a string")]
    [InlineData("""{"variables": ^{}}""", "'variables' must be an array")]
    [InlineData("""{"variables": [^1]}""", "an element of 'variables' must be an object")]
    [InlineData("""{^"nme": 1}""", "unknown key 'nme'")]
    [InlineData("""{"variables": [], ^"variables": []}""", "the key 'variables' is repeated")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}],^}""", "the text is not well-formed JSON")]
    public void DescriptionError_IsReportedAtItsPlace(string markedJson, string detail)
    {
        AssertMarkedError(markedJson, detail);
    }

    // The element of "variables" alone; a missing key is reported at its "{".
    [Theory]
    [InlineData("""{"name": ^1, "type": "int"}""", "'name' must be a string")]
    [InlineData("""{"name": ^"", "type": "int"}""", "the name of a variable is empty")]
    [InlineData("""{"name": "x", "type": ^1}""", "'type' must be a string")]
    [InlineData("""{"name": "x", "type_name": ^1}""", "'type_name' must be a string")]
    [InlineData("""{"name": "x", "type": "int", "description": ^1}""", "'description' must be a string")]
    [InlineData("""{"name": "x", "type": "int", ^"typ": 1}""", "unknown key 'typ'")]
    [InlineData("""{"name": "x", "type": "int", ^"is_type_param": false}""", "the key 'is_type_param' is not supported")]
    [InlineData("""{"name": "x", ^"name": "y", "type": "int"}""", "the key 'name' is repeated")]
    [InlineData("""^{"type": "int"}""", "a variable has no 'name'")]
    [InlineData("""^{"name": "x"}""", "a variable has neither 'type' nor 'type_name'")]
    [InlineData("""^{"name": "x", "type": "int", "type_name": "int"}""", "a variable has both 'type' and 'type_name'")]
    [InlineData("""^{"name": "x", "type": "int", "params": []}""", "a variable has 'params' without 'type_name'")]
    [InlineData("""{"name": "x", "type_name": ^"list<int>"}""", "'list<int>' is not a type name")]
    [InlineData("""{"name": "x", "type_name": ^" int"}""", "' int' is not a type name")]
    [InlineData("""{"name": "x", "type_name": ^"optional_type"}""", "the type 'optional_type' takes 1 type parameter: 'optional_type'")]
    [InlineData("""{"name": "x", "type_name": ^"map", "params": ["int"]}""", "the type 'map' takes 2 type parameters, but 'map<int>' gives 1")]
    [InlineData("""{"name": "x", "type_name": ^"int", "params": ["int", "dyn"]}""", "the type 'int' has no type parameters: 'int<int, dyn>'")]
    [InlineData("""{"name": "x", "type_name": "list", "params": ^"int"}""", "'params' must be an array")]
    [InlineData("""{"name": "x", "type_name": "list", "params": [^1]}""", "an element of 'params' must be a string")]
    [InlineData("""{"name": "x", "type_name": "list", "params": [^"list<"]}""", "'list<' is not a valid type record")]
    public void VariableError_IsReportedAtItsPlace(string markedVariable, string detail)
    {
        AssertMarkedError("""{"variables": [""" + markedVariable + "]}", detail);
    }

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": ^"list<"}]}""", "'list<' is not a valid type record")]
    [InlineData("""{"variables": [{"name": "x", "type": ^"~T"}]}""", "a type parameter cannot be used in a variable: '~T'")]
    [InlineData("""{"variables": [{"name": "x", "type": ^"list<~T>"}]}""", "a type parameter cannot be used in a variable: 'list<~T>'")]
    [InlineData("""{"variables": [{"name": "x", "type": ^"map<string>"}]}""", "the type 'map' takes 2 type parameters, but 'map<string>' gives 1")]
    [InlineData("""{"variables": [{"name": "x", "type": ^"int<int>"}]}""", "the type 'int' has no type parameters: 'int<int>'")]
    public void TypeError_IsReportedAtTheValue(string markedJson, string detail)
    {
        AssertMarkedError(markedJson, detail);
    }

    // An escaped lone surrogate: high without low, low without high, a reversed pair. Utf8JsonReader reads past it but
    // cannot decode the string. One case per line: the JSON text with "^" at the place of the error → the detail.
    private const string LoneSurrogates = """
        {^"\uD800": 1} → a key has an escaped lone surrogate
        {"name": ^"a\uDC00b"} → 'name' has an escaped lone surrogate
        {"description": ^"\uD800"} → 'description' has an escaped lone surrogate
        {"description": ^"\uD800\u0041"} → 'description' has an escaped lone surrogate
        {"description": ^"\uD800A"} → 'description' has an escaped lone surrogate
        {"description": ^"\uDE00\uD83D"} → 'description' has an escaped lone surrogate
        {"variables": [{"name": ^"x\uD800", "type": "int"}]} → 'name' has an escaped lone surrogate
        {"variables": [{"name": "x", "type": ^"\uDC00"}]} → 'type' has an escaped lone surrogate
        {"variables": [{"name": "x", "type_name": ^"int\uD800"}]} → 'type_name' has an escaped lone surrogate
        {"variables": [{"name": "x", "type_name": "list", "params": [^"\uD800"]}]} → an element of 'params' has an escaped lone surrogate
        {"variables": [{"name": "x", "description": ^"\uD800", "type": "int"}]} → 'description' has an escaped lone surrogate
        {"variables": [{^"\uD800": "x", "name": "x", "type": "int"}]} → a key has an escaped lone surrogate
        """;

    public static TheoryData<string, string> LoneSurrogateCases => CaseTable.Parse(LoneSurrogates);

    [Theory]
    [MemberData(nameof(LoneSurrogateCases))]
    public void EscapedLoneSurrogate_IsAnErrorAtTheString(string markedJson, string detail)
    {
        AssertMarkedError(markedJson, detail);
    }

    [Fact]
    public void ReadingGoesOnAfterAnEscapedLoneSurrogate()
    {
        // The value of the key with the error is skipped; the variable whose name has the error is not reported as
        // having no name.
        const string json = """{"\uD800": {"a": [1]}, "description": "\uDC00", "variables": [{"name": "\uDE00\uD83D", "type": "int"}, {"name": "y", "type": "list<"}], "nme": 1}""";

        int[] offsets = [1, 38, 71, 125, 136];
        string[] details =
        [
            "a key has an escaped lone surrogate",
            "'description' has an escaped lone surrogate",
            "'name' has an escaped lone surrogate",
            "'list<' is not a valid type record",
            "unknown key 'nme'",
        ];

        ImmutableArray<CelError> errors = Apply(json);

        Assert.Equal(offsets, errors.Select(error => Place(error).Offset));
        Assert.Equal(details, errors.Select(Detail));
    }

    [Fact]
    public void EscapedSurrogatePair_IsNotAnError()
    {
        Assert.Empty(Apply("""{"name": "m\uD83D\uDE00", "description": "\uD83D\uDE00", "variables": [{"name": "x", "type": "int"}]}"""));
        Assert.Equal(new[] { ("x", CelType.Int) }, Declarations());
    }

    [Theory]
    [InlineData("imports")]
    [InlineData("stdlib")]
    [InlineData("context_variable")]
    [InlineData("validators")]
    [InlineData("features")]
    [InlineData("limits")]
    public void KeyOutsideTheSubset_IsNotSupported(string key)
    {
        CelError error = Assert.Single(Apply($$"""{"{{key}}": [{"name": [1, {}]}]}"""));

        AssertError(error, "environment JSON", 1, 1, 2, $"the key '{key}' is not supported");
    }

    [Fact]
    public void EveryError_IsReportedInTheOrderOfTheText()
    {
        // The error at the "{" of the first variable is found at its end, after the error of its key.
        const string json = """{"variables": [{"nme": "x", "type": "int"}, {"name": "", "type": "list<"}], "stdlib": 1}""";

        ImmutableArray<CelError> errors = Apply(json);

        Assert.Equal(
            new[]
            {
                (15, "a variable has no 'name'"),
                (16, "unknown key 'nme'"),
                (53, "the name of a variable is empty"),
                (65, "'list<' is not a valid type record"),
                (76, "the key 'stdlib' is not supported"),
            },
            errors.Select(error => (Place(error).Offset, Detail(error))));
    }

    [Fact]
    public void ErrorsAtOnePlace_KeepTheOrderTheyAreFoundIn()
    {
        string[] details =
        [
            "a variable has no 'name'",
            "a variable has neither 'type' nor 'type_name'",
            "a variable has 'params' without 'type_name'",
        ];

        ImmutableArray<CelError> errors = Apply("""{"variables": [{"params": []}]}""");

        Assert.Equal(details, errors.Select(Detail));
        Assert.All(errors, error => Assert.Equal(15, Place(error).Offset));
    }

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}], "nme": 1}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}, {"name": "y", "type": "list<int"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}], "variables": []}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}]""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}]} {}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}, {"name": "y\uD800", "type": "int"}]}""")]
    [InlineData("")]
    public void Error_LeavesTheBuilderUnchanged(string json)
    {
        builder.AddVariable("before", CelType.Bool);

        Assert.NotEmpty(Apply(json));

        Assert.Equal(new[] { ("before", CelType.Bool) }, Declarations());
    }

    [Fact]
    public void InvalidUtf8_IsAnErrorAtTheFirstInvalidByte()
    {
        // The value of an unknown key is not read, yet its bytes are checked too. U+00E9 before the invalid byte is
        // two bytes and one UTF-16 code unit.
        string text = """{"variables": [{"name": "x", "type": "int"}], "x": """ + "\"\x00e9";
        byte[] json = [.. Encoding.UTF8.GetBytes(text), 0xC3, 0x28, .. "\"}"u8];
        builder.AddVariable("before", CelType.Bool);

        CelError error = Assert.Single(CelEnvironmentJson.Apply(builder, json));

        AssertError(error, "environment JSON", 53, 1, 54, "the text is not valid UTF-8");
        Assert.Equal(new[] { ("before", CelType.Bool) }, Declarations());
    }

    [Fact]
    public void TruncatedUtf8AtTheEnd_IsAnError()
    {
        CelError error = Assert.Single(CelEnvironmentJson.Apply(builder, [.. "{} "u8, 0xC3]));

        AssertError(error, "environment JSON", 3, 1, 4, "the text is not valid UTF-8");
    }

    // Malformed JSON ends reading where Utf8JsonReader stopped.
    [Theory]
    [InlineData("", 0, 1, 1)]
    [InlineData("{} x", 3, 1, 4)]
    [InlineData("{\r\n  \"name\": tru\r\n}", 16, 2, 14)]
    [InlineData("{\n\"variables\": []\n", 18, 3, 1)]
    [InlineData("{\"description\": \"x\ty\"}", 18, 1, 19)]
    public void MalformedJson_IsAnErrorWhereReadingStopped(string json, int offset, int line, int column)
    {
        CelError error = Assert.Single(Apply(json));

        AssertError(error, "environment JSON", offset, line, column, "the text is not well-formed JSON");
    }

    [Fact]
    public void NestingDeeperThan64Levels_IsMalformedJson()
    {
        // The object is the first level; the first element of "variables" is an error before reading stops.
        string json = """{"variables": """ + new string('[', 64) + new string(']', 64) + "}";

        ImmutableArray<CelError> errors = Apply(json);

        Assert.Equal(2, errors.Length);
        AssertError(errors[1], "environment JSON", 77, 1, 78, "the text is not well-formed JSON");
    }

    [Fact]
    public void SchemaErrorsBeforeMalformedJson_AreKept()
    {
        ImmutableArray<CelError> errors = Apply("""{"x": 1, "name": "m", "y": tru}""");

        Assert.Equal(
            new[] { (1, "unknown key 'x'"), (30, "the text is not well-formed JSON") },
            errors.Select(error => (Place(error).Offset, Detail(error))));
        Assert.All(errors, error => Assert.Equal("environment JSON 'm'", Place(error).SourceName));
    }

    [Theory]
    [InlineData("""{"name": "", "nme": 1}""", "environment JSON ''")]
    [InlineData("""{"nme": 1, "name": "m"}""", "environment JSON 'm'")]
    [InlineData("""{"name": "a", "name": "b"}""", "environment JSON")]
    [InlineData("""{"name": 1}""", "environment JSON")]
    [InlineData("""{"x": tru, "name": "m"}""", "environment JSON")]
    public void Errors_NameTheDescriptionByTheValueOfName(string json, string sourceName)
    {
        ImmutableArray<CelError> errors = Apply(json);

        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Equal(sourceName, Place(error).SourceName));
    }

    [Fact]
    public void SourceName_IsTheDecodedValueOfName()
    {
        CelError error = Assert.Single(Apply("{\"name\": \"m" + Escape('\x00e9') + "\", \"nme\": 1}"));

        Assert.Equal("environment JSON 'm\x00e9'", Place(error).SourceName);
    }

    [Fact]
    public void Positions_CountUtf16CodeUnitsAndAllLineBreaks()
    {
        const string json = "{\"description\": \"\U0001F600\",\r\n \"a\": 1,\r \"b\": 2,\n \"c\": 3}";

        ImmutableArray<CelError> errors = Apply(json);

        Assert.Equal(
            "24 2:2|33 3:2|42 4:2",
            string.Join('|', errors.Select(error => string.Create(
                CultureInfo.InvariantCulture,
                $"{Place(error).Offset} {Place(error).Line}:{Place(error).Column}"))));
    }

    [Fact]
    public void OnlyOneByteOrderMark_IsSkipped()
    {
        byte[] json = [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. "{}"u8];

        CelError error = Assert.Single(CelEnvironmentJson.Apply(builder, json));

        AssertError(error, "environment JSON", 0, 1, 1, "the text is not well-formed JSON");
    }

    private ImmutableArray<CelError> Apply(string json) =>
        CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(json));

    private (string Name, CelType Type)[] Declarations() =>
        [.. builder.VariableDeclarations.Select(declaration => (declaration.Name, declaration.Type))];

    // The JSON escape of a character, a backslash, "u" and four hexadecimal digits.
    private static string Escape(char c) =>
        string.Create(CultureInfo.InvariantCulture, $"{'\\'}u{(int)c:x4}");

    private static CelSourceLocation Place(CelError error) => Assert.NotNull(error.SourceLocation);

    private void AssertMarkedError(string markedJson, string detail)
    {
        int offset = markedJson.AsSpan().IndexOf('^');

        CelError error = Assert.Single(Apply(string.Concat(markedJson.AsSpan(0, offset), markedJson.AsSpan(offset + 1))));

        AssertError(error, "environment JSON", offset, 1, offset + 1, detail);
    }

    private static string Detail(CelError error) => error.Message[(error.Message.IndexOf(": ", StringComparison.Ordinal) + 2)..];

    private static void AssertError(CelError error, string sourceName, int offset, int line, int column, string detail)
    {
        Assert.Equal(CelErrorCode.InvalidConfiguration, error.Code);
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Equal(default, error.Location);
        Assert.Null(error.Exception);
        Assert.Equal(new CelSourceLocation(sourceName, offset, line, column), error.SourceLocation);
        Assert.Equal(
            string.Create(CultureInfo.InvariantCulture, $"invalid environment description at line {line}, column {column}: {detail}"),
            error.Message);
    }
}
