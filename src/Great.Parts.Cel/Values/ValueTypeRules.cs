using System;
using System.Collections.Generic;
using System.Diagnostics;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The runtime type of a value, and whether a value fits a declared type at any depth.
/// </summary>
internal static partial class ValueTypeRules
{
    private static readonly CelType ListType = CelType.ListOf(CelType.Dyn);
    private static readonly CelType MapType = CelType.MapOf(CelType.Dyn, CelType.Dyn);
    private static readonly CelType TypeType = CelType.TypeOf(CelType.Dyn);
    private static readonly CelType OptionalType = CelType.OptionalOf(CelType.Dyn);

    /// <summary>
    /// The runtime type of a value, the value of the function <c>type</c>: the parameters of lists, maps and type
    /// values are erased to <c>dyn</c>.
    /// </summary>
    /// <param name="value">A value that is not an error value.</param>
    /// <returns>The type.</returns>
    internal static CelType TypeOf(CelValue value) => value.Kind switch
    {
        CelValueKind.Null => CelType.Null,
        CelValueKind.Bool => CelType.Bool,
        CelValueKind.Int => CelType.Int,
        CelValueKind.UInt => CelType.UInt,
        CelValueKind.Double => CelType.Double,
        CelValueKind.String => CelType.String,
        CelValueKind.Bytes => CelType.Bytes,
        CelValueKind.List => ListType,
        CelValueKind.Map => MapType,
        CelValueKind.Type => TypeType,
        CelValueKind.Abstract => AbstractTypeOf(value),
        CelValueKind.Timestamp => CelType.Timestamp,
        CelValueKind.Duration => CelType.Duration,
        CelValueKind.Optional => OptionalType,
        CelValueKind.Decimal => CelType.Decimal,
        CelValueKind.Date => CelType.Date,
        CelValueKind.TimeOfDay => CelType.TimeOfDay,

        // Objects come with their part.
        _ => throw new UnreachableException("The caller handles object values."),
    };

    // The type of the object of a value of the kind Abstract.
    private static CelType AbstractTypeOf(CelValue value)
    {
        _ = value.TryGetAbstract(out AbstractValue? content);
        return content!.Type;
    }

    /// <summary>
    /// The name of the type of a value as a message writes it: the description of an object value, the full name of an
    /// abstract type, the name of the kind of any other value.
    /// </summary>
    /// <param name="value">A value that is not an error value.</param>
    /// <param name="types">The type index of the environment, which describes object values; <see langword="null"/> outside an evaluation.</param>
    /// <returns>The name.</returns>
    internal static string NameOf(CelValue value, TypeIndex? types) => value.Kind switch
    {
        CelValueKind.Object => ObjectRules.TypeName(value, types),
        CelValueKind.Abstract => AbstractTypeOf(value).Name,
        _ => ValueTypeNames.Of(value.Kind),
    };

    /// <summary>
    /// Tells whether a value fits a declared type, at any depth: <c>dyn</c>, <c>Any</c> and type parameters take any value,
    /// a primitive type a value of its kind, <c>wrapper(P)</c> <c>null</c> or a value of <c>P</c>, <c>null_type</c>
    /// <c>null</c>, <c>type(…)</c> any type value, <c>list(E)</c> a list whose every element fits <c>E</c>, and
    /// <c>map(K, V)</c> a map whose every key fits <c>K</c> and every value <c>V</c>. An error value fits nothing at the
    /// top; an error value that an element, a key or a value of a collection of the application is, is not a mismatch of
    /// type: the outcome is that error, the first one in the order of the walk.
    /// </summary>
    /// <remarks>
    /// The check descends only into a parameter that is neither <c>dyn</c> nor a type parameter. Before it descends
    /// into a list of n elements or a map of n entries it charges n units.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <returns>The outcome: fits, does not fit, or an error element.</returns>
    internal static TypeFit Fits(CelValue value, CelType type, IEvaluationMeter? meter) => Fits(value, type, meter, nullElements: false);

