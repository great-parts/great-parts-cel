using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The functions of the extension set lists, version 4, but for the sort, which is in ListSort. The results are new lists of an array
// that is filled once; a list that is the argument and the result may be returned itself. Errors are InvalidArgument at the call
// whose detail is formed when the error is read: a failure holds at most one number that came from the arguments, a reference to a
// list that exists, and the name of the function, so it takes 64 bytes and no text.
internal static class ListFunctions
{
    // The cases of an error of the set; the number of the error and its references depend on the case.
    internal enum ListDetail : byte
    {
        NegativeIndex,
        StartAboveEnd,
        EndAboveLength,
        NegativeLevel,
        NegativeSize,
        SizeAboveLimit,
        UnequalSizes,
        NotComparable,
        DifferentTypes,
    }

    private static readonly CelValue EmptyList = CelValue.FromList(new ValueArrayList([]));

    // invalid argument to 'function': detail. The function name is the first part; the number of the error is the one number
    // of the arguments; UnequalSizes names the two lists in the first and the second part, and EndAboveLength the list in the second.
    private static readonly Func<CelError, string> ErrorText = static error =>
        "invalid argument to '" + ((error.First as string) ?? "@sortByAssociatedKeys") + "': " + Detail(error);

    // slice(list, int, int): the elements from the first index up to the second, which is excluded.
    internal static CelValue Slice(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list) || !arguments[1].TryGetInt64(out long start) || !arguments[2].TryGetInt64(out long end))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (start < 0 || end < 0)
        {
            return site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, start < 0 ? start : end, (byte)ListDetail.NegativeIndex);
        }

        if (start > end)
        {
            return site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, start, (byte)ListDetail.StartAboveEnd);
        }

        if (end > list.Count)
        {
            return site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, list, end, (byte)ListDetail.EndAboveLength);
        }

        if (start == end)
        {
            return EmptyList;
        }

        var items = new CelValue[(int)(end - start)];
        for (int i = 0; i < items.Length; i++)
        {
            CelValue item = list.Read((int)start + i, site.Meter);
            if (item.Kind == CelValueKind.Error)
            {
                return site.Placed(item);
            }

            items[i] = item;
        }

        return CelValue.FromList(new ValueArrayList(items));
    }

    // The size of the result of slice, which the call node charges: end − start, and 0 for the arguments that are an error.
    internal static ulong SliceSize(ReadOnlySpan<CelValue> arguments)
    {
        if (arguments.Length != 3 || !arguments[0].TryGetList(out CelList? list) || !arguments[1].TryGetInt64(out long start) || !arguments[2].TryGetInt64(out long end))
        {
            return 0;
        }

        return start < 0 || end < start || end > list.Count ? 0 : (ulong)(end - start);
    }

    // flatten(list): one level; flatten(list, int): up to the depth. An element that is not a list stays as it is, and a list that
    // the walk descends into is charged by its size before its elements are viewed. The receiver is charged with the call.
    internal static CelValue Flatten(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        long depth = 1;
        if (arguments.Length == 2)
        {
            if (!arguments[1].TryGetInt64(out depth))
            {
                return site.NoMatchingOverload(arguments);
            }

            if (depth < 0)
            {
                return site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, 0, (byte)ListDetail.NegativeLevel);
            }

            if (depth == 0)
            {
                return arguments[0];
            }
        }

        // The lists being walked and the next index in each, by level; a list nested in application data can be deeper than any text.
        var lists = new CelList[4];
        var positions = new int[4];
        int height = 1;
        lists[0] = list;
        var items = new List<CelValue>(list.Count);
        while (height > 0)
        {
            CelList current = lists[height - 1];
            int position = positions[height - 1];
            if (position == current.Count)
            {
                height--;
                continue;
            }

            positions[height - 1] = position + 1;
            CelValue item = current.Read(position, site.Meter);
            if (item.Kind == CelValueKind.Error)
            {
                return site.Placed(item);
            }

            if (height <= depth && item.TryGetList(out CelList? nested))
            {
                site.Charge((ulong)nested.Count);
                if (height == lists.Length)
                {
                    Array.Resize(ref lists, height * 2);
                    Array.Resize(ref positions, height * 2);
                }

                lists[height] = nested;
                positions[height] = 0;
                height++;
            }
            else
            {
                items.Add(item);
            }
        }

        return CelValue.FromList(new ValueArrayList(items.ToArray()));
    }

    // lists.range(int): [0, 1, …, n − 1] for n up to the limit; a request beyond it is refused before the list is made.
    internal static CelValue Range(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetInt64(out long size))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (size < 0)
        {
            return site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, size, (byte)ListDetail.NegativeSize);
        }

        if (size > ExtensionLimits.MaxRangeSize)
        {
            return site.Error(CelErrorCode.InvalidArgument, ErrorText, site.Function, null, size, (byte)ListDetail.SizeAboveLimit);
        }

        if (size == 0)
        {
            return EmptyList;
        }

        var items = new CelValue[size];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = CelValue.FromInt64(i);
        }

        return CelValue.FromList(new ValueArrayList(items));
    }

    // The argument cost of lists.range: n for an argument from 0 up to the limit, and nothing for another, whose error comes with the
    // unit of the call alone.
    internal static ulong RangeSize(ReadOnlySpan<CelValue> arguments) =>
        arguments.Length == 1 && arguments[0].TryGetInt64(out long size) && size >= 0 && size <= ExtensionLimits.MaxRangeSize ? (ulong)size : 0;

    // reverse(list): the elements in reverse order.
    internal static CelValue Reverse(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (list.Count == 0)
        {
            return arguments[0];
        }

        var items = new CelValue[list.Count];
        for (int i = 0; i < items.Length; i++)
        {
            CelValue item = list.Read(i, site.Meter);
            if (item.Kind == CelValueKind.Error)
            {
                return site.Placed(item);
            }

            items[items.Length - 1 - i] = item;
        }

        return CelValue.FromList(new ValueArrayList(items));
    }

    // distinct(list): the first of each group of equal elements, in order. Each element is compared with the ones kept so far, in the
    // order they were kept, up to the first equal one, and CEL equality charges each pair before it compares it: so 1, 1u and 1.0 are
    // one element and a NaN is equal to nothing, each NaN staying.
    internal static CelValue Distinct(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (list.Count == 0)
        {
            return arguments[0];
        }

        var kept = new CelValue[list.Count];
        int keptCount = 0;
        for (int i = 0; i < kept.Length; i++)
        {
            CelValue item = list.Read(i, site.Meter);
            if (item.Kind == CelValueKind.Error)
            {
                return site.Placed(item);
            }

            bool found = false;
            for (int j = 0; j < keptCount && !found; j++)
            {
                switch (EqualityRules.AreEqual(item, kept[j], site.Meter, out CelValue error, site.Types, site.Boundary))
                {
                    case ValueEquality.Equal:
                        found = true;
                        break;
                    case ValueEquality.Error:
                        return site.Placed(error);
                    case ValueEquality.NotEqual:
                        break;
                    default:
                        return site.Error(CelErrorCode.InsufficientStack, EqualityRules.InsufficientStackMessage);
                }
            }

            if (!found)
            {
                kept[keptCount++] = item;
            }
        }

        if (keptCount == kept.Length)
        {
            return arguments[0];
        }

        return CelValue.FromList(new ValueArrayList(kept.AsSpan(0, keptCount).ToArray()));
    }

    // Reads every element into the destination in order; an element that is an error value is the error of the call.
    internal static bool TryRead(CelList list, CelValue[] destination, in CallSite site, out CelValue error)
    {
        error = default;
        for (int i = 0; i < list.Count; i++)
        {
            CelValue item = list.Read(i, site.Meter);
            if (item.Kind == CelValueKind.Error)
            {
                error = site.Placed(item);
                return false;
            }

            destination[i] = item;
        }

        return true;
    }

    // @sortByAssociatedKeys, the receiver and the keys of different sizes: the sizes are read from the two lists.
    internal static CelValue SizesError(CelList receiver, CelList keys, in CallSite site) =>
        site.Error(CelErrorCode.InvalidArgument, ErrorText, receiver, keys, 0, (byte)ListDetail.UnequalSizes);

    // The first key is not of a comparable kind, or a key is not of the kind of the first.
    internal static CelValue KeyError(ListDetail detail, in CallSite site) =>
        site.Error(CelErrorCode.InvalidArgument, ErrorText, "@sortByAssociatedKeys", null, 0, (byte)detail);

    // The detail of an error by its case. The numbers of the text are the number of the error and, for the two cases that name the
    // size of a list, the size read from the list that the error holds.
    private static string Detail(CelError error) => (ListDetail)error.Small switch
    {
        ListDetail.NegativeIndex => "cannot slice with a negative index " + error.Number.ToString(CultureInfo.InvariantCulture),
        ListDetail.StartAboveEnd => "cannot slice: start index " + error.Number.ToString(CultureInfo.InvariantCulture)
            + " must be less than or equal to the end index",
        ListDetail.EndAboveLength => "cannot slice: end index " + error.Number.ToString(CultureInfo.InvariantCulture)
            + " is above the list length " + ((CelList)error.Second!).Count.ToString(CultureInfo.InvariantCulture),
        ListDetail.NegativeLevel => "level must be non-negative",
        ListDetail.NegativeSize => "size must be non-negative, got " + error.Number.ToString(CultureInfo.InvariantCulture),
        ListDetail.SizeAboveLimit => "size " + error.Number.ToString(CultureInfo.InvariantCulture) + " exceeds maximum allowed ("
            + ExtensionLimits.MaxRangeSize.ToString(CultureInfo.InvariantCulture) + ")",
        ListDetail.UnequalSizes => "@sortByAssociatedKeys() expected a list of the same size as the associated keys list, but got "
            + ((CelList)error.First!).Count.ToString(CultureInfo.InvariantCulture) + " and "
            + ((CelList)error.Second!).Count.ToString(CultureInfo.InvariantCulture) + " elements respectively",
        ListDetail.NotComparable => "list elements must be comparable",
        _ => "list elements must have the same type",
    };
}
