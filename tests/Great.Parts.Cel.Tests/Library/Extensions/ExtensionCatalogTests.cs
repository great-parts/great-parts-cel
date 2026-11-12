using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Declarations;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions;

// The catalog of the extension sets: the table of versions of the ten sets, one definition and one CelExtension for each
// pair of name and version, and what the members of CelExtensions hand out. A catalog of the tests has families with factories;
// the catalog of the library has the table of the sets that the library names, and the name of each joins Names with the factory.
public sealed class ExtensionCatalogTests
{
    private static readonly string[] LibraryNames =
        ["strings", "math", "lists", "sets", "encoders", "bindings", "two-var-comprehensions", "network", "protos", "optional"];

    [Fact]
    public void CatalogOfTheLibrary_HasTheVersionsOfTheTenSets()
    {
        Assert.Equal(new uint[] { 5 }, ExtensionCatalog.Default.VersionsOf("strings"));
        Assert.Equal(new uint[] { 3 }, ExtensionCatalog.Default.VersionsOf("math"));
        Assert.Equal(new uint[] { 4 }, ExtensionCatalog.Default.VersionsOf("lists"));
        Assert.Equal(new uint[] { 0 }, ExtensionCatalog.Default.VersionsOf("sets"));
        Assert.Equal(new uint[] { 1 }, ExtensionCatalog.Default.VersionsOf("encoders"));
        Assert.Equal(new uint[] { 1 }, ExtensionCatalog.Default.VersionsOf("bindings"));
        Assert.Equal(new uint[] { 0 }, ExtensionCatalog.Default.VersionsOf("two-var-comprehensions"));
        Assert.Equal(new uint[] { 1 }, ExtensionCatalog.Default.VersionsOf("network"));
        Assert.Equal(new uint[] { 0 }, ExtensionCatalog.Default.VersionsOf("protos"));
        Assert.Equal(new uint[] { 2 }, ExtensionCatalog.Default.VersionsOf("optional"));
        Assert.Empty(ExtensionCatalog.Default.VersionsOf("block"));
    }

    [Fact]
    public void NamesOfTheLibrary_AreSetsOfTheTableAndNeverTheInternalSet()
    {
        Assert.All(CelExtensions.Names, name => Assert.Contains(name, LibraryNames));
        Assert.DoesNotContain("block", CelExtensions.Names.Items);
        Assert.Contains("optional", CelExtensions.Names.Items);
    }

    [Fact]
    public void MembersOfCelExtensions_HandOutTheSetOfTheirNameAndVersion()
    {
        (Func<uint, CelExtension> Member, string Name)[] members =
        [
            (CelExtensions.Strings, "strings"),
            (CelExtensions.Math, "math"),
            (CelExtensions.Lists, "lists"),
            (CelExtensions.Sets, "sets"),
            (CelExtensions.Encoders, "encoders"),
            (CelExtensions.Bindings, "bindings"),
            (CelExtensions.TwoVarComprehensions, "two-var-comprehensions"),
            (CelExtensions.Network, "network"),
            (CelExtensions.Protos, "protos"),
            (CelExtensions.Optional, "optional"),
        ];

        foreach ((Func<uint, CelExtension> member, string name) in members)
        {
            CelExtension extension = member(4000000);

            Assert.Equal(name, extension.Name);
            Assert.Equal(4000000u, extension.Version);
            Assert.Equal(name, CelExtensions.Create(name, 4000000).Name);
        }
    }

    [Fact]
    public void NamesOfACatalog_AreThoseOfFamiliesWithAFactory()
    {
        ExtensionCatalog catalog = TestCatalog(out _);

        string[] names = [.. catalog.Names.Order(StringComparer.Ordinal)];

        Assert.Equal(2, names.Length);
        Assert.Equal("alpha", names[0]);
        Assert.Equal("beta", names[1]);
        Assert.DoesNotContain("gamma", catalog.Names.Items);
    }

    [Fact]
    public void PairOfTheTable_IsOneCelExtensionWithOneDefinition()
    {
        ExtensionCatalog catalog = TestCatalog(out Dictionary<string, int> made);

        CelExtension first = catalog.Get("alpha", 1);
        CelExtension again = catalog.Get("alpha", 1);

        Assert.Same(first, again);
        Assert.NotNull(first.Definition);
        Assert.Equal("extension alpha v1", first.Definition.Source);
        Assert.Equal("alpha", catalog.Get("alpha", 2).Name);
        Assert.Equal("beta", catalog.Get("beta", 0).Name);
        Assert.NotSame(first, catalog.Get("alpha", 2));
        Assert.Equal(1, made["alpha1"]);
        Assert.Equal(new uint[] { 1, 2 }, first.AvailableVersions);
    }

