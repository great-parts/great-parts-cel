using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The CEL types that the parts of an error name (CelError.ExpectedType and CelError.ActualType): the type of a value as a message
// names it, as CEL types instead of text.
internal static partial class ValueTypeRules
{
    /// <summary>
    /// The CEL type of a value of a kind, for an error that kept only the kind of the value and, for an object or an abstract
    /// value, the name of its type: the description or the .NET type of an object, the name of the type of an abstract value.
    /// The parameters of a list, a map, a type and an optional are erased to <c>dyn</c>, as the function <c>type</c> gives them.
    /// </summary>
    /// <param name="kind">The kind of the value.</param>
    /// <param name="name">The description, the .NET type or the name of the type for the kinds that need one; otherwise ignored.</param>
    /// <returns>The type.</returns>
    internal static CelType TypeOfKind(CelValueKind kind, object? name) => kind switch
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
        CelValueKind.Optional => OptionalType,
        CelValueKind.Timestamp => CelType.Timestamp,
        CelValueKind.Duration => CelType.Duration,
        CelValueKind.Object => ObjectTypeNamed(name),
        CelValueKind.Abstract => CelType.AbstractNamed((string)name!, []),
        CelValueKind.Decimal => CelType.Decimal,
        _ => CelType.Dyn,
    };

    /// <summary>
    /// The CEL type that a name of a message stands for, as <see cref="ValueTypeNames.Of"/> and <see cref="NameOf"/> write it: the type of a kind
    /// for the name of a kind, an object type for any other name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The type.</returns>
    internal static CelType TypeOfName(string name) => name switch
    {
        "null_type" => CelType.Null,
        "bool" => CelType.Bool,
        "int" => CelType.Int,
        "uint" => CelType.UInt,
        "double" => CelType.Double,
        "string" => CelType.String,
        "bytes" => CelType.Bytes,
        "list" => ListType,
        "map" => MapType,
        "type" => TypeType,
        "optional_type" => OptionalType,
        "timestamp" => CelType.Timestamp,
        "duration" => CelType.Duration,
        "decimal" => CelType.Decimal,
        _ => CelType.ObjectNamed(name),
    };

    // The object type of a description or of the .NET type of an instance that no description covers.
    private static CelType ObjectTypeNamed(object? name) => name is CelObjectType description
        ? CelType.ObjectNamed(description.Name)
        : CelType.ObjectNamed(name?.ToString() is { Length: > 0 } text ? text : "object");

    /// <summary>
    /// The CEL type of a value that is not an error value, an object value included: the description of an object or the .NET type of
    /// an instance without one.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="types">The type index of the environment, which describes object values; <see langword="null"/> outside an evaluation.</param>
    /// <returns>The type.</returns>
    internal static CelType TypeOfValue(CelValue value, TypeIndex? types) =>
        value.Kind == CelValueKind.Object ? CelType.ObjectNamed(ObjectRules.TypeName(value, types)) : TypeOf(value);

    /// <summary>
    /// The actual type that the parts of an error name for a value that does not fit a declared type, as
    /// <see cref="MismatchType(CelValue, CelType, bool, TypeIndex?)"/> names it in text: at the top level the type of the value; deeper
    /// the declared type with the parameter at the path of the first element, key or value that does not fit replaced by its actual type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The declared type.</param>
    /// <param name="nullElements">Whether the elements and the values at the top take <c>null</c>.</param>
    /// <param name="types">The type index of the environment, which names the description of an object value.</param>
    /// <returns>The actual type; <see langword="null"/> when the value fits.</returns>
    internal static CelType? MismatchCelType(CelValue value, CelType type, bool nullElements, TypeIndex? types = null)
    {
        if (!FitsTopLevel(value, type, types))
        {
            return TypeOfValue(value, types);
        }

        if (value.TryGetList(out CelList? list) && !type.Parameters.IsEmpty && Descends(type.Parameters[0]))
        {
            return ElementMismatchType(list, type.Parameters[0], nullElements, types);
        }

        if (type.Kind == CelTypeKind.Optional && value.TryGetOptional(out CelOptional<CelValue> optional))
        {
            return HeldMismatchType(optional, type.Parameters[0], nullElements, types);
        }

        return value.TryGetMap(out CelMap? map) && !type.Parameters.IsEmpty
            ? EntryMismatchType(map, type.Parameters[0], type.Parameters[1], nullElements, types)
            : null;
    }

    private static CelType? HeldMismatchType(CelOptional<CelValue> optional, CelType held, bool nullElements, TypeIndex? types)
    {
        if (!optional.HasValue || !Descends(held) || (nullElements && optional.Value.Kind == CelValueKind.Null))
        {
            return null;
        }

        return MismatchCelType(optional.Value, held, nullElements, types) is { } heldType ? CelType.OptionalOf(heldType) : null;
    }

    private static CelType? ElementMismatchType(CelList list, CelType element, bool nullElements, TypeIndex? types)
    {
        for (int index = 0; index < list.Count; index++)
        {
            CelValue item = list[index];
            if (!(nullElements && item.Kind == CelValueKind.Null) && MismatchCelType(item, element, false, types) is { } itemType)
            {
                return CelType.ListOf(itemType);
            }
        }

        return null;
    }

    private static CelType? EntryMismatchType(CelMap map, CelType key, CelType value, bool nullElements, TypeIndex? types)
    {
        bool keys = Descends(key);
        bool values = Descends(value);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if (keys && MismatchCelType(entry.Key, key, false, types) is { } keyType)
            {
                return CelType.MapOf(keyType, value);
            }

            if (values && !(nullElements && entry.Value.Kind == CelValueKind.Null) && MismatchCelType(entry.Value, value, false, types) is { } valueType)
            {
                return CelType.MapOf(key, valueType);
            }
        }

        return null;
    }
}
