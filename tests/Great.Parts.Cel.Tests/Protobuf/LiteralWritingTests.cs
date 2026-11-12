using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// The writing of the fields of a message by an object literal, beyond the rows that the examples fix: the text of the range error for
// each 32-bit kind and for a key, the narrowing of a float, a wrapper that holds the default, a null in a repeated field and a map
// field for each element kind, the rows of a Value for the boundaries of the integers and for each kind of value, an object written
// into a Value as canonical JSON, the type URL that each kind of value gets in an Any, the text of a kind mismatch, a message that is
// put into a field shared and not copied, and a value nested too deeply.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "R-1")]
public sealed class LiteralWritingTests
{
    private const string Type = "cel.expr.conformance.proto3.TestAllTypes";

    [Theory]
    [InlineData("TestAllTypes{single_int32: 2147483648}", "2147483648 is out of the range of int32")]
    [InlineData("TestAllTypes{single_sint32: -2147483649}", "-2147483649 is out of the range of int32")]
    [InlineData("TestAllTypes{single_sfixed32: 2147483648}", "2147483648 is out of the range of int32")]
    [InlineData("TestAllTypes{single_uint32: 4294967296u}", "4294967296 is out of the range of uint32")]
    [InlineData("TestAllTypes{single_fixed32: 4294967296u}", "4294967296 is out of the range of uint32")]
    [InlineData("TestAllTypes{standalone_enum: 2147483648}", "2147483648 is out of the range of int32")]
    [InlineData("TestAllTypes{repeated_nested_enum: [-2147483649]}", "-2147483649 is out of the range of int32")]
    [InlineData("TestAllTypes{map_int32_bool: {2147483648: true}}", "2147483648 is out of the range of int32")]
    [InlineData("TestAllTypes{map_uint32_string: {4294967296u: 'a'}}", "4294967296 is out of the range of uint32")]
    [InlineData("TestAllTypes{single_int32_wrapper: -2147483649}", "-2147483649 is out of the range of int32")]
    [InlineData("TestAllTypes{single_uint32_wrapper: 4294967296u}", "4294967296 is out of the range of uint32")]
    public static void RangeError_NamesTheValueAndTheTypeOfTheField(string expression, string text)
    {
        CelError error = Reading.ReadError(expression);

        Assert.Equal(CelErrorCode.ConversionRange, error.Code);
        Assert.Equal(text, error.Message);
    }

