using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The literal of a protobuf message, M{f: v}, evaluated (the type mapping page, the rule of the literal of the table of
// protobuf messages): the message is created empty and each field is set from its value, a repeated field from the
// elements of a list and a map field from the pairs of a map; a value goes to the type of its field by the rows of the
// table, so an int that is out of the range of its field — a 32-bit integer, a uint32, an enumeration, an element or a
// key or a value of a collection, the value of a wrapper — is ConversionRange, "<value> is out of the range of <type>",
// at the "{" of the literal that created the message. A float field takes the nearest float, and a number
// that is too large for it is infinity, as in cel-go v0.32.0. Value takes a number as a number when its absolute value is at most 2^53 - 1 and as a string otherwise (an int and a uint
// both), a string, a bool, null, a list and a map; Struct and ListValue a map and a list of such values; a message field
// null and a wrapper null leave the field not set; an Any field takes a message (packed with the address
// "type.googleapis.com/<full name>") or a value of the well-known kinds. A oneof takes the last of its members that
// the literal has. The values are those of cel-go v0.32.0 (a probe of the oracle, 2026-11-11) where it has them.
// The text of a range error is checked up to the value and the words "is out of the range of"; the name of the type that follows is that of the page of the error codes.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "R-1")]
public sealed class ProtobufLiteralTests
{
    private const string Values = """
        TestAllTypes{single_int32: -2147483648}.single_int32 => -2147483648
        TestAllTypes{single_int32: 2147483647}.single_int32 => 2147483647
        TestAllTypes{single_int64: 9223372036854775807}.single_int64 => 9223372036854775807
        TestAllTypes{single_uint32: 4294967295u}.single_uint32 => 4294967295u
        TestAllTypes{single_uint64: 18446744073709551615u}.single_uint64 => 18446744073709551615u
        TestAllTypes{single_sint32: -7, single_sfixed32: -11}.single_sfixed32 => -11
        TestAllTypes{single_fixed64: 10u}.single_fixed64 => 10u
        TestAllTypes{single_float: 0.1}.single_float => 0.10000000149011612
        TestAllTypes{single_float: 1e300}.single_float > 1.7976931348623157e308 => true
        TestAllTypes{single_double: 0.1}.single_double => 0.1
        TestAllTypes{single_bool: true, single_string: 'a'}.single_string => 'a'
        TestAllTypes{single_bytes: b'ab'}.single_bytes => b'ab'
        TestAllTypes{standalone_enum: 2}.standalone_enum => 2
        TestAllTypes{standalone_enum: TestAllTypes.NestedEnum.BAZ}.standalone_enum => 2
        TestAllTypes{standalone_enum: -2147483648}.standalone_enum => -2147483648
        TestAllTypes{standalone_enum: 2147483647}.standalone_enum => 2147483647
        TestAllTypes{repeated_int32: [1, 2]}.repeated_int32 => [1, 2]
        TestAllTypes{repeated_string: []}.repeated_string => []
        TestAllTypes{repeated_nested_message: [TestAllTypes.NestedMessage{bb: 1}]}.repeated_nested_message[0].bb => 1
        TestAllTypes{map_string_int64: {'a': 1}}.map_string_int64 => {'a': 1}
        TestAllTypes{map_uint32_string: {7u: 'a'}}.map_uint32_string[7u] => 'a'
        TestAllTypes{map_int64_nested_type: {1: NestedTestAllTypes{}}}.map_int64_nested_type[1] == NestedTestAllTypes{} => true
        TestAllTypes{standalone_message: TestAllTypes.NestedMessage{bb: 1}}.standalone_message.bb => 1
        TestAllTypes{single_int64_wrapper: 5}.single_int64_wrapper => 5
        TestAllTypes{single_int64_wrapper: 0}.single_int64_wrapper => 0
        TestAllTypes{single_int64_wrapper: null}.single_int64_wrapper => null
        TestAllTypes{single_bytes_wrapper: b'a'}.single_bytes_wrapper => b'a'
        TestAllTypes{standalone_message: null}.standalone_message.bb => 0
        TestAllTypes{single_value: 'a'}.single_value => 'a'
        TestAllTypes{single_value: 1}.single_value => 1.0
        TestAllTypes{single_value: 1u}.single_value => 1.0
        TestAllTypes{single_value: null}.single_value => null
        TestAllTypes{single_value: [1, 'a']}.single_value => [1.0, 'a']
        TestAllTypes{single_value: {'a': 1}}.single_value => {'a': 1.0}
        TestAllTypes{single_struct: {'a': 1}}.single_struct => {'a': 1.0}
        TestAllTypes{list_value: [1, 'a']}.list_value => [1.0, 'a']
        TestAllTypes{single_any: TestAllTypes.NestedMessage{bb: 1}}.single_any == TestAllTypes.NestedMessage{bb: 1} => true
        TestAllTypes{single_any: 'a'}.single_any => 'a'
        TestAllTypes{single_any: [1, 2]}.single_any => [1.0, 2.0]
        TestAllTypes{single_timestamp: timestamp(0)}.single_timestamp => timestamp(0)
        TestAllTypes{single_duration: duration('1s')}.single_duration => duration('1s')
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 5}}.single_nested_message.bb => 5
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 1}, single_nested_enum: 1}.single_nested_enum => 1
        has(TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 1}, single_nested_enum: 1}.single_nested_message) => false
        TestAllTypes{single_nested_enum: 1, single_nested_message: TestAllTypes.NestedMessage{bb: 2}}.single_nested_message.bb => 2
        has(TestAllTypes{single_nested_enum: 1, single_nested_message: TestAllTypes.NestedMessage{bb: 2}}.single_nested_enum) => false
        TestAllTypes{single_int32: 1} == TestAllTypes{single_int32: 1} => true
        TestAllTypes{single_int32: 1}.single_int32 + TestAllTypes{single_int32: 2}.single_int32 => 3
        """;

    // The place is the "{" of the literal of the message that has the field, also in a literal inside another. The value
    // that does not fit is named, not the field.
    private const string RangeErrors = """
        Evaluate | TestAllTypes^{single_int32: 3000000000} | ConversionRange | 3000000000 is out of the range of*
        Evaluate | TestAllTypes^{single_int32: -2147483649} | ConversionRange | -2147483649 is out of the range of*
        Evaluate | TestAllTypes^{single_uint32: 4294967296u} | ConversionRange | 4294967296 is out of the range of*
        Evaluate | TestAllTypes^{standalone_enum: 2147483648} | ConversionRange | 2147483648 is out of the range of*
        Evaluate | TestAllTypes^{standalone_enum: -2147483649} | ConversionRange | -2147483649 is out of the range of*
        Evaluate | TestAllTypes^{repeated_int32: [1, 3000000000]} | ConversionRange | 3000000000 is out of the range of*
        Evaluate | TestAllTypes^{repeated_uint32: [4294967296u]} | ConversionRange | 4294967296 is out of the range of*
        Evaluate | TestAllTypes^{map_int32_bool: {3000000000: true}} | ConversionRange | 3000000000 is out of the range of*
        Evaluate | TestAllTypes^{map_string_int32: {'a': 3000000000}} | ConversionRange | 3000000000 is out of the range of*
        Evaluate | TestAllTypes^{single_int32_wrapper: 3000000000} | ConversionRange | 3000000000 is out of the range of*
        Evaluate | TestAllTypes^{single_uint32_wrapper: 4294967296u} | ConversionRange | 4294967296 is out of the range of*
        Evaluate | TestAllTypes^{single_int32: dyn(3000000000)} | ConversionRange | 3000000000 is out of the range of*
        Evaluate | NestedTestAllTypes{payload: TestAllTypes^{single_int32: 3000000000}} | ConversionRange | 3000000000 is out of the range of*
        """;

    private readonly CelEnvironment environment = ProtobufExample.Builder3()
        .AddVariable<Proto3.TestAllTypes>("e")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings().Add("e", new Proto3.TestAllTypes());

    [Fact]
    public void LiteralOfEachKindOfField_CreatesTheMessage()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Values, bindings));
    }

    [Fact]
    public void ValueOutOfTheRangeOfItsField_IsConversionRangeAtTheLiteral()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, RangeErrors, bindings));
    }

    // The message that the facade returns is the instance of the generated class with every field set, equal to the one
    // that the application builds; the Any holds the message under the address of the standard prefix.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void Literal_IsTheInstanceOfTheGeneratedClass()
    {
        const string Text = """
            TestAllTypes{
              single_int32: -5, single_int64: 3000000000, single_uint32: 4294967295u, single_uint64: 18446744073709551615u,
              single_float: 1.5, single_double: 0.1, single_bool: true, single_string: 's', single_bytes: b'\x01\x02',
              standalone_enum: TestAllTypes.NestedEnum.BAZ, standalone_message: TestAllTypes.NestedMessage{bb: 3},
              repeated_int32: [1, 2], repeated_nested_message: [TestAllTypes.NestedMessage{bb: 4}],
              map_string_int64: {'a': 1}, map_int64_nested_type: {1: NestedTestAllTypes{}},
              single_int64_wrapper: 7, single_string_wrapper: null, single_value: 'v', single_struct: {'k': 1},
              list_value: [1, 'a'], single_any: TestAllTypes.NestedMessage{bb: 9},
              single_timestamp: timestamp('1970-01-01T00:01:00Z'), single_duration: duration('2s')
            }
            """;
        var expected = new Proto3.TestAllTypes
        {
            SingleInt32 = -5,
            SingleInt64 = 3_000_000_000,
            SingleUint32 = uint.MaxValue,
            SingleUint64 = ulong.MaxValue,
            SingleFloat = 1.5f,
            SingleDouble = 0.1,
            SingleBool = true,
            SingleString = "s",
            SingleBytes = ByteString.CopyFrom(1, 2),
            StandaloneEnum = Proto3.TestAllTypes.Types.NestedEnum.Baz,
            StandaloneMessage = ProtobufExample.Nested(3),
            RepeatedInt32 = { 1, 2 },
            RepeatedNestedMessage = { ProtobufExample.Nested(4) },
            MapStringInt64 = { ["a"] = 1 },
            MapInt64NestedType = { [1] = new Proto3.NestedTestAllTypes() },
            SingleInt64Wrapper = 7,
            SingleValue = Value.ForString("v"),
            SingleStruct = new Struct { Fields = { ["k"] = Value.ForNumber(1) } },
            ListValue = new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } },
            SingleAny = Any.Pack(ProtobufExample.Nested(9)),
            SingleTimestamp = new Timestamp { Seconds = 60 },
            SingleDuration = new Duration { Seconds = 2 },
        };

        Proto3.TestAllTypes actual = environment.Compile<Proto3.TestAllTypes>(Text.ReplaceLineEndings(" "))
            .GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal(expected, actual);
    }

    // A value is created when the literal is evaluated, each time: two evaluations give two messages, and a message that the
    // expression made is not changed by what it is compared with or read from.
    [Fact]
    public void EachEvaluation_CreatesItsOwnMessage()
    {
        CelProgram<Proto3.TestAllTypes> program = environment.Compile<Proto3.TestAllTypes>("TestAllTypes{single_int32: 1}")
            .GetProgramOrThrow();

        Proto3.TestAllTypes first = program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
        Proto3.TestAllTypes second = program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }
}
