using System;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// The functions of lists and maps: index, in, size and concatenation of lists. Each reads the kinds of its
// arguments and rejects another kind, whatever the static type. Messages with a key or an index are formed
// when the error is read.
internal static class Collections
{
    // The list is the first part, whose size is read with the message; the index is kept by ErrorValueParts.
    private static readonly Func<CelError, string> OutOfRangeText = static error =>
        "index " + IndexText(ErrorValueParts.ValueOf(error)) + " out of range [0, " + ((CelList)error.First!).Count.ToString(CultureInfo.InvariantCulture) + ")";

    // The function name is the first part, the bits of the index the number.
    private static readonly Func<CelError, string> NotAnIntegerText = static error =>
        "invalid argument to '" + (string)error.First! + "': index " + NumberText.FormatDouble(BitConverter.Int64BitsToDouble(error.Number))
        + " is not an integer";

    // The key is kept by ErrorValueParts: its content for the kinds of keys and double, otherwise its kind.
    private static readonly Func<CelError, string> NoSuchKeyText = static error => "no such key: " + KeyText(ErrorValueParts.KindOf(error), ErrorValueParts.ValueOf(error));

    // index_list, (list(A), int) -> A; with dyn the index may also be a uint, or a double with an integral
    // value, which stands for that integer.
    internal static CelValue IndexList(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        CelValue index = arguments[1];
        int count = list.Count;
        if (index.TryGetInt64(out long signed))
        {
            return signed >= 0 && signed < count ? list[(int)signed] : OutOfRange(in site, index, list);
        }

        if (index.TryGetUInt64(out ulong unsigned))
        {
            return unsigned < (ulong)count ? list[(int)unsigned] : OutOfRange(in site, index, list);
        }

        if (index.TryGetDouble(out double number))
        {
            // NaN and the infinities are not integral either; -0.0 is the index 0.
            if (!double.IsFinite(number) || Math.Floor(number) != number)
            {
                return site.Error(CelErrorCode.InvalidArgument, NotAnIntegerText, site.Function, number: BitConverter.DoubleToInt64Bits(number));
            }

            return number >= 0 && number < count ? list[(int)number] : OutOfRange(in site, index, list);
        }

        return site.NoMatchingOverload(arguments);
    }

    // index_map, (map(A, B), A) -> B: the value of the key by the key lookup, across the numeric kinds.
    internal static CelValue IndexMap(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetMap(out CelMap? map))
        {
            return site.NoMatchingOverload(arguments);
        }

        return map.TryGetValue(arguments[1], out CelValue value) ? value : site.ErrorWithValue(CelErrorCode.NoSuchKey, NoSuchKeyText, null, arguments[1]);
    }

    // in_list, (A, list(A)) -> bool: whether an element equals the value, comparing in order up to the first
    // equal one and charging each pair before comparing it.
    internal static CelValue InList(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[1].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        CelValue value = arguments[0];
        for (int i = 0; i < list.Count; i++)
        {
            switch (EqualityRules.AreEqual(value, list[i], site.Meter, out CelValue error))
            {
                case ValueEquality.Equal:
                    return CelValue.FromBoolean(true);
                case ValueEquality.Error:
                    return error;
                case ValueEquality.NotEqual:
                    break;
                default:
                    return site.Error(CelErrorCode.InsufficientStack, EqualityRules.InsufficientStackMessage);
            }
        }

        return CelValue.FromBoolean(false);
    }

    // in_map, (A, map(A, B)) -> bool: whether the map has the key, by the key lookup.
    internal static CelValue InMap(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[1].TryGetMap(out CelMap? map)
            ? CelValue.FromBoolean(map.TryGetValue(arguments[0], out _))
            : site.NoMatchingOverload(arguments);

    // size_list and list_size, (list(A)) -> int.
    internal static CelValue ListSize(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetList(out CelList? list) ? CelValue.FromInt64(list.Count) : site.NoMatchingOverload(arguments);

    // size_map and map_size, (map(A, B)) -> int.
    internal static CelValue MapSize(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetMap(out CelMap? map) ? CelValue.FromInt64(map.Count) : site.NoMatchingOverload(arguments);

    // add_list, (list(A), list(A)) -> list(A): a new list of both, the elements in order.
    internal static CelValue AddList(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? left) || !arguments[1].TryGetList(out CelList? right))
        {
            return site.NoMatchingOverload(arguments);
        }

        // A list does not change, so an empty operand leaves the other one as the result.
        if (left.Count == 0)
        {
            return arguments[1];
        }

        if (right.Count == 0)
        {
            return arguments[0];
        }

        var items = new CelValue[left.Count + right.Count];
        for (int i = 0; i < left.Count; i++)
        {
            items[i] = left[i];
        }

        for (int i = 0; i < right.Count; i++)
        {
            items[left.Count + i] = right[i];
        }

        return CelValue.FromList(new ValueArrayList(items));
    }

    private static CelValue OutOfRange(in CallSite site, CelValue index, CelList list) =>
        site.ErrorWithValue(CelErrorCode.IndexOutOfRange, OutOfRangeText, list, index);

    // An index as string() writes it: an int or a uint in decimal, a double in its shortest form.
    private static string IndexText(CelValue index)
    {
        if (index.TryGetInt64(out long signed))
        {
            return signed.ToString(CultureInfo.InvariantCulture);
        }

        if (index.TryGetUInt64(out ulong unsigned))
        {
            return unsigned.ToString(CultureInfo.InvariantCulture);
        }

        _ = index.TryGetDouble(out double number);
        return NumberText.FormatDouble(number);
    }

    // A key of the kinds of map keys as a CEL literal, as duplicate keys of a literal are written; a double as
    // string() writes it; a value of another kind, which can be no key, as the name of its type.
    private static string KeyText(CelValueKind kind, CelValue key)
    {
        if (key.TryGetDouble(out double number))
        {
            return NumberText.FormatDouble(number);
        }

        if (!KeyHashRules.IsKeyKind(kind))
        {
            return ValueTypeNames.Of(kind);
        }

        var text = new StringBuilder();
        Printer.AppendConstant(text, key);
        return text.ToString();
    }
}
