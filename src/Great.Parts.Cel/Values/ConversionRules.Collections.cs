using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The collections of the application: the rows of lists, sets, collections without an index and dictionaries of the
// input table, the conversion of elements and keys, the lookup of a CEL key back to .NET, and the order of enumeration.
internal static partial class ConversionRules
{
    private const string ListCallee = "a list of the application";
    private const string MapCallee = "a map of the application";

    private static readonly System.Reflection.Assembly LibraryAssembly = typeof(ConversionRules).Assembly;

    private static readonly Func<object, CelValue> ObjectConverter = static item => FromObject(item);

    private static readonly Func<CelError, string> ApplicationExceptionText =
        static error => "application code threw " + error.First!.GetType() + " in " + (string)error.Second!;

    private static readonly Comparison<CelValue> ByOrderOfCel = static (left, right) => OrderingRules.Compare(left, right) switch
    {
        ValueOrdering.Less => -1,
        ValueOrdering.Greater => 1,
        _ => 0,
    };

    // The keys by the order of CEL; a key that is an error value comes last.
    private static readonly IComparer<CelValue> KeyOrder = Comparer<CelValue>.Create(static (left, right) =>
    {
        bool leftFailed = left.Kind == CelValueKind.Error;
        bool rightFailed = right.Kind == CelValueKind.Error;
        return leftFailed || rightFailed ? leftFailed.CompareTo(rightFailed) : ByOrderOfCel(left, right);
    });

    // The error value of an exception that application code threw where a list or a map of the application, or a JSON
    // element, reads its data: it has no place, and the node that reads the value gives it one.
    internal static CelValue ApplicationError(Exception exception, string callee) =>
        CelValue.FromError(new CelError(CelErrorCode.ApplicationException, ApplicationExceptionText, null, 0, exception, callee));

    // Every exception of application code but OutOfMemoryException becomes an error value.
    internal static bool IsApplicationException(Exception exception) => exception is not OutOfMemoryException;

    // The element at the index of a view over a list, converted; an exception of the list or of the converter is the element.
    internal static CelValue ReadElement<T>(IReadOnlyList<T> source, int index, Func<T, CelValue> converter)
    {
        try
        {
            return NameRefusal(converter(source[index]), ListCallee);
        }
        catch (Exception exception) when (IsApplicationException(exception))
        {
            return ApplicationError(exception, ListCallee);
        }
    }

    // A value of a view over a map, converted; an exception of the converter is the value.
    internal static CelValue ConvertValue<TValue>(Func<TValue, CelValue> converter, TValue value)
    {
        try
        {
            return NameRefusal(converter(value), MapCallee);
        }
        catch (Exception exception) when (IsApplicationException(exception))
        {
            return ApplicationError(exception, MapCallee);
        }
    }

    // A copy of a collection without an index: the elements converted now, in the order of enumeration. An element that
    // cannot be converted stays as an error value; an exception of the enumeration is the error value of the elements
    // that were not enumerated.
    internal static CelList CopyList<T>(IReadOnlyCollection<T> source, Func<T, CelValue> converter, CelType? recordedType = null)
    {
        var items = new CelValue[source.Count];
        int index = 0;
        try
        {
            foreach (T item in source)
            {
                if (index == items.Length)
                {
                    break;
                }

                items[index++] = ConvertElement(converter, item);
            }
        }
        catch (Exception exception) when (IsApplicationException(exception))
        {
            CelValue error = ApplicationError(exception, ListCallee);
            for (; index < items.Length; index++)
            {
                items[index] = error;
            }
        }

        CelValue[] copied = index < items.Length ? items[..index] : items;
        return recordedType is null ? new ValueArrayList(copied) : new CopiedList(copied, recordedType);
    }

