using System;
using System.Collections.Immutable;
using System.Text;

namespace Great.Parts.Cel.Types;

/// <summary>
/// A CEL type, as type checking sees it: a primitive or well-known type, <c>dyn</c>, a list, map,
/// optional, wrapper or type of types with its parameters, an object type by full name, or a type
/// parameter of a generic overload.
/// </summary>
/// <remarks>
/// <para>
/// Types are declared in an environment — the types of variables, of overload parameters and results, of fields of
/// types described at run time — and reported back, for example as the type of the result of a compiled program. Most
/// declarations infer the CEL type from a .NET type by
/// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>;
/// this class is for the cases the inference does not cover, such as <c>dyn</c>, generic overloads, or a
/// <c>list(int)</c> result requested as a CEL value.
/// </para>
/// <para>
/// Two instances that describe the same type are equal by <see cref="Equals(CelType)"/>; the static
/// properties return shared instances. Instances are immutable and can be shared between threads.
/// </para>
/// </remarks>
public sealed class CelType : IEquatable<CelType>
{
    internal CelType(CelTypeKind kind, string name, ImmutableArray<CelType> parameters)
    {
        Kind = kind;
        Name = name;
        Parameters = parameters;
    }

    /// <summary>
    /// <c>dyn</c>: the type is known only at evaluation, and type checking accepts any value for it.
    /// </summary>
    public static CelType Dyn { get; } = new(CelTypeKind.Dyn, "dyn", []);

    /// <summary>
    /// <c>null_type</c>: the type whose only value is <c>null</c>.
    /// </summary>
    public static CelType Null { get; } = new(CelTypeKind.Null, "null_type", []);

    /// <summary>
    /// <c>bool</c>; on .NET, <see cref="bool"/>.
    /// </summary>
    public static CelType Bool { get; } = new(CelTypeKind.Bool, "bool", []);

    /// <summary>
    /// <c>int</c>, a signed 64-bit integer; on .NET, <see cref="long"/>.
    /// </summary>
    public static CelType Int { get; } = new(CelTypeKind.Int, "int", []);

    /// <summary>
    /// <c>uint</c>, an unsigned 64-bit integer; on .NET, <see cref="ulong"/>.
    /// </summary>
    public static CelType UInt { get; } = new(CelTypeKind.UInt, "uint", []);

    /// <summary>
    /// <c>double</c>, an IEEE 754 binary64 number; on .NET, <see cref="double"/>.
    /// </summary>
    public static CelType Double { get; } = new(CelTypeKind.Double, "double", []);

    /// <summary>
    /// <c>string</c>, a sequence of Unicode code points; on .NET, <see cref="string"/>. Functions on strings
    /// count code points, not UTF-16 code units.
    /// </summary>
    public static CelType String { get; } = new(CelTypeKind.String, "string", []);

    /// <summary>
    /// <c>bytes</c>; on .NET, <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/>.
    /// </summary>
    public static CelType Bytes { get; } = new(CelTypeKind.Bytes, "bytes", []);

    /// <summary>
    /// <c>google.protobuf.Timestamp</c>: a point in time from 0001-01-01T00:00:00Z to
    /// 9999-12-31T23:59:59.999999999Z with nanosecond precision; on .NET, <c>CelTimestamp</c>.
    /// </summary>
    public static CelType Timestamp => throw new NotImplementedException();

    /// <summary>
    /// <c>google.protobuf.Duration</c>: a signed time span of a 64-bit number of nanoseconds; on .NET,
    /// <c>CelDuration</c>.
    /// </summary>
    public static CelType Duration => throw new NotImplementedException();

    /// <summary>
    /// <c>google.protobuf.Any</c>: a protobuf message of any type. Values of this type are unpacked to
    /// the contained message when they are read, so a value never has this type at evaluation.
    /// </summary>
    public static CelType Any => throw new NotImplementedException();

#if NET11_0_OR_GREATER
    /// <summary>
    /// <c>decimal</c>, an IEEE 754 decimal128 number with 34 significant digits; on .NET,
    /// <see cref="System.Numerics.Decimal128"/>. Only on .NET 11. An environment knows the type only
    /// when the <c>decimal</c> extension is added to it; otherwise a declaration that uses it is a build
    /// error <see cref="Diagnostics.CelErrorCode.TypeNotDeclared"/>.
    /// </summary>
    public static CelType Decimal => throw new NotImplementedException();
#endif

    /// <summary>
    /// Which family the type belongs to, and so how to read <see cref="Name"/> and
    /// <see cref="Parameters"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For primitive and well-known types and for <c>dyn</c> and <c>null_type</c>, the kind identifies the
    /// type and <see cref="Parameters"/> is empty. For <see cref="CelTypeKind.List"/>,
    /// <see cref="CelTypeKind.Map"/>, <see cref="CelTypeKind.Optional"/>, <see cref="CelTypeKind.Wrapper"/>
    /// and <see cref="CelTypeKind.Type"/>, the type is defined by <see cref="Parameters"/>, in the order
    /// that member describes; for a wrapper <see cref="Name"/> is also the full name of the wrapper type.
    /// For <see cref="CelTypeKind.Object"/>, <see cref="Name"/> is the full name of the object type and
    /// is the only thing that identifies it. For <see cref="CelTypeKind.TypeParameter"/>,
    /// <see cref="Name"/> is the name of the parameter within its overload.
    /// </para>
    /// <para>
    /// On .NET 11 the kind can be <c>Decimal</c>, including in code compiled for .NET 10 that runs on
    /// .NET 11; a <see langword="switch"/> over the kind needs a default branch.
    /// </para>
    /// </remarks>
    public CelTypeKind Kind { get; }