    /// <summary>
    /// Tells whether a value fits a declared type as <see cref="Fits(CelValue, CelType, IEvaluationMeter?)"/> does, and
    /// when <paramref name="nullElements"/> is set, <c>null</c> fits also an element of the list or a value of the map
    /// at the top: the positions where a lazy view reads <c>null</c> as a null reference. A key never takes <c>null</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="nullElements">Whether the elements and the values at the top take <c>null</c>.</param>
    /// <param name="types">
    /// The type index of the environment for object types: <c>null</c> or an object value whose description is the
    /// description of the type in the index fits; without the index no object value fits.
    /// </param>
    /// <returns>The outcome: fits, does not fit, or an error element.</returns>
    internal static TypeFit Fits(CelValue value, CelType type, IEvaluationMeter? meter, bool nullElements, TypeIndex? types = null)
    {
        if (!FitsTopLevel(value, type, types))
        {
            return TypeFit.No;
        }

        return type.Kind switch
        {
            CelTypeKind.List when value.TryGetList(out CelList? list) => ElementsFit(list, type.Parameters[0], meter, nullElements, types),
            CelTypeKind.Map when value.TryGetMap(out CelMap? map) =>
                EntriesFit(map, type.Parameters[0], type.Parameters[1], meter, nullElements, types),
            CelTypeKind.Optional when value.TryGetOptional(out CelOptional<CelValue> optional) =>
                HeldFits(optional, type.Parameters[0], meter, nullElements, types),
            _ => TypeFit.Yes,
        };
    }

    // The held value of an optional against the parameter of optional_type(E): nothing is checked for an empty optional or for a
    // parameter that is dyn or a type parameter; otherwise the held value is checked as a value is, charging what the check of it
    // charges and nothing for the optional itself. With nullElements a null held value fits, as the check before a lazy view lets it
    // through the positions that the view reads as null references.
    private static TypeFit HeldFits(CelOptional<CelValue> optional, CelType held, IEvaluationMeter? meter, bool nullElements, TypeIndex? types)
    {
        if (!optional.HasValue || !Descends(held) || (nullElements && optional.Value.Kind == CelValueKind.Null))
        {
            return TypeFit.Yes;
        }

        return Fits(optional.Value, held, meter, nullElements, types);
    }

    /// <summary>
    /// The actual type that the messages of a mismatch name for a value that does not fit a declared type: at the
    /// top level the name of the type of the value; deeper the declared type with the parameter at the path of the
    /// first element, key or value that does not fit, in the order of enumeration, replaced by its actual type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <returns>The actual type, such as <c>string</c> or <c>list(string)</c>; <see langword="null"/> when the value fits.</returns>
    internal static string? MismatchType(CelValue value, CelType type) => MismatchType(value, type, nullElements: false);

    /// <summary>
    /// The actual type that the messages of a mismatch name, as <see cref="MismatchType(CelValue, CelType)"/>, for the check
    /// <see cref="Fits(CelValue, CelType, IEvaluationMeter?, bool, TypeIndex?)"/>: with <paramref name="nullElements"/> set, a <c>null</c>
    /// element or value at the top fits and is not named.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="nullElements">Whether the elements and the values at the top take <c>null</c>.</param>
    /// <param name="types">The type index of the environment, which names the description of an object value.</param>
    /// <returns>The actual type; <see langword="null"/> when the value fits.</returns>
    internal static string? MismatchType(CelValue value, CelType type, bool nullElements, TypeIndex? types = null) =>
        Mismatch<string, NameShape>(value, type, nullElements, types);

    // How an actual type is made, for the walk that finds it: as the text that a message writes, or as a CEL type. The walk is
    // the same; what a type of a value is at the top, and how the first element, key or value that does not fit is named inside
    // list(…), optional_type(…) and map(…, …), is the shape's. A shape is a struct, so that each walk is its own code and the calls
    // of the shape are direct.
    private interface IMismatchShape<TResult>
        where TResult : class
    {
        // The actual type of a value that does not fit the top level of the declared type.
        static abstract TResult OfValue(CelValue value, TypeIndex? types);

        static abstract TResult ListOf(TResult element);

        static abstract TResult OptionalOf(TResult held);

        // A map whose key is the actual type and whose value is the declared one, and the other way round.
        static abstract TResult MapWithKey(TResult key, CelType value);

        static abstract TResult MapWithValue(CelType key, TResult value);
    }

