using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The kind of a <see cref="CelValue"/>: which CEL type the value has at evaluation and which
/// <c>TryGet…</c> method of <see cref="CelValue"/> reads it.
/// </summary>
/// <remarks>
/// <para>
/// The kinds follow <c>kind</c> of the <c>Value</c> message in <c>value.proto</c> of cel-spec, with
/// separate kinds for timestamps, durations and optionals, and the kind <see cref="Error"/> for an
/// error that is a value inside evaluation. Enumeration values of CEL are <see cref="Int"/> values.
/// </para>
/// <para>
/// New kinds can be added: on .NET 11 the kind <c>Decimal</c> exists, and code compiled for .NET 10 can
/// meet a value of that kind when it runs on .NET 11. A <see langword="switch"/> over kinds needs a
/// default branch. The numeric values are part of the public contract and do not change.
/// </para>
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1027:Mark enums with FlagsAttribute",
    Justification = "The numbers follow value.proto and are not bit flags.")]
public enum CelValueKind
{
    /// <summary>
    /// <c>null</c>. <c>default(CelValue)</c> has this kind.
    /// </summary>
    Null = 0,

    /// <summary>
    /// A <c>bool</c>; read with <see cref="CelValue.TryGetBoolean(out bool)"/>.
    /// </summary>
    Bool = 1,

    /// <summary>
    /// An <c>int</c>, a signed 64-bit integer; read with <see cref="CelValue.TryGetInt64(out long)"/>.
    /// </summary>
    Int = 2,

    /// <summary>
    /// A <c>uint</c>, an unsigned 64-bit integer; read with <see cref="CelValue.TryGetUInt64(out ulong)"/>.
    /// </summary>
    UInt = 3,

    /// <summary>
    /// A <c>double</c>; read with <see cref="CelValue.TryGetDouble(out double)"/>.
    /// </summary>
    Double = 4,

    /// <summary>
    /// A <c>string</c>; read with <see cref="CelValue.TryGetString(out string)"/>.
    /// </summary>
    String = 5,

    /// <summary>
    /// A <c>bytes</c> value; read with <see cref="CelValue.TryGetBytes(out System.ReadOnlyMemory{byte})"/>.
    /// </summary>
    Bytes = 6,

    /// <summary>
    /// A <c>list</c>; read with <see cref="CelValue.TryGetList(out CelList)"/>.
    /// </summary>
    List = 7,

    /// <summary>
    /// A <c>map</c>; read with <see cref="CelValue.TryGetMap(out CelMap)"/>.
    /// </summary>
    Map = 8,

    /// <summary>
    /// A <c>google.protobuf.Timestamp</c>; read with <see cref="CelValue.TryGetTimestamp(out CelTimestamp)"/>.
    /// </summary>
    Timestamp = 9,

    /// <summary>
    /// A <c>google.protobuf.Duration</c>; read with <see cref="CelValue.TryGetDuration(out CelDuration)"/>.
    /// </summary>
    Duration = 10,

    /// <summary>
    /// A type value, such as the result of <c>type(x)</c>; read with
    /// <see cref="CelValue.TryGetType(out Types.CelType)"/>.
    /// </summary>
    Type = 11,

    /// <summary>
    /// An <c>optional_type(T)</c> value of the <c>optional</c> extension; read with
    /// <see cref="CelValue.TryGetOptional(out CelOptional{CelValue})"/>.
    /// </summary>
    Optional = 12,

    /// <summary>
    /// An object: an instance of an application type described by a type source, a value of a type
    /// described at run time, or a protobuf message.
    /// </summary>
    Object = 13,

    /// <summary>
    /// An error inside evaluation, such as an element of a list over application data that could not
    /// be read; read with <see cref="CelValue.TryGetError(out Diagnostics.CelError)"/>. Logical operators
    /// and some macros absorb errors as the CEL language definition prescribes; an error that reaches the
    /// result becomes the error of the evaluation result.
    /// </summary>
    Error = 14,

#if NET11_0_OR_GREATER
    /// <summary>
    /// A <c>decimal</c>, an IEEE 754 decimal128 number; read with
    /// <see cref="CelValue.TryGetDecimal(out System.Numerics.Decimal128)"/>. Only on .NET 11.
    /// </summary>
    Decimal = 15,
#endif

    /// <summary>
    /// A value of an abstract type of an extension set, such as <c>net.IP</c>. This release has no <c>TryGet…</c>
    /// method for it: an expression reads it through the functions of its set, and an application receives it as a
    /// <see cref="CelValue"/>.
    /// </summary>
    Abstract = 16,
}
