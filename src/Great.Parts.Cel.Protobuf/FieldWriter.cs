using System;
using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Converts a CEL value to the value of a field by the rows of the literal table: the value is the boxed object that the field
/// accessor, or the typed extension object, takes.
/// </summary>
internal sealed class FieldWriter
{
    private const long NanosecondsPerSecond = 1_000_000_000;

    private readonly ValueShape _shape;
    private readonly MessageType _owner;
    private readonly int _number;

    /// <summary>
    /// Creates the writer of one field.
    /// </summary>
    /// <param name="shape">The shape of a value of the field.</param>
    /// <param name="owner">The description of the message that has the field.</param>
    /// <param name="number">The number of the field in the description.</param>
    internal FieldWriter(ValueShape shape, MessageType owner, int number)
    {
        _shape = shape;
        _owner = owner;
        _number = number;
    }

    /// <summary>
    /// Converts a value of a single field, or an element or a value of a collection.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="inContainer"><see langword="true"/> for an element of a <c>repeated</c> field or a value of a <c>map</c> field.</param>
    /// <param name="result">The converted value; <see langword="null"/> when the field is left as it is, or the element is left out.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the value fits the field.</returns>
    internal bool TryConvert(CelValue value, bool inContainer, out object? result, out CelValue error)
    {
        result = null;
        error = default;
        if (value.Kind == CelValueKind.Error)
        {
            error = value;
            return false;
        }

        if (value.Kind == CelValueKind.Null)
        {
            return TryConvertNull(inContainer, out result, out error);
        }

        switch (_shape.Kind)
        {
            case FieldKind.Message:
                return TryMessage(value, out result, out error);
            case FieldKind.Any:
                bool packed = AnyPacker.TryPack(value, _owner, _number, out Any? any, out error);
                result = any;
                return packed;
            case FieldKind.Value:
                bool written = JsonWriter.TryValue(value, _owner, _number, out Value? json, out error);
                result = json;
                return written;
            case FieldKind.Struct:
                return TryStruct(value, out result, out error);
            case FieldKind.ListValue:
                return TryListValue(value, out result, out error);
            case FieldKind.Timestamp:
                return TryTimestamp(value, out result, out error);
            case FieldKind.Duration:
                return TryDuration(value, out result, out error);
            case FieldKind.Wrapper:
                return TryPrimitive(_shape.Inner, value, out result, out error);
            default:
                return TryPrimitive(_shape.Kind, value, out result, out error);
        }
    }

    /// <summary>
    /// Converts the key of a <c>map</c> field.
    /// </summary>
    /// <param name="kind">The kind of the key: an integer, <c>bool</c> or <c>string</c>.</param>
    /// <param name="key">The key.</param>
    /// <param name="result">The converted key.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the key fits.</returns>
    internal bool TryConvertKey(FieldKind kind, CelValue key, out object? result, out CelValue error)
    {
        if (key.Kind == CelValueKind.Error)
        {
            result = null;
            error = key;
            return false;
        }

        return TryPrimitive(kind, key, out result, out error);
    }

    // A null leaves a message, a Timestamp, a Duration and a wrapper as they are, and in a collection leaves the element out, except a
    // message; an Any and a Value hold null.
    private bool TryConvertNull(bool inContainer, out object? result, out CelValue error)
    {
        result = null;
        error = default;
        switch (_shape.Kind)
        {
            case FieldKind.Timestamp:
            case FieldKind.Duration:
            case FieldKind.Wrapper:
                return true;
            case FieldKind.Message when !inContainer:
                return true;
            case FieldKind.Any:
                result = Any.Pack(Value.ForNull());
                return true;
            case FieldKind.Value:
                result = Value.ForNull();
                return true;
            default:
                error = FieldErrors.Mismatch(_owner, _number, "null_type");
                return false;
        }
    }

    // An object that is an instance of the class of the message of the field.
    private bool TryMessage(CelValue value, out object? result, out CelValue error)
    {
        error = default;
        if (value.Kind == CelValueKind.Object
            && value.TryGetObject(out object? instance)
            && instance is IMessage message
            && ReferenceEquals(message.Descriptor, _shape.Message))
        {
            result = message;
            return true;
        }

        result = null;
        error = FieldErrors.Mismatch(_owner, _number, value);
        return false;
    }

