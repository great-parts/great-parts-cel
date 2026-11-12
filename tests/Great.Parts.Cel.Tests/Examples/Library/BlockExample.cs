using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Extensions;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The environments of the examples of the internal set block (the page of the extension sets, "block"). The set has no member
// of CelExtensions and no name in an environment description: the conformance runner gets it as the internal entry
// ExtensionCatalog.Block, and so do these examples (the test project sees the internals of the library). That entry is the one
// line of the examples that depends on the factory of the set. The runner puts the set into the environment of every case
// together with bindings 1, on which it depends, and with the set optional, which four of its cases use.
internal static class BlockExample
{
    // The internal set block, version 1.
    internal static CelExtension Set() => ExtensionCatalog.Block;

    // An environment builder as the runner has it for these cases: the standard library, bindings 1, block 1, optional 2.
    internal static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Bindings(1))
        .AddExtension(Set())
        .AddExtension(CelExtensions.Optional(2));
}
