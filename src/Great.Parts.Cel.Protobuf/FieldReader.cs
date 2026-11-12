using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Converts one value of a field — a single value, an element of a repeated field, a value of a map field — to a CEL value
/// by the rows of the reading table. The value is the boxed object that the field accessor gives.
/// </summary>
internal sealed class FieldReader
{
    private readonly ValueShape _shape;
    private readonly MessageType _owner;
    private readonly int _number;
    private readonly IMessage? _empty;

    /// <summary>
    /// Creates the reader of one field.
    /// </summary>
    /// <param name="shape">The shape of a value.</param>
    /// <param name="owner">The description of the message that has the field.</param>
    /// <param name="number">The number of the field in the description.</param>
    /// <param name="empty">For a message value, the empty message that an unset field reads as; otherwise <see langword="null"/>.</param>
    internal FieldReader(ValueShape shape, MessageType owner, int number, IMessage? empty)
    {
        _shape = shape;
        _owner = owner;
        _number = number;
        _empty = empty;
        Converter = Convert;
    }

    /// <summary>
    /// The conversion as a delegate, made once for the field: it is what a view of a collection calls for each element.
    /// </summary>
    internal Func<object?, CelValue> Converter { get; }

    /// <summary>
    /// Converts a value.
    /// </summary>
    /// <param name="value">The value as the accessor gives it; <see langword="null"/> for a field that is not set.</param>
    /// <returns>The CEL value, or an error value for a <c>Timestamp</c> or <c>Duration</c> out of range.</returns>
    internal CelValue Convert(object? value)
    {
        return _shape.Kind switch
        {
            FieldKind.Wrapper => value is null ? CelValue.Null : Primitive(_shape.Inner, value),
            FieldKind.Message => MessageType.Bare(value ?? _empty!),
            FieldKind.Any => value is Any any ? AnyUnpacker.Unpack(_owner, _number, any) : CelValue.Null,
            FieldKind.Value => JsonViews.FromValue(value as Value),
            FieldKind.Struct => CelValue.FromMap(JsonViews.StructView(value as Struct)),
            FieldKind.ListValue => CelValue.FromList(JsonViews.ListView(value as ListValue)),
            FieldKind.Timestamp => KnownValues.ToTimestamp(value as Timestamp, _owner, _number),
            FieldKind.Duration => KnownValues.ToDuration(value as Duration, _owner, _number),
            _ => Primitive(_shape.Kind, value),
        };
    }

    // The primitive kinds. A float is widened to a double, an enumeration is the number of its value.
    private static CelValue Primitive(FieldKind kind, object? value)
    {
        return kind switch
        {
            FieldKind.Int32 or FieldKind.Enum => CelValue.FromInt64((int)value!),
            FieldKind.Int64 => CelValue.FromInt64((long)value!),
            FieldKind.UInt32 => CelValue.FromUInt64((uint)value!),
            FieldKind.UInt64 => CelValue.FromUInt64((ulong)value!),
            FieldKind.Float => CelValue.FromDouble((float)value!),
            FieldKind.Double => CelValue.FromDouble((double)value!),
            FieldKind.Bool => CelValue.FromBoolean((bool)value!),
            FieldKind.String => CelValue.FromString(value as string ?? string.Empty),
            _ => CelValue.FromBytes(value is ByteString bytes ? bytes.Memory : ReadOnlyMemory<byte>.Empty),
        };
    }
}