    [Fact]
    public void Definition_IsMadeOnceEvenFromManyThreads()
    {
        ExtensionCatalog catalog = TestCatalog(out Dictionary<string, int> made);
        CelExtension[] results = new CelExtension[64];

        Parallel.For(0, results.Length, index => results[index] = catalog.Get("beta", 0));

        Assert.All(results, result => Assert.Same(results[0], result));
        Assert.Equal(1, made["beta0"]);
    }

    [Fact]
    public void VersionOutsideTheTable_IsASetWithoutADefinitionThatListsTheVersions()
    {
        ExtensionCatalog catalog = TestCatalog(out _);

        CelExtension first = catalog.Get("alpha", 7);
        CelExtension second = catalog.Get("alpha", 7);

        Assert.NotSame(first, second);
        Assert.Null(first.Definition);
        Assert.Equal((7u, "alpha"), (first.Version, first.Name));
        Assert.Equal(new uint[] { 1, 2 }, first.AvailableVersions);
    }

    [Fact]
    public void FamilyWithoutAFactory_HasTheTableButNoDefinition()
    {
        ExtensionCatalog catalog = TestCatalog(out _);

        CelExtension extension = catalog.Get("gamma", 3);

        Assert.Null(extension.Definition);
        Assert.Equal(new uint[] { 3 }, extension.AvailableVersions);
        Assert.Equal(new uint[] { 3 }, catalog.VersionsOf("gamma"));
    }

    [Fact]
    public void NameThatTheCatalogLacks_IsASetWithoutADefinitionAndWithoutVersions()
    {
        ExtensionCatalog catalog = TestCatalog(out _);

        CelExtension extension = catalog.Get("nosuchset", 1);

        Assert.Null(extension.Definition);
        Assert.Empty(extension.AvailableVersions);
        Assert.Empty(catalog.VersionsOf("nosuchset"));
    }

    [Fact]
    public void CatalogSetOfAVersionTheTableLacks_IsUnknownVersionAtBuild()
    {
        ExtensionCatalog catalog = TestCatalog(out _);

        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(catalog.Get("alpha", 7)).Build();

        Assert.False(result.IsSuccess);
        Assert.Equal("extension 'alpha' has no version 7; available versions: 1, 2", Assert.Single(result.Errors).Message);
    }

    // Every set that the library names builds with the others in one environment: no repeated identifier, no overlap with
    // another set or with version 1, and the macros of all sets with the standard ones have distinct keys.
    [Fact]
    public void EverySetOfTheLibrary_BuildsTogetherWithTheOthers()
    {
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        List<Macro> macros = [.. MacroTable.Standard.Macros.ToArray()];
        foreach (string name in CelExtensions.Names)
        {
            foreach (uint version in ExtensionCatalog.Default.VersionsOf(name))
            {
                CelExtension extension = CelExtensions.Create(name, version);
                builder.AddExtension(extension);
                if (extension.Definition is CatalogExtension catalog)
                {
                    macros.AddRange(catalog.Macros);
                }
            }
        }

        CelBuildResult result = builder.Build();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        Assert.NotNull(MacroTable.Create(macros));
    }

    [Fact]
    public void FamilyNamedTwice_IsRefused()
    {
        ExtensionFamily family = new("alpha", [1], static _ => null);

        Assert.Throws<ArgumentException>(() => new ExtensionCatalog([family, family]));
    }

    // A catalog with alpha 1 and 2, beta 0 and a family gamma 3 that has no factory yet; made counts the definitions by name
    // and version.
    private static ExtensionCatalog TestCatalog(out Dictionary<string, int> made)
    {
        Dictionary<string, int> counts = [];
        made = counts;
        return new ExtensionCatalog(
        [
            new ExtensionFamily("alpha", [1, 2], version => Make(counts, "alpha", version)),
            new ExtensionFamily("beta", [0], version => Make(counts, "beta", version)),
            new ExtensionFamily("gamma", [3], null),
        ]);
    }

    private static CatalogExtension Make(Dictionary<string, int> counts, string name, uint version)
    {
        lock (counts)
        {
            string key = name + version;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return ExtensionTestSets.Set(name, version);
    }
}
