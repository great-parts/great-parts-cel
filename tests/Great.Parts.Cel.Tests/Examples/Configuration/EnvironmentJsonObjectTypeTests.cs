using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The key greatPartsCel of the environment description in JSON (the page of the description, "Mapping onto
// builder calls"): objectTypes describes types over a dictionary, the one form of a type described at run
// time that data can describe. All the types of the array form one provider, AddTypes(CelTypeProvider.Create)
// with the label of the description as its source name, and each type is CelObjectTypeBuilder.ForDictionary
// with its fields in the order of the array, which gives the field numbers. The environment from such a
// description, a variable of such a type and a dictionary bound to it are the use of the type (F-7); the errors
// of the building are DuplicateType (4001) with the sources named. In the marked documents each '|' is the
// place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-2")]
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

    [Fact]
    public void ObjectTypes_AreOneProviderWithTheLabelAsItsSourceName()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, Crm));

        CelTypeProvider provider = Assert.Single(builder.TypeProviders);
        Assert.Equal("environment JSON 'crm'", provider.SourceName);
        Assert.Equal(new[] { "acme.crm.Contact", "acme.crm.Empty", "acme.crm.Deal" }, provider.ObjectTypes.Select(type => type.Name));
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
    public void ProviderOfADescriptionWithoutAName_IsLabelledWithoutIt()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, """{"greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]}}"""));

        Assert.Equal("environment JSON", Assert.Single(builder.TypeProviders).SourceName);
    }

    // The same field name in two types of one description is right: the names are unique within a type.
    [Fact]
    public void SameFieldNameInTwoTypes_IsRight()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, """
            {"greatPartsCel": {"objectTypes": [
              {"name": "a.A", "fields": [{"name": "x", "type": "int"}]},
              {"name": "a.B", "fields": [{"name": "x", "type": "string"}]}
            ]}}
            """));
        Assert.True(builder.Build().IsSuccess);
    }

    // F-7: the application describes a type by data, declares a variable of it and binds a dictionary. It holds
    // no description of the type: the dictionary is bound by its .NET type and the place rule takes it as the
    // carrier of the declared type. An entry has no static type, so a field of a list or map type holds a CelList
    // or a CelMap — here views of .NET collections; a .NET collection there is FieldTypeMismatch (ObjectFieldTests).
    [Fact]
    public void DictionaryBoundToAVariableOfATypeOfTheDescription_IsAnObjectOfThatType()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Crm));
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        var manager = new Dictionary<string, object?> { ["name"] = "Bob" };
        var dictionary = new Dictionary<string, object?>
        {
            ["name"] = "Ann",
            ["age"] = 41L,
            ["manager"] = manager,
            ["tags"] = CelList.CreateView(new List<string> { "vip", "new" }, CelValue.FromString),
            ["attributes"] = CelMap.CreateView(new Dictionary<string, string> { ["region"] = "north" }, CelValue.FromString, keepSourceOrder: true),
            ["in"] = null,
        };
        var bindings = new CelBindings().Add("c", dictionary);

        Assert.Equal("Ann", Evaluate<string>(environment, "c.name", bindings));
        Assert.Equal(41L, Evaluate<long>(environment, "c.age", bindings));
        Assert.Equal("Bob", Evaluate<string>(environment, "c.manager.name", bindings));
        Assert.Equal(2L, Evaluate<long>(environment, "size(c.tags)", bindings));
        Assert.Equal("north", Evaluate<string>(environment, "c.attributes.region", bindings));
        Assert.True(Evaluate<bool>(environment, "has(c.name) && has(c.manager) && !has(c.manager.age) && has(c.`in`)", bindings));
        Assert.True(Evaluate<bool>(environment, "type(c) == acme.crm.Contact", bindings));
        Assert.Equal(CelType.Int, environment.Compile<long>("c.age").GetProgramOrThrow().ResultType);
    }

    // The same dictionary bound as IReadOnlyDictionary, as IDictionary or as object is the same carrier.
    [Fact]
    public void DictionaryBoundByAnotherStaticType_IsTheSameObject()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Crm));
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        var dictionary = new Dictionary<string, object?> { ["name"] = "Ann" };

        foreach (CelBindings bindings in new[]
        {
            new CelBindings().Add<IReadOnlyDictionary<string, object?>>("c", dictionary),
            new CelBindings().Add<IDictionary<string, object?>>("c", dictionary),
            new CelBindings().Add<object>("c", dictionary),
        })
        {
            Assert.Equal("Ann", Evaluate<string>(environment, "c.name", bindings));
        }
    }

    // A field without an entry is FieldNotSet and an entry of another type FieldTypeMismatch, both at the "."
    // of the selection.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void FieldWithoutAnEntryOrOfAnotherType_IsAnErrorOfTheEvaluation()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Crm));
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        var bindings = new CelBindings().Add("c", new Dictionary<string, object?> { ["name"] = "Ann", ["age"] = "forty" });

        CelError notSet = SingleError(environment, "c.manager", bindings);
        CelError mismatch = SingleError(environment, "c.age", bindings);

        Assert.Equal(
            (CelErrorCode.FieldNotSet, "field 'manager' of type 'acme.crm.Contact' has no value", 1),
            (notSet.Code, notSet.Message, notSet.Location.Offset));
        Assert.Equal(
            (CelErrorCode.FieldTypeMismatch, "field 'age' of type 'acme.crm.Contact' has a value of type 'string', expected 'int'", 1),
            (mismatch.Code, mismatch.Message, mismatch.Location.Offset));
    }

    // The type of a field is looked up at Build like the type of a variable: a name that no source declares is
    // TypeNotDeclared, naming the field as the type and the field.
    [Fact]
    public void FieldOfATypeThatNoSourceDeclares_IsTypeNotDeclaredAtBuild()
    {
        const string Json = """{"name": "m", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal", "fields": [{"name": "owner", "type": "acme.crm.Person"}]}]}}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        // The text names no source; the place is the "{" of the field in the JSON.
        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.TypeNotDeclared,
            "field 'acme.crm.Deal.owner' has type 'acme.crm.Person', which no type source or extension of the environment declares",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"owner\"", System.StringComparison.Ordinal));
    }

    // The name of a type is a full name: identifiers separated by dots.
    [Theory]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": |"acme..Deal"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": |"acme.crm-Deal"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": |"1a"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": |""}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": |1}]}}""")]
    public void TypeNameThatIsNotAFullName_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    [Theory]
    [InlineData("""{"greatPartsCel": {"objectTypes": [|{"fields": []}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [|{"type": "int"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [|{"name": "x"}]}]}}""")]
    public void MissingKey_IsAnErrorAtTheObject(string marked) =>
        JsonExample.AssertError(marked);

    // The names of a field are identifiers (a reserved word is one); a repeated name in one type is an error at
    // the value of the second.
    [Theory]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": |"x-y", "type": "int"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": |"", "type": "int"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": |"x.y", "type": "int"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": |1, "type": "int"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": "int"}, {"name": |"x", "type": "string"}]}]}}""")]
    public void FieldNameThatIsNotAnIdentifierOrRepeats_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // A type parameter is no type of a field, and a record of the wrong kind or form is an error at the value.
    [Theory]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |"~T"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |"list<>"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |1}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": |{"type_name": "int"}}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": |{}}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [|1]}]}}""")]
    public void FieldTypeOrFieldsOfTheWrongKindOrForm_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // There are no other keys in greatPartsCel, in a type or in a field.
    [Theory]
    [InlineData("""{"greatPartsCel": {|"types": []}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", |"description": "d"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": "int", |"description": "d"}]}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", |"name": "b.B"}]}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": "int", |"type": "int"}]}]}}""")]
    public void UnknownOrRepeatedKey_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertError(marked);

    // DuplicateType (4001): one name for two descriptions of one description in JSON names the label twice; the
    // place is the first of the two types in the text.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void TwoTypesOfOneNameInTheDescription_AreDuplicateType()
    {
        const string Json = """{"name": "m", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal", "fields": [{"name": "amount", "type": "int"}]}, {"name": "acme.crm.Deal", "fields": [{"name": "title", "type": "string"}]}]}}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.DuplicateType,
            "type 'acme.crm.Deal' is declared by both environment JSON 'm' and environment JSON 'm'",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"acme.crm.Deal\"", StringComparison.Ordinal));
    }

    // A type of the description and a type of another source, here a provider of the code: the sources are
    // named in order, environment JSON before type source; the place is the type in the JSON.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void TypeOfTheDescriptionAndOfTheCode_AreDuplicateType()
    {
        const string Json = """{"name": "m", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]}}""";
        CelObjectType deal = CelObjectTypeBuilder.ForDictionary("acme.crm.Deal").AddField("amount", CelType.Int).Build();
        var builder = JsonExample.NewBuilder().AddTypes(CelTypeProvider.Create("crm", deal));
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.DuplicateType,
            "type 'acme.crm.Deal' is declared by both environment JSON 'm' and type source 'crm'",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"acme.crm.Deal\"", StringComparison.Ordinal));
    }

    // Two descriptions with a type of one name: the labels in order, the place in the first.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void TypesOfTwoDescriptions_AreDuplicateType()
    {
        const string First = """{"name": "m1", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal"}]}}""";
        const string Second = """{"name": "m2", "greatPartsCel": {"objectTypes": [{"name": "acme.crm.Deal", "fields": [{"name": "a", "type": "int"}]}]}}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Second));
        Assert.Empty(JsonExample.Apply(builder, First));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.DuplicateType,
            "type 'acme.crm.Deal' is declared by both environment JSON 'm1' and environment JSON 'm2'",
            "environment JSON 'm1'",
            First.IndexOf("{\"name\": \"acme.crm.Deal\"", StringComparison.Ordinal));
    }

    // A type of the description with the name of a type of the standard library.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void TypeWithAnIdentifierOfTheStandardLibrary_IsDuplicateType()
    {
        const string Json = """{"greatPartsCel": {"objectTypes": [{"name": "google.protobuf.Duration"}]}}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.DuplicateType,
            "type 'google.protobuf.Duration' is declared by both standard library v1 and environment JSON",
            "environment JSON",
            Json.IndexOf("{\"name\": \"google.protobuf.Duration\"", StringComparison.Ordinal));
    }

    private static T Evaluate<T>(CelEnvironment environment, string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None)
            .GetValueOrThrow();

    private static CelError SingleError(CelEnvironment environment, string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        return Assert.Single(result.Errors);
    }
}
