using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// distinct and the functions of the extension set sets over the hash of the values in place of every pair (ValueIndex): the result,
// the order in which the code of the application is called and the error of the equality are those of the pairwise passes, and the
// cost is that of the page of cost (docs/cost.md, "Hashing in distinct and sets"), whatever the length of the lists. distinct keeps the
// elements for which no equal one was kept before, in order. The parameter constantHash is for the tests: it gives every hashed value
// one hash, so that the collisions that a random seed hardly gives are all there, and nothing a call returns or charges changes with it.
internal static class HashedLists
{
    // distinct for a list; the argument is returned as it is when every element stays.
    internal static CelValue Distinct(CelValue argument, CelList list, in CallSite site, bool constantHash = false)
    {
        var index = new ValueIndex(list.Count, site.Meter, site.Types, site.Boundary, constantHash);
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

    // Whether every element of the needles is in the haystack; the first that is not decides, and the empty needles are contained.
    internal static CelValue AllIn(CelList needles, CelList haystack, in CallSite site, bool constantHash = false)
    {
        if (needles.Count == 0)
        {
            return CelValue.FromBoolean(true);
        }

        if (!TryIndex(haystack, in site, constantHash, out ValueIndex? index, out CelValue failure))
        {
            return failure;
        }

        for (int i = 0; i < needles.Count; i++)
        {
            ValueEquality result = Lookup(needles, i, index!, in site, out failure);
            if (result == ValueEquality.NotEqual)
            {
                return CelValue.FromBoolean(false);
            }

            if (result != ValueEquality.Equal)
            {
                return failure;
            }
        }

        return CelValue.FromBoolean(true);
    }

    // Whether some element of the first list is in the second.
    internal static CelValue Intersects(CelList first, CelList second, in CallSite site, bool constantHash = false)
    {
        if (first.Count == 0)
        {
            return CelValue.FromBoolean(false);
        }

        if (!TryIndex(second, in site, constantHash, out ValueIndex? index, out CelValue failure))
        {
            return failure;
        }

        for (int i = 0; i < first.Count; i++)
        {
            ValueEquality result = Lookup(first, i, index!, in site, out failure);
            if (result == ValueEquality.Equal)
            {
                return CelValue.FromBoolean(true);
            }

            if (result != ValueEquality.NotEqual)
            {
                return failure;
            }
        }

        return CelValue.FromBoolean(false);
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

    // The index of the elements of a list; false with the error value when an element is an error or the equality of two failed.
    private static bool TryIndex(CelList list, in CallSite site, bool constantHash, out ValueIndex? index, out CelValue failure)
    {
        index = new ValueIndex(list.Count, site.Meter, site.Types, site.Boundary, constantHash);
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
