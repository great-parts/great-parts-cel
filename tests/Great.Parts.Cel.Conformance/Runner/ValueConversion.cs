using System;
using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Google.Protobuf;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Any = Google.Protobuf.WellKnownTypes.Any;
using BoolValue = Google.Protobuf.WellKnownTypes.BoolValue;
using BytesValue = Google.Protobuf.WellKnownTypes.BytesValue;
using DoubleValue = Google.Protobuf.WellKnownTypes.DoubleValue;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using FloatValue = Google.Protobuf.WellKnownTypes.FloatValue;
using Int32Value = Google.Protobuf.WellKnownTypes.Int32Value;
using Int64Value = Google.Protobuf.WellKnownTypes.Int64Value;
using JsonList = Google.Protobuf.WellKnownTypes.ListValue;
using JsonStruct = Google.Protobuf.WellKnownTypes.Struct;
using JsonValue = Google.Protobuf.WellKnownTypes.Value;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;
using StringValue = Google.Protobuf.WellKnownTypes.StringValue;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using UInt32Value = Google.Protobuf.WellKnownTypes.UInt32Value;
using UInt64Value = Google.Protobuf.WellKnownTypes.UInt64Value;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a value between <c>cel.expr.Value</c> and <see cref="CelValue"/> through the public API of the library only:
/// <c>eng/difftest</c> links this file. An <c>object_value</c> is an <c>Any</c>. The known types of protobuf are converted here,
/// by the classes of <c>Google.Protobuf.WellKnownTypes</c>, which both projects have: the two kinds of time, the nine wrappers,
/// <c>Struct</c>, <c>ListValue</c> and <c>Value</c>, and an <c>Any</c> that holds an <c>Any</c> is unpacked again. Any other message
/// is converted by <see cref="MessageResolver"/>, which the runner sets; the stand of <c>eng/difftest</c> has none, so a message
/// there throws <see cref="NotImplementedException"/>.
/// </summary>
internal static class ValueConversion
{
    /// <summary>
    /// Turns an <c>Any</c> of a message that is not a known type into an object value, or gives <see langword="null"/> for a message it
    /// does not know. It is <see langword="null"/> until the runner sets it.
    /// </summary>
    internal static Func<Any, CelValue?>? MessageResolver { get; set; }

    /// <summary>Converts a value of a case, such as a binding, into a value of the library.</summary>
    /// <param name="value">The value of the case.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static CelValue ToCelValue(Value value) => ToCelValue(value, MessageResolver);

    /// <summary>Converts a value of a case into a value of the library, with a given resolver of messages.</summary>
    /// <param name="value">The value of the case.</param>
    /// <param name="resolver">Turns an <c>Any</c> of a message that is not a known type into an object value; <see langword="null"/> for none.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static CelValue ToCelValue(Value value, Func<Any, CelValue?>? resolver) => value.KindCase switch
    {
        Value.KindOneofCase.NullValue => CelValue.Null,
        Value.KindOneofCase.BoolValue => CelValue.FromBoolean(value.BoolValue),
        Value.KindOneofCase.Int64Value => CelValue.FromInt64(value.Int64Value),
        Value.KindOneofCase.Uint64Value => CelValue.FromUInt64(value.Uint64Value),
        Value.KindOneofCase.DoubleValue => CelValue.FromDouble(value.DoubleValue),
        Value.KindOneofCase.StringValue => CelValue.FromString(value.StringValue),
        Value.KindOneofCase.BytesValue => CelValue.FromBytes(value.BytesValue.Memory),
        Value.KindOneofCase.ListValue => CelValue.FromList(CelList.Create([.. value.ListValue.Values.Select(element => ToCelValue(element, resolver))])),
        Value.KindOneofCase.MapValue => CelValue.FromMap(CelMap.Create(
            value.MapValue.Entries.Select(entry => KeyValuePair.Create(ToCelValue(entry.Key, resolver), ToCelValue(entry.Value, resolver))))),
        Value.KindOneofCase.TypeValue => CelValue.FromType(TypeOfName(value.TypeValue)),
        Value.KindOneofCase.ObjectValue => FromObject(value.ObjectValue, resolver),
        _ => throw new NotImplementedException(),
    };

