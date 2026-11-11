using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Tests.Declarations;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The key extensions with a catalog that names sets: the name and the version resolve through the catalog into the call of
// AddExtension, and the build errors of a set that a description declared have the place of its element. The catalog is the
// test's own, since the sets of the library come with their parts: alpha in versions 1 and 2, and beta 1, which needs alpha.
public sealed class EnvironmentJsonCatalogExtensionTests
{
    private static readonly ExtensionCatalog Catalog = new(
    [
        new ExtensionFamily("alpha", [1, 2], static version => ExtensionTestSets.Set("alpha", version)),
        new ExtensionFamily("beta", [1], static version => ExtensionTestSets.Set("beta", version, dependencies: ["alpha"])),
    ]);

    [Fact]
    public void ElementWithANameOfTheCatalogAndAVersion_AddsTheSet()
    {
        CelEnvironmentBuilder builder = NewBuilder();

        ImmutableArray<CelError> errors = Apply(builder, """{"extensions": [{"name": "alpha", "version": "2"}, {"version": "1", "name": "beta"}]}""");

        Assert.Empty(errors);
        Assert.Equal(new[] { Catalog.Get("alpha", 2), Catalog.Get("beta", 1) }, builder.Extensions);
    }

    [Fact]
    public void SameSetFromTheDescriptionAndFromCode_IsOneSet()
    {
        CelEnvironmentBuilder builder = NewBuilder().AddExtension(Catalog.Get("alpha", 1));

        Apply(builder, """{"extensions": [{"name": "alpha", "version": "1"}, {"name": "alpha", "version": "1"}]}""");

        Assert.Equal(new[] { Catalog.Get("alpha", 1) }, builder.Extensions);
        Assert.True(builder.Build().IsSuccess);
    }

    [Fact]
    public void ElementWithAGoodNameAndABadVersion_IsAnErrorAndChangesNothing()
    {
        (string json, int[] offsets) = Mark("""{"extensions": [{"name": "alpha", "version": ^"latest"}, {"name": ^"gamma", "version": ^"x"}]}""");
        CelEnvironmentBuilder builder = NewBuilder();

        ImmutableArray<CelError> errors = Apply(builder, json);

        Assert.Equal(offsets, errors.Select(error => error.SourceLocation?.Offset ?? -1));
        Assert.Empty(builder.Extensions);
    }

    [Fact]
    public void VersionOutsideTheTable_IsNotAnErrorWhileReadingButUnknownVersionAtBuildAtTheElement()
    {
        (string json, int[] offsets) = Mark("""{"name": "m", "extensions": [^{"name": "alpha", "version": "7"}]}""");
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, json));

        CelError error = BuildError(builder);
        AssertBuildError(
            error,
            CelErrorCode.UnknownExtensionVersion,
            "extension 'alpha' has no version 7; available versions: 1, 2",
            "environment JSON 'm'",
            offsets[0]);
    }

    [Fact]
    public void VersionConflict_IsAtTheElementOfTheSmallerVersion()
    {
        (string json, int[] offsets) = Mark("""{"extensions": [{"name": "alpha", "version": "2"}, ^{"name": "alpha", "version": "1"}]}""");
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, json));

        AssertBuildError(BuildError(builder), CelErrorCode.ExtensionVersionConflict, "extension 'alpha' is added in versions 1 and 2", Label, offsets[0]);
    }

    [Fact]
    public void VersionConflictWithASetFromCode_IsAtTheElementOfTheDescription()
    {
        (string json, int[] offsets) = Mark("""{"extensions": [^{"name": "alpha", "version": "1"}]}""");
        CelEnvironmentBuilder builder = NewBuilder().AddExtension(Catalog.Get("alpha", 2));

        Assert.Empty(Apply(builder, json));

        AssertBuildError(BuildError(builder), CelErrorCode.ExtensionVersionConflict, "extension 'alpha' is added in versions 1 and 2", Label, offsets[0]);
    }

    [Fact]
    public void MissingDependency_IsAtTheElementOfTheSetThatNeedsTheOther()
    {
        (string json, int[] offsets) = Mark("""{"extensions": [^{"name": "beta", "version": "1"}]}""");
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.MissingExtensionDependency,
            "extension beta v1 requires extension 'alpha', which is not added to the environment",
            Label,
            offsets[0]);
    }

    [Fact]
    public void SetOfTheDescriptionAndASetOfTheApplicationWithOneNameAndVersion_IsDuplicateExtensionAtTheElement()
    {
        (string json, int[] offsets) = Mark("""{"extensions": [^{"name": "alpha", "version": "1"}]}""");
        CelEnvironmentBuilder builder = NewBuilder().AddExtension(CelExtension.Create("alpha", 1, [], new Dictionary<string, CelFunctionImplementation>()));

        Assert.Empty(Apply(builder, json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateExtension,
            "extension 'alpha' version 1 is added as two different sets: application and extension alpha v1",
            Label,
            offsets[0]);
    }

    // The order of the elements changes neither the set of sets nor the errors.
    [Fact]
    public void EveryOrderOfTheElements_GivesTheSameSetsAndTheSameErrors()
    {
        string[] elements =
        [
            """{"name": "alpha", "version": "1"}""",
            """{"name": "alpha", "version": "2"}""",
            """{"name": "beta", "version": "1"}""",
            """{"name": "alpha", "version": "9"}""",
        ];
        string[]? expected = null;
        foreach (int[] order in Orders(elements.Length))
        {
            CelEnvironmentBuilder builder = NewBuilder();
            string json = "{\"extensions\": [" + string.Join(", ", order.Select(index => elements[index])) + "]}";

            Assert.Empty(Apply(builder, json));

            Assert.Equal(elements.Length, builder.Extensions.Length);
            string[] errors = [.. builder.Build().Errors.Select(error => $"{(int)error.Code} {error.Message}")];
            expected ??= errors;
            Assert.Equal(expected, errors);
        }

        Assert.NotNull(expected);
        Assert.Equal(
            [
                "4003 extension 'alpha' is added in versions 1 and 2",
                "4004 extension 'alpha' has no version 9; available versions: 1, 2",
                "4005 extension beta v1 requires extension 'alpha', which is not added to the environment",
            ],
            expected);
    }

    private static ImmutableArray<CelError> Apply(CelEnvironmentBuilder builder, string json) =>
        CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(json), Catalog.Names, Catalog.Get);

    // Every ordering of 0..count-1.
    private static IEnumerable<int[]> Orders(int count) => Order([], count);

    private static IEnumerable<int[]> Order(List<int> prefix, int count)
    {
        if (prefix.Count == count)
        {
            yield return [.. prefix];
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (prefix.Contains(i))
            {
                continue;
            }

            prefix.Add(i);
            foreach (int[] order in Order(prefix, count))
            {
                yield return order;
            }

            prefix.RemoveAt(prefix.Count - 1);
        }
    }
}
