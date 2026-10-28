using System.Globalization;
using System.Linq;
using System.Text;
using Cel.Expr;
using Google.Protobuf;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// One-line English text of values, types and messages for the reasons of the rule: a value is its kind and its literal, such as
/// <c>int 0</c> or <c>string "a"</c>; a type is its CEL name, such as <c>list(int)</c>.
/// </summary>
internal static class ExprText
{
    /// <summary>The text of a value.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    internal static string Of(Value? value) => value?.KindCase switch
    {
        Value.KindOneofCase.EnumValue => $"enum {value.EnumValue.Type} {Number(value.EnumValue.Value)}",
        Value.KindOneofCase.ObjectValue => $"object {value.ObjectValue.TypeUrl}",
        Value.KindOneofCase.MapValue => $"map {{{string.Join(", ", value.MapValue.Entries.Select(entry => $"{Of(entry.Key)}: {Of(entry.Value)}"))}}}",
        Value.KindOneofCase.ListValue => $"list [{string.Join(", ", value.ListValue.Values.Select(Of))}]",
        Value.KindOneofCase.TypeValue => $"type {value.TypeValue}",
        Value.KindOneofCase.None or null => "no value",
        _ => Scalar(value),
    };

    /// <summary>The text of a type.</summary>
    /// <param name="type">The type, or <see langword="null"/>.</param>
    internal static string Of(Type? type) => type?.TypeKindCase switch
    {
        Type.TypeKindOneofCase.ListType => $"list({Of(type.ListType.ElemType)})",
        Type.TypeKindOneofCase.MapType => $"map({Of(type.MapType.KeyType)}, {Of(type.MapType.ValueType)})",
        Type.TypeKindOneofCase.Function => $"function({string.Join(", ", type.Function.ArgTypes.Select(Of))}) -> {Of(type.Function.ResultType)}",
        Type.TypeKindOneofCase.Type_ => $"type({Of(type.Type_)})",
        Type.TypeKindOneofCase.AbstractType when type.AbstractType.ParameterTypes.Count > 0 =>
            $"{type.AbstractType.Name}({string.Join(", ", type.AbstractType.ParameterTypes.Select(Of))})",
        Type.TypeKindOneofCase.AbstractType => type.AbstractType.Name,
        Type.TypeKindOneofCase.None or null => "no type",
        _ => Simple(type),
    };

    /// <summary>A text in single quotes on one line: a quote, a backslash and the control characters are escaped.</summary>
    /// <param name="text">The text, such as an error message.</param>
    internal static string Quote(string text) => Quoted(text, '\'');

    private static string Scalar(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.NullValue => "null",
        Value.KindOneofCase.BoolValue => value.BoolValue ? "bool true" : "bool false",
        Value.KindOneofCase.Int64Value => "int " + value.Int64Value.ToString(CultureInfo.InvariantCulture),
        Value.KindOneofCase.Uint64Value => "uint " + value.Uint64Value.ToString(CultureInfo.InvariantCulture),
        Value.KindOneofCase.DoubleValue => "double " + value.DoubleValue.ToString(CultureInfo.InvariantCulture),
        Value.KindOneofCase.StringValue => "string " + Quoted(value.StringValue, '"'),
        _ => "bytes " + Bytes(value.BytesValue),
    };

    private static string Simple(Type type) => type.TypeKindCase switch
    {
        Type.TypeKindOneofCase.Dyn => "dyn",
        Type.TypeKindOneofCase.Null => "null_type",
        Type.TypeKindOneofCase.Primitive => Primitive(type.Primitive),
        Type.TypeKindOneofCase.Wrapper => $"wrapper({Primitive(type.Wrapper)})",
        Type.TypeKindOneofCase.WellKnown => type.WellKnown switch
        {
            Type.Types.WellKnownType.Any => "google.protobuf.Any",
            Type.Types.WellKnownType.Timestamp => "google.protobuf.Timestamp",
            Type.Types.WellKnownType.Duration => "google.protobuf.Duration",
            _ => "well_known " + Number((int)type.WellKnown),
        },
        Type.TypeKindOneofCase.MessageType => type.MessageType,
        Type.TypeKindOneofCase.TypeParam => type.TypeParam,
        _ => "error",
    };

    private static string Primitive(Type.Types.PrimitiveType primitive) => primitive switch
    {
        Type.Types.PrimitiveType.Bool => "bool",
        Type.Types.PrimitiveType.Int64 => "int",
        Type.Types.PrimitiveType.Uint64 => "uint",
        Type.Types.PrimitiveType.Double => "double",
        Type.Types.PrimitiveType.String => "string",
        Type.Types.PrimitiveType.Bytes => "bytes",
        _ => "primitive " + Number((int)primitive),
    };

    private static string Number(int number) => number.ToString(CultureInfo.InvariantCulture);

    private static string Quoted(string text, char quote)
    {
        StringBuilder builder = new(text.Length + 2);
        builder.Append(quote);
        foreach (char character in text)
        {
            if (character == quote || character == '\\')
            {
                builder.Append('\\').Append(character);
            }
            else if (char.IsControl(character))
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}");
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.Append(quote).ToString();
    }

    private static string Bytes(ByteString bytes)
    {
        StringBuilder builder = new("b\"", (bytes.Length * 4) + 3);
        foreach (byte octet in bytes.Span)
        {
            if (octet is >= 0x20 and < 0x7f && octet != '"' && octet != '\\')
            {
                builder.Append((char)octet);
            }
            else
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\x{octet:x2}");
            }
        }

        return builder.Append('"').ToString();
    }
}