    /// <summary>Converts a value of the library, such as the result of an evaluation, into a value of a case.</summary>
    /// <param name="value">The value of the library.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static Value ToExprValue(CelValue value) => ToExprScalar(value) ?? ToExprComposite(value);

    // The values that are one scalar of cel.expr.Value; null for any other kind.
    private static Value? ToExprScalar(CelValue value)
    {
        if (value.Kind == CelValueKind.Null)
        {
            return new Value { NullValue = NullValue.NullValue };
        }

        if (value.TryGetBoolean(out bool boolValue))
        {
            return new Value { BoolValue = boolValue };
        }

        if (value.TryGetInt64(out long intValue))
        {
            return new Value { Int64Value = intValue };
        }

        if (value.TryGetUInt64(out ulong uintValue))
        {
            return new Value { Uint64Value = uintValue };
        }

        if (value.TryGetDouble(out double doubleValue))
        {
            return new Value { DoubleValue = doubleValue };
        }

        if (value.TryGetString(out string? text))
        {
            return new Value { StringValue = text };
        }

        return value.TryGetBytes(out ReadOnlyMemory<byte> bytes) ? new Value { BytesValue = ByteString.CopyFrom(bytes.Span) } : null;
    }

    private static Value ToExprComposite(CelValue value)
    {
        if (value.TryGetList(out CelList? list))
        {
            return new Value { ListValue = new ListValue { Values = { list.Select(ToExprValue) } } };
        }

        if (value.TryGetMap(out CelMap? map))
        {
            return new Value
            {
                MapValue = new MapValue
                {
                    Entries = { map.Select(entry => new MapValue.Types.Entry { Key = ToExprValue(entry.Key), Value = ToExprValue(entry.Value) }) },
                },
            };
        }

        if (value.TryGetType(out CelType? type))
        {
            // A type value is named as type() gives it: its parameters are erased at evaluation.
            return new Value { TypeValue = type.Name };
        }

        if (value.TryGetTimestamp(out CelTimestamp timestamp))
        {
            return new Value { ObjectValue = Any.Pack(new Timestamp { Seconds = timestamp.Seconds, Nanos = timestamp.Nanoseconds }) };
        }

        if (value.TryGetDuration(out CelDuration duration))
        {
            return new Value { ObjectValue = Any.Pack(ToProtobuf(duration)) };
        }

        // A message of a case is an object value; the table of the runner unpacks it again where it compares.
        if (value.TryGetObject(out object? instance) && instance is IMessage message)
        {
            return new Value { ObjectValue = Any.Pack(message) };
        }

        // cel.expr.Value has no optional: a result of that kind, like any other that has no row, names its kind.
        throw new NotImplementedException("no row for a value of the kind " + value.Kind);
    }

    // An Any that holds an Any is unpacked until its content is no Any: the case dynamic/any/var binds such a chain. The known
    // types are converted by their classes, any other message by the resolver.
    private static CelValue FromObject(Any message, Func<Any, CelValue?>? resolver)
    {
        while (message.Is(Any.Descriptor))
        {
            message = message.Unpack<Any>();
        }

        string name = TypeName(message.TypeUrl);
        return FromTime(message, name) ?? FromWrapper(message, name) ?? FromJson(message, name) ?? resolver?.Invoke(message) ?? throw new NotImplementedException();
    }

    // A message of the two kinds of time: the langdef "Abstract Types" makes google.protobuf.Timestamp and
    // google.protobuf.Duration the CEL types timestamp and duration, so a binding of such a message is a value of the kind.
    private static CelValue? FromTime(Any message, string name)
    {
        if (name == Timestamp.Descriptor.FullName)
        {
            Timestamp timestamp = message.Unpack<Timestamp>();
            return CelValue.FromTimestamp(new CelTimestamp(timestamp.Seconds, timestamp.Nanos));
        }

        if (name == Duration.Descriptor.FullName)
        {
            Duration duration = message.Unpack<Duration>();
            return CelValue.FromDuration(new CelDuration(checked((duration.Seconds * 1_000_000_000L) + duration.Nanos)));
        }

        return null;
    }