    // A set or another collection of CEL values sorted by the order of CEL: refused for an element that is not of one
    // kind that CEL orders, a NaN, or two elements equal in CEL.
    internal static CelList SortList<T>(IReadOnlyCollection<T> source, Func<T, CelValue> converter)
    {
        var items = new CelValue[source.Count];
        int index = 0;
        foreach (T item in source)
        {
            if (index == items.Length)
            {
                break;
            }

            items[index++] = converter(item);
        }

        CelValue[] values = index < items.Length ? items[..index] : items;
        return TrySort(values)
            ? new ValueArrayList(values)
            : throw new ArgumentException(
                "The elements are not of one kind that CEL orders, or CEL does not order two of them: a null, an error value, elements of two kinds, a NaN, or two elements equal in CEL.",
                nameof(source));
    }

    // Sorts the values in place by the order of CEL; false, and the order undefined, when the values are not of one kind
    // that CEL orders, one is a NaN, or two are equal.
    internal static bool TrySort(CelValue[] values)
    {
        if (values.Length == 0)
        {
            return true;
        }

        CelValueKind kind = values[0].Kind;
        if (!IsOrderedKind(kind))
        {
            return false;
        }

        foreach (CelValue value in values)
        {
            if (value.Kind != kind || (value.TryGetDouble(out double number) && double.IsNaN(number)))
            {
                return false;
            }
        }

        Array.Sort(values, ByOrderOfCel);
        for (int index = 1; index < values.Length; index++)
        {
            if (OrderingRules.Compare(values[index - 1], values[index]) != ValueOrdering.Less)
            {
                return false;
            }
        }

        return true;
    }

    // The kinds that CEL orders among values of one kind: the elements of a set sorted by the order of CEL.
    private static bool IsOrderedKind(CelValueKind kind) =>
        kind is CelValueKind.Bool or CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double or CelValueKind.String or CelValueKind.Bytes
            or CelValueKind.Timestamp or CelValueKind.Duration
#if NET11_0_OR_GREATER
            or CelValueKind.Decimal
#endif
        ;

    internal static bool IsMapKeyType<TKey>() => KeyOf<TKey>.IsSupported;

