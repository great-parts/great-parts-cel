using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set protos, version 0: the macros proto.getExt(m, a.b.c) and proto.hasExt(m, a.b.c) and nothing else — no function,
// no overload, no abstract type. The macros turn the call into the selection of the field whose name is the full name of the
// extension, or into its presence test; the field itself comes from a type provider, in practice the protobuf package.
internal static class ProtosExtension
{
    internal const string SetName = "protos";
    internal const uint Version = 0;

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, [], macros: ExtensionMacros.Protos) : null;
}
