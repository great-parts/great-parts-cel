using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The runtime type of a value, and whether a value fits a declared type at any depth.
/// </summary>
internal static class ValueTypeRules
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
#if NET11_0_OR_GREATER
        CelValueKind.Decimal => CelType.Decimal,
#endif

        // Objects come with their part.
        _ => throw new NotImplementedException(),
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
    internal static string? MismatchType(CelValue value, CelType type, bool nullElements, TypeIndex? types = null)
    {
        if (!FitsTopLevel(value, type, types))
        {
            return NameOf(value, types);
        }

        if (value.TryGetList(out CelList? list) && !type.Parameters.IsEmpty && Descends(type.Parameters[0]))
        {
            return ElementMismatch(list, type.Parameters[0], nullElements, types);
        }

        if (type.Kind == CelTypeKind.Optional && value.TryGetOptional(out CelOptional<CelValue> optional))
        {
            return HeldMismatch(optional, type.Parameters[0], nullElements, types);
        }

        return value.TryGetMap(out CelMap? map) && !type.Parameters.IsEmpty
            ? EntryMismatch(map, type.Parameters[0], type.Parameters[1], nullElements, types)
            : null;
    }

    // The mismatch type of the held value of an optional that does not fit optional_type(E), as optional_type(…); with nullElements
    // a null held value fits.
    private static string? HeldMismatch(CelOptional<CelValue> optional, CelType held, bool nullElements, TypeIndex? types)
    {
        if (!optional.HasValue || !Descends(held) || (nullElements && optional.Value.Kind == CelValueKind.Null))
        {
            return null;
        }

        string? heldType = MismatchType(optional.Value, held, nullElements, types);
        return heldType is null ? null : "optional_type(" + heldType + ")";
    }

    // The mismatch type of the first element that does not fit list(E), as list(…); with nullElements a null element fits.
    private static string? ElementMismatch(CelList list, CelType element, bool nullElements, TypeIndex? types)
    {
        for (int index = 0; index < list.Count; index++)
        {
            CelValue item = list[index];
            string? itemType = nullElements && item.Kind == CelValueKind.Null ? null : MismatchType(item, element, false, types);
            if (itemType is not null)
            {
                return "list(" + itemType + ")";
            }
        }

        return null;
    }

    // The mismatch type of the first key or value that does not fit map(K, V), as map(…, …); with nullElements a null
    // value fits, a null key never.
    private static string? EntryMismatch(CelMap map, CelType key, CelType value, bool nullElements, TypeIndex? types)
    {
        bool keys = Descends(key);
        bool values = Descends(value);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            string? keyType = keys ? MismatchType(entry.Key, key, false, types) : null;
            if (keyType is not null)
            {
                return "map(" + keyType + ", " + value + ")";
            }

            string? valueType = values && !(nullElements && entry.Value.Kind == CelValueKind.Null) ? MismatchType(entry.Value, value, false, types) : null;
            if (valueType is not null)
            {
                return "map(" + key + ", " + valueType + ")";
            }
        }

        return null;
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
        for (int index = 0; index < count; index++)
        {
            CelValue item = list[index];
            if (nullElements && item.Kind == CelValueKind.Null)
            {
                continue;
            }

            TypeFit fit = FitsItem(item, element, meter, types);
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
                TypeFit valueFit = FitsItem(entry.Value, value, meter, types);
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
#if NET11_0_OR_GREATER

            // A decimal fits only decimal, not null: the static null is assignable to the type, a null value is not a decimal.
            CelTypeKind.Decimal => kind == CelValueKind.Decimal,
#endif

            // Every kind of type has its arm above.
            _ => throw new NotImplementedException(),
        };
    }
}
