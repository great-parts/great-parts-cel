using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set sets, version 0: sets.contains, sets.equivalent and sets.intersects over two lists of the same element type. The
// elements compare by CEL equality and are looked up by their hash (HashedLists); the cost, which depends only on the data, is that of
// docs/cost.md, "Hashing in distinct and sets": the hashing of the list that is searched, and for each element looked up 1, its hashing and the one comparison
// that found it, or those of the elements that hold an object of the application, in the order of cel-go.
internal static class SetsExtension
{
    internal const string SetName = "sets";
    internal const uint Version = 0;
    private const string Source = "extension sets v0";

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, Overloads()) : null;

    // sets.contains(a, b), (list(T), list(T)) -> bool: whether every element of b is in a.
    internal static CelValue Contains(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? first) || !arguments[1].TryGetList(out CelList? second))
        {
            return site.NoMatchingOverload(arguments);
        }

        return AllIn(second, first, in site);
    }

    // sets.intersects(a, b), (list(T), list(T)) -> bool: whether some element of a is in b.
    internal static CelValue Intersects(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? first) || !arguments[1].TryGetList(out CelList? second))
        {
            return site.NoMatchingOverload(arguments);
        }

        return HashedLists.Intersects(first, second, in site);
    }

    // sets.equivalent(a, b), (list(T), list(T)) -> bool: contains(a, b) and then, only if that is true, contains(b, a).
    internal static CelValue Equivalent(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? first) || !arguments[1].TryGetList(out CelList? second))
        {
            return site.NoMatchingOverload(arguments);
        }

        CelValue forward = AllIn(second, first, in site);
        return forward.TryGetBoolean(out bool holds) && holds ? AllIn(first, second, in site) : forward;
    }

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType element = CelType.TypeParameter("T");
        ImmutableArray<CelType> parameters = [CelType.ListOf(element), CelType.ListOf(element)];
        return
        [
            Function("sets.contains", "list_sets_contains_list", parameters, Contains),
            Function("sets.equivalent", "list_sets_equivalent_list", parameters, Equivalent),
            Function("sets.intersects", "list_sets_intersects_list", parameters, Intersects),
        ];
    }

    // A global overload of 1 unit: the pairs that the function compares are charged by the equality as it goes.
    private static OverloadBinding Function(string function, string overloadId, ImmutableArray<CelType> parameters, OverloadImplementation implementation) =>
        new(function, new CelOverloadDecl(overloadId, false, parameters, CelType.Bool), implementation, 1, null, Source);

    // Whether every element of the needles is in the haystack; the first that is not decides, and the empty needles are contained.
    private static CelValue AllIn(CelList needles, CelList haystack, in CallSite site) => HashedLists.AllIn(needles, haystack, in site);
}