    // Whether a dictionary enumerates in an order that does not depend on hash codes.
    internal static bool EnumeratesInOrder<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source)
    {
        for (Type? type = source.GetType(); type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() is Type definition
                && (definition == typeof(Dictionary<,>) || definition == typeof(OrderedDictionary<,>) || definition == typeof(SortedDictionary<,>)
                    || definition == typeof(SortedList<,>) || definition == typeof(ImmutableSortedDictionary<,>)))
            {
                return true;
            }
        }

        return false;
    }

    // The lookup of a CEL key in a dictionary: the key converted back to the key type, one lookup, the value converted. A
    // key of another kind or out of the range of the key type finds nothing; an exception is the value.
    internal static bool TryFind<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source, CelValue key, Func<TValue, CelValue> converter, out CelValue value)
    {
        value = default;
        if (!KeyOf<TKey>.TryFromCel(key, out TKey? native))
        {
            return false;
        }

        try
        {
            if (!source.TryGetValue(native, out TValue? found))
            {
                return false;
            }

            value = NameRefusal(converter(found), MapCallee);
            return true;
        }
        catch (Exception exception) when (IsApplicationException(exception))
        {
            value = ApplicationError(exception, MapCallee);
            return true;
        }
    }

    // The entries of a dictionary in the order of the source, or by ascending CEL keys sorted at each enumeration.
    internal static IEnumerator<KeyValuePair<CelValue, CelValue>> Enumerate<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source,
        Func<TValue, CelValue> converter,
        bool inSourceOrder)
    {
        if (inSourceOrder)
        {
            foreach (KeyValuePair<TKey, TValue> entry in source)
            {
                yield return new KeyValuePair<CelValue, CelValue>(KeyOf<TKey>.ToCel(entry.Key), ConvertValue(converter, entry.Value));
            }

            yield break;
        }

        var keys = new CelValue[source.Count];
        var values = new TValue[keys.Length];
        int count = 0;
        foreach (KeyValuePair<TKey, TValue> entry in source)
        {
            if (count == keys.Length)
            {
                break;
            }

            keys[count] = KeyOf<TKey>.ToCel(entry.Key);
            values[count++] = entry.Value;
        }

        Array.Sort(keys, values, 0, count, KeyOrder);
        for (int index = 0; index < count; index++)
        {
            yield return new KeyValuePair<CelValue, CelValue>(keys[index], ConvertValue(converter, values[index]));
        }
    }

    // A value of an element or of a result, as the value of its static type X by the input rows.
    internal static CelValue FromResult<T>(T value, string overloadId, IEvaluationMeter? meter)
    {
        if (ShapeOf<T>.IsCollection && InputRowOf<T>.Row is { } row)
        {
            return row.TryConvert(value, meter, out CelValue converted)
                ? converted
                : CelValue.FromError(new CelError(
                    CelErrorCode.InvalidArgument,
                    CelError.FirstPart,
                    null,
                    0,
                    "invalid argument to '" + overloadId + "': a result set whose elements CEL cannot sort"));
        }

        if (typeof(T) == typeof(JsonElement))
        {
            return JsonRules.FromElement(Unsafe.As<T, JsonElement>(ref value), overloadId);
        }

        if (typeof(T) == typeof(DateTime))
        {
            return FromDateTime(Unsafe.As<T, DateTime>(ref value), overloadId);
        }

        _ = TryFromClr(value, out CelValue result);
        return NameRefusal(result, overloadId);
    }

    // Whether a refusal of CelBindings.Add names an undefined JsonElement, which has no CEL value.
    internal static bool IsRefusedByBindings(CelValue value) =>
        value.TryGetError(out CelError? error) && error.Code == CelErrorCode.InvalidArgument;

    // The CEL type that the rows of collections and application lists give a .NET type; null outside them.
    private static CelType? CollectionInputType(Type type)
    {
        if ((type.IsArray || type.IsGenericType) && Inputs.All.TryGetValue(type, out InputRow? row))
        {
            return row.CelType;
        }

        return Classify(type) is Shape.List or Shape.Collection ? DynList : null;
    }

    private static CelValue ConvertElement<T>(Func<T, CelValue> converter, T item)
    {
        try
        {
            return NameRefusal(converter(item), ListCallee);
        }
        catch (Exception exception) when (IsApplicationException(exception))
        {
            return ApplicationError(exception, ListCallee);
        }
    }

    // The value of a .NET collection by the row of its static type, or a class of the application.
    private static bool TryFromCollection<T>(T value, out CelValue result)
    {
        switch (ShapeOf<T>.Kind)
        {
            case Shape.None:
                if (ShapeOf<T>.IsCollection && InputRowOf<T>.Row is { } row)
                {
                    return row.TryConvert(value, null, out result);
                }

                break;
            case Shape.Object:
                result = value is null ? CelValue.Null : CelValue.FromBareObject((object)value);
                return true;
            case Shape.List:
                if (value is null)
                {
                    result = CelValue.Null;
                    return true;
                }

                if ((object)value is IReadOnlyList<object> list)
                {
                    result = CelValue.FromList(new ApplicationList<object>(list, ObjectConverter, DynList, value));
                    return true;
                }

                break;
            case Shape.Collection:
                if (value is null)
                {
                    result = CelValue.Null;
                    return true;
                }

                if ((object)value is IReadOnlyCollection<object> collection)
                {
                    result = CelValue.FromList(CopyList(collection, ObjectConverter));
                    return true;
                }

                break;
        }

        result = default;
        return false;
    }

    private static bool TryFromJson<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(JsonElement))
        {
            result = JsonRules.FromElement(Unsafe.As<T, JsonElement>(ref value));
            return true;
        }

        result = default;
        return false;
    }

    private enum Shape
    {
        None,
        Object,
        List,
        Collection,
    }

    private static Shape Classify(Type type)
    {
        if (IsApplicationObject(type) || (Nullable.GetUnderlyingType(type) is { } underlying && IsApplicationObject(underlying)))
        {
            return Shape.Object;
        }

        if (type.IsArray)
        {
            return type.GetElementType() is { IsValueType: false } element && IsApplicationObject(element) ? Shape.List : Shape.None;
        }

        if (!type.IsGenericType || type.GetGenericArguments() is not [{ IsValueType: false } argument] || !IsApplicationObject(argument))
        {
            return Shape.None;
        }

        Type definition = type.GetGenericTypeDefinition();
        if (definition == typeof(IReadOnlyList<>) || definition == typeof(IList<>) || definition == typeof(List<>))
        {
            return Shape.List;
        }

        return definition == typeof(IReadOnlyCollection<>) ? Shape.Collection : Shape.None;
    }

    // The class of a result of IReadOnlyList<Class>, IList<Class>, List<Class>, Class[] or IReadOnlyCollection<Class>, Class a class of
    // the application: the input rows give such a result the type list(Class) with the object type that a type source describes
    // for exactly that .NET type, resolved at Build. Null for any other type.
    internal static Type? ApplicationElementOf(Type type) =>
        Classify(type) is Shape.List or Shape.Collection
            ? (type.IsArray ? type.GetElementType() : type.GetGenericArguments()[0])
            : null;

    // A class, record, structure or interface outside System and outside this library: a bare instance of an object type,
    // whose CEL type is the object type that a type source describes for exactly this .NET type, resolved at Build.
    internal static bool IsApplicationObject(Type type) =>
        !type.IsPrimitive
        && !type.IsEnum
        && !type.IsArray
        && !type.IsPointer
        && !type.IsByRef
        && !type.ContainsGenericParameters
        && type.Assembly != LibraryAssembly
        && !(type.Namespace is { } name && (name == "System" || name.StartsWith("System.", StringComparison.Ordinal)));

    private static class ShapeOf<T>
    {
        internal static readonly Shape Kind = Classify(typeof(T));

        // Whether T can be a row of the table of collections, which is built when the first such row is asked for: an
        // array or a generic type that is not Nullable<X>.
        internal static readonly bool IsCollection = typeof(T).IsArray || (typeof(T).IsGenericType && Nullable.GetUnderlyingType(typeof(T)) is null);
    }

    private static class ElementConverter<T>
    {
        internal static readonly Func<T, CelValue> Instance = static item =>
        {
            _ = TryFromClr(item, out CelValue value);
            return value;
        };
    }

    private static class InputRowOf<T>
    {
        // The keys of the table are .NET types of the library, never from data.
        internal static readonly InputRow<T>? Row = Inputs.All.TryGetValue(typeof(T), out InputRow? row) ? (InputRow<T>)row : null;
    }

    // The key of a dictionary of the application as a CEL key and back.
    private static class KeyOf<TKey>
    {
        internal static readonly bool IsSupported = typeof(TKey) == typeof(string) || typeof(TKey) == typeof(Guid) || typeof(TKey) == typeof(bool)
            || IsSigned<TKey>() || IsUnsigned<TKey>() || typeof(TKey).IsEnum;

        internal static CelValue ToCel(TKey key)
        {
            _ = TryFromClr(key, out CelValue result);
            return result;
        }

        // The key of the dictionary for a CEL key: string, Guid by the canonical string only, bool, and the integers and
        // enumerations by the number line, so that the key 1 is found by 1, 1u and 1.0.
        internal static bool TryFromCel(CelValue key, [MaybeNullWhen(false)] out TKey result)
        {
            result = default;
            if (typeof(TKey) == typeof(string))
            {
                if (key.TryGetString(out string? text))
                {
                    string native = text;
                    result = Unsafe.As<string, TKey>(ref native);
                    return true;
                }

                return false;
            }

            if (typeof(TKey) == typeof(Guid))
            {
                if (key.TryGetString(out string? text) && TryCanonicalGuid(text, out Guid guid))
                {
                    result = Unsafe.As<Guid, TKey>(ref guid);
                    return true;
                }

                return false;
            }

            if (typeof(TKey) == typeof(bool))
            {
                if (key.TryGetBoolean(out bool flag))
                {
                    result = Unsafe.As<bool, TKey>(ref flag);
                    return true;
                }

                return false;
            }

            return TryFromNumber(key, out result);
        }
    }

    // A Guid only from its canonical text: 36 characters, format D, lower case, which Guid.TryParseExact alone would not
    // insist on.
    private static bool TryCanonicalGuid(string text, out Guid guid)
    {
        guid = default;
        if (text.Length != 36)
        {
            return false;
        }

        for (int index = 0; index < text.Length; index++)
        {
            char c = text[index];
            if (index is 8 or 13 or 18 or 23 ? c != '-' : !(char.IsAsciiDigit(c) || char.IsAsciiHexDigitLower(c)))
            {
                return false;
            }
        }

        return Guid.TryParseExact(text, "D", out guid);
    }

    // 0: the CEL key is no whole number; 1: a number in the range of long; 2: a number above it, in the range of ulong.
    private static int Widen(CelValue key, out long signed, out ulong unsigned)
    {
        signed = 0;
        unsigned = 0;
        if (key.TryGetInt64(out signed))
        {
            return 1;
        }

        if (key.TryGetUInt64(out unsigned))
        {
            if (unsigned <= long.MaxValue)
            {
                signed = (long)unsigned;
                return 1;
            }

            return 2;
        }

        if (!key.TryGetDouble(out double number))
        {
            return 0;
        }

        if (EqualityRules.IsLosslessInt(number))
        {
            signed = (long)number;
            return 1;
        }

        if (!EqualityRules.IsLosslessUInt(number))
        {
            return 0;
        }

        unsigned = (ulong)number;
        if (unsigned <= long.MaxValue)
        {
            signed = (long)unsigned;
            return 1;
        }

        return 2;
    }

    // The integer or enumeration key of a given CEL number, when it is in the range of the key type.
    private static bool TryFromNumber<TKey>(CelValue key, [MaybeNullWhen(false)] out TKey result)
    {
        result = default;
        int kind = Widen(key, out long signed, out ulong unsigned);
        if (kind == 0)
        {
            return false;
        }

        return kind == 1 && signed < 0
            ? TryFromSigned(signed, out result)
            : TryFromUnsigned(kind == 1 ? (ulong)signed : unsigned, out result) || (kind == 1 && TryFromSigned(signed, out result));
    }

    // A non-negative number as an unsigned key; false for a signed key type, which TryFromSigned takes.
    private static bool TryFromUnsigned<TKey>(ulong magnitude, [MaybeNullWhen(false)] out TKey result)
    {
        result = default;
        if (typeof(TKey) == typeof(nuint))
        {
            nuint native = (nuint)magnitude;
            result = Unsafe.As<nuint, TKey>(ref native);
            return native == magnitude;
        }

        switch (Type.GetTypeCode(typeof(TKey)))
        {
            case TypeCode.Byte when magnitude <= byte.MaxValue:
                byte octet = (byte)magnitude;
                result = Unsafe.As<byte, TKey>(ref octet);
                return true;
            case TypeCode.UInt16 when magnitude <= ushort.MaxValue:
                ushort word = (ushort)magnitude;
                result = Unsafe.As<ushort, TKey>(ref word);
                return true;
            case TypeCode.UInt32 when magnitude <= uint.MaxValue:
                uint dword = (uint)magnitude;
                result = Unsafe.As<uint, TKey>(ref dword);
                return true;
            case TypeCode.UInt64:
                result = Unsafe.As<ulong, TKey>(ref magnitude);
                return true;
            default:
                return false;
        }
    }

    // A number in the range of long as a signed key.
    private static bool TryFromSigned<TKey>(long signed, [MaybeNullWhen(false)] out TKey result)
    {
        result = default;
        if (typeof(TKey) == typeof(nint))
        {
            nint native = (nint)signed;
            result = Unsafe.As<nint, TKey>(ref native);
            return native == signed;
        }

        switch (Type.GetTypeCode(typeof(TKey)))
        {
            case TypeCode.SByte when signed is >= sbyte.MinValue and <= sbyte.MaxValue:
                sbyte small = (sbyte)signed;
                result = Unsafe.As<sbyte, TKey>(ref small);
                return true;
            case TypeCode.Int16 when signed is >= short.MinValue and <= short.MaxValue:
                short medium = (short)signed;
                result = Unsafe.As<short, TKey>(ref medium);
                return true;
            case TypeCode.Int32 when signed is >= int.MinValue and <= int.MaxValue:
                int integer = (int)signed;
                result = Unsafe.As<int, TKey>(ref integer);
                return true;
            case TypeCode.Int64:
                result = Unsafe.As<long, TKey>(ref signed);
                return true;
            default:
                return false;
        }
    }

    // The units of the copy of a set before it is sorted: 1 + c · n · ⌈log₂(n + 1)⌉, c = 1 + ⌈2 · L/10⌉, L the longest
    // string or bytes element, 0 without them, found by a pass that converts nothing.
    private static void ChargeSet<T>(IEnumerable<T> items, int count, IEvaluationMeter meter)
    {
        ulong longest = 0;
        foreach (T item in items)
        {
            longest = Math.Max(longest, (ulong)LengthOf(item));
        }

        ChargeSort(longest, count, meter);
    }

    // The units of the enumeration of a view ordered by ascending key, before its first entry: 1 + c · n · ⌈log₂(n + 1)⌉, c as for a
    // set, the longest key found by a pass that converts nothing; a Guid key is 36 long.
    internal static void ChargeSortedKeys<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> source, IEvaluationMeter meter)
    {
        int count = source.Count;
        ulong longest = 0;
        if (typeof(TKey) == typeof(Guid))
        {
            longest = 36;
        }
        else if (typeof(TKey) == typeof(string))
        {
            foreach (KeyValuePair<TKey, TValue> entry in source)
            {
                longest = Math.Max(longest, (ulong)LengthOf(entry.Key));
            }
        }

        ChargeSort(longest, count, meter);
    }

    // 1 + c · n · ⌈log₂(n + 1)⌉ with c = 1 + ⌈2 · L/10⌉ for the longest string or bytes L; 0 without them.
    private static void ChargeSort(ulong longest, int count, IEvaluationMeter meter)
    {
        UInt128 perElement = 1 + (((2 * (UInt128)longest) + 9) / 10);
        UInt128 units = 1 + (perElement * (ulong)count * (ulong)(count == 0 ? 0 : BitOperations.Log2((uint)count) + 1));
        meter.Charge(units > ulong.MaxValue ? ulong.MaxValue : (ulong)units);
    }

    // The length of a string or bytes element in code units or bytes; 0 for any other value.
    private static int LengthOf<T>(T item)
    {
        if (typeof(T) == typeof(string))
        {
            return Unsafe.As<T, string?>(ref item)?.Length ?? 0;
        }

        if (typeof(T) == typeof(byte[]))
        {
            return Unsafe.As<T, byte[]?>(ref item)?.Length ?? 0;
        }

        if (typeof(T) == typeof(ReadOnlyMemory<byte>))
        {
            return Unsafe.As<T, ReadOnlyMemory<byte>>(ref item).Length;
        }

        if (typeof(T) == typeof(CelValue))
        {
            CelValue value = Unsafe.As<T, CelValue>(ref item);
            return value.TryGetString(out string? text) ? text.Length : value.TryGetBytes(out ReadOnlyMemory<byte> bytes) ? bytes.Length : 0;
        }

        return typeof(T) == typeof(object)
            ? Unsafe.As<T, object?>(ref item) switch { string text => text.Length, byte[] bytes => bytes.Length, _ => 0 }
            : 0;
    }

    private static bool TrySortedCopy<T>(IEnumerable<T> items, int count, IEvaluationMeter? meter, CelType recordedType, out CelValue result)
    {
        if (meter is not null)
        {
            ChargeSet(items, count, meter);
        }

        Func<T, CelValue> converter = ElementConverter<T>.Instance;
        var values = new CelValue[count];
        int index = 0;
        foreach (T item in items)
        {
            if (index == values.Length)
            {
                break;
            }

            values[index++] = converter(item);
        }

        CelValue[] sorted = index < values.Length ? values[..index] : values;
        result = default;
        if (!TrySort(sorted))
        {
            return false;
        }

        result = CelValue.FromList(new CopiedList(sorted, recordedType));
        return true;
    }

    // The row of a .NET collection type of the input table: the CEL type it gives and the conversion of a value.
    private abstract class InputRow(CelType celType)
    {
        internal CelType CelType { get; } = celType;
    }

    private abstract class InputRow<T>(CelType celType) : InputRow(celType)
    {
        // Converts the value; false when the value has no CEL value, which is a set whose elements CEL cannot sort. The
        // meter is charged for a copy, a set first.
        internal abstract bool TryConvert(T value, IEvaluationMeter? meter, out CelValue result);
    }

    // An array, IReadOnlyList<X>, IList<X>, List<X>, ImmutableArray<X>, ImmutableList<X> and ReadOnlyCollection<X>: a typed
    // lazy view that records its source.
    private sealed class ListInput<TList, X>(CelType type, Func<TList, IReadOnlyList<X>?> asList) : InputRow<TList>(type)
    {
        internal override bool TryConvert(TList value, IEvaluationMeter? meter, out CelValue result)
        {
            IReadOnlyList<X>? list = value is null ? null : asList(value);
            result = list is null
                ? CelValue.Null
                : CelValue.FromList(new ApplicationList<X>(list, ElementConverter<X>.Instance, CelType, value));
            return true;
        }
    }

    // IReadOnlyCollection<X> that is not a list or a set: a copy in the order of enumeration.
    private sealed class CollectionInput<X>(CelType type) : InputRow<IReadOnlyCollection<X>>(type)
    {
        internal override bool TryConvert(IReadOnlyCollection<X> value, IEvaluationMeter? meter, out CelValue result)
        {
            if (value is null)
            {
                result = CelValue.Null;
                return true;
            }

            meter?.Charge(1 + (ulong)value.Count);
            result = CelValue.FromList(CopyList(value, ElementConverter<X>.Instance, CelType));
            return true;
        }
    }

    // IReadOnlySet<X>: a copy sorted by the order of CEL.
    private sealed class ReadOnlySetInput<X>(CelType type) : InputRow<IReadOnlySet<X>>(type)
    {
        internal override bool TryConvert(IReadOnlySet<X> value, IEvaluationMeter? meter, out CelValue result)
        {
            if (value is null)
            {
                result = CelValue.Null;
                return true;
            }

            return TrySortedCopy(value, value.Count, meter, CelType, out result);
        }
    }

    // ISet<X>: a copy sorted by the order of CEL.
    private sealed class SetInput<X>(CelType type) : InputRow<ISet<X>>(type)
    {
        internal override bool TryConvert(ISet<X> value, IEvaluationMeter? meter, out CelValue result)
        {
            if (value is null)
            {
                result = CelValue.Null;
                return true;
            }

            return TrySortedCopy(value, value.Count, meter, CelType, out result);
        }
    }

    // IReadOnlyDictionary<K, V>, IDictionary<K, V> and Dictionary<K, V>: a typed lazy view that records its source.
    private sealed class DictionaryInput<TDictionary, TKey, TValue>(CelType type, Func<TDictionary, IReadOnlyDictionary<TKey, TValue>?> asDictionary)
        : InputRow<TDictionary>(type)
        where TKey : notnull
    {
        internal override bool TryConvert(TDictionary value, IEvaluationMeter? meter, out CelValue result)
        {
            IReadOnlyDictionary<TKey, TValue>? dictionary = value is null ? null : asDictionary(value);
            result = dictionary is null
                ? CelValue.Null
                : CelValue.FromMap(new ApplicationMap<TKey, TValue>(dictionary, ElementConverter<TValue>.Instance, keepSourceOrder: false, CelType, value));
            return true;
        }
    }

    private static class Inputs
    {
        // The keys are .NET types of the library: trusted keys.
        internal static readonly FrozenDictionary<Type, InputRow> All = Create();

        private static FrozenDictionary<Type, InputRow> Create()
        {
            var rows = new Dictionary<Type, InputRow>();
            Add<bool>(rows);
            Add<long>(rows);
            Add<int>(rows);
            Add<ulong>(rows);
            Add<uint>(rows);
            Add<double>(rows);
            Add<string>(rows);
            Add<Guid>(rows);
            Add<byte[]>(rows);
            Add<ReadOnlyMemory<byte>>(rows);
            Add<bool?>(rows);
            Add<long?>(rows);
            Add<int?>(rows);
            Add<ulong?>(rows);
            Add<uint?>(rows);
            Add<double?>(rows);
            Add<Guid?>(rows);
            Add<ReadOnlyMemory<byte>?>(rows);
            Add<CelValue>(rows);
            Add<object>(rows);
            Add<CelList>(rows);
            Add<CelMap>(rows);
            Add<JsonElement>(rows);
            return rows.ToFrozenDictionary();
        }

        // The rows of the collections of one element type X: lists, sets, a collection without an index and the
        // dictionaries of X values.
        private static void Add<X>(Dictionary<Type, InputRow> rows)
        {
            CelType element = LeafInputType(typeof(X))!;
            CelType list = CelType.ListOf(element);
            rows.Add(typeof(X[]), new ListInput<X[], X>(list, static value => value));
            rows.Add(typeof(IReadOnlyList<X>), new ListInput<IReadOnlyList<X>, X>(list, static value => value));
            rows.Add(typeof(IList<X>), new ListInput<IList<X>, X>(list, static value => value as IReadOnlyList<X> ?? new ReadOnlyCollection<X>(value)));
            rows.Add(typeof(List<X>), new ListInput<List<X>, X>(list, static value => value));
            rows.Add(typeof(ImmutableArray<X>), new ListInput<ImmutableArray<X>, X>(list, static value => value.IsDefault ? [] : (IReadOnlyList<X>)value));
            rows.Add(typeof(ImmutableList<X>), new ListInput<ImmutableList<X>, X>(list, static value => value));
            rows.Add(typeof(ReadOnlyCollection<X>), new ListInput<ReadOnlyCollection<X>, X>(list, static value => value));
            rows.Add(typeof(IReadOnlyCollection<X>), new CollectionInput<X>(list));
            rows.Add(typeof(IReadOnlySet<X>), new ReadOnlySetInput<X>(list));
            rows.Add(typeof(ISet<X>), new SetInput<X>(list));
            AddMap<string, X>(rows, CelType.String, element);
            AddMap<Guid, X>(rows, CelType.String, element);
            AddMap<long, X>(rows, CelType.Int, element);
            AddMap<int, X>(rows, CelType.Int, element);
            AddMap<ulong, X>(rows, CelType.UInt, element);
            AddMap<uint, X>(rows, CelType.UInt, element);
            AddMap<bool, X>(rows, CelType.Bool, element);
        }

        private static void AddMap<TKey, TValue>(Dictionary<Type, InputRow> rows, CelType key, CelType value)
            where TKey : notnull
        {
            CelType map = CelType.MapOf(key, value);
            rows.Add(
                typeof(IReadOnlyDictionary<TKey, TValue>),
                new DictionaryInput<IReadOnlyDictionary<TKey, TValue>, TKey, TValue>(map, static dictionary => dictionary));
            rows.Add(typeof(Dictionary<TKey, TValue>), new DictionaryInput<Dictionary<TKey, TValue>, TKey, TValue>(map, static dictionary => dictionary));
            rows.Add(
                typeof(IDictionary<TKey, TValue>),
                new DictionaryInput<IDictionary<TKey, TValue>, TKey, TValue>(
                    map,
                    static dictionary => dictionary as IReadOnlyDictionary<TKey, TValue> ?? new ReadOnlyDictionary<TKey, TValue>(dictionary)));
        }
    }
}
