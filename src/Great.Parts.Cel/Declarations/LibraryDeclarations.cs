using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

// The declarations of one version of the standard library, indexed once for the build of every
// environment with that version: the overloads in the order of the table, by identifier and by function,
// the type identifiers with the types they stand for, and the names that a variable cannot have.
internal sealed class LibraryDeclarations
{
    internal LibraryDeclarations(
        uint version,
        ImmutableArray<OverloadBinding> overloads,
        FrozenDictionary<string, CelType> typeIdentifiers,
        IEnumerable<string> laterTypeIdentifiers)
    {
        Version = version;
        VersionText = "v" + version.ToString(CultureInfo.InvariantCulture);
        Overloads = overloads;
        TypeIdentifiers = typeIdentifiers;

        var byId = new Dictionary<string, OverloadBinding>(StringComparer.Ordinal);
        var byFunction = new Dictionary<string, List<OverloadBinding>>(StringComparer.Ordinal);
        foreach (OverloadBinding overload in overloads)
        {
            byId.Add(overload.Declaration.OverloadId, overload);
            (CollectionsMarshal.GetValueRefOrAddDefault(byFunction, overload.Function, out _) ??= []).Add(overload);
        }

        ById = byId.ToFrozenDictionary(StringComparer.Ordinal);
        ByFunction = byFunction.ToFrozenDictionary(entry => entry.Key, entry => entry.Value.ToImmutableArray(), StringComparer.Ordinal);
        ReservedNames = new HashSet<string>([.. typeIdentifiers.Keys, .. laterTypeIdentifiers], StringComparer.Ordinal).ToFrozenSet(StringComparer.Ordinal);
    }

    internal uint Version { get; }

    // The version as build errors write it, such as v1.
    internal string VersionText { get; }

    internal ImmutableArray<OverloadBinding> Overloads { get; }

    internal FrozenDictionary<string, OverloadBinding> ById { get; }

    internal FrozenDictionary<string, ImmutableArray<OverloadBinding>> ByFunction { get; }

    // The type identifiers of the version, such as int, with the type each stands for.
    internal FrozenDictionary<string, CelType> TypeIdentifiers { get; }

    // The type identifiers and the names that a table lists beyond them: a variable with one of these names conflicts
    // with the standard library.
    internal FrozenSet<string> ReservedNames { get; }
}
