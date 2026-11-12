using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// The kind of one value of a field: how reading converts it, decided once from the descriptor.
/// </summary>
internal enum FieldKind
{
    Int32,
    Int64,
    UInt32,
    UInt64,
    Float,
    Double,
    Bool,
    String,
    Bytes,
    Enum,
    Message,
    Any,
    Value,
    Struct,
    ListValue,
    Timestamp,
    Duration,
    Wrapper,
}

/// <summary>
/// How a field holds its values.
/// </summary>
internal enum FieldContainer
{
    Single,
    Repeated,
    Map,
}

/// <summary>
/// The kind and the CEL type of one value of a field, taken from the descriptor.
/// </summary>
internal sealed class ValueShape
{
    // The kind of each value of FieldType, by the number of the enumeration; Message stands for message and group fields
    // until the message is looked at.
    private static readonly FieldKind[] KindByFieldType = CreateKindTable();

    private ValueShape(FieldKind kind, FieldKind inner, MessageDescriptor? message)
    {
        Kind = kind;
        Inner = inner;
        Message = message;
        CelType = TypeOf(kind, inner, message);
    }

    /// <summary>
    /// The kind of the value.
    /// </summary>
    internal FieldKind Kind { get; }

    /// <summary>
    /// For a wrapper, the kind of the primitive it holds; otherwise the same as <see cref="Kind"/>.
    /// </summary>
    internal FieldKind Inner { get; }

    /// <summary>
    /// The message of a value of the kind <see cref="FieldKind.Message"/>; otherwise <see langword="null"/>.
    /// </summary>
    internal MessageDescriptor? Message { get; }

    /// <summary>
    /// The CEL type of the value that reading gives.
    /// </summary>
    internal CelType CelType { get; }

    /// <summary>
    /// Finds the shape of a value of a field, ignoring whether the field is repeated or a map.
    /// </summary>
    /// <param name="field">The descriptor of the field, or of the key or value field of a map entry.</param>
    /// <returns>The shape.</returns>
    internal static ValueShape Of(FieldDescriptor field)
    {
        FieldKind kind = KindByFieldType[(int)field.FieldType];
        return kind == FieldKind.Message ? OfMessage(field.MessageType) : new ValueShape(kind, kind, null);
    }

    private static ValueShape OfMessage(MessageDescriptor message)
    {
        return message.FullName switch
        {
            "google.protobuf.Any" => new ValueShape(FieldKind.Any, FieldKind.Any, null),
            "google.protobuf.Value" => new ValueShape(FieldKind.Value, FieldKind.Value, null),
            "google.protobuf.Struct" => new ValueShape(FieldKind.Struct, FieldKind.Struct, null),
            "google.protobuf.ListValue" => new ValueShape(FieldKind.ListValue, FieldKind.ListValue, null),
            "google.protobuf.Timestamp" => new ValueShape(FieldKind.Timestamp, FieldKind.Timestamp, null),
            "google.protobuf.Duration" => new ValueShape(FieldKind.Duration, FieldKind.Duration, null),
            _ => ReferenceEquals(message.File, WrappersReflection.Descriptor) ? OfWrapper(message) : new ValueShape(FieldKind.Message, FieldKind.Message, message),
        };
    }

    // A wrapper holds one field, value, whose kind is that of the primitive.
    private static ValueShape OfWrapper(MessageDescriptor wrapper) =>
        new(FieldKind.Wrapper, KindByFieldType[(int)wrapper.FindFieldByNumber(1).FieldType], null);

    private static CelType TypeOf(FieldKind kind, FieldKind inner, MessageDescriptor? message)
    {
        return kind switch
        {
            FieldKind.Message => CelType.ObjectNamed(message!.FullName),
            FieldKind.Any => CelType.Any,
            FieldKind.Value => CelType.Dyn,
            FieldKind.Struct => CelType.MapOf(CelType.String, CelType.Dyn),
            FieldKind.ListValue => CelType.ListOf(CelType.Dyn),
            FieldKind.Timestamp => CelType.Timestamp,
            FieldKind.Duration => CelType.Duration,
            FieldKind.Wrapper => CelType.WrapperOf(PrimitiveType(inner)),
            _ => PrimitiveType(kind),
        };
    }

