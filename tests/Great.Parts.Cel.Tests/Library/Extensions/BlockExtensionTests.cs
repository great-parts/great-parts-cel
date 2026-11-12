using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions;

// The internal set block: its entry in the catalog class, which no member of CelExtensions and no name of a description reaches,
// and its definition — the four macros, the thirty variables, the dependency on bindings and nothing else.
public sealed class BlockExtensionTests
{
    private static readonly string[] Dependencies = ["bindings"];
    private static readonly string[] MacroNames = ["block", "index", "iterVar", "accuVar"];
    private static readonly int[] MacroArguments = [2, 1, 2, 2];

    // The entry is made once and is a set of the name and the version of its definition, which only the catalog table lacks.
    [Fact]
    public static void Entry_IsOneSetOfVersionOne_ThatTheCatalogTableDoesNotKnow()
    {
        CelExtension entry = ExtensionCatalog.Block;

        Assert.Same(entry, ExtensionCatalog.Block);
        Assert.Equal(("block", 1u), (entry.Name, entry.Version));
        Assert.Equal(new uint[] { 1 }, entry.AvailableVersions);
        Assert.NotNull(entry.Definition);
        Assert.Null(ExtensionCatalog.Default.Get("block", 1).Definition);
        Assert.Empty(ExtensionCatalog.Default.Get("block", 1).AvailableVersions);
    }

    [Fact]
    public static void Definition_HasTheMacrosTheVariablesAndTheDependencyOnly()
    {
        var definition = Assert.IsType<CatalogExtension>(ExtensionCatalog.Block.Definition);

        Assert.Equal("extension block v1", definition.Source);
        Assert.Empty(definition.Overloads);
        Assert.Empty(definition.AbstractTypes);
        Assert.Equal(Dependencies, definition.Dependencies);
        Assert.False(definition.OptionalSyntax);
        Assert.Equal(MacroNames, definition.Macros.Select(macro => macro.Name));
        Assert.All(definition.Macros, macro => Assert.Equal("cel", macro.Namespace));
        Assert.Equal(MacroArguments, definition.Macros.Select(macro => macro.ArgumentCount));
    }

    // @index0 to @index29, each of the type dyn and no other name.
    [Fact]
    public static void Variables_AreTheThirtySlotNamesOfTheTypeDyn()
    {
        var definition = Assert.IsType<CatalogExtension>(ExtensionCatalog.Block.Definition);

        Assert.Equal(BlockExtension.IndexCount, definition.Variables.Length);
        for (int index = 0; index < definition.Variables.Length; index++)
        {
            Assert.Equal("@index" + index.ToString(CultureInfo.InvariantCulture), definition.Variables[index].Name);
            Assert.Equal(CelType.Dyn, definition.Variables[index].Type);
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public static void Create_OfAnotherVersion_IsNull(uint version) => Assert.Null(BlockExtension.Create(version));
}