    [Fact]
    public static void BoundaryValues_AreWrittenWithoutAnError()
    {
        Assert.True(Reading.IsTrue("TestAllTypes{single_sint32: -2147483648}.single_sint32 == -2147483648"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_sfixed32: 2147483647}.single_sfixed32 == 2147483647"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_fixed32: 4294967295u}.single_fixed32 == 4294967295u"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_int32_wrapper: -2147483648}.single_int32_wrapper == -2147483648"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_uint32_wrapper: 4294967295u}.single_uint32_wrapper == 4294967295u"));
        Assert.True(Reading.IsTrue("TestAllTypes{map_int32_bool: {-2147483648: true, 2147483647: false}}.map_int32_bool[2147483647] == false"));
        Assert.True(Reading.IsTrue("TestAllTypes{repeated_uint32: [0u, 4294967295u]}.repeated_uint32[1] == 4294967295u"));
    }

    // A float field takes the nearest float: a value too small is 0, a value too large is an infinity, a NaN stays a NaN, and none of
    // them is an error.
    [Fact]
    public static void FloatField_TakesTheNearestFloat()
    {
        Assert.True(Reading.IsTrue("TestAllTypes{single_float: 1e-50}.single_float == 0.0"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_float: 1.4e55}.single_float > 1.7976931348623157e308"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_float: -1.4e55}.single_float < -1.7976931348623157e308"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_float: double('NaN')}.single_float != double('NaN')"));
        Assert.True(Reading.IsTrue("TestAllTypes{repeated_float: [1.4e55]}.repeated_float[0] > 1.7976931348623157e308"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_float_wrapper: 0.1}.single_float_wrapper == TestAllTypes{single_float: 0.1}.single_float"));
    }

    // A wrapper that is set to the default is present, and an unset one is not.
    [Fact]
    public static void WrapperThatHoldsTheDefault_IsPresent()
    {
        Assert.True(Reading.IsTrue("has(TestAllTypes{single_uint64_wrapper: 0u}.single_uint64_wrapper)"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_uint64_wrapper: 0u}.single_uint64_wrapper == 0u"));
        Assert.True(Reading.IsTrue("has(TestAllTypes{single_string_wrapper: ''}.single_string_wrapper)"));
        Assert.True(Reading.IsTrue("has(TestAllTypes{single_bool_wrapper: false}.single_bool_wrapper)"));
        Assert.False(Reading.IsTrue("has(TestAllTypes{single_uint64_wrapper: null}.single_uint64_wrapper)"));
    }

    // In a repeated or a map field, an element that is null is left out for Timestamp, Duration and the wrappers, and is kept as a null
    // for Any and Value; for any other element type it is a type mismatch. The list is a dyn value so that type checking lets it pass.
    [Fact]
    public static void NullInAContainer_IsLeftOutOrKeptByTheKindOfTheElement()
    {
        Assert.True(Reading.IsTrue("size(TestAllTypes{repeated_int32_wrapper: dyn([1, null, 3])}.repeated_int32_wrapper) == 2"));
        Assert.True(Reading.IsTrue("TestAllTypes{repeated_int32_wrapper: dyn([1, null, 3])}.repeated_int32_wrapper[1] == 3"));
        Assert.True(Reading.IsTrue("size(TestAllTypes{repeated_timestamp: dyn([timestamp(1), null])}.repeated_timestamp) == 1"));
        Assert.True(Reading.IsTrue("size(TestAllTypes{repeated_duration: dyn([null, duration('1s')])}.repeated_duration) == 1"));
        Assert.True(Reading.IsTrue("size(TestAllTypes{map_string_int64_wrapper: dyn({'a': 1, 'b': null})}.map_string_int64_wrapper) == 1"));
        Assert.False(Reading.IsTrue("'b' in TestAllTypes{map_string_int64_wrapper: dyn({'a': 1, 'b': null})}.map_string_int64_wrapper"));
        Assert.True(Reading.IsTrue("size(TestAllTypes{repeated_any: dyn([null])}.repeated_any) == 1"));
        Assert.True(Reading.IsTrue("TestAllTypes{repeated_any: dyn([null])}.repeated_any[0] == null"));
        Assert.True(Reading.IsTrue("size(TestAllTypes{map_string_value: dyn({'k': null})}.map_string_value) == 1"));
        Assert.True(Reading.IsTrue("TestAllTypes{map_string_value: dyn({'k': null})}.map_string_value['k'] == null"));
    }

    [Theory]
    [InlineData("TestAllTypes{repeated_int32: dyn([1, null])}", "repeated_int32", "null_type", "list(int)")]
    [InlineData("TestAllTypes{repeated_nested_message: dyn([null])}", "repeated_nested_message", "null_type", "list(cel.expr.conformance.proto3.TestAllTypes.NestedMessage)")]
    [InlineData("TestAllTypes{single_int32: dyn('a')}", "single_int32", "string", "int")]
    [InlineData("TestAllTypes{single_string: dyn(1)}", "single_string", "int", "string")]
    [InlineData("TestAllTypes{single_uint32: dyn(1)}", "single_uint32", "int", "uint")]
    [InlineData("TestAllTypes{single_float: dyn(1)}", "single_float", "int", "double")]
    [InlineData("TestAllTypes{single_bytes: dyn('a')}", "single_bytes", "string", "bytes")]
    [InlineData("TestAllTypes{single_timestamp: dyn(duration('1s'))}", "single_timestamp", "duration", "timestamp")]
    [InlineData("TestAllTypes{single_int64_wrapper: dyn('a')}", "single_int64_wrapper", "string", "wrapper(int)")]
    [InlineData("TestAllTypes{repeated_int32: dyn(1)}", "repeated_int32", "int", "list(int)")]
    [InlineData("TestAllTypes{map_string_string: dyn([1])}", "map_string_string", "list", "map(string, string)")]
    [InlineData("TestAllTypes{single_int32: dyn(null)}", "single_int32", "null_type", "int")]
    [InlineData("TestAllTypes{standalone_message: dyn(5)}", "standalone_message", "int", "cel.expr.conformance.proto3.TestAllTypes.NestedMessage")]
    [InlineData("TestAllTypes{standalone_message: dyn(NestedTestAllTypes{})}", "standalone_message", "cel.expr.conformance.proto3.NestedTestAllTypes", "cel.expr.conformance.proto3.TestAllTypes.NestedMessage")]
    [InlineData("TestAllTypes{single_value: int}", "single_value", "type", "dyn")]
    [InlineData("TestAllTypes{single_any: int}", "single_any", "type", "any")]
    public static void ValueOfAnotherKind_IsFieldTypeMismatchWithTheNamesOfBothTypes(string expression, string field, string actual, string declared)
    {
        CelError error = Reading.ReadError(expression);

        Assert.Equal(CelErrorCode.FieldTypeMismatch, error.Code);
        Assert.Equal("field '" + field + "' of type '" + Type + "' has a value of type '" + actual + "', expected '" + declared + "'", error.Message);
    }

    // A Value takes an integer as a number when a double holds it exactly, from -(2^53 - 1) to 2^53 - 1, and as its decimal text
    // beyond that, for an int and for a uint.
    [Fact]
    public static void IntegerInAValue_IsANumberUpToTheExactRangeOfADouble()
    {
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 9007199254740991}.single_value == 9007199254740991.0"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 9007199254740992}.single_value == '9007199254740992'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: -9007199254740991}.single_value == -9007199254740991.0"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: -9007199254740992}.single_value == '-9007199254740992'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 4294967296}.single_value == 4294967296.0"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 9007199254740991u}.single_value == 9007199254740991.0"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 9007199254740992u}.single_value == '9007199254740992'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 18446744073709551615u}.single_value == '18446744073709551615'"));
    }

    [Fact]
    public static void EachKindOfValue_IsWrittenIntoAValueByItsRow()
    {
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: true}.single_value == true"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: b'foo'}.single_value == 'Zm9v'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: b''}.single_value == ''"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: timestamp('1970-01-01T00:01:00Z')}.single_value == '1970-01-01T00:01:00Z'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: timestamp('1970-01-01T00:00:01.500Z')}.single_value == '1970-01-01T00:00:01.5Z'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: duration('1000000s')}.single_value == '1000000s'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: duration('1.5s')}.single_value == '1.5s'"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: double('NaN')}.single_value != double('NaN')"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: 1.0 / 0.0}.single_value == 1.0 / 0.0"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: [1, [2, 'a'], {'k': null}]}.single_value == [1.0, [2.0, 'a'], {'k': null}]"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: {'a': {'b': [true]}}}.single_value.a.b[0] == true"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_struct: {'a': b'foo'}}.single_struct.a == 'Zm9v'"));
        Assert.True(Reading.IsTrue("TestAllTypes{list_value: [b'foo', 1u]}.list_value == ['Zm9v', 1.0]"));
    }

    // An object goes into a Value as the canonical JSON of protobuf: a Struct of its fields by their JSON names, with 64-bit integers
    // as text.
    [Fact]
    public static void ObjectInAValue_IsItsCanonicalJson()
    {
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: TestAllTypes.NestedMessage{bb: 5}}.single_value == {'bb': 5.0}"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_value: TestAllTypes{single_int64: 5, single_string: 's'}}.single_value == {'singleInt64': '5', 'singleString': 's'}"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_struct: {'m': TestAllTypes.NestedMessage{bb: 2}}}.single_struct.m.bb == 2.0"));
    }

    // A key of a map that is not a string cannot be a field of a Struct, and the text names the type of the key.
    [Fact]
    public static void MapWithAKeyThatIsNotAString_IsInvalidArgumentInAValueAndInAStruct()
    {
        CelError inValue = Reading.ReadError("TestAllTypes{single_value: dyn({1: 'a'})}");
        CelError inStruct = Reading.ReadError("TestAllTypes{single_struct: dyn({true: 'a'})}");

        Assert.Equal(CelErrorCode.InvalidArgument, inValue.Code);
        Assert.Equal("invalid argument to '" + Type + ".single_value': a key of the type int is not a string", inValue.Message);
        Assert.Equal(CelErrorCode.InvalidArgument, inStruct.Code);
        Assert.Equal("invalid argument to '" + Type + ".single_struct': a key of the type bool is not a string", inStruct.Message);
    }

    // A message that holds an Any of a type that no file of the provider describes cannot be written as JSON, so it cannot go into a
    // Value, a Struct or a ListValue: the error is InvalidArgument with a fixed text, not the exception of the formatter.
    [Theory]
    [InlineData("TestAllTypes{single_value: x}", "single_value")]
    [InlineData("TestAllTypes{single_struct: {'m': x}}", "single_struct")]
    [InlineData("TestAllTypes{list_value: [1, x]}", "list_value")]
    public static void MessageWithAnAnyOfAnUndescribedType_IsInvalidArgumentInAValue(string expression, string field)
    {
        var undescribed = new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = "type.googleapis.com/missing.Type", Value = ByteString.CopyFromUtf8("a") } };

        CelError error = Reading.ReadError(expression, undescribed);

        Assert.Equal(CelErrorCode.InvalidArgument, error.Code);
        Assert.Equal(
            "invalid argument to '" + Type + "." + field + "': the message cannot be written as JSON: it holds a google.protobuf.Any of a type that the type source does not describe, or with content that does not parse",
            error.Message);
    }

    // The type URL of an Any is type.googleapis.com and the full name of what the value is packed as.
    [Fact]
    public static void ValueInAnAny_IsPackedAsTheMessageOfItsRow()
    {
        Assert.Equal("type.googleapis.com/google.protobuf.Value", Packed("null").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.BoolValue", Packed("true").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.Int64Value", Packed("5").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.UInt64Value", Packed("5u").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.DoubleValue", Packed("1.5").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.StringValue", Packed("'a'").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.BytesValue", Packed("b'a'").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.Timestamp", Packed("timestamp(0)").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.Duration", Packed("duration('1s')").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.ListValue", Packed("[1]").TypeUrl);
        Assert.Equal("type.googleapis.com/google.protobuf.Struct", Packed("{'a': 1}").TypeUrl);
        Assert.Equal("type.googleapis.com/" + Type, Packed("TestAllTypes{}").TypeUrl);
        Assert.Equal(new Int64Value { Value = 5 }, Packed("5").Unpack<Int64Value>());
    }

    // A duration of the other sign is packed with seconds and nanoseconds of that sign, and reads back the same.
    [Fact]
    public static void NegativeDurationInAnAny_KeepsItsSign()
    {
        Duration packed = Packed("duration('-1.5s')").Unpack<Duration>();

        Assert.Equal((-1L, -500_000_000), (packed.Seconds, packed.Nanos));
        Assert.True(Reading.IsTrue("TestAllTypes{single_any: duration('-1.5s')}.single_any == duration('-1.5s')"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_any: timestamp('1970-01-01T00:00:01.500Z')}.single_any == timestamp('1970-01-01T00:00:01.500Z')"));
        Assert.True(Reading.IsTrue("TestAllTypes{single_any: b'a'}.single_any == b'a'"));
    }

    // A message that goes into a field is that instance, shared and not copied; an expression never changes a message it has read.
    [Fact]
    public static void MessageInAField_IsSharedNotCopied()
    {
        var x = new Proto3.TestAllTypes { StandaloneMessage = new Proto3.TestAllTypes.Types.NestedMessage { Bb = 3 } };

        Proto3.TestAllTypes viaOneof = Reading.Message<Proto3.TestAllTypes>("TestAllTypes{single_nested_message: x.standalone_message}", x);
        Proto3.TestAllTypes viaRepeated = Reading.Message<Proto3.TestAllTypes>("TestAllTypes{repeated_nested_message: [x.standalone_message]}", x);

        Assert.Same(x.StandaloneMessage, viaOneof.SingleNestedMessage);
        Assert.Same(x.StandaloneMessage, viaRepeated.RepeatedNestedMessage[0]);
        Assert.Equal(3, x.StandaloneMessage.Bb);
    }

    // A value nested more deeply than the thread stack allows is an error and not a stack overflow.
    [Fact]
    public static void ValueNestedTooDeeply_IsInvalidArgument()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(ProtobufExample.TypesAll)
            .UseContainer(ProtobufExample.Container3)
            .AddVariable("v", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();
        CelValue deep = CelValue.Null;
        for (int level = 0; level < 200_000; level++)
        {
            deep = CelValue.FromList(CelList.Create(deep));
        }

        CelEvalResult<CelValue> result = environment.Compile<CelValue>("TestAllTypes{single_value: v}").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("v", deep), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidArgument, error.Code);
        Assert.Equal("invalid argument to '" + Type + ".single_value': the value is nested too deeply", error.Message);
    }

    private static Any Packed(string value) =>
        Reading.Message<Proto3.TestAllTypes>("TestAllTypes{single_any: " + value + "}").SingleAny;
}
