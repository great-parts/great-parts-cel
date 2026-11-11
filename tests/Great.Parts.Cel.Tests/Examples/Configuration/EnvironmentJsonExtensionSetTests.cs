using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The key extensions of the environment description in JSON with the sets that the library implements (the page of the description,
// "Extension sets"; the page of the extension sets): the short name of each of the eight sets of this part and its
// version make the call AddExtension of that set; the names of cel-go that the catalog does not take, the full names
// cel.lib.ext.*, block and decimal (which is added in code only) stay errors of the description, as
// EnvironmentJsonExtensionTests has them. The build errors of a set added from JSON, UnknownExtensionVersion and
// the conflicts with a set added in code, are the errors of Build with the place of the "{" of the element in the JSON and the
// label of the description as the name of the source.
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-3")]
public sealed class EnvironmentJsonExtensionSetTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    // Each name with its one version in the description, and an expression that only that set makes true.
    [Theory]
    [InlineData("strings", "5", "'abc'.reverse() == 'cba'")]
    [InlineData("math", "3", "math.greatest(1, 3, 2) == 3")]
    [InlineData("lists", "4", "[3, 1, 2].sort() == [1, 2, 3]")]
    [InlineData("sets", "0", "sets.contains([1, 2, 3], [1, 2])")]
    [InlineData("encoders", "1", "base64.encode(b'abc') == 'YWJj'")]
    [InlineData("bindings", "1", "cel.bind(x, 2, x * x) == 4")]
    [InlineData("two-var-comprehensions", "0", "[1, 2, 3].all(i, v, v > i)")]
    [InlineData("network", "1", "ip('1.2.3.4').family() == 4")]
    public static void NameOfASetOfTheCatalog_AddsTheSet(string name, string version, string expression)
    {
        string json = "{\"extensions\": [{\"name\": \"" + name + "\", \"version\": \"" + version + "\"}]}";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, json));

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        CelValue value = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.True(value.TryGetBoolean(out bool truth) && truth);
        Assert.False(JsonExample.NewBuilder().Build().GetEnvironmentOrThrow().Compile<CelValue>(expression).IsSuccess);
    }

    [Fact]
    public void AllEightSets_InOneDescription_BuildTogether()
    {
        const string Json = """
            {"extensions": [
              {"name": "network", "version": "1"}, {"name": "two-var-comprehensions", "version": "0"}, {"name": "bindings", "version": "1"},
              {"name": "encoders", "version": "1"}, {"name": "sets", "version": "0"}, {"name": "lists", "version": "4"},
              {"name": "math", "version": "3"}, {"name": "strings", "version": "5"}]}
            """;
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json.ReplaceLineEndings(" ")));

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        Assert.True(environment.Compile<CelValue>("'abc'.reverse() == 'cba' && math.sqrt(4) == 2.0 && isIP('::1')").IsSuccess);
    }

    // The names that the catalog does not take: decimal is a set of the code only, optional and protos have no name
    // in this part, and the names of the sets are written in lower case with the hyphen of the two-variable set.
    [Theory]
    [InlineData("""{"extensions": [{"name": |"decimal", "version": "1"}]}""", "decimal")]
    [InlineData("""{"extensions": [{"name": |"two_var_comprehensions", "version": "0"}]}""", "two_var_comprehensions")]
    [InlineData("""{"extensions": [{"name": |"comprev2", "version": "0"}]}""", "comprev2")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.network", "version": "1"}]}""", "cel.lib.ext.network")]
    [InlineData("""{"extensions": [{"name": |"cel.lib.ext.comprev2", "version": "0"}]}""", "cel.lib.ext.comprev2")]
    [InlineData("""{"extensions": [{"name": |"Math", "version": "3"}]}""", "Math")]
    [InlineData("""{"extensions": [{"name": |" strings", "version": "5"}]}""", " strings")]
    public static void NameThatTheCatalogDoesNotTake_IsAnErrorAtTheValue(string marked, string name) =>
        JsonExample.AssertError(marked, mentions: name);

    // A version that the set does not have is not an error of the description: Apply accepts it, and Build says
    // UnknownExtensionVersion with the place of the element. The zero, the version of the set below and above, a version in
    // leading zeros and the largest are all read as numbers.
    [Theory]
    [InlineData("strings", "4", 4u, 5u)]
    [InlineData("strings", "6", 6u, 5u)]
    [InlineData("strings", "0", 0u, 5u)]
    [InlineData("strings", "007", 7u, 5u)]
    [InlineData("strings", "4294967295", 4294967295u, 5u)]
    [InlineData("math", "2", 2u, 3u)]
    [InlineData("lists", "5", 5u, 4u)]
    [InlineData("sets", "1", 1u, 0u)]
    [InlineData("encoders", "0", 0u, 1u)]
    [InlineData("bindings", "2", 2u, 1u)]
    [InlineData("two-var-comprehensions", "1", 1u, 0u)]
    [InlineData("network", "2", 2u, 1u)]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersionAtBuild(string name, string version, uint number, uint known)
    {
        string json = "{\"name\": \"m\", \"extensions\": [{\"name\": \"" + name + "\", \"version\": \"" + version + "\"}]}";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, json));

        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(builder),
            CelErrorCode.UnknownExtensionVersion,
            "extension '" + name + "' has no version " + number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "; available versions: " + known.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "environment JSON 'm'",
            json.IndexOf("{\"name\": \"" + name + "\"", StringComparison.Ordinal));
    }

    // The same set twice, in the description or in the description and in code, is one set; the set with an unknown
    // version is left out of the other checks, so it is one error and not a conflict of versions, at its own element.
    [Fact]
    public void SameSetAgain_IsOneSet_AndAnUnknownVersionIsOneError()
    {
        const string Twice = """{"extensions": [{"name": "strings", "version": "5"}, {"name": "strings", "version": "5"}]}""";
        var builder = JsonExample.NewBuilder().AddExtension(CelExtensions.Strings(5));
        Assert.Empty(JsonExample.Apply(builder, Twice));
        Assert.True(builder.Build().IsSuccess);

        const string TwoVersions = """{"name": "m", "extensions": [{"name": "strings", "version": "5"}, {"name": "strings", "version": "4"}]}""";
        var second = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(second, TwoVersions));

        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(second),
            CelErrorCode.UnknownExtensionVersion,
            "extension 'strings' has no version 4; available versions: 5",
            "environment JSON 'm'",
            TwoVersions.IndexOf("{\"name\": \"strings\", \"version\": \"4\"", StringComparison.Ordinal));
    }

    // A set of the description and a set of the application in another version: ExtensionVersionConflict, with the place
    // of the element of the description, whichever call comes first.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void SetOfTheDescriptionAndASetOfTheCodeInAnotherVersion_IsExtensionVersionConflict()
    {
        const string Json = """{"name": "m", "extensions": [{"name": "strings", "version": "5"}]}""";
        int element = Json.IndexOf("{\"name\": \"strings\"", StringComparison.Ordinal);

        var codeFirst = JsonExample.NewBuilder().AddExtension(Own("strings", 4));
        Assert.Empty(JsonExample.Apply(codeFirst, Json));
        var jsonFirst = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(jsonFirst, Json));
        jsonFirst.AddExtension(Own("strings", 4));

        foreach (CelEnvironmentBuilder builder in new[] { codeFirst, jsonFirst })
        {
            JsonExample.AssertBuildErrorAt(
                JsonExample.BuildError(builder),
                CelErrorCode.ExtensionVersionConflict,
                "extension 'strings' is added in versions 4 and 5",
                "environment JSON 'm'",
                element);
        }
    }

    // A set of the description and another set of the application with its name and version: DuplicateExtension, the
    // sources named by their text, the place that of the element of the description.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void SetOfTheDescriptionAndADifferentSetOfTheCode_IsDuplicateExtension()
    {
        const string Json = """{"name": "m", "extensions": [{"name": "strings", "version": "5"}]}""";
        int element = Json.IndexOf("{\"name\": \"strings\"", StringComparison.Ordinal);

        var codeFirst = JsonExample.NewBuilder().AddExtension(Own("strings", 5));
        Assert.Empty(JsonExample.Apply(codeFirst, Json));
        var jsonFirst = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(jsonFirst, Json));
        jsonFirst.AddExtension(Own("strings", 5));

        foreach (CelEnvironmentBuilder builder in new[] { codeFirst, jsonFirst })
        {
            JsonExample.AssertBuildErrorAt(
                JsonExample.BuildError(builder),
                CelErrorCode.DuplicateExtension,
                "extension 'strings' version 5 is added as two different sets: application and extension strings v5",
                "environment JSON 'm'",
                element);
        }
    }

    // The set of the application that needs a set is satisfied by the set of the description, and without the description
    // the error is that of the set of the application, which has no place in the JSON.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void DependencyOfASetOfTheCode_IsMetByTheDescription()
    {
        const string Json = """{"extensions": [{"name": "strings", "version": "5"}]}""";
        var met = JsonExample.NewBuilder().AddExtension(Own("report", 1, "strings"));
        Assert.Empty(JsonExample.Apply(met, Json));
        Assert.True(met.Build().IsSuccess);

        var unmet = JsonExample.NewBuilder().AddExtension(Own("report", 1, "strings"));
        Assert.Empty(JsonExample.Apply(unmet, """{"extensions": [{"name": "math", "version": "3"}]}"""));

        CelError error = JsonExample.BuildError(unmet);

        JsonExample.AssertBuildError(
            error,
            CelErrorCode.MissingExtensionDependency,
            "extension report v1 requires extension 'strings', which is not added to the environment");
        Assert.Null(error.SourceLocation);
    }

    private static CelExtension Own(string name, uint version, params string[] dependencies) =>
        CelExtension.Create(
            name,
            version,
            [new CelFunctionDecl(name + "Probe", CelOverloadDecl.Global(name + "_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { [name + "_probe_string"] = Echo },
            dependencies);
}