    private bool TryStruct(CelValue value, out object? result, out CelValue error)
    {
        result = null;
        if (!value.TryGetMap(out CelMap? map))
        {
            error = FieldErrors.Mismatch(_owner, _number, value);
            return false;
        }

        bool written = JsonWriter.TryStruct(map, _owner, _number, out Struct? structure, out error);
        result = structure;
        return written;
    }

    private bool TryListValue(CelValue value, out object? result, out CelValue error)
    {
        result = null;
        if (!value.TryGetList(out CelList? list))
        {
            error = FieldErrors.Mismatch(_owner, _number, value);
            return false;
        }

        bool written = JsonWriter.TryList(list, _owner, _number, out ListValue? listValue, out error);
        result = listValue;
        return written;
    }

    private bool TryTimestamp(CelValue value, out object? result, out CelValue error)
    {
        error = default;
        if (value.TryGetTimestamp(out CelTimestamp timestamp))
        {
            result = new Timestamp { Seconds = timestamp.Seconds, Nanos = timestamp.Nanoseconds };
            return true;
        }

        result = null;
        error = FieldErrors.Mismatch(_owner, _number, value);
        return false;
    }

    // The seconds and the nanoseconds of a duration have the sign of the duration.
    private bool TryDuration(CelValue value, out object? result, out CelValue error)
    {
        error = default;
        if (value.TryGetDuration(out CelDuration duration))
        {
            result = new Duration
            {
                Seconds = duration.TotalNanoseconds / NanosecondsPerSecond,
                Nanos = (int)(duration.TotalNanoseconds % NanosecondsPerSecond),
            };
            return true;
        }

        result = null;
        error = FieldErrors.Mismatch(_owner, _number, value);
        return false;
    }

    // The primitive kinds: a 32-bit integer is range checked, a float takes the nearest float, bytes are copied.
    private bool TryPrimitive(FieldKind kind, CelValue value, out object? result, out CelValue error)
    {
        result = null;
        error = default;
        switch (kind)
        {
            case FieldKind.Int32:
            case FieldKind.Enum:
                return TryInt32(value, out result, out error);
            case FieldKind.Int64 when value.TryGetInt64(out long int64):
                result = int64;
                return true;
            case FieldKind.UInt32:
                return TryUInt32(value, out result, out error);
            case FieldKind.UInt64 when value.TryGetUInt64(out ulong uint64):
                result = uint64;
                return true;
            case FieldKind.Float when value.TryGetDouble(out double single):
                result = (float)single;
                return true;
            case FieldKind.Double when value.TryGetDouble(out double number):
                result = number;
                return true;
            case FieldKind.Bool when value.TryGetBoolean(out bool boolean):
                result = boolean;
                return true;
            case FieldKind.String when value.TryGetString(out string? text):
                result = text;
                return true;
            case FieldKind.Bytes when value.TryGetBytes(out ReadOnlyMemory<byte> bytes):
                result = ByteString.CopyFrom(bytes.Span);
                return true;
            default:
                error = FieldErrors.Mismatch(_owner, _number, value);
                return false;
        }
    }

    private bool TryInt32(CelValue value, out object? result, out CelValue error)
    {
        result = null;
        error = default;
        if (!value.TryGetInt64(out long number))
        {
            error = FieldErrors.Mismatch(_owner, _number, value);
            return false;
        }

        if (number is < int.MinValue or > int.MaxValue)
        {
            error = FieldErrors.Range(_owner, _number, number.ToString(CultureInfo.InvariantCulture), "int32");
            return false;
        }

        result = (int)number;
        return true;
    }

    private bool TryUInt32(CelValue value, out object? result, out CelValue error)
    {
        result = null;
        error = default;
        if (!value.TryGetUInt64(out ulong number))
        {
            error = FieldErrors.Mismatch(_owner, _number, value);
            return false;
        }

        if (number > uint.MaxValue)
        {
            error = FieldErrors.Range(_owner, _number, number.ToString(CultureInfo.InvariantCulture), "uint32");
            return false;
        }

        result = (uint)number;
        return true;
    }
}
