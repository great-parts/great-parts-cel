using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.MathSet;

// The tables of the sets math and bindings: the overload identifiers of cel-go, the function name and the call style of each overload,
// the cost that each charges for the call itself, the flags of the list forms, the macros of each set and its place in the catalog.
public sealed class MathExtensionTests
{
    // The 15 overloads of math.@max and of math.@min after the prefix "math_@max" or "math_@min".
    private static readonly string[] ExtremeSuffixes =
    [
        "_double", "_int", "_uint",
        "_double_double", "_int_int", "_uint_uint",
        "_int_uint", "_int_double", "_double_int", "_double_uint", "_uint_int", "_uint_double",
        "_list_double", "_list_int", "_list_uint",
    ];

    // "function | overload identifier": all global.
    private static readonly string[] OtherOverloads =
    [
        "math.ceil | math_ceil_double",
        "math.floor | math_floor_double",
        "math.round | math_round_double",
        "math.trunc | math_trunc_double",
        "math.isInf | math_isInf_double",
        "math.isNaN | math_isNaN_double",
        "math.isFinite | math_isFinite_double",
        "math.abs | math_abs_double",
        "math.abs | math_abs_int",
        "math.abs | math_abs_uint",
        "math.sign | math_sign_double",
        "math.sign | math_sign_int",
        "math.sign | math_sign_uint",
        "math.bitAnd | math_bitAnd_int_int",
        "math.bitAnd | math_bitAnd_uint_uint",
        "math.bitOr | math_bitOr_int_int",
        "math.bitOr | math_bitOr_uint_uint",
        "math.bitXor | math_bitXor_int_int",
        "math.bitXor | math_bitXor_uint_uint",
        "math.bitNot | math_bitNot_int_int",
        "math.bitNot | math_bitNot_uint_uint",
        "math.bitShiftLeft | math_bitShiftLeft_int_int",
        "math.bitShiftLeft | math_bitShiftLeft_uint_int",
        "math.bitShiftRight | math_bitShiftRight_int_int",
        "math.bitShiftRight | math_bitShiftRight_uint_int",
        "math.sqrt | math_sqrt_double",
        "math.sqrt | math_sqrt_int",
        "math.sqrt | math_sqrt_uint",
    ];

    [Fact]
    public static void Math_HasFiftyEightOverloadsWithTheIdentifiersOfCelGoInTheOrderOfTheTable()
    {
        CatalogExtension definition = DefinitionOf(CelExtensions.Math(3));
        List<string> expected = [];
        expected.AddRange(ExtremeSuffixes.Select(static suffix => "math.@max | math_@max" + suffix));
        expected.AddRange(ExtremeSuffixes.Select(static suffix => "math.@min | math_@min" + suffix));
        expected.AddRange(OtherOverloads);

        Assert.Equal(58, expected.Count);
        Assert.Equal(expected, definition.Overloads.Select(static overload => overload.Function + " | " + overload.Declaration.OverloadId));
        Assert.All(definition.Overloads, static overload => Assert.False(overload.Declaration.IsReceiverStyle));
        Assert.Equal("extension math v3", definition.Source);
    }

    // A call is 1 unit; the six list forms add one unit for each element, in the argument cost, and have the flag that keeps the
    // planner from checking their elements at depth. Every overload has an implementation.
    [Fact]
    public static void Math_ChargesOneUnitAndTheListSizeOfTheListForms()
    {
        foreach (OverloadBinding overload in DefinitionOf(CelExtensions.Math(3)).Overloads)
        {
            bool isList = overload.Declaration.OverloadId.Contains("_list_", System.StringComparison.Ordinal);

            Assert.Equal(1UL, overload.FixedCost);
            Assert.NotNull(overload.Implementation);
            Assert.False(overload.IsApplication);
            Assert.False(overload.DynOnly);
            Assert.Equal(isList, overload.ArgumentCost is not null);
            Assert.Equal(isList, overload.ShallowArguments);
            Assert.Null(overload.Validator);
        }
    }