    // The walk of a mismatch: the actual type of the value at the top level when it does not fit, otherwise that of the first element,
    // key or value that does not fit, in the order of enumeration, inside the declared type; null when the value fits. With nullElements
    // a null element, value or held value fits, a null key never.
    private static TResult? Mismatch<TResult, TShape>(CelValue value, CelType type, bool nullElements, TypeIndex? types)
        where TResult : class
        where TShape : struct, IMismatchShape<TResult>
    {
        if (!FitsTopLevel(value, type, types))
        {
            return TShape.OfValue(value, types);
        }

        if (value.TryGetList(out CelList? list) && !type.Parameters.IsEmpty && Descends(type.Parameters[0]))
        {
            return ElementMismatch<TResult, TShape>(list, type.Parameters[0], nullElements, types);
        }

        if (type.Kind == CelTypeKind.Optional && value.TryGetOptional(out CelOptional<CelValue> optional))
        {
            return HeldMismatch<TResult, TShape>(optional, type.Parameters[0], nullElements, types);
        }

        return value.TryGetMap(out CelMap? map) && !type.Parameters.IsEmpty
            ? EntryMismatch<TResult, TShape>(map, type.Parameters[0], type.Parameters[1], nullElements, types)
            : null;
    }

    // The mismatch of the held value of an optional that does not fit optional_type(E); with nullElements a null held value fits.
    private static TResult? HeldMismatch<TResult, TShape>(CelOptional<CelValue> optional, CelType held, bool nullElements, TypeIndex? types)
        where TResult : class
        where TShape : struct, IMismatchShape<TResult>
    {
        if (!optional.HasValue || !Descends(held) || (nullElements && optional.Value.Kind == CelValueKind.Null))
        {
            return null;
        }

        return Mismatch<TResult, TShape>(optional.Value, held, nullElements, types) is { } heldType ? TShape.OptionalOf(heldType) : null;
    }

    // The mismatch of the first element that does not fit list(E); with nullElements a null element fits.
    private static TResult? ElementMismatch<TResult, TShape>(CelList list, CelType element, bool nullElements, TypeIndex? types)
        where TResult : class
        where TShape : struct, IMismatchShape<TResult>
    {
        for (int index = 0; index < list.Count; index++)
        {
            CelValue item = list[index];
            if (!(nullElements && item.Kind == CelValueKind.Null) && Mismatch<TResult, TShape>(item, element, false, types) is { } itemType)
            {
                return TShape.ListOf(itemType);
            }
        }

        return null;
    }

    // The mismatch of the first key or value that does not fit map(K, V); with nullElements a null value fits, a null key never.
    private static TResult? EntryMismatch<TResult, TShape>(CelMap map, CelType key, CelType value, bool nullElements, TypeIndex? types)
        where TResult : class
        where TShape : struct, IMismatchShape<TResult>
    {
        bool keys = Descends(key);
        bool values = Descends(value);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if (keys && Mismatch<TResult, TShape>(entry.Key, key, false, types) is { } keyType)
            {
                return TShape.MapWithKey(keyType, value);
            }

            if (values && !(nullElements && entry.Value.Kind == CelValueKind.Null)
                && Mismatch<TResult, TShape>(entry.Value, value, false, types) is { } valueType)
            {
                return TShape.MapWithValue(key, valueType);
            }
        }

