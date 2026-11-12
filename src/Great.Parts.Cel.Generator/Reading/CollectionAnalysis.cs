using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// What the interfaces of a type say about it as a collection: the family, the element type (for a dictionary the value type)
// and the key type. Ambiguous names the interface that the type implements for two different element types. ReadOnlyCollection
// tells whether the type implements IReadOnlyCollection<T>.
internal sealed record CollectionInfo(CollectionFamily Family, ITypeSymbol ElementType, ITypeSymbol? KeyType, string? Ambiguous, bool ReadOnlyCollection = false);

// The collection rows of the type mapping, by the interfaces of the type, in this priority; the first that matches decides: an
// array or a type that implements IReadOnlyList<T>; a type that implements IList<T> only; a set (IReadOnlySet<T> or ISet<T>); a
// dictionary (IReadOnlyDictionary<K,V> or IDictionary<K,V>); a type that implements IReadOnlyCollection<T>.
internal static class CollectionAnalysis
{
    private static readonly HashSet<string> Interfaces = new(StringComparer.Ordinal)
    {
        WellKnown.ReadOnlyList,
        WellKnown.List,
        WellKnown.ReadOnlySet,
        WellKnown.Set,
        WellKnown.ReadOnlyDictionary,
        WellKnown.Dictionary,
        WellKnown.ReadOnlyCollection,
    };

    internal static CollectionInfo? Analyze(INamedTypeSymbol type)
    {
        var found = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        if (type.TypeKind == TypeKind.Interface)
        {
            Add(found, type);
        }

        foreach (INamedTypeSymbol implemented in type.AllInterfaces)
        {
            Add(found, implemented);
        }

        if (found.Count == 0)
        {
            return null;
        }

        CollectionInfo? info = Choose(type, found);
        return info is null ? null : info with { ReadOnlyCollection = found.ContainsKey(WellKnown.ReadOnlyCollection) };
    }

    private static CollectionInfo? Choose(INamedTypeSymbol type, Dictionary<string, List<INamedTypeSymbol>> found)
    {
        if (found.TryGetValue(WellKnown.ReadOnlyList, out List<INamedTypeSymbol>? readOnlyList))
        {
            return Elements(ListFamily(type), readOnlyList);
        }

        if (found.TryGetValue(WellKnown.List, out List<INamedTypeSymbol>? list))
        {
            return Elements(CollectionFamily.ListOnly, list);
        }

        List<INamedTypeSymbol>? set = Pick(found, WellKnown.ReadOnlySet, WellKnown.Set);
        if (set is not null)
        {
            return Elements(CollectionFamily.Set, set);
        }

        List<INamedTypeSymbol>? dictionary = Pick(found, WellKnown.ReadOnlyDictionary, WellKnown.Dictionary);
        if (dictionary is not null)
        {
            CollectionFamily family = found.ContainsKey(WellKnown.ReadOnlyDictionary) ? CollectionFamily.Dictionary : CollectionFamily.DictionaryOnly;
            return Pairs(family, dictionary);
        }

        return found.TryGetValue(WellKnown.ReadOnlyCollection, out List<INamedTypeSymbol>? collection)
            ? Elements(CollectionFamily.Collection, collection)
            : null;
    }

    private static void Add(Dictionary<string, List<INamedTypeSymbol>> found, INamedTypeSymbol implemented)
    {
        string name = TypeNames.FullName(implemented);
        if (!Interfaces.Contains(name))
        {
            return;
        }

        if (!found.TryGetValue(name, out List<INamedTypeSymbol>? list))
        {
            list = [];
            found[name] = list;
        }

        list.Add(implemented);
    }

    // The interfaces found for the first name, else for the second; null when there are none.
    private static List<INamedTypeSymbol>? Pick(Dictionary<string, List<INamedTypeSymbol>> found, string first, string second)
    {
        if (found.TryGetValue(first, out List<INamedTypeSymbol>? result))
        {
            return result;
        }

        return found.TryGetValue(second, out result) ? result : null;
    }

    private static CollectionFamily ListFamily(INamedTypeSymbol type)
    {
        if (TypeNames.FullName(type) == WellKnown.ImmutableArray)
        {
            return CollectionFamily.ImmutableArray;
        }

        return type.IsValueType ? CollectionFamily.StructList : CollectionFamily.List;
    }

    private static CollectionInfo Elements(CollectionFamily family, List<INamedTypeSymbol> interfaces)
    {
        INamedTypeSymbol first = interfaces[0];
        ITypeSymbol element = first.TypeArguments[0];
        foreach (INamedTypeSymbol other in interfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(other.TypeArguments[0], element))
            {
                return new CollectionInfo(family, element, null, TypeNames.Display(first.OriginalDefinition));
            }
        }

        return new CollectionInfo(family, element, null, null);
    }

    private static CollectionInfo Pairs(CollectionFamily family, List<INamedTypeSymbol> interfaces)
    {
        INamedTypeSymbol first = interfaces[0];
        ITypeSymbol key = first.TypeArguments[0];
        ITypeSymbol value = first.TypeArguments[1];
        foreach (INamedTypeSymbol other in interfaces)
        {
            bool same = SymbolEqualityComparer.Default.Equals(other.TypeArguments[0], key)
                && SymbolEqualityComparer.Default.Equals(other.TypeArguments[1], value);
            if (!same)
            {
                return new CollectionInfo(family, value, key, TypeNames.Display(first.OriginalDefinition));
            }
        }

        return new CollectionInfo(family, value, key, null);
    }
}
