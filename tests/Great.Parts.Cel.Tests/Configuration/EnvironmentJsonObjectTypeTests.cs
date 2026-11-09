using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The object greatPartsCel: all the types of objectTypes form one provider named by the label of the description, and each
// type is CelObjectTypeBuilder.ForDictionary with its fields in the order of the array.
public sealed class EnvironmentJsonObjectTypeTests
{
    private const string Crm = """
        {
          "name": "crm",
          "greatPartsCel": {
            "objectTypes": [
              {
                "name": "acme.crm.Contact",
                "fields": [
                  { "name": "name", "type": "string" },
                  { "name": "age", "type": "int" },
                  { "name": "manager", "type": "acme.crm.Contact" },
                  { "name": "tags", "type": "list<string>" },
                  { "name": "attributes", "type": "map<string, dyn>" },
                  { "name": "in", "type": "dyn" }
                ]
              },
              { "name": "acme.crm.Empty" },
              { "name": "acme.crm.Deal", "fields": [] }
            ]
          },
          "variables": [{ "name": "c", "type": "acme.crm.Contact" }]
        }
        """;

    private static readonly string[] CrmTypeNames = ["acme.crm.Contact", "acme.crm.Empty", "acme.crm.Deal"];

    [Fact]
    public static void ObjectTypes_AreOneProviderNamedByTheLabel()
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, Crm));

        CelTypeProvider provider = Assert.Single(builder.TypeProviders);
        Assert.Equal("environment JSON 'crm'", provider.SourceName);
        Assert.Equal(CrmTypeNames, provider.ObjectTypes.Select(type => type.Name));
        CelObjectType contact = provider.ObjectTypes[0];
        Assert.Null(contact.ClrType);
        Assert.Equal(
            new (int, string, CelType)[]
            {
                (0, "name", CelType.String),
                (1, "age", CelType.Int),
                (2, "manager", CelType.ObjectNamed("acme.crm.Contact")),
                (3, "tags", CelType.ListOf(CelType.String)),
                (4, "attributes", CelType.MapOf(CelType.String, CelType.Dyn)),
                (5, "in", CelType.Dyn),
            },
            contact.Fields.Select(field => (field.Number, field.Name, field.Type)));
        Assert.Empty(provider.ObjectTypes[1].Fields);
        Assert.Empty(provider.ObjectTypes[2].Fields);
    }

    [Fact]
    public static void ProviderOfADescriptionWithoutAName_IsLabelledWithoutIt()
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, """{"greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]}}"""));

        Assert.Equal(Label, Assert.Single(builder.TypeProviders).SourceName);
    }

    [Fact]
    public static void SameFieldNameInTwoTypes_IsRight()
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, """
            {"greatPartsCel": {"objectTypes": [
              {"name": "a.A", "fields": [{"name": "x", "type": "int"}]},
              {"name": "a.B", "fields": [{"name": "x", "type": "string"}]}
            ]}}
            """));

        Assert.True(builder.Build().IsSuccess);
    }

    // A dictionary bound to a variable of a type of the description is an object of that type.
    [Fact]
    public static void DictionaryBoundToAVariableOfTheType_IsAnObjectOfThatType()
    {
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Crm));
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        var manager = new Dictionary<string, object?> { ["name"] = "Bob" };
        var contact = new Dictionary<string, object?>
        {
            ["name"] = "Ann",
            ["age"] = 41L,
            ["manager"] = manager,
            ["tags"] = CelList.Create(CelValue.FromString("vip"), CelValue.FromString("new")),
        };
        CelBindings bindings = new CelBindings().Add<IReadOnlyDictionary<string, object?>>("c", contact);

        Assert.Equal("Ann", Evaluate<string>(environment, "c.name", bindings));
        Assert.Equal(41L, Evaluate<long>(environment, "c.age", bindings));
        Assert.Equal("Bob", Evaluate<string>(environment, "c.manager.name", bindings));
        Assert.Equal(2L, Evaluate<long>(environment, "size(c.tags)", bindings));
        Assert.True(Evaluate<bool>(environment, "has(c.name) && !has(c.manager.age) && !has(c.attributes)", bindings));
    }

    // The type of a field is looked up at Build like the type of a variable; the error names no source in its text and has the
    // place of the field in the JSON.
    [Fact]
    public static void FieldOfATypeThatNoSourceDeclares_IsTypeNotDeclaredAtTheField()
    {
        const string Json = """{"name": "m", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal", "fields": [{"name": "owner", "type": "acme.crm.Person"}]}]}}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.TypeNotDeclared,
            "field 'acme.crm.Deal.owner' has type 'acme.crm.Person', which no type source or extension of the environment declares",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"owner\""));
    }

    [Theory]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": ^"acme..Deal"}]}}""", "is not a full name")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": ^"acme.crm-Deal"}]}}""", "is not a full name")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": ^""}]}}""", "is not a full name")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": ^1}]}}""", "'name' must be a string")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [^{"fields": []}]}}""", "has no 'name'")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [^{"type": "int"}]}]}}""", "has no 'name'")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [^{"name": "x"}]}]}}""", "has no 'type'")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": ^"x-y", "type": "int"}]}]}}""", "is not an identifier")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": ^"", "type": "int"}]}]}}""", "is not an identifier")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": ^"x.y", "type": "int"}]}]}}""", "is not an identifier")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": ^1, "type": "int"}]}]}}""", "'name' must be a string")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": "int"}, {"name": ^"x", "type": "string"}]}]}}""", "declared twice")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": ^"~T"}]}]}}""", "a type parameter cannot be used in a field")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": ^"list<>"}]}]}}""", "not a valid type record")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": ^1}]}]}}""", "'type' must be a string")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": ^{}}]}}""", "'fields' must be an array")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [^1]}]}}""", "must be an object")]
    [InlineData("""{"greatPartsCel": ^[]}""", "'greatPartsCel' must be an object")]
    [InlineData("""{"greatPartsCel": {"objectTypes": ^{}}}""", "'objectTypes' must be an array")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [^1]}}""", "must be an object")]
    public static void WrongObjectTypeOrField_IsAnErrorAtItsPlace(string marked, string detail) =>
        AssertError(marked, detail);

    [Theory]
    [InlineData("""{"greatPartsCel": {^"types": []}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", ^"description": "d"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": "int", ^"description": "d"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", ^"name": "b.B"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [], ^"objectTypes": []}}""")]
    public static void UnknownOrRepeatedKey_IsAnErrorAtTheKey(string marked) =>
        AssertErrors(marked, Label);

    // A type with an error is not built, and the next ones are read: every error is reported.
    [Fact]
    public static void EveryObjectType_IsChecked() =>
        AssertErrors(
            """{"greatPartsCel": {"objectTypes": [{"name": ^"a-b"}, {"name": "c.C", "fields": [{"name": ^"x y", "type": ^"~T"}]}]}}""",
            Label,
            "is not a full name",
            "is not an identifier",
            "a type parameter cannot be used in a field");

    // One name for two types of one description names the label twice; the place is the first type in the text.
    [Fact]
    public static void TwoTypesOfOneNameInTheDescription_AreDuplicateType()
    {
        const string Json = """{"name": "m", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal", "fields": [{"name": "amount", "type": "int"}]}, {"name": "acme.crm.Deal", "fields": [{"name": "title", "type": "string"}]}]}}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateType,
            "type 'acme.crm.Deal' is declared by both environment JSON 'm' and environment JSON 'm'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"acme.crm.Deal\""));
    }

    // The sources are named in order, the description before a type source of the code, and the place is the type in the JSON.
    [Fact]
    public static void TypeOfTheDescriptionAndOfTheCode_AreDuplicateType()
    {
        const string Json = """{"name": "m", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]}}""";
        CelObjectType deal = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal").AddField("amount", CelType.Int).Build();
        CelEnvironmentBuilder builder = NewBuilder().AddTypes(CelTypeProvider.Create("crm", deal));
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateType,
            "type 'acme.crm.Deal' is declared by both environment JSON 'm' and type source 'crm'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"acme.crm.Deal\""));
    }

    [Fact]
    public static void TypesOfTwoDescriptions_AreDuplicateType()
    {
        const string First = """{"name": "m1", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]}}""";
        const string Second = """{"name": "m2", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal", "fields": [{"name": "a", "type": "int"}]}]}}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Second));
        Assert.Empty(Apply(builder, First));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateType,
            "type 'acme.crm.Deal' is declared by both environment JSON 'm1' and environment JSON 'm2'",
            "environment JSON 'm1'",
            IndexOf(First, "{\"name\": \"acme.crm.Deal\""));
    }

    [Fact]
    public static void TypeNamedAsATypeOfTheStandardLibrary_IsDuplicateTypeWithTheLibraryFirst()
    {
        const string Json = """{"greatPartsCel": {"objectTypes": [{"name": "google.protobuf.Duration"}]}}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateType,
            "type 'google.protobuf.Duration' is declared by both standard library v1 and environment JSON",
            Label,
            IndexOf(Json, "{\"name\": \"google.protobuf.Duration\""));
    }

    private static T Evaluate<T>(CelEnvironment environment, string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None)
            .GetValueOrThrow();
}