    // The nine wrappers are the value they wrap: the integers of 32 bits widen, a float widens to a double.
    private static CelValue? FromWrapper(Any message, string name) => name switch
    {
        "google.protobuf.Int32Value" => CelValue.FromInt64(message.Unpack<Int32Value>().Value),
        "google.protobuf.Int64Value" => CelValue.FromInt64(message.Unpack<Int64Value>().Value),
        "google.protobuf.UInt32Value" => CelValue.FromUInt64(message.Unpack<UInt32Value>().Value),
        "google.protobuf.UInt64Value" => CelValue.FromUInt64(message.Unpack<UInt64Value>().Value),
        "google.protobuf.FloatValue" => CelValue.FromDouble(message.Unpack<FloatValue>().Value),
        "google.protobuf.DoubleValue" => CelValue.FromDouble(message.Unpack<DoubleValue>().Value),
        "google.protobuf.BoolValue" => CelValue.FromBoolean(message.Unpack<BoolValue>().Value),
        "google.protobuf.StringValue" => CelValue.FromString(message.Unpack<StringValue>().Value),
        "google.protobuf.BytesValue" => CelValue.FromBytes(message.Unpack<BytesValue>().Value.Memory),
        _ => null,
    };

    // Struct is a map, ListValue a list and Value the kind that is set.
    private static CelValue? FromJson(Any message, string name) => name switch
    {
        "google.protobuf.Struct" => FromStruct(message.Unpack<JsonStruct>()),
        "google.protobuf.ListValue" => FromList(message.Unpack<JsonList>()),
        "google.protobuf.Value" => FromJsonValue(message.Unpack<JsonValue>()),
        _ => null,
    };

    private static CelValue FromStruct(JsonStruct message) =>
        CelValue.FromMap(CelMap.Create(message.Fields.Select(field => KeyValuePair.Create(CelValue.FromString(field.Key), FromJsonValue(field.Value)))));

    private static CelValue FromList(JsonList message) =>
        CelValue.FromList(CelList.Create([.. message.Values.Select(FromJsonValue)]));

    // A Value with no kind set is null, as the null_value member is.
    private static CelValue FromJsonValue(JsonValue value) => value.KindCase switch
    {
        JsonValue.KindOneofCase.NumberValue => CelValue.FromDouble(value.NumberValue),
        JsonValue.KindOneofCase.StringValue => CelValue.FromString(value.StringValue),
        JsonValue.KindOneofCase.BoolValue => CelValue.FromBoolean(value.BoolValue),
        JsonValue.KindOneofCase.StructValue => FromStruct(value.StructValue),
        JsonValue.KindOneofCase.ListValue => FromList(value.ListValue),
        _ => CelValue.Null,
    };

    // The full name of the message of a type URL: the text after the last slash.
    private static string TypeName(string typeUrl) => typeUrl[(typeUrl.AsSpan().LastIndexOf('/') + 1)..];

    // A duration with the seconds and the nanoseconds of the same sign, as the protobuf rule for Duration asks.
    private static Duration ToProtobuf(CelDuration duration) => new()
    {
        Seconds = duration.TotalNanoseconds / 1_000_000_000L,
        Nanos = (int)(duration.TotalNanoseconds % 1_000_000_000L),
    };

    // The type value of a name, with the parameters erased as at evaluation; the time types are read by their full names.
    private static CelType TypeOfName(string name) => name switch
    {
        "null_type" => CelType.Null,
        "bool" => CelType.Bool,
        "int" => CelType.Int,
        "uint" => CelType.UInt,
        "double" => CelType.Double,
        "string" => CelType.String,
        "bytes" => CelType.Bytes,
        "list" => CelType.ListOf(CelType.Dyn),
        "map" => CelType.MapOf(CelType.Dyn, CelType.Dyn),
        "type" => CelType.TypeOf(CelType.Dyn),
        "google.protobuf.Timestamp" => CelType.Timestamp,
        "google.protobuf.Duration" => CelType.Duration,
        _ => throw new NotImplementedException(),
    };
}
