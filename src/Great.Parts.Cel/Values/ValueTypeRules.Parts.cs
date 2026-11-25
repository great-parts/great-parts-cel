using System;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The CEL types that the parts of an error name (CelError.ExpectedType and CelError.ActualType): the type of a value as a message
// names it, as CEL types instead of text.
internal static partial class ValueTypeRules
{
    // The kinds whose name is a name of a type that the kind alone gives: neither an object nor an abstract value, which are named by
    // their own types, nor an error value, which has no CEL type.
    private static readonly CelValueKind[] NamedKinds =
    [
        CelValueKind.Null, CelValueKind.Bool, CelValueKind.Int, CelValueKind.UInt, CelValueKind.Double, CelValueKind.String,
        CelValueKind.Bytes, CelValueKind.List, CelValueKind.Map, CelValueKind.Type, CelValueKind.Optional, CelValueKind.Timestamp,
        CelValueKind.Duration, CelValueKind.Decimal, CelValueKind.Date, CelValueKind.TimeOfDay,
    ];

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
        CelValueKind.Date => CelType.Date,
        CelValueKind.TimeOfDay => CelType.TimeOfDay,
        _ => CelType.Dyn,
    };

    /// <summary>
    /// The CEL type that a name of a message stands for, as <see cref="ValueTypeNames.Of"/> and <see cref="NameOf"/> write it: the type of a kind
    /// for the name of a kind, an object type for any other name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The type.</returns>
    internal static CelType TypeOfName(string name)
    {
        foreach (CelValueKind kind in NamedKinds)
        {
            if (string.Equals(ValueTypeNames.Of(kind), name, StringComparison.Ordinal))
            {
                return TypeOfKind(kind, null);
            }
        }

        return CelType.ObjectNamed(name);
    }

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
    internal static CelType? MismatchCelType(CelValue value, CelType type, bool nullElements, TypeIndex? types = null) =>
        Mismatch<CelType, TypeShape>(value, type, nullElements, types);

    // The actual type as a CEL type: the type of the value, and the declared type built with it.
    private readonly struct TypeShape : IMismatchShape<CelType>
    {
        public static CelType OfValue(CelValue value, TypeIndex? types) => TypeOfValue(value, types);

        public static CelType ListOf(CelType element) => CelType.ListOf(element);

        public static CelType OptionalOf(CelType held) => CelType.OptionalOf(held);

        public static CelType MapWithKey(CelType key, CelType value) => CelType.MapOf(key, value);

        public static CelType MapWithValue(CelType key, CelType value) => CelType.MapOf(key, value);
    }
}
