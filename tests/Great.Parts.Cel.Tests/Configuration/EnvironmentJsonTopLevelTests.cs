using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The keys of the top level that call the builder with a value of their own: container, and the empty forms of the others.
public sealed class EnvironmentJsonTopLevelTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("acme.crm")]
    [InlineData("a1._b.c_2")]
    public static void Container_CallsUseContainer(string container)
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, $$"""{"container": "{{container}}"}"""));

        Assert.Equal(container, builder.Container);
        Assert.True(builder.Build().IsSuccess);
    }

    [Theory]
    [InlineData("""{"container": ""}""")]
    [InlineData("""{"extensions": [], "variables": [], "functions": [], "greatPartsCel": {}}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": []}}""")]
    public static void EmptyKeys_MakeNoCall(string json)
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, json));

        Assert.Empty(builder.VariableDeclarations);
        Assert.Empty(builder.FunctionDeclarations);
        Assert.Empty(builder.TypeProviders);
        Assert.Equal(string.Empty, builder.Container);
    }

    [Theory]
    [InlineData("""{"container": ^"a..b"}""")]
    [InlineData("""{"container": ^"a."}""")]
    [InlineData("""{"container": ^"1a"}""")]
    [InlineData("""{"container": ^"a b"}""")]
    [InlineData("""{"container": ^".a"}""")]
    [InlineData("""{"container": ^"a.in"}""")]
    public static void ContainerThatIsNotAQualifiedName_IsAnErrorAtTheValue(string marked) =>
        AssertError(marked, "is not a qualified name");

    [Theory]
    [InlineData("""{"container": ^1}""")]
    [InlineData("""{"container": ^null}""")]
    [InlineData("""{"container": ^["a"]}""")]
    public static void ContainerOfTheWrongKind_IsAnErrorAtTheValue(string marked) =>
        AssertError(marked, "'container' must be a string");

    [Fact]
    public static void Container_ResolvesNamesOfTheEnvironment()
    {
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, """{"container": "acme", "variables": [{"name": "acme.x", "type": "int"}]}"""));
        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        long result = environment.Compile<long>("x").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("acme.x", 7L), cancellationToken: CancellationToken.None)
            .GetValueOrThrow();

        Assert.Equal(7L, result);
    }

    // The empty string declares nothing, and the same container twice is one declaration.
    [Fact]
    public static void EmptyContainer_ConflictsWithNothing_AndTheSameContainerTwiceIsOne()
    {
        CelEnvironmentBuilder builder = NewBuilder().UseContainer("a.b");

        Assert.Empty(Apply(builder, """{"container": ""}"""));
        Assert.Empty(Apply(builder, """{"container": "a.b"}"""));

        Assert.True(builder.Build().IsSuccess);
        Assert.Equal("a.b", builder.Container);
    }

    // The conflict names both sources, the application first, and has the place of the value in the JSON; the order of the
    // calls changes neither.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void ContainerOfTheCodeAndOfTheDescription_AreContainerConflict(bool codeFirst)
    {
        const string Json = """{"name": "m", "container": "b"}""";
        CelEnvironmentBuilder builder = NewBuilder();
        if (codeFirst)
        {
            builder.UseContainer("a");
        }

        Assert.Empty(Apply(builder, Json));
        if (!codeFirst)
        {
            builder.UseContainer("a");
        }

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.ContainerConflict,
            "name resolution container 'a' of application conflicts with container 'b' of environment JSON 'm'",
            "environment JSON 'm'",
            IndexOf(Json, "\"b\""));
    }

    // Two descriptions: the source that sorts first is named first and gives the place.
    [Fact]
    public static void ContainersOfTwoDescriptions_AreContainerConflict()
    {
        const string First = """{"name": "m1", "container": "z"}""";
        const string Second = """{"name": "m2", "container": "a"}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Second));
        Assert.Empty(Apply(builder, First));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.ContainerConflict,
            "name resolution container 'z' of environment JSON 'm1' conflicts with container 'a' of environment JSON 'm2'",
            "environment JSON 'm1'",
            IndexOf(First, "\"z\""));
    }

    // The code is the application: its container has no place, so the place is the one in the JSON, whichever comes first.
    [Fact]
    public static void ContainerConflictOfTheCodeBeforeTheDescription_NamesTheApplicationFirst()
    {
        const string Json = """{"container": "a"}""";
        CelEnvironmentBuilder builder = NewBuilder().UseContainer("z");
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.ContainerConflict,
            "name resolution container 'z' of application conflicts with container 'a' of environment JSON",
            Label,
            IndexOf(Json, "\"a\""));
    }

    [Fact]
    public static void EveryKeyOfCelGoOutsideTheFormat_IsReportedInTheOrderOfTheText() =>
        AssertErrors(
            """{^"imports": [], ^"stdlib": {}, ^"context_variable": {}, ^"validators": [], ^"features": [], ^"limits": []}""",
            Label,
            "'imports'",
            "'stdlib'",
            "'context_variable'",
            "'validators'",
            "'features'",
            "'limits'");

    [Theory]
    [InlineData("""{"variables": [{"name": "x", "type": ^{"type_name": "int"}}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": ^{"type_name": "int"}}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "args": [^{"type_name": "int"}], "return": "int"}]}]}""")]
    [InlineData("""{"greatPartsCel": {"objectTypes": [{"name": "a.A", "fields": [{"name": "x", "type": ^{"type_name": "int"}}]}]}}""")]
    public static void ObjectFormOfARecord_IsAnErrorAtTheValue(string marked) =>
        AssertError(marked, "must be a string");

    [Theory]
    [InlineData("""{^"Name": "a"}""")]
    [InlineData("""{^"greatpartscel": {}}""")]
    [InlineData("""{^"contextVariable": {}}""")]
    public static void KeysAreCaseSensitive_AndCamelCaseIsAnotherKey(string marked) =>
        AssertError(marked, "unknown key");

    [Fact]
    public static void ApplyOfADescriptionWithAnError_ChangesNothing()
    {
        CelEnvironmentBuilder builder = NewBuilder();
        builder.AddVariable("before", CelType.Bool);

        string json = """
            {"container": "acme", "variables": [{"name": "x", "type": "int"}],
             "functions": [{"name": "f", "overloads": [{"id": "f1", "return": "int"}]}],
             "greatPartsCel": {"objectTypes": [{"name": "acme.A"}]}, "nme": 1}
            """;

        Assert.NotEmpty(Apply(builder, json));

        Assert.Equal(new[] { ("before", CelType.Bool) }, Variables(builder));
        Assert.Equal(string.Empty, builder.Container);
        Assert.Empty(builder.FunctionDeclarations);
        Assert.Empty(builder.TypeProviders);
    }
}