    // The CEL type of a primitive kind; an enumeration is an int.
    private static CelType PrimitiveType(FieldKind kind)
    {
        return kind switch
        {
            FieldKind.UInt32 or FieldKind.UInt64 => CelType.UInt,
            FieldKind.Float or FieldKind.Double => CelType.Double,
            FieldKind.Bool => CelType.Bool,
            FieldKind.String => CelType.String,
            FieldKind.Bytes => CelType.Bytes,
            _ => CelType.Int,
        };
    }

    private static FieldKind[] CreateKindTable()
    {
        var kinds = new FieldKind[18];
        kinds[(int)FieldType.Double] = FieldKind.Double;
        kinds[(int)FieldType.Float] = FieldKind.Float;
        kinds[(int)FieldType.Int64] = FieldKind.Int64;
        kinds[(int)FieldType.UInt64] = FieldKind.UInt64;
        kinds[(int)FieldType.Int32] = FieldKind.Int32;
        kinds[(int)FieldType.Fixed64] = FieldKind.UInt64;
        kinds[(int)FieldType.Fixed32] = FieldKind.UInt32;
        kinds[(int)FieldType.Bool] = FieldKind.Bool;
        kinds[(int)FieldType.String] = FieldKind.String;
        kinds[(int)FieldType.Group] = FieldKind.Message;
        kinds[(int)FieldType.Message] = FieldKind.Message;
        kinds[(int)FieldType.Bytes] = FieldKind.Bytes;
        kinds[(int)FieldType.UInt32] = FieldKind.UInt32;
        kinds[(int)FieldType.SFixed32] = FieldKind.Int32;
        kinds[(int)FieldType.SFixed64] = FieldKind.Int64;
        kinds[(int)FieldType.SInt32] = FieldKind.Int32;
        kinds[(int)FieldType.SInt64] = FieldKind.Int64;
        kinds[(int)FieldType.Enum] = FieldKind.Enum;
        return kinds;
    }
}

/// <summary>
/// How a field holds its values and what the CEL type of the field is: the shape of a field.
/// </summary>
internal sealed class FieldShape
{
    private FieldShape(FieldContainer container, ValueShape value, FieldKind key, CelType celType)
    {
        Container = container;
        Value = value;
        Key = key;
        CelType = celType;
    }

    /// <summary>
    /// Whether the field is single, repeated or a map.
    /// </summary>
    internal FieldContainer Container { get; }

    /// <summary>
    /// The shape of a single value, of an element of a repeated field, or of a value of a map field.
    /// </summary>
    internal ValueShape Value { get; }

    /// <summary>
    /// The kind of the key of a map field; otherwise <see cref="FieldKind.String"/>, unused.
    /// </summary>
    internal FieldKind Key { get; }

    /// <summary>
    /// The CEL type of the field.
    /// </summary>
    internal CelType CelType { get; }

    /// <summary>
    /// Finds the shape of a field of a message, or of an extension.
    /// </summary>
    /// <param name="field">The descriptor.</param>
    /// <returns>The shape.</returns>
    internal static FieldShape Of(FieldDescriptor field)
    {
        if (field.IsMap)
        {
            MessageDescriptor entry = field.MessageType;
            ValueShape key = ValueShape.Of(entry.FindFieldByNumber(1));
            ValueShape value = ValueShape.Of(entry.FindFieldByNumber(2));
            return new FieldShape(FieldContainer.Map, value, key.Kind, CelType.MapOf(key.CelType, value.CelType));
        }

        ValueShape single = ValueShape.Of(field);
        return field.IsRepeated
            ? new FieldShape(FieldContainer.Repeated, single, FieldKind.String, CelType.ListOf(single.CelType))
            : new FieldShape(FieldContainer.Single, single, FieldKind.String, single.CelType);
    }
}
