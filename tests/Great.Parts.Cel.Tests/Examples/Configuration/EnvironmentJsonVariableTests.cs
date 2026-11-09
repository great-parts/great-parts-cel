using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The key variables of the environment description in JSON (the page of the description, "Mapping onto builder
// calls" and "Type records"): a variable has a name and exactly one of type and type_name, which takes params
// and a name without white space; description is metadata. A variable is AddVariable(name, type). What the
// builder reports at Build is not an error of the description: Apply returns none, and Build names the
// declaration by the label of the description. In the marked documents each '|' is the place of one expected
// error (see JsonExample).
[Trait("Requirement", "F-7")]
public sealed class EnvironmentJsonVariableTests
{
    // type and type_name with params are two forms of one record, and a variable is the call of AddVariable.
    [Theory]
    [InlineData("""{"name": "x", "type": "list<int>"}""", "list_int")]
    [InlineData("""{"name": "x", "type_name": "list", "params": ["int"]}""", "list_int")]
    [InlineData("""{"name": "x", "type": "map<string, list<int>>"}""", "map_string_list_int")]
    [InlineData("""{"name": "x", "type_name": "map", "params": ["string", "list<int>"]}""", "map_string_list_int")]
    [InlineData("{\"name\": \"x\", \"type\": \"map<\\tstring ,\\r\\n list< int > >\"}", "map_string_list_int")]
    [InlineData("""{"name": "x", "type_name": "type", "params": ["int"]}""", "type_int")]
    [InlineData("""{"name": "x", "type": "int", "description": "A count."}""", "int")]
    [InlineData("""{"name": "x", "type_name": "int", "params": []}""", "int")]
    public void TypeForms_AreOneRecord(string variable, string key)
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, "{\"variables\": [" + variable + "]}"));

        (string name, CelType type) = Assert.Single(JsonExample.Variables(builder));
        Assert.Equal("x", name);
        Assert.Equal(
            key switch
            {
                "int" => CelType.Int,
                "list_int" => CelType.ListOf(CelType.Int),
                "map_string_list_int" => CelType.MapOf(CelType.String, CelType.ListOf(CelType.Int)),
                "type_int" => CelType.TypeOf(CelType.Int),
                _ => throw new System.ArgumentOutOfRangeException(nameof(key)),
            },
            type);
    }

    [Fact]
    public void Variables_AreDeclaredInTheOrderOfTheTextAsASet()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, """
            {"variables": [
              {"name": "b", "type": "string"},
              {"name": "acme.a", "type": "list<double>"},
              {"name": "c", "type": "dyn"}
            ]}
            """));

        Assert.Equal(
            new[] { ("acme.a", CelType.ListOf(CelType.Double)), ("b", CelType.String), ("c", CelType.Dyn) },
            JsonExample.Variables(builder));
    }

    // type_name is the name alone, so it names an object type of a source as well as a simple type; a leading
    // dot of a record is dropped.
    [Theory]
    [InlineData("""{"name": "x", "type": "acme.crm.Deal"}""")]
    [InlineData("""{"name": "x", "type": ".acme.crm.Deal"}""")]
    [InlineData("""{"name": "x", "type_name": "acme.crm.Deal"}""")]
    public void NameOfAnObjectType_IsAnObjectTypeNamedByIt(string variable)
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, "{\"variables\": [" + variable + "]}"));

        Assert.Equal(new[] { ("x", CelType.ObjectNamed("acme.crm.Deal")) }, JsonExample.Variables(builder));
    }

    // A missing name, neither or both of the type forms and params without type_name are errors at the "{" of
    // the element.
    [Theory]
    [InlineData("""{"variables": [|{"type": "int"}]}""")]
    [InlineData("""{"variables": [|{"name": "x"}]}""")]
    [InlineData("""{"variables": [|{"name": "x", "type": "int", "type_name": "int"}]}""")]
    [InlineData("""{"variables": [|{"name": "x", "type": "int", "params": []}]}""")]
    [InlineData("""{"variables": [|{"name": "x", "description": "d"}]}""")]
    public void MissingOrExcessiveKeys_AreAnErrorAtTheElement(string marked) =>
        JsonExample.AssertError(marked);

    // An empty name and a value of the wrong kind are errors at the value.
    [Theory]
    [InlineData("""{"variables": [{"name": |"", "type": "int"}]}""")]
    [InlineData("""{"variables": [{"name": |1, "type": "int"}]}""")]
    [InlineData("""{"variables": [{"name": |null, "type": "int"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |1}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |["int"]}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int", "description": |1}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |1}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": "int", "params": |"int"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": "int", "params": [|1]}]}""")]
    public void EmptyNameOrValueOfTheWrongKind_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // type_name is a type name without white space and without parameters; the parameters are in params.
    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type_name": |"list<int>"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |" int"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |"int "}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |""}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |"a..b"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |"1a"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type_name": |"~T"}]}""")]
    public void TypeNameThatIsNotAName_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // A type parameter is no type of a variable, in either form; the object form of a record is outside the
    // format, and the keys of the object are not read.
    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": |"~T"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |"list<~T>"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": |{"type_name": "int", "params": [], "is_type_param": false}}]}""")]
    public void TypeParameterOrObjectForm_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": "int", |"default": 1}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int", |"is_type_param": false}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int", |"typeName": "int"}]}""")]
    [InlineData("""{"variables": [{"name": "x", "type": "int", |"type": "int"}]}""")]
    [InlineData("""{"variables": [{"name": "x", |"name": "y", "type": "int"}]}""")]
    public void UnknownOrRepeatedKeyOfAVariable_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertError(marked);

    // Every variable is checked and every error is reported, in the order of the text.
    [Fact]
    public void EveryVariable_IsChecked() =>
        JsonExample.AssertErrors(
            """{"variables": [{"name": |"", "type": "int"}, {"name": "x", "type": |1}, |{"name": "y"}, {"name": "z", "type": |"~T"}]}""");

    // A name that is no identifier or qualified name is not an error of the description: the builder reports
    // InvalidVariableName at Build, with the label of the description and the place of the variable.
    [Theory]
    [InlineData("a-b")]
    [InlineData("1x")]
    [InlineData("a..b")]
    [InlineData("a b")]
    public void NameThatIsNotAnIdentifier_IsInvalidVariableNameAtBuild(string name)
    {
        string json = "{\"name\": \"m\", \"variables\": [{\"name\": \"" + name + "\", \"type\": \"int\"}]}";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.InvalidVariableName,
            "variable name '" + name + "' of environment JSON 'm' is not an identifier or a qualified name",
            "environment JSON 'm'",
            json.IndexOf("{\"name\": \"" + name, System.StringComparison.Ordinal));
    }

    // The same name with two types: the sources are the declarations, application before environment JSON; of
    // two of one description the one earlier in the text comes first, and the place is the first JSON
    // declaration that the error names.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void TwoVariablesOfOneName_AreDuplicateVariable()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "int"}, {"name": "x", "type": "string"}]}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.DuplicateVariable,
            "variable 'x' is declared by both environment JSON 'm' and environment JSON 'm'",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"x\"", System.StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    public void VariableOfTheCodeAndOfTheDescription_AreDuplicateVariable()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "string"}]}""";
        var builder = JsonExample.NewBuilder().AddVariable("x", CelType.Int);
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.DuplicateVariable,
            "variable 'x' is declared by both application and environment JSON 'm'",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"x\"", System.StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    public void VariableWithAnIdentifierOfTheStandardLibrary_IsStandardLibraryConflict()
    {
        const string Json = """{"variables": [{"name": "int", "type": "dyn"}]}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.StandardLibraryConflict,
            "variable 'int' of environment JSON conflicts with standard library v1: it is a type identifier",
            "environment JSON",
            Json.IndexOf("{\"name\": \"int\"", System.StringComparison.Ordinal));
    }

    // An object type that no source declares is TypeNotDeclared at Build, not an error of the description.
    [Fact]
    public void ObjectTypeThatNoSourceDeclares_IsTypeNotDeclaredAtBuild()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "acme.crm.Deal"}]}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        // The text names no source; the place is the "{" of the variable in the JSON.
        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.TypeNotDeclared,
            "variable 'x' has type 'acme.crm.Deal', which no type source or extension of the environment declares",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"x\"", System.StringComparison.Ordinal));
    }

    // The same error for a variable of the code has no place: no JSON declaration is named.
    [Fact]
    public void VariableOfTheCodeWithATypeNoSourceDeclares_IsTypeNotDeclaredWithoutAPlace()
    {
        var builder = JsonExample.NewBuilder().AddVariable("x", CelType.ObjectNamed("acme.crm.Deal"));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildError(
            error,
            CelErrorCode.TypeNotDeclared,
            "variable 'x' has type 'acme.crm.Deal', which no type source or extension of the environment declares");
        Assert.Null(error.SourceLocation);
    }

    // The type of a record is found by its full name; the container of the description does not apply to it, so
    // crm.Deal is not acme.crm.Deal.
    [Fact]
    public void ContainerDoesNotApplyToATypeRecord()
    {
        const string Json = """
            {
              "name": "m",
              "container": "acme",
              "greatPartsCel": { "objectTypes": [{ "name": "acme.crm.Deal" }] },
              "variables": [{ "name": "found", "type": "acme.crm.Deal" }, { "name": "lost", "type": "crm.Deal" }]
            }
            """;
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        int offset = Json.IndexOf("{ \"name\": \"lost\"", System.StringComparison.Ordinal);
        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, "variable 'lost' has type 'crm.Deal', which no type source or extension of the environment declares", "environment JSON 'm'", offset),
            (error.Code, error.Message, place.SourceName, place.Offset));
    }

    // The type any is the name google.protobuf.Any, which the core without the provider of protobuf does not
    // declare: TypeNotDeclared at Build.
    [Fact]
    public void Any_IsTheNameGoogleProtobufAny()
    {
        const string JsonWithAny = """{"name": "m", "variables": [{"name": "x", "type": "any"}]}""";
        var withAny = JsonExample.NewBuilder();
        var withName = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(withAny, JsonWithAny));
        Assert.Empty(JsonExample.Apply(withName, """{"name": "m", "variables": [{"name": "x", "type": "google.protobuf.Any"}]}"""));

        Assert.Equal(JsonExample.Variables(withName), JsonExample.Variables(withAny));
        CelError error = JsonExample.BuildError(withAny);
        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.StartsWith("variable 'x' has type '", error.Message, System.StringComparison.Ordinal);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(
            ("environment JSON 'm'", JsonWithAny.IndexOf("{\"name\": \"x\"", System.StringComparison.Ordinal)),
            (place.SourceName, place.Offset));
    }
}