    // Mixed pairs are the only overloads whose result is dyn; the three list forms of a function return the element type.
    [Fact]
    public static void Math_MixedPairsReturnDyn()
    {
        IEnumerable<string> dynResults = DefinitionOf(CelExtensions.Math(3)).Overloads
            .Where(static overload => overload.Declaration.ResultType.Kind == CelTypeKind.Dyn)
            .Select(static overload => overload.Declaration.OverloadId);

        Assert.Equal(
            [
                "math_@max_int_uint", "math_@max_int_double", "math_@max_double_int", "math_@max_double_uint", "math_@max_uint_int", "math_@max_uint_double",
                "math_@min_int_uint", "math_@min_int_double", "math_@min_double_int", "math_@min_double_uint", "math_@min_uint_int", "math_@min_uint_double",
            ],
            dynResults);
    }

    [Fact]
    public static void Math_BringsTheMacrosGreatestAndLeast()
    {
        CatalogExtension definition = DefinitionOf(CelExtensions.Math(3));

        Assert.Equal(["greatest", "least"], definition.Macros.Select(static macro => macro.Name));
        Assert.All(definition.Macros, static macro =>
        {
            Assert.Equal("math", macro.Namespace);
            Assert.True(macro.IsReceiver);
            Assert.Equal(Macro.AnyArgumentCount, macro.ArgumentCount);
        });
        Assert.Equal(["greatest | any", "least | any"], definition.MacroForms.Select(static form => form.Name + " | " + (form.ArgumentCount == -1 ? "any" : "n")));
    }

    [Fact]
    public static void Bindings_HasTheBlockOverloadWithoutAnImplementationAndTheMacroBind()
    {
        CatalogExtension definition = DefinitionOf(CelExtensions.Bindings(1));

        OverloadBinding block = Assert.Single(definition.Overloads);
        Assert.Equal("cel.@block", block.Function);
        Assert.Equal("cel_block_list", block.Declaration.OverloadId);
        Assert.False(block.Declaration.IsReceiverStyle);
        Assert.Equal(2, block.Declaration.ParameterTypes.Length);
        Assert.Equal(CelType.ListOf(CelType.Dyn), block.Declaration.ParameterTypes[0]);
        Assert.Equal(CelTypeKind.TypeParameter, block.Declaration.ParameterTypes[1].Kind);
        Assert.Equal(block.Declaration.ParameterTypes[1], block.Declaration.ResultType);
        Assert.Null(block.Implementation);
        Assert.Equal(0UL, block.FixedCost);
        Assert.Null(block.ArgumentCost);
        Assert.NotNull(block.Validator);
        Assert.Equal("extension bindings v1", definition.Source);

        Macro bind = Assert.Single(definition.Macros);
        Assert.Equal(("bind", "cel", 3, true), (bind.Name, bind.Namespace, bind.ArgumentCount, bind.IsReceiver));
    }

    [Fact]
    public static void Catalog_HasBothSetsWithTheirOnlyVersion()
    {
        Assert.Contains("math", CelExtensions.Names.Items);
        Assert.Contains("bindings", CelExtensions.Names.Items);
        Assert.Equal(new uint[] { 3 }, CelExtensions.Math(3).AvailableVersions);
        Assert.Equal(new uint[] { 1 }, CelExtensions.Bindings(1).AvailableVersions);
        Assert.Null(CelExtensions.Math(2).Definition);
        Assert.Null(CelExtensions.Bindings(0).Definition);
        Assert.Same(CelExtensions.Math(3), CelExtensions.Math(3));
        Assert.NotNull(ExtensionCatalog.Default.Get("math", 3).Definition);
    }

    // The sets with the others of the catalog build together, and a repeated call adds nothing.
    [Fact]
    public static void BothSets_BuildInOneEnvironmentAndAddedTwiceAreOne()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(CelExtensions.Bindings(1))
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Math(3))
            .Build();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(static error => error.Message)));
    }

    private static CatalogExtension DefinitionOf(CelExtension extension) =>
        Assert.IsType<CatalogExtension>(extension.Definition);
}
