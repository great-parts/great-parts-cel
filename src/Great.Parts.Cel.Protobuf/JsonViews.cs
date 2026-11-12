using System;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// <c>Struct</c>, <c>Value</c> and <c>ListValue</c> read as the JSON-like maps, values and lists of CEL: views over the
/// message, which read what they are asked for and copy nothing.
/// </summary>
internal static class JsonViews
{
    private static readonly Func<Value, CelValue> ValueConverter = FromValue;

    private static readonly CelMap EmptyMap = CelMap.Create([]);

    private static readonly CelList EmptyList = CelList.Create();

    /// <summary>
    /// Reads a <c>Value</c> by the kind that is set.
    /// </summary>
    /// <param name="value">The message, or <see langword="null"/> for a field that is not set.</param>
    /// <returns><c>null</c> for no kind, a <c>null_value</c> and a field that is not set; otherwise the value of the kind.</returns>
    internal static CelValue FromValue(Value? value)
    {
        if (value is null)
        {
            return CelValue.Null;
        }

        return value.KindCase switch
        {
            Value.KindOneofCase.NumberValue => CelValue.FromDouble(value.NumberValue),
            Value.KindOneofCase.StringValue => CelValue.FromString(value.StringValue),
            Value.KindOneofCase.BoolValue => CelValue.FromBoolean(value.BoolValue),
            Value.KindOneofCase.StructValue => CelValue.FromMap(StructView(value.StructValue)),
            Value.KindOneofCase.ListValue => CelValue.FromList(ListView(value.ListValue)),
            _ => CelValue.Null,
        };
    }

    /// <summary>
    /// Reads a <c>Struct</c> as a map from string to value, in the order of insertion.
    /// </summary>
    /// <param name="value">The message, or <see langword="null"/> for a field that is not set.</param>
    /// <returns>The view, or the empty map.</returns>
    internal static CelMap StructView(Struct? value) =>
        value is null ? EmptyMap : CelMap.CreateView(value.Fields, ValueConverter, keepSourceOrder: true);

    /// <summary>
    /// Reads a <c>ListValue</c> as a list.
    /// </summary>
    /// <param name="value">The message, or <see langword="null"/> for a field that is not set.</param>
    /// <returns>The view, or the empty list.</returns>
    internal static CelList ListView(ListValue? value) =>
        value is null ? EmptyList : CelList.CreateView(value.Values, ValueConverter);
}