    /// <summary>
    /// The name CEL uses for the type: <c>int</c>, <c>list</c>, <c>map</c>, <c>optional_type</c>,
    /// <c>type</c>, <c>google.protobuf.Timestamp</c>, the full name of a wrapper such as
    /// <c>google.protobuf.Int64Value</c>, the full name of an object type, or the name of a type parameter.
    /// The name does not include the parameters; <see cref="ToString"/> does.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The type parameters, in the order CEL writes them: the element type of a list; the key and value
    /// types of a map; the value type of an optional; the wrapped primitive type of a wrapper; the
    /// described type of <c>type(T)</c>. Empty for other kinds.
    /// </summary>
    public ImmutableArray<CelType> Parameters { get; }

    /// <summary>
    /// Creates <c>list(E)</c>.
    /// </summary>
    /// <param name="elementType">The type of the elements.</param>
    /// <returns>The list type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="elementType"/> is <see langword="null"/>.</exception>
    public static CelType ListOf(CelType elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        return new CelType(CelTypeKind.List, "list", [elementType]);
    }

    /// <summary>
    /// Creates <c>map(K, V)</c>. Keys of map values are <c>int</c>, <c>uint</c>, <c>bool</c> or
    /// <c>string</c> values, as the CEL language definition allows.
    /// </summary>
    /// <param name="keyType">The type of the keys.</param>
    /// <param name="valueType">The type of the values.</param>
    /// <returns>The map type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="keyType"/> or <paramref name="valueType"/> is <see langword="null"/>.</exception>
    public static CelType MapOf(CelType keyType, CelType valueType)
    {
        ArgumentNullException.ThrowIfNull(keyType);
        ArgumentNullException.ThrowIfNull(valueType);
        return new CelType(CelTypeKind.Map, "map", [keyType, valueType]);
    }

    /// <summary>
    /// Creates <c>optional_type(T)</c> of the <c>optional</c> extension. A declaration that uses the type
    /// needs the extension in the environment.
    /// </summary>
    /// <param name="valueType">The type of the value the optional can hold.</param>
    /// <returns>The optional type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="valueType"/> is <see langword="null"/>.</exception>
    public static CelType OptionalOf(CelType valueType)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates the wrapper type of a primitive type, such as <c>google.protobuf.Int64Value</c> for
    /// <c>int</c>: its values are values of the primitive type or <c>null</c>. On .NET, wrappers
    /// correspond to nullable value types such as <see cref="Nullable{T}"/> of <see cref="long"/>.
    /// </summary>
    /// <param name="primitiveType">
    /// <see cref="Bool"/>, <see cref="Int"/>, <see cref="UInt"/>, <see cref="Double"/>, <see cref="String"/>
    /// or <see cref="Bytes"/>; CEL has wrappers only for these.
    /// </param>
    /// <returns>The wrapper type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="primitiveType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="primitiveType"/> is not one of the types listed above.</exception>
    public static CelType WrapperOf(CelType primitiveType)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates <c>type(T)</c>, the type of the type value <paramref name="type"/>; <c>TypeOf(Dyn)</c> is
    /// the type of any type value. At evaluation type values have their parameters erased:
    /// <c>type([1]) == list</c>.
    /// </summary>
    /// <param name="type">The described type.</param>
    /// <returns>The type of the type value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public static CelType TypeOf(CelType type)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Refers to an object type by its full CEL name, such as <c>acme.sales.Order</c> or the full name of a
    /// protobuf message. The name only refers to the type: a type source added to the environment must
    /// describe it, otherwise a declaration that uses it is a build error
    /// <see cref="Diagnostics.CelErrorCode.TypeNotDeclared"/>.
    /// </summary>
    /// <param name="fullName">The full name of the object type.</param>
    /// <returns>The object type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fullName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fullName"/> is empty.</exception>
    public static CelType ObjectNamed(string fullName)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a type parameter for a generic overload declaration, such as <c>T</c> in
    /// <c>list(T) -> T</c>. Type checking binds the parameter to a type at each call.
    /// </summary>
    /// <param name="name">The name of the parameter, unique within its overload.</param>
    /// <returns>The type parameter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public static CelType TypeParameter(string name)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Compares two types: they are equal when their kinds, names and parameters are equal.
    /// </summary>
    /// <param name="other">The type to compare with.</param>
    /// <returns><see langword="true"/> when the types are equal.</returns>
    public bool Equals(CelType? other) =>
        other is not null
        && Kind == other.Kind
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && Parameters.AsSpan().SequenceEqual(other.Parameters.AsSpan());

    /// <summary>
    /// Compares this type with an object, as <see cref="Equals(CelType)"/> does when the object is a <see cref="CelType"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal type.</returns>
    public override bool Equals(object? obj) => Equals(obj as CelType);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelType)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() =>
        HashCode.Combine(Kind, Name.GetHashCode(StringComparison.Ordinal), Parameters.Length);

    /// <summary>
    /// Formats the type in CEL notation with its parameters, as error messages show it, for example
    /// <c>list(int)</c> or <c>map(string, dyn)</c>.
    /// </summary>
    /// <returns>The type in CEL notation.</returns>
    public override string ToString()
    {
        if (Parameters.IsEmpty)
        {
            return Name;
        }

        var text = new StringBuilder(Name).Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            text.Append(Parameters[i].ToString());
        }

        return text.Append(')').ToString();
    }
}
