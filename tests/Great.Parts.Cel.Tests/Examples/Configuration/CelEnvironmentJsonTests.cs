using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The first examples of the environment description in JSON: the keys name, description and variables with
// variables of simple types and dyn, the errors of unknown, repeated and unsupported keys, of values of the
// wrong kind and of a type record that is wrong. The other keys, the type records and the rules of reading
// have examples of their own in this directory. Positions in the JSON text count UTF-16 code units of the
// decoded text; the line and the column start at 1; a byte order mark is not counted.
[Trait("Requirement", "F-7")]
public sealed class CelEnvironmentJsonTests
{
    private readonly CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);

    [Fact]
    public void Subset_GivesTheSameDeclarationsAsAddVariable()
    {
        const string json = """
            {
              "name": "every simple type",
              "description": "Variables of the six simple types and dyn.",
              "variables": [
                { "name": "i", "type": "int" },
                { "name": "u", "type": "uint", "description": "An unsigned integer." },
                { "name": "d", "type": "double" },
                { "name": "b", "type_name": "bool" },
                { "name": "s", "type_name": "string", "params": [] },
                { "name": "y", "type": "bytes" },
                { "name": "z", "type": "dyn" }
              ]
            }
            """;
        CelEnvironmentBuilder inCode = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("i", CelType.Int)
            .AddVariable("u", CelType.UInt)
            .AddVariable("d", CelType.Double)
            .AddVariable("b", CelType.Bool)
            .AddVariable("s", CelType.String)
            .AddVariable("y", CelType.Bytes)
            .AddVariable("z", CelType.Dyn);

        Assert.Empty(Apply(json));
        Assert.Equal(Declarations(inCode), Declarations(builder));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"variables": []}""")]
    [InlineData("""{"name": "empty", "description": ""}""")]
    public void NoVariables_DeclaresNothing(string json)
    {
        Assert.Empty(Apply(json));
        Assert.Empty(Declarations(builder));
    }

    [Fact]
    public void ByteOrderMark_IsSkipped()
    {
        byte[] json = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("""{"variables": [{"name": "x", "type": "int"}]}""")];

        Assert.Empty(CelEnvironmentJson.Apply(builder, json));
        Assert.Equal(new[] { ("x", CelType.Int) }, Declarations(builder));
    }

    [Fact]
    public void ByteOrderMark_IsNotCountedInPositions()
    {
        byte[] json = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("""{"nme": 1}""")];

        CelError error = Assert.Single(CelEnvironmentJson.Apply(builder, json));

        AssertInvalidConfiguration(error, "environment JSON", 1, 1, 2);
    }

    // Unknown, repeated and unsupported keys are reported at the key.
    [Theory]
    [InlineData("""{"nme": "a"}""", 1, 1, 2)]
    [InlineData("""{"description": "a", "description": "b"}""", 21, 1, 22)]
    [InlineData("""{"stdlib": {}}""", 1, 1, 2)]
    [InlineData("""{"variables": [{"name": "x", "type": "int", "is_type_param": false}]}""", 44, 1, 45)]
    [InlineData("{\"description\": \"\u00e9\U0001F600\", \"nme\": 1}", 23, 1, 24)]
    public void KeyError_IsReportedAtTheKey(string json, int offset, int line, int column)
    {
        CelError error = Assert.Single(Apply(json));

        AssertInvalidConfiguration(error, "environment JSON", offset, line, column);
    }

    // A value of the wrong kind, an empty name and a wrong type record are reported at the value;
    // a missing required key, "type" with "type_name" and "params" without "type_name" at the "{" of the object.
    [Theory]
    [InlineData(14, """{"variables": {}}""")]
    [InlineData(24, """{"variables": [{"name": "", "type": "int"}]}""")]
    [InlineData(37, """{"variables": [{"name": "x", "type": "list<int, int>"}]}""")]
    [InlineData(37, """{"variables": [{"name": "x", "type": "~T"}]}""")]
    [InlineData(15, """{"variables": [{"type": "int"}]}""")]
    [InlineData(15, """{"variables": [{"name": "x", "type": "int", "type_name": "int"}]}""")]
    [InlineData(15, """{"variables": [{"name": "x", "type": "int", "params": []}]}""")]
    public void ValueOrObjectError_IsReportedAtTheValueOrTheObject(int offset, string json)
    {
        CelError error = Assert.Single(Apply(json));

        AssertInvalidConfiguration(error, "environment JSON", offset, 1, offset + 1);
    }

    [Fact]
    public void Errors_NameTheDescriptionAndTheLine()
    {
        const string json = "{\n  \"name\": \"m\",\n  \"imports\": []\n}";

        CelError error = Assert.Single(Apply(json));

        AssertInvalidConfiguration(error, "environment JSON 'm'", 19, 3, 3);
    }

    [Fact]
    public void EverySchemaError_IsReportedInTheOrderOfTheText()
    {
        const string json = """{"nme": 1, "variables": [{"name": "x", "type": "list<int, int>"}], "stdlib": {}}""";

        ImmutableArray<CelError> errors = Apply(json);

        Assert.Equal(new int?[] { 1, 47, 67 }, errors.Select(error => error.SourceLocation?.Offset));
    }

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}], "nme": 1}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}],}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int"}]""")]
    public void Error_LeavesTheBuilderUnchanged(string json)
    {
        builder.AddVariable("before", CelType.Bool);

        ImmutableArray<CelError> errors = Apply(json);

        Assert.NotEmpty(errors);
        Assert.All(errors, error => Assert.Equal(CelErrorCode.InvalidConfiguration, error.Code));
        Assert.Equal(new[] { ("before", CelType.Bool) }, Declarations(builder));
    }

    private ImmutableArray<CelError> Apply(string json) =>
        CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(json));

    // The declarations are a set: the order of the calls does not matter.
    private static (string Name, CelType Type)[] Declarations(CelEnvironmentBuilder source) =>
        [.. source.VariableDeclarations
            .Select(declaration => (declaration.Name, declaration.Type))
            .OrderBy(declaration => declaration.Name, StringComparer.Ordinal)];

    private static void AssertInvalidConfiguration(CelError error, string sourceName, int offset, int line, int column)
    {
        Assert.Equal(CelErrorCode.InvalidConfiguration, error.Code);
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Equal(0, error.Location.Line);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal((sourceName, offset, line, column), (place.SourceName, place.Offset, place.Line, place.Column));
        string prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"invalid environment description at line {line}, column {column}: ");
        Assert.StartsWith(prefix, error.Message, StringComparison.Ordinal);
    }
}
