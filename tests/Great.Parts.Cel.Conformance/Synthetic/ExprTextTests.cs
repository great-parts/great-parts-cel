using System.Collections.Generic;
using Cel.Expr;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;
using EnumValue = Cel.Expr.EnumValue;
using ListValue = Cel.Expr.ListValue;
using Type = Cel.Expr.Type;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The text of values and types in the reasons of the rule: one line, the kind and the literal, the CEL name of a type.
public sealed class ExprTextTests
{
    private static readonly Type Int = new() { Primitive = Type.Types.PrimitiveType.Int64 };

    public static TheoryData<string, string> Values { get; } = new()
    {
        { "null", Text(new Value { NullValue = NullValue.NullValue }) },
        { "bool false", Text(new Value { BoolValue = false }) },
        { "int -7", Text(new Value { Int64Value = -7 }) },
        { "uint 18446744073709551615", Text(new Value { Uint64Value = ulong.MaxValue }) },
        { "double -1.5E-07", Text(new Value { DoubleValue = -1.5e-7 }) },
        { "double NaN", Text(new Value { DoubleValue = double.NaN }) },
        { "string \"a\\\"b\\\\c\\u000a\"", Text(new Value { StringValue = "a\"b\\c\n" }) },
        { "bytes b\"a\\x22\\x5c\\x00\\xff\"", Text(new Value { BytesValue = ByteString.CopyFrom(0x61, 0x22, 0x5c, 0x00, 0xff) }) },
        { "enum cel.expr.T 2", Text(new Value { EnumValue = new EnumValue { Type = "cel.expr.T", Value = 2 } }) },
        { "object type.googleapis.com/google.protobuf.Int32Value", Text(new Value { ObjectValue = Any.Pack(new Int32Value { Value = 1 }) }) },
        { "map {int 1: string \"a\"}", Text(new Value { MapValue = new MapValue { Entries = { new MapValue.Types.Entry { Key = new Value { Int64Value = 1 }, Value = new Value { StringValue = "a" } } } } }) },
        { "list [bool true, list []]", Text(new Value { ListValue = new ListValue { Values = { new Value { BoolValue = true }, new Value { ListValue = new ListValue() } } } }) },
        { "type google.protobuf.Duration", Text(new Value { TypeValue = "google.protobuf.Duration" }) },
        { "no value", Text(new Value()) },
    };

    public static TheoryData<string, string> Types { get; } = new()
    {
        { "dyn", Text(new Type { Dyn = new Empty() }) },
        { "null_type", Text(new Type { Null = NullValue.NullValue }) },
        { "bool uint double string bytes primitive 0", Primitives() },
        { "wrapper(int)", Text(new Type { Wrapper = Type.Types.PrimitiveType.Int64 }) },
        { "google.protobuf.Any", Text(new Type { WellKnown = Type.Types.WellKnownType.Any }) },
        { "google.protobuf.Timestamp", Text(new Type { WellKnown = Type.Types.WellKnownType.Timestamp }) },
        { "google.protobuf.Duration", Text(new Type { WellKnown = Type.Types.WellKnownType.Duration }) },
        { "well_known 0", Text(new Type { WellKnown = Type.Types.WellKnownType.Unspecified }) },
        { "list(int)", Text(new Type { ListType = new Type.Types.ListType { ElemType = Int } }) },
        { "map(int, dyn)", Text(new Type { MapType = new Type.Types.MapType { KeyType = Int, ValueType = new Type { Dyn = new Empty() } } }) },
        { "function(int, int) -> int", Text(new Type { Function = new Type.Types.FunctionType { ResultType = Int, ArgTypes = { Int, Int } } }) },
        { "cel.expr.conformance.proto3.TestAllTypes", Text(new Type { MessageType = "cel.expr.conformance.proto3.TestAllTypes" }) },
        { "T", Text(new Type { TypeParam = "T" }) },
        { "type(int)", Text(new Type { Type_ = Int }) },
        { "error", Text(new Type { Error = new Empty() }) },
        { "optional_type(int)", Text(new Type { AbstractType = new Type.Types.AbstractType { Name = "optional_type", ParameterTypes = { Int } } }) },
        { "vector", Text(new Type { AbstractType = new Type.Types.AbstractType { Name = "vector" } }) },
        { "no type", Text(new Type()) },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public static void Value_IsTheKindAndTheLiteral(string expected, string actual)
    {
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Types))]
    public static void Type_IsTheCelName(string expected, string actual)
    {
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Null_IsNoValueAndNoType()
    {
        Assert.Equal(("no value", "no type"), (ExprText.Of((Value?)null), ExprText.Of((Type?)null)));
    }

    [Fact]
    public void Quote_EscapesTheQuoteTheBackslashAndControlCharacters()
    {
        Assert.Equal("'it\\'s a \\\\ \\u000d\\u000a\\u0009'", ExprText.Quote("it's a \\ \r\n\t"));
    }

    private static string Text(Value value) => ExprText.Of(value);

    private static string Text(Type type) => ExprText.Of(type);

    private static string Primitives()
    {
        List<string> names = [];
        foreach (Type.Types.PrimitiveType primitive in new[] { Type.Types.PrimitiveType.Bool, Type.Types.PrimitiveType.Uint64, Type.Types.PrimitiveType.Double, Type.Types.PrimitiveType.String, Type.Types.PrimitiveType.Bytes, Type.Types.PrimitiveType.Unspecified })
        {
            names.Add(ExprText.Of(new Type { Primitive = primitive }));
        }

        return string.Join(" ", names);
    }
}
