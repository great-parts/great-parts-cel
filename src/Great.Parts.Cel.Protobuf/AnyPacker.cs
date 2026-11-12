using System;
using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Packs a CEL value into a <c>google.protobuf.Any</c>: a primitive into its wrapper, a time into <c>Timestamp</c> or <c>Duration</c>,
/// a list into <c>ListValue</c>, a map into <c>Struct</c>, <c>null</c> into a <c>Value</c> holding <c>NULL_VALUE</c>, a message as it is,
/// under the type URL <c>type.googleapis.com/&lt;full name&gt;</c>.
/// </summary>
internal static class AnyPacker
{
    private const long NanosecondsPerSecond = 1_000_000_000;

    /// <summary>
    /// Packs a value.
    /// </summary>
    /// <param name="value">The value, not an error value.</param>
    /// <param name="owner">The description that errors name.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <param name="result">The <c>Any</c> when the method returns <see langword="true"/>.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the value can be packed.</returns>
    internal static bool TryPack(CelValue value, MessageType owner, int number, [NotNullWhen(true)] out Any? result, out CelValue error)
    {
        error = default;
        result = null;
        switch (value.Kind)
        {
            case CelValueKind.Error:
                error = value;
                return false;
            case CelValueKind.List:
                _ = value.TryGetList(out CelList? list);
                if (!JsonWriter.TryList(list!, owner, number, out ListValue? listValue, out error))
                {
                    return false;
                }

                result = Any.Pack(listValue);
                return true;
            case CelValueKind.Map:
                _ = value.TryGetMap(out CelMap? map);
                if (!JsonWriter.TryStruct(map!, owner, number, out Struct? structure, out error))
                {
                    return false;
                }

                result = Any.Pack(structure);
                return true;
            default:
                IMessage? message = Wrapped(value);
                if (message is null)
                {
                    error = FieldErrors.Mismatch(owner, number, value);
                    return false;
                }

                result = Any.Pack(message);
                return true;
        }
    }

    // The message that holds a value that is not a collection; null for a kind that cannot be packed.
    private static IMessage? Wrapped(CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Null:
                return Value.ForNull();
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool boolean);
                return new BoolValue { Value = boolean };
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long integer);
                return new Int64Value { Value = integer };
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                return new UInt64Value { Value = unsigned };
            case CelValueKind.Double:
                _ = value.TryGetDouble(out double number);
                return new DoubleValue { Value = number };
            case CelValueKind.String:
                _ = value.TryGetString(out string? text);
                return new StringValue { Value = text! };
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                return new BytesValue { Value = ByteString.CopyFrom(bytes.Span) };
            case CelValueKind.Timestamp:
                _ = value.TryGetTimestamp(out CelTimestamp timestamp);
                return new Timestamp { Seconds = timestamp.Seconds, Nanos = timestamp.Nanoseconds };
            case CelValueKind.Duration:
                _ = value.TryGetDuration(out CelDuration duration);
                return new Duration
                {
                    Seconds = duration.TotalNanoseconds / NanosecondsPerSecond,
                    Nanos = (int)(duration.TotalNanoseconds % NanosecondsPerSecond),
                };
            case CelValueKind.Object:
                _ = value.TryGetObject(out object? instance);
                return instance as IMessage;
            default:
                return null;
        }
    }
}
