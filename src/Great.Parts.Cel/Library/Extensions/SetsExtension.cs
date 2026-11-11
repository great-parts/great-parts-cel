using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set sets, version 0: sets.contains, sets.equivalent and sets.intersects over two lists of the same element type. The
// elements compare by CEL equality, in the order of cel-go, so the cost, which is charged pair by pair, depends only on the data.
// Each looked-up element is an `in` over the other list: 1 and the equality of each pair up to the first equal element.
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

        for (int i = 0; i < first.Count; i++)
        {
            site.Charge(1);
            CelValue element = first.Read(i, site.Meter);
            if (element.Kind == CelValueKind.Error)
            {
                return site.Placed(element);
            }

            switch (Lookup(element, second, in site, out CelValue failure))
            {
                case LookupOutcome.Found:
                    return CelValue.FromBoolean(true);
                case LookupOutcome.Failed:
                    return failure;
                default:
                    break;
            }
        }

        return CelValue.FromBoolean(false);
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

    // Whether every element of the needles is in the haystack, each looked up in order; the first that is not decides, and the empty
    // needles are contained.
    private static CelValue AllIn(CelList needles, CelList haystack, in CallSite site)
    {
        for (int i = 0; i < needles.Count; i++)
        {
            site.Charge(1);
            CelValue needle = needles.Read(i, site.Meter);
            if (needle.Kind == CelValueKind.Error)
            {
                return site.Placed(needle);
            }

            switch (Lookup(needle, haystack, in site, out CelValue failure))
            {
                case LookupOutcome.Failed:
                    return failure;
                case LookupOutcome.Missing:
                    return CelValue.FromBoolean(false);
                default:
                    break;
            }
        }

        return CelValue.FromBoolean(true);
    }

    // The search of an element in a list by CEL equality as `in` makes it: the pairs in order up to the first equal one, each charged
    // before it is compared.
    private static LookupOutcome Lookup(CelValue needle, CelList haystack, in CallSite site, out CelValue failure)
    {
        failure = default;
        for (int i = 0; i < haystack.Count; i++)
        {
            switch (EqualityRules.AreEqual(needle, haystack.Read(i, site.Meter), site.Meter, out CelValue error, site.Types, site.Boundary))
            {
                case ValueEquality.Equal:
                    return LookupOutcome.Found;
                case ValueEquality.Error:
                    failure = site.Placed(error);
                    return LookupOutcome.Failed;
                case ValueEquality.NotEqual:
                    break;
                default:
                    failure = site.Error(CelErrorCode.InsufficientStack, EqualityRules.InsufficientStackMessage);
                    return LookupOutcome.Failed;
            }
        }

        return LookupOutcome.Missing;
    }

    private enum LookupOutcome
    {
        Found,
        Missing,
        Failed,
    }
}