        return null;
    }

    // The actual type as the text of a message: the name of the type of the value, and the declared type written with it.
    private readonly struct NameShape : IMismatchShape<string>
    {
        public static string OfValue(CelValue value, TypeIndex? types) => NameOf(value, types);

        public static string ListOf(string element) => "list(" + element + ")";

        public static string OptionalOf(string held) => "optional_type(" + held + ")";

        public static string MapWithKey(string key, CelType value) => "map(" + key + ", " + value + ")";

        public static string MapWithValue(CelType key, string value) => "map(" + key + ", " + value + ")";
    }

    // The elements of a list against list(E): n units before the descent into n elements; with nullElements a null
    // element fits. An element that is an error value ends the walk with that error, as a mismatch ends it.
    private static TypeFit ElementsFit(CelList list, CelType element, IEvaluationMeter? meter, bool nullElements, TypeIndex? types)
    {
        if (!Descends(element) || (list.RecordedType is { Kind: CelTypeKind.List } recorded && Covers(recorded.Parameters[0], element)))
        {
            return TypeFit.Yes;
        }

        int count = list.Count;
        meter?.Charge((ulong)count);
        CelObjectType? declared = DeclaredObject(element, types);
        for (int index = 0; index < count; index++)
        {
            CelValue item = list[index];
            if (nullElements && item.Kind == CelValueKind.Null)
            {
                continue;
            }

            TypeFit fit = FitsItem(item, element, declared, meter, types);
            if (!fit.IsFit)
            {
                return fit;
            }
        }

        return TypeFit.Yes;
    }

    // The entries of a map against map(K, V): n units before the descent into n entries; with nullElements a null
    // value fits, a null key never. Each entry is walked key first; the first error value ends the walk with that error.
    private static TypeFit EntriesFit(CelMap map, CelType key, CelType value, IEvaluationMeter? meter, bool nullElements, TypeIndex? types)
    {
        bool keys = Descends(key);
        bool values = Descends(value);
        if ((!keys && !values)
            || (map.RecordedType is { Kind: CelTypeKind.Map } recorded && Covers(recorded.Parameters[0], key) && Covers(recorded.Parameters[1], value)))
        {
            return TypeFit.Yes;
        }

        if (meter is not null)
        {
            meter.Charge((ulong)map.Count);
            map.ChargeEnumeration(meter);
        }

        CelObjectType? declared = DeclaredObject(value, types);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if (keys)
            {
                TypeFit keyFit = FitsItem(entry.Key, key, meter, types);
                if (!keyFit.IsFit)
                {
                    return keyFit;
                }
            }

            if (values && !(nullElements && entry.Value.Kind == CelValueKind.Null))
            {
                TypeFit valueFit = FitsItem(entry.Value, value, declared, meter, types);
                if (!valueFit.IsFit)
                {
                    return valueFit;
                }
            }
        }

        return TypeFit.Yes;
    }

    // An element, a key or a value of a collection against the declared type of its position: an error value is the
    // outcome, anything else is checked as a value.
    private static TypeFit FitsItem(CelValue item, CelType type, IEvaluationMeter? meter, TypeIndex? types) =>
        item.TryGetError(out CelError? error) ? TypeFit.OfElementError(error) : Fits(item, type, meter, false, types);

    // The same with the description of an object type that the walk has looked up once for all its items (DeclaredObject); null
    // checks the item as a value.
    private static TypeFit FitsItem(CelValue item, CelType type, CelObjectType? declared, IEvaluationMeter? meter, TypeIndex? types) =>
        declared is null ? FitsItem(item, type, meter, types) : FitsObject(item, declared, types);

    // The description that the type index has for the object type of the elements or the values of a collection, looked up once
    // for the walk instead of once for each item; null when the type is not an object type or the index does not have it, and the
    // walk then checks each item as a value.
    private static CelObjectType? DeclaredObject(CelType type, TypeIndex? types) =>
        type.Kind == CelTypeKind.Object && types is not null && types.TryGetObjectType(type.Name, out CelObjectType? declared) ? declared : null;

    // An item against an object type whose description the walk has looked up: what FitsItem gives for it without the lookup, an
    // error value the outcome, null and a value that the index describes by that description fitting, anything else not.
    private static TypeFit FitsObject(CelValue item, CelObjectType declared, TypeIndex? types)
    {
        if (item.TryGetError(out CelError? error))
        {
            return TypeFit.OfElementError(error);
        }

        return item.Kind == CelValueKind.Null
            || (item.Kind == CelValueKind.Object && ReferenceEquals(ObjectRules.Describe(item, types), declared))
            ? TypeFit.Yes
            : TypeFit.No;
    }

    // Whether the type of an abstract value has the name and the parameters of the declared abstract type.
    private static bool AbstractFits(CelValue value, CelType declared)
    {
        CelType actual = AbstractTypeOf(value);
        return string.Equals(actual.Name, declared.Name, StringComparison.Ordinal)
            && actual.Parameters.AsSpan().SequenceEqual(declared.Parameters.AsSpan());
    }

    // Whether every value of the recorded type of a view that the input rows made fits the declared type without `dyn`: the
    // view then needs no descent. A wrapper takes null and its parameter, so a view of nullable elements does not fit a
    // declared primitive.
    private static bool Covers(CelType recorded, CelType declared)
    {
        if (declared.Kind is CelTypeKind.Dyn or CelTypeKind.TypeParameter)
        {
            return true;
        }

        if (declared.Kind == CelTypeKind.Wrapper)
        {
            return recorded.Kind == CelTypeKind.Wrapper
                ? Covers(recorded.Parameters[0], declared.Parameters[0])
                : Covers(recorded, declared.Parameters[0]);
        }

        if (recorded.Kind != declared.Kind)
        {
            return false;
        }

        for (int index = 0; index < recorded.Parameters.Length && index < declared.Parameters.Length; index++)
        {
            if (!Covers(recorded.Parameters[index], declared.Parameters[index]))
            {
                return false;
            }
        }

        return true;
    }

    // Whether the check goes into values of a parameter of the declared type.
    private static bool Descends(CelType parameter) => parameter.Kind is not (CelTypeKind.Dyn or CelTypeKind.TypeParameter);

    /// <summary>
    /// Tells whether the top level of a value fits the top level of a type: the kind for a primitive, <c>list(…)</c>,
    /// <c>map(…)</c> and <c>type(…)</c>; <c>null</c> or the primitive for <c>wrapper(P)</c>; <c>null</c> for
    /// <c>null_type</c>; for an object type, <see cref="ObjectRules.Fits"/>; anything for <c>dyn</c>. An error value fits nothing.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The type.</param>
    /// <param name="types">The type index of the environment for object types, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the top level fits.</returns>
    internal static bool FitsTopLevel(CelValue value, CelType type, TypeIndex? types = null)
    {
        CelValueKind kind = value.Kind;
        if (kind == CelValueKind.Error)
        {
            return false;
        }

        return type.Kind switch
        {
            CelTypeKind.Dyn or CelTypeKind.Any or CelTypeKind.TypeParameter => true,
            CelTypeKind.Null => kind == CelValueKind.Null,
            CelTypeKind.Bool => kind == CelValueKind.Bool,
            CelTypeKind.Int => kind == CelValueKind.Int,
            CelTypeKind.UInt => kind == CelValueKind.UInt,
            CelTypeKind.Double => kind == CelValueKind.Double,
            CelTypeKind.String => kind == CelValueKind.String,
            CelTypeKind.Bytes => kind == CelValueKind.Bytes,
            CelTypeKind.Wrapper => kind == CelValueKind.Null || FitsTopLevel(value, type.Parameters[0]),
            CelTypeKind.Object => ObjectRules.Fits(value, type, types),
            CelTypeKind.List => kind == CelValueKind.List,
            CelTypeKind.Map => kind == CelValueKind.Map,
            CelTypeKind.Type => kind == CelValueKind.Type,
            CelTypeKind.Abstract => kind == CelValueKind.Null || (kind == CelValueKind.Abstract && AbstractFits(value, type)),
            CelTypeKind.Timestamp => kind == CelValueKind.Timestamp,
            CelTypeKind.Duration => kind == CelValueKind.Duration,
            CelTypeKind.Optional => kind == CelValueKind.Optional,

            // A decimal fits only decimal, not null: the static null is assignable to the type, a null value is not a decimal.
            CelTypeKind.Decimal => kind == CelValueKind.Decimal,
            CelTypeKind.Date => kind == CelValueKind.Date,
            CelTypeKind.TimeOfDay => kind == CelValueKind.TimeOfDay,

            // Every kind of type has its arm above.
            _ => throw new UnreachableException("Every kind of type has its arm above."),
        };
    }
}
