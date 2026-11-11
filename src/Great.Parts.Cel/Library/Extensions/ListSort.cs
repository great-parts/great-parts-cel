using System;
using System.Buffers;
using System.Numerics;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The order of two elements of an array of indices, by position.
internal interface IIndexOrder
{
    // Whether the element at the first index is strictly less than the one at the second.
    bool Less(int left, int right);
}

// The sort of an array of indices that the sort of lists and the order of the keys of a JSON map share. It is the code's own, not
// Array.Sort or List.Sort, whose comparers may throw on an order that is not consistent, as the order of a list with a NaN is. Both
// forms are stable, and both leave the same order for the same data: up to 12 elements an insertion sort that moves an element left
// while it is less than the previous one, above 12 a top-down merge sort with a second array, which takes the right element only
// when it is less than the left.
internal static class IndexSort
{
    // The longest array that the insertion sort orders.
    internal const int InsertionLimit = 12;

    // Orders the indices in place; the scratch array holds at least half of them.
    internal static void Sort<TOrder>(Span<int> order, Span<int> scratch, in TOrder less)
        where TOrder : struct, IIndexOrder
    {
        if (order.Length <= InsertionLimit)
        {
            Insert(order, in less);
        }
        else
        {
            Merge(order, scratch, in less);
        }
    }

    private static void Insert<TOrder>(Span<int> order, in TOrder less)
        where TOrder : struct, IIndexOrder
    {
        for (int i = 1; i < order.Length; i++)
        {
            int moved = order[i];
            int j = i;
            while (j > 0 && less.Less(moved, order[j - 1]))
            {
                order[j] = order[j - 1];
                j--;
            }

            order[j] = moved;
        }
    }

    private static void Merge<TOrder>(Span<int> order, Span<int> scratch, in TOrder less)
        where TOrder : struct, IIndexOrder
    {
        int length = order.Length;
        if (length <= 1)
        {
            return;
        }

        int middle = length / 2;
        Merge(order[..middle], scratch, in less);
        Merge(order[middle..], scratch, in less);

        // The left half moves to the scratch array, and the merge fills the array from the start: the place written is never
        // past the next element of the right half.
        order[..middle].CopyTo(scratch);
        int left = 0;
        int right = middle;
        int place = 0;
        while (left < middle && right < length)
        {
            order[place++] = less.Less(order[right], scratch[left]) ? order[right++] : scratch[left++];
        }

        while (left < middle)
        {
            order[place++] = scratch[left++];
        }
    }
}

// The functions list_<T>_sort and list_<U>_sortByAssociatedKeys of the extension set lists. The first is the second with the list as its
// own keys. The keys are ordered by CEL ordering of their kind, stably; the elements of the receiver follow their keys.
internal static class ListSort
{
    // The units of a sort of numbers, bools and time, whose argument is the receiver: n + n · ⌈log₂(n + 1)⌉, the 1 of the call apart.
    internal static readonly CelCostFunction SortUnits = static arguments => Units(arguments, 0, text: false);

    // The units of a sort of strings and bytes: c is 1 + ⌈2 · Lmax/10⌉ for the longest element.
    internal static readonly CelCostFunction SortTextUnits = static arguments => Units(arguments, 0, text: true);

    // The same for the list of keys, the second argument.
    internal static readonly CelCostFunction SortByKeysUnits = static arguments => Units(arguments, 1, text: false);

    internal static readonly CelCostFunction SortByTextKeysUnits = static arguments => Units(arguments, 1, text: true);

