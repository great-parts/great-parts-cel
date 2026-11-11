using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The tables of the sets strings and sets: the overload identifiers of cel-go in its order, the function name and the call style of each
// overload, the unit that each charges for the call itself, and the place of the sets in the catalog.
public sealed class StringsExtensionTests
{
    // "function | overload identifier | style": the order of cel-go.
    private static readonly string[] StringsTable =
    [
        "charAt | string_char_at_int | member",
        "indexOf | string_index_of_string | member",
        "indexOf | string_index_of_string_int | member",
        "lastIndexOf | string_last_index_of_string | member",
        "lastIndexOf | string_last_index_of_string_int | member",
        "lowerAscii | string_lower_ascii | member",
        "replace | string_replace_string_string | member",
        "replace | string_replace_string_string_int | member",
        "split | string_split_string | member",
        "split | string_split_string_int | member",
        "substring | string_substring_int | member",
        "substring | string_substring_int_int | member",
        "trim | string_trim | member",
        "upperAscii | string_upper_ascii | member",
        "join | list_join | member",
        "join | list_join_string | member",
        "format | string_format | member",
        "strings.quote | strings_quote | global",
        "reverse | string_reverse | member",
    ];

    private static readonly string[] SetsTable =
    [
        "sets.contains | list_sets_contains_list | global",
        "sets.equivalent | list_sets_equivalent_list | global",
        "sets.intersects | list_sets_intersects_list | global",
    ];

    [Fact]
    public static void Strings_HasTheOverloadsOfCelGoInItsOrder()
    {
        CatalogExtension definition = DefinitionOf(CelExtensions.Strings(5));

        Assert.Equal(StringsTable, Table(definition));
        Assert.Equal("extension strings v5", definition.Source);
    }

    [Fact]
    public static void Sets_HasThreeOverloads()
    {
        CatalogExtension definition = DefinitionOf(CelExtensions.Sets(0));

        Assert.Equal(SetsTable, Table(definition));
        Assert.Equal("extension sets v0", definition.Source);
    }

    // A call is 1 unit, and the cost of the strings it is given is the argument cost: the sets have none, since they charge their
    // comparisons as they make them.
    [Fact]
    public static void EveryOverload_CostsOneUnitForTheCallAndHasItsImplementation()
    {
        foreach (OverloadBinding overload in DefinitionOf(CelExtensions.Strings(5)).Overloads.Concat(DefinitionOf(CelExtensions.Sets(0)).Overloads))
        {
            Assert.Equal(1UL, overload.FixedCost);
            Assert.NotNull(overload.Implementation);
            Assert.False(overload.IsApplication);
            Assert.False(overload.DynOnly);
            Assert.False(overload.ShallowArguments);
            Assert.Equal(overload.Source.StartsWith("extension sets", System.StringComparison.Ordinal), overload.ArgumentCost is null);
        }
    }

    [Fact]
    public static void Catalog_HasBothSetsWithTheirOnlyVersion()
    {
        Assert.Contains("strings", CelExtensions.Names.Items);
        Assert.Contains("sets", CelExtensions.Names.Items);
        Assert.Equal(new uint[] { 5 }, CelExtensions.Strings(5).AvailableVersions);
        Assert.Equal(new uint[] { 0 }, CelExtensions.Sets(0).AvailableVersions);
        Assert.Null(CelExtensions.Strings(4).Definition);
        Assert.Null(CelExtensions.Sets(1).Definition);
        Assert.Same(CelExtensions.Strings(5), CelExtensions.Strings(5));
    }

    // The sets strings and sets with every other set of the catalog build together, and a repeated call adds nothing.
    [Fact]
    public static void BothSets_BuildInOneEnvironmentAndAddedTwiceAreOne()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Sets(0))
            .AddExtension(CelExtensions.Strings(5))
            .Build();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
    }

    private static CatalogExtension DefinitionOf(CelExtension extension) =>
        Assert.IsType<CatalogExtension>(extension.Definition);

    private static string[] Table(CatalogExtension definition) =>
        [.. definition.Overloads.Select(static overload =>
            overload.Function + " | " + overload.Declaration.OverloadId + " | " + (overload.Declaration.IsReceiverStyle ? "member" : "global"))];
}
