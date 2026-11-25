using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// distinct and the functions of the extension set sets over the hash of the values in place of every pair (ValueIndex): the result,
// the order in which the code of the application is called and the error of the equality are those of the pairwise passes, and the
// cost is that of the page of cost (docs/cost.md, "Hashing in distinct and sets"), whatever the length of the lists. distinct keeps the
// elements for which no equal one was kept before, in order. The index rents its arrays from a pool and a call gives them back when
// it ends, with a result or with an exception of the application. The parameter constantHash is for the tests: it gives every hashed
// value one hash, so that the collisions that a random seed hardly gives are all there; the parameter mode is for the tests and the
// benchmark: it makes the index scan the hashes or use buckets for a list of any length. Nothing a call returns or charges changes
// with either.
internal static class HashedLists
{
    // distinct for a list; the argument is returned as it is when every element stays.
    internal static CelValue Distinct(
        CelValue argument,
        CelList list,
        in CallSite site,
        bool constantHash = false,
        ValueIndexMode mode = ValueIndexMode.Automatic)
    {
        var index = new ValueIndex(list.Count, site.Meter, site.Types, site.Boundary, constantHash, mode);
        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                CelValue item = list.Read(i, site.Meter);
                if (item.Kind == CelValueKind.Error)
                {
                    return site.Placed(item);
                }

                ValueEquality result = index.AddIfAbsent(item, out CelValue error);
                if (Failed(result, error, in site, out CelValue failure))
                {
                    return failure;
                }
            }

            return index.Count == list.Count ? argument : CelValue.FromList(new ValueArrayList(index.ToArray()));
        }
        finally
        {
            index.Release();
        }
    }

    // Whether every element of the needles is in the haystack; the first that is not decides, and the empty needles are contained.
    internal static CelValue AllIn(
        CelList needles,
        CelList haystack,
        in CallSite site,
        bool constantHash = false,
        ValueIndexMode mode = ValueIndexMode.Automatic) =>
        needles.Count == 0 ? CelValue.FromBoolean(true) : Probe(needles, haystack, false, in site, constantHash, mode);

    // Whether some element of the first list is in the second.
    internal static CelValue Intersects(
        CelList first,
        CelList second,
        in CallSite site,
        bool constantHash = false,
        ValueIndexMode mode = ValueIndexMode.Automatic) =>
        first.Count == 0 ? CelValue.FromBoolean(false) : Probe(first, second, true, in site, constantHash, mode);

    // The needles looked up in the index of the haystack, in order. With foundDecides the first needle that is found ends the search
    // with true and none found is false (intersects); without it the first needle that is not found ends it with false and all found
    // is true (all of them in). An error of a needle, of the haystack or of an equality ends it with that error.
    private static CelValue Probe(
        CelList needles,
        CelList haystack,
        bool foundDecides,
        in CallSite site,
        bool constantHash,
        ValueIndexMode mode)
    {
        var index = new ValueIndex(haystack.Count, site.Meter, site.Types, site.Boundary, constantHash, mode);
        try
        {
            if (!TryFill(index, haystack, in site, out CelValue failure))
            {
                return failure;
            }

            for (int i = 0; i < needles.Count; i++)
            {
                ValueEquality result = Lookup(needles, i, index, in site, out failure);
                if (result == ValueEquality.Error)
                {
                    return failure;
                }

                if ((result == ValueEquality.Equal) == foundDecides)
                {
                    return CelValue.FromBoolean(foundDecides);
                }
            }

            return CelValue.FromBoolean(!foundDecides);
        }
        finally
        {
            index.Release();
        }
    }

    // The unit of the element, its read and its search in the index; the error value of a failure is returned with the result.
    private static ValueEquality Lookup(CelList needles, int position, ValueIndex index, in CallSite site, out CelValue failure)
    {
        site.Charge(1);
        CelValue needle = needles.Read(position, site.Meter);
        if (needle.Kind == CelValueKind.Error)
        {
            failure = site.Placed(needle);
            return ValueEquality.Error;
        }

        ValueEquality result = index.Contains(needle, out CelValue error);
        return Failed(result, error, in site, out failure) ? ValueEquality.Error : result;
    }

    // Puts the elements of a list in the index; false with the error value when an element is an error.
    private static bool TryFill(ValueIndex index, CelList list, in CallSite site, out CelValue failure)
    {
        for (int i = 0; i < list.Count; i++)
        {
            CelValue item = list.Read(i, site.Meter);
            if (item.Kind == CelValueKind.Error)
            {
                failure = site.Placed(item);
                return false;
            }

            index.Append(item);
        }

        index.Seal();
        failure = default;
        return true;
    }

    // Whether the result of a search is the error that ends the call, and that error as a value.
    private static bool Failed(ValueEquality result, CelValue error, in CallSite site, out CelValue failure)
    {
        switch (result)
        {
            case ValueEquality.Error:
                failure = site.Placed(error);
                return true;
            case ValueEquality.InsufficientStack:
                failure = site.Error(CelErrorCode.InsufficientStack, EqualityRules.InsufficientStackMessage);
                return true;
            default:
                failure = default;
                return false;
        }
    }
}
