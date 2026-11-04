using System;
using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Google.Protobuf;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a value between <c>cel.expr.Value</c> and <see cref="CelValue"/> for the kinds the library supports, through the
/// public API of the library only: <c>eng/difftest</c> links this file. Other kinds throw <see cref="NotImplementedException"/>.
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

        throw new NotImplementedException();
    }

    // The type value of a name, with the parameters erased as at evaluation; the names of the time types throw.
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
        _ => throw new NotImplementedException(),
    };
}
