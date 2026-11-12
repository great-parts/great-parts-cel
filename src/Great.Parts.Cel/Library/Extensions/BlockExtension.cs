using System.Collections.Immutable;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions;

// The internal set block, version 1: the test macros cel.block, cel.index, cel.iterVar and cel.accuVar of the conformance runner and
// the variables @index0 to @index29 of the type dyn. The set has no overloads: cel.block expands to cel.@block of the set bindings,
// which it needs. It is no part of the catalog — ExtensionCatalog.Block is its only entry, with no name in a description and no
// member of CelExtensions — so only code of the library and of its tests can add it.
internal static class BlockExtension
{
    internal const string SetName = "block";
    internal const uint Version = 1;

    // The number of variables @indexN the set declares: N from 0 to 29.
    internal const int IndexCount = 30;

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version
            ? new CatalogExtension(SetName, Version, [], variables: Variables(), dependencies: [BindingsExtension.SetName], macros: ExtensionMacros.Block)
            : null;

    private static ImmutableArray<CelVariableDecl> Variables()
    {
        ImmutableArray<CelVariableDecl>.Builder variables = ImmutableArray.CreateBuilder<CelVariableDecl>(IndexCount);
        for (int index = 0; index < IndexCount; index++)
        {
            variables.Add(new CelVariableDecl("@index" + index.ToString(CultureInfo.InvariantCulture), CelType.Dyn));
        }

        return variables.MoveToImmutable();
    }
}
