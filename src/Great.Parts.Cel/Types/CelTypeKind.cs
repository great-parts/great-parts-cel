using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Types;

/// <summary>
/// The kind of a CEL type: a primitive type, a well-known type, a constructed type with parameters,
/// an object type, or a type parameter.
/// </summary>
/// <remarks>
/// <para>
/// The kinds follow <c>type_kind</c> of the <c>Type</c> message in <c>checked.proto</c> of cel-spec,
/// with primitive and well-known types as separate kinds and <c>optional_type</c> as its own kind.
/// </para>
/// <para>
/// New kinds can be added: on .NET 11 the kind <c>Decimal</c> exists, and code compiled for .NET 10
/// can meet it when it runs on .NET 11. A <see langword="switch"/> over kinds needs a default branch.
/// The numeric values are part of the public contract and do not change.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1027:Mark enums with FlagsAttribute",
    Justification = "The numbers follow checked.proto and are not bit flags.")]
public enum CelTypeKind
{
    /// <summary>
    /// <c>dyn</c>: the type of a value whose type is known only at evaluation. Any value is assignable to it.
    /// </summary>
    Dyn = 0,

    /// <summary>
    /// <c>null_type</c>: the type of the <c>null</c> value.
    /// </summary>
    Null = 1,

    /// <summary>
    /// <c>bool</c>: <c>true</c> or <c>false</c>.
    /// </summary>
    Bool = 2,

    /// <summary>
    /// <c>int</c>: a signed 64-bit integer.
    /// </summary>
    Int = 3,

    /// <summary>
    /// <c>uint</c>: an unsigned 64-bit integer.
    /// </summary>
    UInt = 4,

    /// <summary>
    /// <c>double</c>: an IEEE 754 binary64 number.
    /// </summary>
    Double = 5,

    /// <summary>
    /// <c>string</c>: a sequence of Unicode code points.
    /// </summary>
    String = 6,

    /// <summary>
    /// <c>bytes</c>: a sequence of bytes.
    /// </summary>
    Bytes = 7,

    /// <summary>
    /// <c>google.protobuf.Timestamp</c>, also written <c>timestamp</c>: a point in time with nanosecond precision.
    /// </summary>
    Timestamp = 8,

    /// <summary>
    /// <c>google.protobuf.Duration</c>, also written <c>duration</c>: a signed time span with nanosecond precision.
    /// </summary>
    Duration = 9,

    /// <summary>
    /// <c>google.protobuf.Any</c>: a protobuf message of any type; at evaluation the value is the unpacked message.
    /// </summary>
    Any = 10,

    /// <summary>
    /// <c>list(E)</c>: the element type is the only parameter.
    /// </summary>
    List = 11,

    /// <summary>
    /// <c>map(K, V)</c>: the key type and the value type are the parameters.
    /// </summary>
    Map = 12,

    /// <summary>
    /// An object type identified by its full name: a .NET type described by a type context, a type
    /// described at run time, or a protobuf message.
    /// </summary>
    Object = 13,

    /// <summary>
    /// A wrapper type such as <c>google.protobuf.Int64Value</c>: a value of the wrapped primitive type,
    /// which is the only parameter, or <c>null</c>.
    /// </summary>
    Wrapper = 14,

    /// <summary>
    /// <c>type(T)</c>: the type of type values; the described type is the only parameter.
    /// </summary>
    Type = 15,

    /// <summary>
    /// A type parameter of a generic overload declaration, such as <c>T</c> in <c>list(T)</c>.
    /// </summary>
    TypeParameter = 16,

    /// <summary>
    /// <c>optional_type(T)</c> of the <c>optional</c> extension: the value type is the only parameter.
    /// </summary>
    Optional = 17,

#if NET11_0_OR_GREATER
    /// <summary>
    /// <c>decimal</c>: an IEEE 754 decimal128 number. Only on .NET 11, and only in environments with the
    /// <c>decimal</c> extension.
    /// </summary>
    Decimal = 18,
#endif

    /// <summary>
    /// An abstract type of an extension set, such as <c>net.IP</c> of the <c>network</c> set: a named type that only the
    /// functions of its set create and read. <see cref="CelType.Name"/> is its full name.
    /// </summary>
    Abstract = 19,
}
