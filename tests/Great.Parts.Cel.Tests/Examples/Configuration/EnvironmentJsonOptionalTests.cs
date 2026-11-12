using System;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The set optional and the type optional_type in the environment description in JSON (the page of the description, "Extension
// sets" and "Type records"), and the name block, which the description does not take. The name optional with the version 2
// is the call AddExtension(CelExtensions.Optional(2)); another version is UnknownExtensionVersion at Build, at the "{" of
// its element, as for the other sets (EnvironmentJsonExtensionSetTests). The record optional_type<E> is the type optional_type(E)
// and has exactly one parameter, and a name declared by the set needs the set: without it the variable is TypeNotDeclared at its
// element. The internal set block has no name in the description: "block" is an unknown name, InvalidConfiguration at the
// value of name, as the names regex and cel.lib.ext.* are (EnvironmentJsonExtensionTests), whatever its version and
// whichever other sets the description has, and the builder is left as it was; the environment that the application builds from
// a description is not wider than the one it builds in code (the macros of block stay out of it). In the marked documents
// each '|' is the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-3")]
public sealed class EnvironmentJsonOptionalTests
{
    [Fact]
    public static void NameOptional_AddsTheSet_AndItsSyntax()
    {
        const string Expression = "optional.of(1).hasValue() && [1, ?optional.of(2)].size() == 2 && {'a': 1}.?a.hasValue()";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, """{"extensions": [{"name": "optional", "version": "2"}]}"""));

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        CelValue value = environment.Compile<CelValue>(Expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.True(value.TryGetBoolean(out bool truth) && truth);
        Assert.False(JsonExample.NewBuilder().Build().GetEnvironmentOrThrow().Compile<CelValue>(Expression).IsSuccess);
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("1", 1u)]
    [InlineData("3", 3u)]
    [InlineData("007", 7u)]
    [InlineData("4294967295", 4294967295u)]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersionAtBuild(string version, uint number)
    {
        string json = "{\"name\": \"m\", \"extensions\": [{\"name\": \"optional\", \"version\": \"" + version + "\"}]}";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, json));

        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(builder),
            CelErrorCode.UnknownExtensionVersion,
            "extension 'optional' has no version " + number.ToString(CultureInfo.InvariantCulture) + "; available versions: 2",
            "environment JSON 'm'",
            json.IndexOf("{\"name\": \"optional\"", StringComparison.Ordinal));
    }

    // A variable of an optional type, in the forms of the record: the type is that of CelType.OptionalOf, at any depth.
    [Theory]
    [InlineData("optional_type<int>", "int")]
    [InlineData("optional_type<string>", "string")]
    [InlineData("optional_type<list<int>>", "list(int)")]
    [InlineData("optional_type<optional_type<int>>", "optional_type(int)")]
    [InlineData(" optional_type < int > ", "int")]
    public static void RecordOfAnOptionalType_IsTheOptionalType(string record, string inner)
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, "{\"extensions\": [{\"name\": \"optional\", \"version\": \"2\"}], \"variables\": [{\"name\": \"x\", \"type\": \"" + record + "\"}]}"));

        (string Name, CelType Type) variable = Assert.Single(JsonExample.Variables(builder));
        Assert.Equal("x", variable.Name);
        Assert.Equal("optional_type(" + inner + ")", variable.Type.ToString());
        Assert.True(builder.Build().IsSuccess);
    }

    // The form with type_name and params is the same record: the type is the same.
    [Fact]
    public static void FormWithTypeNameAndParams_IsTheSameType()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(
            builder,
            """{"extensions": [{"name": "optional", "version": "2"}], "variables": [{"name": "x", "type_name": "optional_type", "params": ["int"]}, {"name": "y", "type_name": "optional_type", "params": ["list<string>"]}]}"""));

        Assert.Equal(
            new[]
            {
                ("x", CelType.OptionalOf(CelType.Int)),
                ("y", CelType.OptionalOf(CelType.ListOf(CelType.String))),
            },
            JsonExample.Variables(builder));
    }

    [Fact]
    public static void OptionalTypeInsideAnotherRecord_IsTheTypeToo()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(
            builder,
            """{"extensions": [{"name": "optional", "version": "2"}], "variables": [{"name": "a", "type": "list<optional_type<string>>"}, {"name": "b", "type": "map<string, optional_type<int>>"}]}"""));

        Assert.Equal(
            new[]
            {
                ("a", CelType.ListOf(CelType.OptionalOf(CelType.String))),
                ("b", CelType.MapOf(CelType.String, CelType.OptionalOf(CelType.Int))),
            },
            JsonExample.Variables(builder));
        Assert.True(builder.Build().IsSuccess);
    }

    // The record is read when the description is applied and resolved at Build, so the set may come before or after the description,
    // in the description or in code; without it the name is declared by nothing and the variable is the error, at its element.
    [Fact]
    public static void RecordOfAnOptionalType_WithoutTheSet_IsTypeNotDeclaredAtTheVariable()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "optional_type<int>"}]}""";
        var withoutSet = JsonExample.NewBuilder();
        var setAfter = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(withoutSet, Json));
        Assert.Empty(JsonExample.Apply(setAfter, Json));
        setAfter.AddExtension(CelExtensions.Optional(2));

        CelError error = JsonExample.BuildError(withoutSet);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.StartsWith("variable 'x' has type 'optional_type", error.Message, StringComparison.Ordinal);
        Assert.Contains("which no type source or extension of the environment declares", error.Message, StringComparison.Ordinal);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(
            ("environment JSON 'm'", Json.IndexOf("{\"name\": \"x\"", StringComparison.Ordinal)),
            (place.SourceName, place.Offset));
        Assert.True(setAfter.Build().IsSuccess);
    }

    // optional_type has exactly one parameter; the record that has none, two or an open bracket is an error at its value.
    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": |"optional_type"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |"optional_type<>"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |"optional_type<int, string>"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |"optional_type<int"}]}""")]
    public static void RecordOfAnOptionalTypeWithAnotherNumberOfParameters_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // The name block: an unknown name at the value of name, whatever the version and the other sets of the description; the builder
    // keeps its state, so the macros of the set are not in it.
    [Theory]
    [InlineData("""{"extensions": [{"name": |"block", "version": "1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"block", "version": "0"}]}""")]
    [InlineData("""{"extensions": [{"name": |"block", "version": "2"}]}""")]
    [InlineData("""{"extensions": [{"name": "bindings", "version": "1"}, {"name": |"block", "version": "1"}]}""")]
    [InlineData("""{"extensions": [{"name": |"block", "version": "1"}, {"name": "bindings", "version": "1"}]}""")]
    [InlineData("""{"extensions": [{"name": "optional", "version": "2"}, {"name": |"block", "version": "1"}]}""")]
    public static void NameBlock_IsAnErrorAtTheValue(string marked) => JsonExample.AssertError(marked, mentions: "block");

    [Theory]
    [InlineData("""{"extensions": [{"name": |"Block", "version": "1"}]}""", "Block")]
    [InlineData("""{"extensions": [{"name": |"cel.block", "version": "1"}]}""", "cel.block")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.cel.block", "version": "1"}]}""", "cel.lib.ext.cel.block")]
    [InlineData("""{"extensions": [{"name": |"block ", "version": "1"}]}""", "block ")]
    public static void NamesNearBlock_AreUnknownNamesToo(string marked, string name) => JsonExample.AssertError(marked, mentions: name);

    // The name block and a version that is wrong are two errors, the name first.
    [Fact]
    public static void NameBlockWithAnotherVersionError_IsTwoErrors() =>
        JsonExample.AssertErrors("""{"extensions": [{"name": |"block", "version": |"latest"}]}""");
}
