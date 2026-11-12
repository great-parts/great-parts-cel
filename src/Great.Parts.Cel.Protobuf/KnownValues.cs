using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// What a message of the known types converts to: the value of a wrapper, the JSON-like value of <c>Value</c>, <c>Struct</c> and
/// <c>ListValue</c>, the content of an <c>Any</c>, the time of <c>Timestamp</c> and <c>Duration</c>. Any other message is a bare
/// object.
/// </summary>
internal static class KnownValues
{
    // The range of the seconds of a timestamp, 0001-01-01T00:00:00Z to 9999-12-31T23:59:59Z.
    private const long MinTimestampSeconds = -62_135_596_800;
    private const long MaxTimestampSeconds = 253_402_300_799;

    private const long NanosecondsPerSecond = 1_000_000_000;

    /// <summary>
    /// Converts a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="owner">The description that errors name, which has the field or the literal that made the message.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <returns>The value; an error value for an <c>Any</c> that cannot be unpacked and for a time out of range.</returns>
    internal static CelValue FromMessage(IMessage message, MessageType owner, int number)
    {
        switch (message)
        {
            case Value value:
                return JsonViews.FromValue(value);
            case Struct structure:
                return CelValue.FromMap(JsonViews.StructView(structure));
            case ListValue list:
                return CelValue.FromList(JsonViews.ListView(list));
            case Any any:
                return AnyUnpacker.Unpack(owner, number, any);
            case Timestamp timestamp:
                return ToTimestamp(timestamp, owner, number);
            case Duration duration:
                return ToDuration(duration, owner, number);
            default:
                return FromWrapper(message);
        }
    }

    // The value of a wrapper; any other message is a bare object.
    private static CelValue FromWrapper(IMessage message)
    {
        switch (message)
        {
            case Int32Value int32:
                return CelValue.FromInt64(int32.Value);
            case Int64Value int64:
                return CelValue.FromInt64(int64.Value);
            case UInt32Value uint32:
                return CelValue.FromUInt64(uint32.Value);
            case UInt64Value uint64:
                return CelValue.FromUInt64(uint64.Value);
            case FloatValue single:
                return CelValue.FromDouble(single.Value);
            case DoubleValue number:
                return CelValue.FromDouble(number.Value);
            case BoolValue boolean:
                return CelValue.FromBoolean(boolean.Value);
            case StringValue text:
                return CelValue.FromString(text.Value);
            case BytesValue bytes:
                return CelValue.FromBytes(bytes.Value.Memory);
            default:
                return MessageType.Bare(message);
        }
    }

    /// <summary>
    /// Reads a <c>Timestamp</c>. A field that is not set reads as the zero timestamp. Seconds out of the range of a timestamp are
    /// reported first, then nanoseconds.
    /// </summary>
    /// <param name="timestamp">The message, or <see langword="null"/> for a field that is not set.</param>
    /// <param name="owner">The description that errors name.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <returns>The time, or <c>ConversionRange</c>.</returns>
    internal static CelValue ToTimestamp(Timestamp? timestamp, MessageType owner, int number)
    {
        if (timestamp is null)
        {
            return CelValue.FromTimestamp(default);
        }

        if (timestamp.Seconds is < MinTimestampSeconds or > MaxTimestampSeconds)
        {
            return FieldErrors.Range(owner, number, Decimal(timestamp.Seconds), "timestamp");
        }

        if (timestamp.Nanos is < 0 or > 999_999_999)
        {
            return FieldErrors.Range(owner, number, Decimal(timestamp.Nanos), "timestamp");
        }

        return CelValue.FromTimestamp(new CelTimestamp(timestamp.Seconds, timestamp.Nanos));
    }

    /// <summary>
    /// Reads a <c>Duration</c>. A field that is not set reads as the zero duration. Nanoseconds that are out of range or of the other
    /// sign than the seconds are reported by the nanoseconds; a total that does not fit a 64-bit number, by the seconds.
    /// </summary>
    /// <param name="duration">The message, or <see langword="null"/> for a field that is not set.</param>
    /// <param name="owner">The description that errors name.</param>
    /// <param name="number">The number of the field that errors name.</param>
    /// <returns>The time, or <c>ConversionRange</c>.</returns>
    internal static CelValue ToDuration(Duration? duration, MessageType owner, int number)
    {
        if (duration is null)
        {
            return CelValue.FromDuration(default);
        }

        long seconds = duration.Seconds;
        int nanos = duration.Nanos;
        if (nanos is < -999_999_999 or > 999_999_999 || (seconds > 0 && nanos < 0) || (seconds < 0 && nanos > 0))
        {
            return FieldErrors.Range(owner, number, Decimal(nanos), "duration");
        }

        return TryTotal(seconds, nanos, out long total)
            ? CelValue.FromDuration(new CelDuration(total))
            : FieldErrors.Range(owner, number, Decimal(seconds), "duration");
    }

    private static string Decimal(long value) => value.ToString(CultureInfo.InvariantCulture);

    // The nanoseconds of a duration, or false when they do not fit a 64-bit number.
    private static bool TryTotal(long seconds, int nanos, out long total)
    {
        total = 0;
        if (seconds is > long.MaxValue / NanosecondsPerSecond or < long.MinValue / NanosecondsPerSecond)
        {
            return false;
        }

        long scaled = seconds * NanosecondsPerSecond;
        if ((nanos > 0 && scaled > long.MaxValue - nanos) || (nanos < 0 && scaled < long.MinValue - nanos))
        {
            return false;
        }

        total = scaled + nanos;
        return true;
    }
}
