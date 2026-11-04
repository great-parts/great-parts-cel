using System;
using System.Collections.Generic;
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

        // Time, optional values, objects and decimals come with their parts.
        _ => throw new NotImplementedException(),
    };

    /// <summary>
    /// Tells whether a value fits a declared type, at any depth: <c>dyn</c> and type parameters take any value,
    /// a primitive type a value of its kind, <c>wrapper(P)</c> <c>null</c> or a value of <c>P</c>, <c>null_type</c>
    /// <c>null</c>, <c>type(…)</c> any type value, <c>list(E)</c> a list whose every element fits <c>E</c>, and
    /// <c>map(K, V)</c> a map whose every key fits <c>K</c> and every value <c>V</c>. An error value fits nothing.
    /// </summary>
    /// <remarks>
    /// The check descends only into a parameter that is neither <c>dyn</c> nor a type parameter. Before it descends
    /// into a list of n elements or a map of n entries it charges n units.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <returns><see langword="true"/> when the value fits.</returns>
    internal static bool Fits(CelValue value, CelType type, IEvaluationMeter? meter) => Fits(value, type, meter, nullElements: false);

    /// <summary>
    /// Tells whether a value fits a declared type as <see cref="Fits(CelValue, CelType, IEvaluationMeter?)"/> does, and
    /// when <paramref name="nullElements"/> is set, <c>null</c> fits also an element of the list or a value of the map
    /// at the top: the positions where a lazy view reads <c>null</c> as a null reference. A key never takes <c>null</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="nullElements">Whether the elements and the values at the top take <c>null</c>.</param>
    /// <returns><see langword="true"/> when the value fits.</returns>
    internal static bool Fits(CelValue value, CelType type, IEvaluationMeter? meter, bool nullElements)
    {
        if (!FitsTopLevel(value, type))
        {
            return false;
        }

        return type.Kind switch
        {
            CelTypeKind.List when value.TryGetList(out CelList? list) => ElementsFit(list, type.Parameters[0], meter, nullElements),
            CelTypeKind.Map when value.TryGetMap(out CelMap? map) =>
                EntriesFit(map, type.Parameters[0], type.Parameters[1], meter, nullElements),
            _ => true,
        };
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
    /// <see cref="Fits(CelValue, CelType, IEvaluationMeter?, bool)"/>: with <paramref name="nullElements"/> set, a <c>null</c>
    /// element or value at the top fits and is not named.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="nullElements">Whether the elements and the values at the top take <c>null</c>.</param>
    /// <returns>The actual type; <see langword="null"/> when the value fits.</returns>
    internal static string? MismatchType(CelValue value, CelType type, bool nullElements)
    {
        if (!FitsTopLevel(value, type))
        {
            return ValueTypeNames.Of(value.Kind);
        }

        if (value.TryGetList(out CelList? list) && !type.Parameters.IsEmpty && Descends(type.Parameters[0]))
        {
            return ElementMismatch(list, type.Parameters[0], nullElements);
        }

        return value.TryGetMap(out CelMap? map) && !type.Parameters.IsEmpty
            ? EntryMismatch(map, type.Parameters[0], type.Parameters[1], nullElements)
            : null;
    }

    // The mismatch type of the first element that does not fit list(E), as list(…); with nullElements a null element fits.
    private static string? ElementMismatch(CelList list, CelType element, bool nullElements)
    {
        for (int index = 0; index < list.Count; index++)
        {
            CelValue item = list[index];
            string? itemType = nullElements && item.Kind == CelValueKind.Null ? null : MismatchType(item, element);
            if (itemType is not null)
            {
                return "list(" + itemType + ")";
            }
        }

        return null;
    }

    // The mismatch type of the first key or value that does not fit map(K, V), as map(…, …); with nullElements a null
    // value fits, a null key never.
    private static string? EntryMismatch(CelMap map, CelType key, CelType value, bool nullElements)
    {
        bool keys = Descends(key);
        bool values = Descends(value);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            string? keyType = keys ? MismatchType(entry.Key, key) : null;
            if (keyType is not null)
            {
                return "map(" + keyType + ", " + value + ")";
            }

            string? valueType = values && !(nullElements && entry.Value.Kind == CelValueKind.Null) ? MismatchType(entry.Value, value) : null;
            if (valueType is not null)
            {
                return "map(" + key + ", " + valueType + ")";
            }
        }

        return null;
    }

    // The elements of a list against list(E): n units before the descent into n elements; with nullElements a null
    // element fits.
    private static bool ElementsFit(CelList list, CelType element, IEvaluationMeter? meter, bool nullElements)
    {
        if (!Descends(element))
        {
            return true;
        }

        int count = list.Count;
        meter?.Charge((ulong)count);
        for (int index = 0; index < count; index++)
        {
            CelValue item = list[index];
            if (!(nullElements && item.Kind == CelValueKind.Null) && !Fits(item, element, meter))
            {
                return false;
            }
        }

        return true;
    }

    // The entries of a map against map(K, V): n units before the descent into n entries; with nullElements a null
    // value fits, a null key never.
    private static bool EntriesFit(CelMap map, CelType key, CelType value, IEvaluationMeter? meter, bool nullElements)
    {
        bool keys = Descends(key);
        bool values = Descends(value);
        if (!keys && !values)
        {
            return true;
        }

        meter?.Charge((ulong)map.Count);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if ((keys && !Fits(entry.Key, key, meter))
                || (values && !(nullElements && entry.Value.Kind == CelValueKind.Null) && !Fits(entry.Value, value, meter)))
            {
                return false;
            }
        }

        return true;
    }

    // Whether the check goes into values of a parameter of the declared type.
    private static bool Descends(CelType parameter) => parameter.Kind is not (CelTypeKind.Dyn or CelTypeKind.TypeParameter);

    // The top level of the value against the top level of the type.
    private static bool FitsTopLevel(CelValue value, CelType type)
    {
        CelValueKind kind = value.Kind;
        if (kind == CelValueKind.Error)
        {
            return false;
        }

        return type.Kind switch
        {
            CelTypeKind.Dyn or CelTypeKind.TypeParameter => true,
            CelTypeKind.Null => kind == CelValueKind.Null,
            CelTypeKind.Bool => kind == CelValueKind.Bool,
            CelTypeKind.Int => kind == CelValueKind.Int,
            CelTypeKind.UInt => kind == CelValueKind.UInt,
            CelTypeKind.Double => kind == CelValueKind.Double,
            CelTypeKind.String => kind == CelValueKind.String,
            CelTypeKind.Bytes => kind == CelValueKind.Bytes,
            CelTypeKind.Wrapper => kind == CelValueKind.Null || FitsTopLevel(value, type.Parameters[0]),
            CelTypeKind.List => kind == CelValueKind.List,
            CelTypeKind.Map => kind == CelValueKind.Map,
            CelTypeKind.Type => kind == CelValueKind.Type,

            // Time, optional values, objects and decimals come with their parts.
            _ => throw new NotImplementedException(),
        };
    }
}
