using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Extensions.Network;
using Great.Parts.Cel.Library.Extensions.Strings;

namespace Great.Parts.Cel.Library.Extensions;

// One name of the catalog: the short name that an environment description writes, the versions of its table and the factory
// of the definition of a version. A family whose factory is null has no set yet: its part of the library brings the factory.
internal sealed record ExtensionFamily(string Name, ImmutableArray<uint> Versions, Func<uint, CatalogExtension?>? Factory);

// The catalog of the sets that the library implements: name, then version, then one definition, made lazily and once, with
// one CelExtension for each pair of name and version of the table. Everything in it is given by the code of the library, so
// the frozen collections have trusted keys, and no key is text of an expression or of a description. A set that a part adds
// gets its row in ExtensionFamilies and its factory; its name then joins Names, and so the names that an environment
// description accepts.
internal sealed class ExtensionCatalog
{
    // The families of the library, with the versions of the table of the sets: one version each in this release. A part that
    // implements a set gives its row the factory.
    private static readonly ExtensionFamily[] ExtensionFamilies =
    [
        new("strings", [5], StringsExtension.Create),
        new("math", [3], MathExtension.Create),
        new("lists", [4], ListsExtension.Create),
        new("sets", [0], SetsExtension.Create),
        new("encoders", [1], EncodersExtension.Create),
        new("bindings", [1], BindingsExtension.Create),
        new("two-var-comprehensions", [0], TwoVarComprehensionsExtension.Create),
        new("network", [1], NetworkExtension.Create),
    ];

    private readonly FrozenDictionary<string, ExtensionFamily> _families;

    // The set of each pair of the table, made at its first request.
    private readonly FrozenDictionary<(string Name, uint Version), Lazy<CelExtension>> _known;

    internal ExtensionCatalog(IEnumerable<ExtensionFamily> families)
    {
        Dictionary<string, ExtensionFamily> byName = new(StringComparer.Ordinal);
        Dictionary<(string, uint), Lazy<CelExtension>> known = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (ExtensionFamily family in families)
        {
            byName.Add(family.Name, family);
            if (family.Factory is null)
            {
                continue;
            }

            names.Add(family.Name);
            foreach (uint version in family.Versions)
            {
                known.Add((family.Name, version), new Lazy<CelExtension>(() => Wrap(family, version, family.Factory!(version)), LazyThreadSafetyMode.ExecutionAndPublication));
            }
        }

        _families = byName.ToFrozenDictionary(StringComparer.Ordinal);
        _known = known.ToFrozenDictionary();
        Names = names.ToFrozenSet(StringComparer.Ordinal);
    }

    // The catalog of the library.
    internal static ExtensionCatalog Default { get; } = new(ExtensionFamilies);

    // The names of the sets that the library implements, as an environment description writes them.
    internal FrozenSet<string> Names { get; }

    // The set of the name and the version. A pair of the table gives the same CelExtension each time; another version gives a
    // set without a definition, which Build reports as UnknownExtensionVersion with the versions of the table. A name that
    // the catalog does not have gives a set without a definition and without versions.
    internal CelExtension Get(string name, uint version)
    {
        if (_known.TryGetValue((name, version), out Lazy<CelExtension>? set))
        {
            return set.Value;
        }

        return new CelExtension(name, version, null, VersionsOf(name));
    }

    // The short name of the set that declares the abstract type of a full name, such as network for net.IP; null for a name that
    // no set of the catalog declares. The hint of TypeNotDeclared names it. The table is the code of the sets, not text of an
    // expression, so a name is looked up in it without being trusted with anything.
    internal static string? SetOfAbstractType(string typeName) => NetworkExtension.SetOfType(typeName);

    // The versions of the name in the table, ascending; empty for a name that the catalog does not have.
    internal ImmutableArray<uint> VersionsOf(string name) =>
        _families.TryGetValue(name, out ExtensionFamily? family) ? family.Versions : [];

    private static CelExtension Wrap(ExtensionFamily family, uint version, CatalogExtension? definition) =>
        new(family.Name, version, definition, family.Versions);
}
