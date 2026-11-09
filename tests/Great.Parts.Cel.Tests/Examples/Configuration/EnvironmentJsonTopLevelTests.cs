using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The keys of the top level of the environment description in JSON (the page of the description, "Mapping
// onto builder calls"): name and description are metadata, container is UseContainer, an empty array, an empty
// object and a missing key are the same. The keys extensions, variables, functions and greatPartsCel have
// examples of their own. In the marked documents each '|' is the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
public sealed class EnvironmentJsonTopLevelTests
{
    [Fact]
    public void NameAndDescription_AreMetadata()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, """{"name": "crm", "description": "The environment of the CRM."}"""));

        Assert.Empty(JsonExample.Variables(builder));
        Assert.Empty(builder.FunctionDeclarations);
        Assert.Equal(string.Empty, builder.Container);
        Assert.True(builder.Build().IsSuccess);
    }

    // The name labels the description: a schema error carries it in the name of its source.
    [Fact]
    public void Name_LabelsTheErrorsOfTheDescription() =>
        JsonExample.AssertError("""{"name": "crm", |"nme": 1}""", "environment JSON 'crm'");

    [Theory]
    [InlineData("""{"name": |1}""")]
    [InlineData("""{"name": |null}""")]
    [InlineData("""{"name": |["m"]}""")]
    [InlineData("""{"description": |false}""")]
    [InlineData("""{"description": |{}}""")]
    [InlineData("""{"container": |1}""")]
    [InlineData("""{"container": |["a"]}""")]
    [InlineData("""{"extensions": |{}}""")]
    [InlineData("""{"extensions": |"strings"}""")]
    [InlineData("""{"variables": |"x"}""")]
    [InlineData("""{"variables": |null}""")]
    [InlineData("""{"functions": |{}}""")]
    [InlineData("""{"greatPartsCel": |[]}""")]
    [InlineData("""{"greatPartsCel": |"x"}""")]
    public void ValueOfTheWrongKind_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    [Theory]
    [InlineData("""{"extensions": [|[]]}""")]
    [InlineData("""{"variables": [|1]}""")]
    [InlineData("""{"variables": [|"x"]}""")]
    [InlineData("""{"variables": [|[]]}""")]
    [InlineData("""{"functions": [|"f"]}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": |{}}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [|1]}}""")]
    public void ElementOfTheWrongKind_IsAnErrorAtTheElement(string marked) =>
        JsonExample.AssertError(marked);

    // A repeated key is reported at its second occurrence; the keys are case-sensitive.
    [Theory]
    [InlineData("""{"name": "a", |"name": "b"}""")]
    [InlineData("""{"description": "a", |"description": "b"}""")]
    [InlineData("""{"container": "a", |"container": "b"}""")]
    [InlineData("""{"extensions": [], |"extensions": []}""")]
    [InlineData("""{"variables": [], |"variables": []}""")]
    [InlineData("""{"functions": [], |"functions": []}""")]
    [InlineData("""{"greatPartsCel": {}, |"greatPartsCel": {}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [], |"objectTypes": []}}""")]
    [InlineData("""{|"Name": "a"}""")]
    [InlineData("""{|"greatpartscel": {}}""")]
    [InlineData("""{|"great_parts_cel": {}}""")]
    public void RepeatedOrUnknownKey_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertError(marked);

    // An empty array, an empty object and a missing key are the same: no call of the builder, and no type
    // source for an empty greatPartsCel.
    [Theory]
    [InlineData("""{"extensions": [], "variables": [], "functions": [], "greatPartsCel": {}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": []}}""")]
    [InlineData("""{"container": ""}""")]
    public void EmptyKeys_AreAbsentKeys(string json)
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, json));

        Assert.Empty(JsonExample.Variables(builder));
        Assert.Empty(builder.FunctionDeclarations);
        Assert.Empty(builder.TypeProviders);
        Assert.Equal(string.Empty, builder.Container);
        Assert.True(builder.Build().IsSuccess);
    }

    [Theory]
    [InlineData("""{"container": "acme"}""", "acme")]
    [InlineData("""{"container": "acme.crm"}""", "acme.crm")]
    [InlineData("""{"container": "a1._b.c_2"}""", "a1._b.c_2")]
    public void Container_IsUseContainer(string json, string expected)
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, json));

        Assert.Equal(expected, builder.Container);
        Assert.True(builder.Build().IsSuccess);
    }

    // The container is a full name: identifiers separated by dots.
    [Theory]
    [InlineData("""{"container": |"a..b"}""")]
    [InlineData("""{"container": |"a."}""")]
    [InlineData("""{"container": |"1a"}""")]
    [InlineData("""{"container": |"a b"}""")]
    [InlineData("""{"container": |"a-b"}""")]
    [InlineData("""{"container": |"a.b-c"}""")]
    [InlineData("""{"container": |" "}""")]
    public void ContainerThatIsNotAFullName_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    // The container resolves names as the container of the builder does: x is acme.x.
    [Fact]
    public void Container_ResolvesNamesOfTheEnvironment()
    {
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, """{"container": "acme", "variables": [{"name": "acme.x", "type": "int"}]}"""));
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        long result = environment.Compile<long>("x").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("acme.x", 7L), cancellationToken: CancellationToken.None)
            .GetValueOrThrow();

        Assert.Equal(7L, result);
    }

    // The empty string equals the absence of the key, so it conflicts with no container of the code; the same
    // container twice, in code and in the description, is one declaration.
    [Fact]
    public void EmptyContainer_ConflictsWithNothing_AndTheSameContainerTwiceIsOne()
    {
        var builder = JsonExample.NewBuilder().UseContainer("a.b");

        Assert.Empty(JsonExample.Apply(builder, """{"container": ""}"""));
        Assert.Empty(JsonExample.Apply(builder, """{"container": "a.b"}"""));

        Assert.True(builder.Build().IsSuccess);
        Assert.Equal("a.b", builder.Container);
    }

    // Two different containers are ContainerConflict, in either order of the calls, naming both sources; the
    // place is the value of the container in the JSON, with the label as the source name.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void ContainerOfTheCodeAndOfTheDescription_AreContainerConflict()
    {
        const string Json = """{"name": "m", "container": "b"}""";
        const string Message = "name resolution container 'a' of application conflicts with container 'b' of environment JSON 'm'";
        int value = Json.IndexOf("\"b\"", System.StringComparison.Ordinal);

        var codeFirst = JsonExample.NewBuilder().UseContainer("a");
        Assert.Empty(JsonExample.Apply(codeFirst, Json));
        var jsonFirst = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(jsonFirst, Json));
        jsonFirst.UseContainer("a");

        foreach (CelEnvironmentBuilder builder in new[] { codeFirst, jsonFirst })
        {
            CelError error = JsonExample.BuildError(builder);

            Assert.Equal(CelErrorCode.ContainerConflict, error.Code);
            JsonExample.AssertBuildErrorAt(error, CelErrorCode.ContainerConflict, Message, "environment JSON 'm'", value);
        }
    }

    // Two descriptions with different containers name both; the source that sorts first is named first, and the
    // place is the value in that description.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void ContainersOfTwoDescriptions_AreContainerConflict()
    {
        const string First = """{"name": "m1", "container": "a"}""";
        const string Second = """{"name": "m2", "container": "b"}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Second));
        Assert.Empty(JsonExample.Apply(builder, First));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.ContainerConflict,
            "name resolution container 'a' of environment JSON 'm1' conflicts with container 'b' of environment JSON 'm2'",
            "environment JSON 'm1'",
            First.IndexOf("\"a\"", System.StringComparison.Ordinal));
    }
}