    // list_<T>_sort: the receiver ordered by its own elements.
    internal static CelValue Sort(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetList(out CelList? list) ? SortBy(arguments[0], list, list, in site) : site.NoMatchingOverload(arguments);

    // list_<U>_sortByAssociatedKeys: the receiver ordered by the list of keys of the same size.
    internal static CelValue SortByKeys(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetList(out CelList? values) && arguments[1].TryGetList(out CelList? keys)
            ? SortBy(arguments[0], values, keys, in site)
            : site.NoMatchingOverload(arguments);

    // The checks in order: sizes, the empty receiver, the kinds of the keys. The cost of the sort has been charged by the call node.
    private static CelValue SortBy(CelValue receiver, CelList values, CelList keys, in CallSite site)
    {
        int count = keys.Count;
        if (values.Count != count)
        {
            return ListFunctions.SizesError(values, keys, in site);
        }

        if (count == 0)
        {
            return receiver;
        }

        CelValue[]? keyItems = null;
        CelValue[]? valueItems = null;
        int[]? order = null;
        int[]? scratch = null;
        try
        {
            keyItems = ArrayPool<CelValue>.Shared.Rent(count);
            CelValue refused = ReadKeys(keys, keyItems, in site);
            if (refused.Kind == CelValueKind.Error)
            {
                return refused;
            }

            if (ReferenceEquals(values, keys))
            {
                valueItems = keyItems;
            }
            else
            {
                valueItems = ArrayPool<CelValue>.Shared.Rent(count);
                if (!ListFunctions.TryRead(values, valueItems, in site, out CelValue error))
                {
                    return error;
                }
            }

            order = ArrayPool<int>.Shared.Rent(count);
            scratch = ArrayPool<int>.Shared.Rent(count);
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            IndexSort.Sort(order.AsSpan(0, count), scratch.AsSpan(0, count), new KeyOrder(keyItems));
            var sorted = new CelValue[count];
            for (int i = 0; i < count; i++)
            {
                sorted[i] = valueItems[order[i]];
            }

            return CelValue.FromList(new ValueArrayList(sorted));
        }
        finally
        {
            if (!ReferenceEquals(valueItems, keyItems) && valueItems is not null)
            {
                ArrayPool<CelValue>.Shared.Return(valueItems, clearArray: true);
            }

            if (keyItems is not null)
            {
                ArrayPool<CelValue>.Shared.Return(keyItems, clearArray: true);
            }

            if (order is not null)
            {
                ArrayPool<int>.Shared.Return(order);
            }

            if (scratch is not null)
            {
                ArrayPool<int>.Shared.Return(scratch);
            }
        }
    }

    // Reads the keys and checks them: the first is of one of the eight comparable kinds, and the others are of its kind, so that
    // int and uint, or int and double, are never mixed. Gives the error value, or a value that is no error.
    private static CelValue ReadKeys(CelList keys, CelValue[] destination, in CallSite site)
    {
        CelValueKind kind = default;
        for (int i = 0; i < keys.Count; i++)
        {
            CelValue key = keys.Read(i, site.Meter);
            if (key.Kind == CelValueKind.Error)
            {
                return site.Placed(key);
            }

            if (i == 0)
            {
                if (!IsComparable(key.Kind))
                {
                    return ListFunctions.KeyError(ListFunctions.ListDetail.NotComparable, in site);
                }

                kind = key.Kind;
            }
            else if (key.Kind != kind)
            {
                return ListFunctions.KeyError(ListFunctions.ListDetail.DifferentTypes, in site);
            }

            destination[i] = key;
        }

        return default;
    }

    private static bool IsComparable(CelValueKind kind) =>
        kind is CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double or CelValueKind.Bool or CelValueKind.Duration
            or CelValueKind.Timestamp or CelValueKind.String or CelValueKind.Bytes;

    // n + c · n · ⌈log₂(n + 1)⌉ of the list of keys at the argument; the ⌈log₂(n + 1)⌉ is the bit length of n. The longest string or
    // bytes element is found in one pass; an element of another kind adds nothing, since the sort refuses it.
    private static ulong Units(ReadOnlySpan<CelValue> arguments, int keyIndex, bool text)
    {
        if (arguments.Length <= keyIndex || !arguments[keyIndex].TryGetList(out CelList? keys))
        {
            return 0;
        }

        ulong count = (ulong)keys.Count;
        ulong factor = 1;
        if (text)
        {
            ulong longest = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                CelValue key = keys.Read(i, null);
                if (key.TryGetString(out string? characters))
                {
                    longest = Math.Max(longest, (ulong)characters.Length);
                }
                else if (key.TryGetBytes(out ReadOnlyMemory<byte> bytes))
                {
                    longest = Math.Max(longest, (ulong)bytes.Length);
                }
            }

            factor = 1 + Costs.Tenths(2 * longest);
        }

        ulong bits = (ulong)(64 - BitOperations.LeadingZeroCount(count));
        return Saturated.Add(count, Saturated.Multiply(Saturated.Multiply(factor, count), bits));
    }

    // CEL order of the keys by position. A NaN is unordered with everything, so it is less than nothing and nothing is less than it.
    private readonly struct KeyOrder(CelValue[] keys) : IIndexOrder
    {
        public bool Less(int left, int right) => OrderingRules.Compare(keys[left], keys[right]) == ValueOrdering.Less;
    }
}

// Arithmetic of units that stops at the largest value instead of wrapping.
internal static class Saturated
{
    internal static ulong Add(ulong left, ulong right)
    {
        ulong sum = left + right;
        return sum < left ? ulong.MaxValue : sum;
    }

    internal static ulong Multiply(ulong left, ulong right)
    {
        ulong high = Math.BigMul(left, right, out ulong low);
        return high == 0 ? low : ulong.MaxValue;
    }
}
