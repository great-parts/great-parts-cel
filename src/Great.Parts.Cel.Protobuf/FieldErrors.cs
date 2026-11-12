using Google.Protobuf;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// The error values of the fields of a message: each is made with <see cref="MessageType.Error"/> and holds the whole text of the
/// message of its code, which the evaluation gives a position.
/// </summary>
internal static class FieldErrors
{
    /// <summary>
    /// A value out of the range of the type of its field.
    /// </summary>
    /// <param name="owner">The description that has the field.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="value">The value in decimal.</param>
    /// <param name="type">The name of the type that the value does not fit.</param>
    /// <returns>The error value.</returns>
    internal static CelValue Range(MessageType owner, int number, string value, string type) =>
        owner.Error(number, CelErrorCode.ConversionRange, value + " is out of the range of " + type);

    /// <summary>
    /// A value whose kind does not fit its field.
    /// </summary>
    /// <param name="owner">The description that has the field.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="actual">The value.</param>
    /// <returns>The error value.</returns>
    internal static CelValue Mismatch(MessageType owner, int number, CelValue actual) => Mismatch(owner, number, ValueNames.Of(actual));

    /// <summary>
    /// A value whose kind does not fit its field.
    /// </summary>
    /// <param name="owner">The description that has the field.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="actual">The name of the type of the value.</param>
    /// <returns>The error value.</returns>
    internal static CelValue Mismatch(MessageType owner, int number, string actual)
    {
        CelObjectField field = owner.Fields[number];
        return owner.Error(
            number,
            CelErrorCode.FieldTypeMismatch,
            "field '" + field.Name + "' of type '" + owner.Name + "' has a value of type '" + actual + "', expected '" + field.Type + "'");
    }

    /// <summary>
    /// A value that the field cannot take for a reason that is not its kind or its range.
    /// </summary>
    /// <param name="owner">The description that has the field.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="detail">The free part of the message.</param>
    /// <returns>The error value.</returns>
    internal static CelValue InvalidArgument(MessageType owner, int number, string detail) =>
        owner.Error(number, CelErrorCode.InvalidArgument, detail);

    /// <summary>
    /// An <c>Any</c> that cannot be unpacked.
    /// </summary>
    /// <param name="owner">The description that has the field.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="typeUrl">The type URL of the <c>Any</c> that failed.</param>
    /// <param name="detail">Why it failed.</param>
    /// <returns>The error value.</returns>
    internal static CelValue InvalidAny(MessageType owner, int number, string typeUrl, string detail) =>
        owner.Error(number, CelErrorCode.InvalidAny, "cannot unpack google.protobuf.Any with type URL '" + typeUrl + "': " + detail);
}

/// <summary>
/// The names of the CEL types of values, as the messages of errors write them.
/// </summary>
internal static class ValueNames
{
    /// <summary>
    /// Gives the name of the runtime type of a value, without parameters: <c>list</c>, not <c>list(int)</c>; for an object the name of
    /// its message, or its .NET type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    internal static string Of(CelValue value)
    {
        return value.Kind switch
        {
            CelValueKind.Null => "null_type",
            CelValueKind.Bool => "bool",
            CelValueKind.Int => "int",
            CelValueKind.UInt => "uint",
            CelValueKind.Double => "double",
            CelValueKind.String => "string",
            CelValueKind.Bytes => "bytes",
            CelValueKind.List => "list",
            CelValueKind.Map => "map",
            CelValueKind.Timestamp => "timestamp",
            CelValueKind.Duration => "duration",
            CelValueKind.Type => "type",
            CelValueKind.Optional => "optional_type",
            CelValueKind.Error => "error",
            CelValueKind.Object => ObjectName(value),
            _ => "abstract",
        };
    }

    private static string ObjectName(CelValue value)
    {
        _ = value.TryGetObject(out object? instance);
        return instance is IMessage message ? message.Descriptor.FullName : instance!.GetType().ToString();
    }
}
