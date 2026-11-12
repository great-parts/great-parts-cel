using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The implementations of the nine overloads of the extension set optional, version 2, that are functions with a body: optional.of,
// optional.ofNonZeroValue, optional.none, value, hasValue, first, last, optional.unwrap and unwrapOpt. The overloads or and
// orValue are special forms of the planner, and the overloads of the operators are not here. An argument is never an error value; an
// element of a list that is one is the result of the call.
internal static class OptionalFunctions
{
    // optional_value of an optional that holds no value: OptionalHasNoValue at the call.
    private const string EmptyMessage = "optional value is empty";

    // The one empty list that unwrap and unwrapOpt return when nothing is held.
    private static readonly CelValue EmptyList = CelValue.FromList(new ValueArrayList([]));

    // optional.of(v), V -> optional_type(V): an optional that holds the argument, null included, in one box.
    internal static CelValue Of(ReadOnlySpan<CelValue> arguments, in CallSite _) => CelValue.OptionalOfValue(arguments[0]);

    // optional.none(), -> optional_type(V): the one empty optional; nothing is allocated.
    internal static CelValue None(ReadOnlySpan<CelValue> _, in CallSite __) => CelValue.EmptyOptional;

    // optional.ofNonZeroValue(v), V -> optional_type(V): the empty optional for a zero value of its kind, else as optional.of. An object
    // is a zero value when none of its fields is set: the call charges the units of the f fields it may read before the first read.
    internal static CelValue OfNonZeroValue(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        CelValue value = arguments[0];
        if (value.Kind == CelValueKind.Object)
        {
            if (!TryIsZeroObject(value, in site, out bool zeroObject, out CelValue error))
            {
                return site.Placed(error);
            }

            return zeroObject ? CelValue.EmptyOptional : CelValue.OptionalOfValue(value);
        }

        return IsZero(value) ? CelValue.EmptyOptional : CelValue.OptionalOfValue(value);
    }

    // optional_type(V).value(), -> V: the held value; OptionalHasNoValue for an empty optional.
    internal static CelValue Value(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetOptional(out CelOptional<CelValue> optional))
        {
            return site.NoMatchingOverload(arguments);
        }

        return optional.HasValue ? optional.Value : site.Error(CelErrorCode.OptionalHasNoValue, EmptyMessage);
    }

    // optional_type(V).hasValue(), -> bool.
    internal static CelValue HasValue(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetOptional(out CelOptional<CelValue> optional)
            ? CelValue.FromBoolean(optional.HasValue)
            : site.NoMatchingOverload(arguments);

    // list(V).first(), -> optional_type(V): the empty optional for an empty list, else the optional of the first element.
    internal static CelValue First(ReadOnlySpan<CelValue> arguments, in CallSite site) => Pick(arguments, in site, last: false);

    // list(V).last(), -> optional_type(V): the empty optional for an empty list, else the optional of the last element.
    internal static CelValue Last(ReadOnlySpan<CelValue> arguments, in CallSite site) => Pick(arguments, in site, last: true);

    // optional.unwrap(list(optional_type(V))) and list(optional_type(V)).unwrapOpt(), -> list(V): the held values of the optionals that
    // have one, in order, in a new list; an element that is not an optional is no match.
    internal static CelValue Unwrap(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        int count = list.Count;
        if (count == 0)
        {
            return EmptyList;
        }

        var held = new CelValue[count];
        int stored = 0;
        for (int i = 0; i < count; i++)
        {
            CelValue element = list.Read(i, site.Meter);
            if (element.Kind == CelValueKind.Error)
            {
                return site.Placed(element);
            }

            if (!element.TryGetOptional(out CelOptional<CelValue> optional))
            {
                return site.NoMatchingOverload(arguments);
            }

            if (optional.HasValue)
            {
                held[stored++] = optional.Value;
            }
        }

        return stored == 0 ? EmptyList : CelValue.FromList(new ValueArrayList(stored < count ? held[..stored] : held));
    }

    private static CelValue Pick(ReadOnlySpan<CelValue> arguments, in CallSite site, bool last)
    {
        if (!arguments[0].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        int count = list.Count;
        if (count == 0)
        {
            return CelValue.EmptyOptional;
        }

        CelValue element = list.Read(last ? count - 1 : 0, site.Meter);
        return element.Kind == CelValueKind.Error ? site.Placed(element) : CelValue.OptionalOfValue(element);
    }

    // The zero values of a kind other than an object: null; false; the int 0 and the uint 0; the double 0.0 and -0.0, but not a NaN;
    // the empty string, bytes, list and map; the duration of 0 nanoseconds; the timestamp 0001-01-01T00:00:00Z. Nothing else is zero: an
    // optional, a type value and a value of an abstract type are not.
    private static bool IsZero(CelValue value) => value.Kind switch
    {
        CelValueKind.Null => true,
        CelValueKind.Bool or CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double => IsZeroNumber(value),
        CelValueKind.String or CelValueKind.Bytes or CelValueKind.List or CelValueKind.Map => IsEmpty(value),
        CelValueKind.Duration or CelValueKind.Timestamp => IsZeroTime(value),
        _ => false,
    };

    // false, 0, 0u, 0.0 and -0.0; a NaN is not zero.
    private static bool IsZeroNumber(CelValue value) =>
        value.TryGetBoolean(out bool truth) ? !truth
        : value.TryGetInt64(out long integer) ? integer == 0
        : value.TryGetUInt64(out ulong unsigned) ? unsigned == 0
        : value.TryGetDouble(out double number) && number == 0;

    // The empty string, bytes, list and map.
    private static bool IsEmpty(CelValue value) =>
        value.TryGetString(out string? text) ? text.Length == 0
        : value.TryGetBytes(out ReadOnlyMemory<byte> bytes) ? bytes.IsEmpty
        : value.TryGetList(out CelList? list) ? list.Count == 0
        : value.TryGetMap(out CelMap? map) && map.Count == 0;

    // The duration of 0 nanoseconds and the timestamp 0001-01-01T00:00:00Z.
    private static bool IsZeroTime(CelValue value) =>
        value.TryGetDuration(out CelDuration span)
            ? span.TotalNanoseconds == 0
            : value.TryGetTimestamp(out CelTimestamp moment) && moment.Seconds == CelTimestamp.MinSeconds && moment.Nanoseconds == 0;

    // Whether none of the fields of an object is set: the fields in the order of the description, up to the first one that is set. The
    // units of the f fields are charged before the first read. An exception of application code becomes the error value of the read.
    private static bool TryIsZeroObject(CelValue value, in CallSite site, out bool zero, out CelValue error)
    {
        zero = false;
        if (!ObjectRules.TryDescribe(value, site.Types, out CelObjectType? owner, out error))
        {
            return false;
        }

        _ = value.TryGetObject(out object? instance);
        int count = owner!.Fields.Length;
        site.Charge((ulong)count);
        IApplicationBoundary? boundary = site.Boundary;
        for (int number = 0; number < count; number++)
        {
            boundary?.Event();
            try
            {
                if (owner.HasField(instance!, number))
                {
                    return true;
                }
            }
            catch (Exception exception) when (boundary is not null && boundary.IsApplicationException(exception))
            {
                error = boundary.FieldException(exception, owner, number);
                return false;
            }
        }

        zero = true;
        return true;
    }
}
