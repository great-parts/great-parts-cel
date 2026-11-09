using System;
using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Google.Protobuf;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Any = Google.Protobuf.WellKnownTypes.Any;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a value between <c>cel.expr.Value</c> and <see cref="CelValue"/> for the kinds the library supports, through the
/// public API of the library only: <c>eng/difftest</c> links this file. The two kinds of time are <c>object_value</c>, an
/// <c>Any</c> of <c>google.protobuf.Timestamp</c> or <c>google.protobuf.Duration</c>. Other kinds throw <see cref="NotImplementedException"/>.
/// </summary>
internal static class ValueConversion
{
    /// <summary>Converts a value of a case, such as a binding, into a value of the library.</summary>
    /// <param name="value">The value of the case.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static CelValue ToCelValue(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.NullValue => CelValue.Null,
        Value.KindOneofCase.BoolValue => CelValue.FromBoolean(value.BoolValue),
        Value.KindOneofCase.Int64Value => CelValue.FromInt64(value.Int64Value),
        Value.KindOneofCase.Uint64Value => CelValue.FromUInt64(value.Uint64Value),
        Value.KindOneofCase.DoubleValue => CelValue.FromDouble(value.DoubleValue),
        Value.KindOneofCase.StringValue => CelValue.FromString(value.StringValue),
        Value.KindOneofCase.BytesValue => CelValue.FromBytes(value.BytesValue.Memory),
        Value.KindOneofCase.ListValue => CelValue.FromList(CelList.Create([.. value.ListValue.Values.Select(ToCelValue)])),
        Value.KindOneofCase.MapValue => CelValue.FromMap(CelMap.Create(
            value.MapValue.Entries.Select(entry => KeyValuePair.Create(ToCelValue(entry.Key), ToCelValue(entry.Value))))),
        Value.KindOneofCase.TypeValue => CelValue.FromType(TypeOfName(value.TypeValue)),
        Value.KindOneofCase.ObjectValue => FromObject(value.ObjectValue),
        _ => throw new NotImplementedException(),
    };

    /// <summary>Converts a value of the library, such as the result of an evaluation, into a value of a case.</summary>
    /// <param name="value">The value of the library.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static Value ToExprValue(CelValue value)
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

        if (value.TryGetBytes(out ReadOnlyMemory<byte> bytes))
        {
            return new Value { BytesValue = ByteString.CopyFrom(bytes.Span) };
        }

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

        throw new NotImplementedException();
    }

    // A message of the two kinds of time: the langdef "Abstract Types" makes google.protobuf.Timestamp and
    // google.protobuf.Duration the CEL types timestamp and duration, so a binding of such a message is a value of the kind.
    private static CelValue FromObject(Any message)
    {
        if (message.Is(Timestamp.Descriptor))
        {
            Timestamp timestamp = message.Unpack<Timestamp>();
            return CelValue.FromTimestamp(new CelTimestamp(timestamp.Seconds, timestamp.Nanos));
        }

        if (message.Is(Duration.Descriptor))
        {
            Duration duration = message.Unpack<Duration>();
            return CelValue.FromDuration(new CelDuration(checked((duration.Seconds * 1_000_000_000L) + duration.Nanos)));
        }

        throw new NotImplementedException();
    }

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
