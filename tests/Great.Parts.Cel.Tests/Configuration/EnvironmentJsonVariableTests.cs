using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The key variables with the type record in full, and what Build reports about a variable of a description: the label of the
// description in the message and the place of the variable in the JSON.
public sealed class EnvironmentJsonVariableTests
{
    [Theory]
    [InlineData("""{"name": "x", "type": "list<int>"}""")]
    [InlineData("""{"name": "x", "type_name": "list", "params": ["int"]}""")]
    [InlineData("""{"name": "x", "type": "list < int >"}""")]
    public static void TypeAndTypeNameWithParams_AreOneRecord(string variable)
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, "{\"variables\": [" + variable + "]}"));

        Assert.Equal(new[] { ("x", CelType.ListOf(CelType.Int)) }, Variables(builder));
    }

    [Fact]
    public static void TypeRecords_GiveTheTypesOfTheTypeTable()
    {
        CelEnvironmentBuilder builder = NewBuilder();
        const string Json = """
            {"variables": [
              {"name": "a", "type": "map<string, list<double>>"},
              {"name": "c", "type": "int_wrapper"},
              {"name": "d", "type": "null_type"},
              {"name": "e", "type": "type<int>"},
              {"name": "f", "type": "type"},
              {"name": "g", "type": ".acme.crm.Deal"}
            ]}
            """;

        Assert.Empty(Apply(builder, Json));

        Assert.Equal(
            new[]
            {
                ("a", CelType.MapOf(CelType.String, CelType.ListOf(CelType.Double))),
                ("c", CelType.WrapperOf(CelType.Int)),
                ("d", CelType.Null),
                ("e", CelType.TypeOf(CelType.Int)),
                ("f", CelType.TypeOf(CelType.Dyn)),
                ("g", CelType.ObjectNamed("acme.crm.Deal")),
            },
            Variables(builder));
    }

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": ^"optional_type"}]}""", "the type 'optional_type' is not supported")]
    [InlineData("""{"variables": [{"name": "x", "type": ^"optional_type<int>"}]}""", "the type 'optional_type' is not supported")]
    [InlineData("""{"variables": [{"name": "x", "type": ^"map<string>"}]}""", "takes 2 type parameters")]
    [InlineData("""{"variables": [{"name": "x", "type_name": ^"list"}]}""", "takes 1 type parameter")]
    [InlineData("""{"variables": [{"name": "x", "type_name": "list", "params": [^1]}]}""", "must be a string")]
    public static void TypeRecordThatCannotBeResolved_IsAnErrorAtTheValue(string marked, string detail) =>
        AssertError(marked, detail);

    // A name that is not an identifier is not an error of the description: Build reports it with the label and the place
    // of the variable.
    [Theory]
    [InlineData("a-b")]
    [InlineData("1x")]
    [InlineData("a..b")]
    public static void NameThatIsNotAnIdentifier_IsInvalidVariableNameAtBuild(string name)
    {
        string json = "{\"name\": \"m\", \"variables\": [{\"name\": \"" + name + "\", \"type\": \"int\"}]}";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.InvalidVariableName,
            "variable name '" + name + "' of environment JSON 'm' is not an identifier or a qualified name",
            "environment JSON 'm'",
            IndexOf(json, "{\"name\": \"" + name));
    }

    [Fact]
    public static void TwoVariablesOfOneNameInTheDescription_NameTheLabelTwiceAndTheFirstInTheText()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "int"}, {"name": "x", "type": "string"}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateVariable,
            "variable 'x' is declared by both environment JSON 'm' and environment JSON 'm'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"x\""));
    }

    // The application sorts before the description, and the place is the one in the description whichever call came first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void VariableOfTheCodeAndOfTheDescription_NameTheApplicationFirst(bool codeFirst)
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "string"}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        if (codeFirst)
        {
            builder.AddVariable("x", CelType.Int);
        }

        Assert.Empty(Apply(builder, Json));
        if (!codeFirst)
        {
            builder.AddVariable("x", CelType.Int);
        }

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateVariable,
            "variable 'x' is declared by both application and environment JSON 'm'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"x\""));
    }

    // The environment keeps the sources of its declarations, so a builder made from it names them in a later conflict.
    [Fact]
    public static void ToBuilder_KeepsTheSourceAndThePlaceOfTheDescription()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "int"}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));
        CelEnvironmentBuilder again = builder.Build().GetEnvironmentOrThrow().ToBuilder().AddVariable("x", CelType.String);

        AssertBuildError(
            BuildError(again),
            CelErrorCode.DuplicateVariable,
            "variable 'x' is declared by both application and environment JSON 'm'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"x\""));
    }

    // The same variable in the code and in the description is one declaration, whichever call came first.
    [Fact]
    public static void SameVariableOfTheCodeAndOfTheDescription_IsOneDeclaration()
    {
        CelEnvironmentBuilder builder = NewBuilder().AddVariable("x", CelType.Int);
        Assert.Empty(Apply(builder, """{"variables": [{"name": "x", "type": "int"}]}"""));

        Assert.True(builder.Build().IsSuccess);
        Assert.Equal(new[] { ("x", CelType.Int) }, Variables(builder));
    }

    [Fact]
    public static void VariableNamedAsATypeIdentifier_IsStandardLibraryConflictAtTheVariable()
    {
        const string Json = """{"variables": [{"name": "int", "type": "dyn"}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.StandardLibraryConflict,
            "variable 'int' of environment JSON conflicts with standard library v1: it is a type identifier",
            Label,
            IndexOf(Json, "{\"name\": \"int\""));
    }

    // The error names no source in its text, and has the place of the variable of the description; the same error of the
    // code has none.
    [Fact]
    public static void ObjectTypeThatNoSourceDeclares_IsTypeNotDeclaredAtTheVariable()
    {
        const string Json = """{"name": "m", "variables": [{"name": "x", "type": "list<acme.crm.Deal>"}]}""";
        CelEnvironmentBuilder fromJson = NewBuilder();
        Assert.Empty(Apply(fromJson, Json));
        CelEnvironmentBuilder fromCode = NewBuilder().AddVariable("x", CelType.ListOf(CelType.ObjectNamed("acme.crm.Deal")));
        const string Message = "variable 'x' has type 'acme.crm.Deal', which no type source or extension of the environment declares";

        AssertBuildError(BuildError(fromJson), CelErrorCode.TypeNotDeclared, Message, "environment JSON 'm'", IndexOf(Json, "{\"name\": \"x\""));
        AssertBuildError(BuildError(fromCode), CelErrorCode.TypeNotDeclared, Message, "environment JSON 'm'", null);
    }

    [Fact]
    public static void Any_IsTheNameGoogleProtobufAny_AndWithoutAProviderItIsTypeNotDeclared()
    {
        CelEnvironmentBuilder withAny = NewBuilder();
        CelEnvironmentBuilder withName = NewBuilder();
        Assert.Empty(Apply(withAny, """{"variables": [{"name": "x", "type": "any"}]}"""));
        Assert.Empty(Apply(withName, """{"variables": [{"name": "x", "type": "google.protobuf.Any"}]}"""));

        Assert.Equal(Variables(withName), Variables(withAny));
        Assert.Equal(CelErrorCode.TypeNotDeclared, BuildError(withAny).Code);
    }

    // A record names its type exactly: the container does not apply to it.
    [Fact]
    public static void ContainerDoesNotApplyToATypeRecord()
    {
        const string Json = """
            {
              "container": "acme",
              "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]},
              "variables": [{"name": "found", "type": "acme.crm.Deal"}, {"name": "lost", "type": "crm.Deal"}]
            }
            """;
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        CelError error = BuildError(builder);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Contains("variable 'lost'", error.Message, System.StringComparison.Ordinal);
    }
}
