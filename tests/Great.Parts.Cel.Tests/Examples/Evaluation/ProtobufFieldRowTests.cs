using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The rows of the table of protobuf messages of the type mapping page (the table "Protobuf messages", the rows of
// field types): what the CEL type of a field is, and what a field that is set and a field that is not set read as.
// x is cel.expr.conformance.proto3.TestAllTypes with every field set (ProtobufExample.Full), e the message with no
// field set, n an empty NestedTestAllTypes. The values rows are "expression => expected", checked as the rows of
// the extension sets are (ExtensionRows.CheckValues): the kind and the value of both sides are equal in CEL.
// The values of every row that cel-go v0.32.0 has are those that it gives (a probe of the oracle with the test messages
// of the suite, 2026-11-11); the exceptions are named where they stand.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-1")]
public sealed class ProtobufFieldRowTests
{
    // One row of the table per kind of field: the integers of every width and kind, the floating point numbers, bool,
    // string and bytes, an enumeration (an int; its constants are identifiers), repeated and map fields, a message
    // field, Any, Struct, Value, ListValue, NullValue (an enumeration: the type of its row below is int), Timestamp, Duration
    // and the nine wrappers.
    private const string SetFields = """
        x.single_int32 => -5
        x.single_int64 => -9223372036854775808
        x.single_uint32 => 4294967295u
        x.single_uint64 => 18446744073709551615u
        x.single_sint32 => -7
        x.single_sint64 => -8
        x.single_fixed32 => 9u
        x.single_fixed64 => 10u
        x.single_sfixed32 => -11
        x.single_sfixed64 => -12
        x.single_float => 0.10000000149011612
        x.single_double => 0.1
        x.single_bool => true
        x.single_string => 'héllo'
        x.single_bytes => b'\x01\x02\x03'
        x.optional_bool => false
        x.optional_string => 'o'
        x.standalone_enum => 1
        x.standalone_enum == TestAllTypes.NestedEnum.BAR => true
        TestAllTypes.NestedEnum.BAZ => 2
        GlobalEnum.GAR => 1
        type(x.standalone_enum) == int => true
        x.repeated_nested_enum => [2, 0]
        x.repeated_int32 => [1, 2, 3]
        x.repeated_string => ['b', 'a']
        x.repeated_bytes => [b'\x01']
        x.repeated_nested_message[1].bb => 8
        size(x.repeated_nested_message) => 2
        x.map_string_int64 => {'a': 1, 'b': 2}
        x.map_string_int64.map(k, k) => ['b', 'a']
        x.map_uint32_string[7u] => 'a'
        x.map_bool_string[true] => 't'
        x.map_int32_bool[3] => true
        x.map_int64_nested_type[1].payload.single_int32 => 3
        x.standalone_message.bb => 6
        x.standalone_message == TestAllTypes.NestedMessage{bb: 6} => true
        type(x.standalone_message) == TestAllTypes.NestedMessage => true
        x.single_any == TestAllTypes.NestedMessage{bb: 5} => true
        x.single_any.bb => 5
        x.single_struct => {'a': 1.0, 'b': ['x']}
        x.single_struct.b[0] => 'x'
        x.single_value => 1.5
        x.list_value => [1.0, 'a']
        x.single_timestamp => timestamp('2024-01-02T03:04:05.123456789Z')
        x.single_duration => duration('3723.5s')
        x.single_int64_wrapper => 7
        x.single_int32_wrapper => 0
        x.single_double_wrapper => 2.5
        x.single_float_wrapper => 1.5
        x.single_uint64_wrapper => 8u
        x.single_uint32_wrapper => 9u
        x.single_string_wrapper => 'w'
        x.single_bool_wrapper => false
        x.single_bytes_wrapper => b'\x04'
        """;

    // A NullValue field is an enumeration like any other: it reads as the int 0, as in cel-go v0.32.0, and has the type int;
    // it is never null (the type mapping page, the row of enumerations).
    private const string NullValueFields = """
        x.null_value => 0
        x.optional_null_value => 0
        e.null_value => 0
        type(x.null_value) == int => true
        """;

    // A field that is not set reads as the default of its kind: zero, false, the empty string and bytes, a list and a
    // map with no element, and a message field as the empty message at every depth. A wrapper that is not set is null,
    // and so is a Value and an Any that are not set; Struct and ListValue are the empty map and the empty list,
    // Timestamp and Duration their zero.
    private const string UnsetFields = """
        e.single_int32 => 0
        e.single_uint64 => 0u
        e.single_double => 0.0
        e.single_bool => false
        e.single_string => ''
        e.single_bytes => b''
        e.optional_bool => false
        e.standalone_enum => 0
        e.repeated_int32 => []
        e.map_string_int64 => {}
        e.standalone_message == TestAllTypes.NestedMessage{} => true
        e.standalone_message.bb => 0
        n.child.child.payload.single_int32 => 0
        n.payload.standalone_message.bb => 0
        e.single_struct => {}
        e.list_value => []
        e.single_value => null
        e.single_any => null
        e.single_timestamp => timestamp(0)
        e.single_duration => duration('0s')
        e.single_int64_wrapper => null
        e.single_int32_wrapper => null
        e.single_uint64_wrapper => null
        e.single_uint32_wrapper => null
        e.single_double_wrapper => null
        e.single_float_wrapper => null
        e.single_string_wrapper => null
        e.single_bool_wrapper => null
        e.single_bytes_wrapper => null
        """;

    // The Float is read as the double that it is: 0.1f is 0.10000000149011612. A wrapper that holds the zero of its
    // kind is not null.
    private const string Exactness = """
        x.single_float == 0.1 => false
        x.single_float == 0.10000000149011612 => true
        x.single_int32_wrapper == 0 => true
        x.single_int32_wrapper == null => false
        e.single_int32_wrapper == null => true
        """;

    private static readonly (string Expression, CelType Type)[] FieldTypes =
    [
        ("x.single_int32", CelType.Int),
        ("x.single_int64", CelType.Int),
        ("x.single_sint32", CelType.Int),
        ("x.single_sfixed64", CelType.Int),
        ("x.single_uint32", CelType.UInt),
        ("x.single_uint64", CelType.UInt),
        ("x.single_fixed32", CelType.UInt),
        ("x.single_float", CelType.Double),
        ("x.single_double", CelType.Double),
        ("x.single_bool", CelType.Bool),
        ("x.single_string", CelType.String),
        ("x.single_bytes", CelType.Bytes),
        ("x.optional_string", CelType.String),
        ("x.standalone_enum", CelType.Int),
        ("TestAllTypes.NestedEnum.BAR", CelType.Int),
        ("x.repeated_int32", CelType.ListOf(CelType.Int)),
        ("x.repeated_uint64", CelType.ListOf(CelType.UInt)),
        ("x.repeated_bytes", CelType.ListOf(CelType.Bytes)),
        ("x.repeated_nested_enum", CelType.ListOf(CelType.Int)),
        ("x.repeated_nested_message", CelType.ListOf(CelType.ObjectNamed(ProtobufExample.NestedName3))),
        ("x.map_string_int64", CelType.MapOf(CelType.String, CelType.Int)),
        ("x.map_uint32_string", CelType.MapOf(CelType.UInt, CelType.String)),
        ("x.map_bool_string", CelType.MapOf(CelType.Bool, CelType.String)),
        ("x.map_int64_nested_type", CelType.MapOf(CelType.Int, CelType.ObjectNamed("cel.expr.conformance.proto3.NestedTestAllTypes"))),
        ("x.standalone_message", CelType.ObjectNamed(ProtobufExample.NestedName3)),
        ("x.single_nested_message", CelType.ObjectNamed(ProtobufExample.NestedName3)),
        ("n.child", CelType.ObjectNamed("cel.expr.conformance.proto3.NestedTestAllTypes")),
        ("x.single_any", CelType.Any),
        ("x.single_struct", CelType.MapOf(CelType.String, CelType.Dyn)),
        ("x.single_value", CelType.Dyn),
        ("x.list_value", CelType.ListOf(CelType.Dyn)),
        ("x.null_value", CelType.Int),
        ("x.single_timestamp", CelType.Timestamp),
        ("x.single_duration", CelType.Duration),
        ("x.single_int64_wrapper", CelType.WrapperOf(CelType.Int)),
        ("x.single_int32_wrapper", CelType.WrapperOf(CelType.Int)),
        ("x.single_uint64_wrapper", CelType.WrapperOf(CelType.UInt)),
        ("x.single_uint32_wrapper", CelType.WrapperOf(CelType.UInt)),
        ("x.single_double_wrapper", CelType.WrapperOf(CelType.Double)),
        ("x.single_float_wrapper", CelType.WrapperOf(CelType.Double)),
        ("x.single_string_wrapper", CelType.WrapperOf(CelType.String)),
        ("x.single_bool_wrapper", CelType.WrapperOf(CelType.Bool)),
        ("x.single_bytes_wrapper", CelType.WrapperOf(CelType.Bytes)),
    ];

    private readonly CelEnvironment environment = ProtobufExample.Builder3()
        .AddVariable<Proto3.TestAllTypes>("x")
        .AddVariable<Proto3.TestAllTypes>("e")
        .AddVariable<Proto3.NestedTestAllTypes>("n")
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("x", ProtobufExample.Full())
        .Add("e", new Proto3.TestAllTypes())
        .Add("n", new Proto3.NestedTestAllTypes());

    [Fact]
    public void FieldThatIsSet_ReadsAsItsCelType()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, SetFields, bindings));
    }

    [Fact]
    public void NullValueField_IsTheIntZero()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, NullValueFields, bindings));
    }

    [Fact]
    public void FieldThatIsNotSet_ReadsAsItsDefault()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, UnsetFields, bindings));
    }

    [Fact]
    public void FloatAndWrapperRows_AreExact()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Exactness, bindings));
    }

    // The type of a selection is the type of the field of the row of the table, with the names of the messages in
    // full; Any is the type any, the field of Value is dyn, and the nine wrappers are the wrappers of the six primitive
    // types (the 32-bit ones share the wrapper of the 64-bit kind).
    [Fact]
    public void Selection_HasTheCelTypeOfItsRow()
    {
        var failures = new List<string>();
        foreach ((string expression, CelType type) in FieldTypes)
        {
            CelType actual = environment.Compile<CelValue>(expression).GetProgramOrThrow().ResultType;
            if (!actual.Equals(type))
            {
                failures.Add(expression + ": " + actual + " instead of " + type);
            }
        }

        Assert.Empty(failures);
    }

    // The message in the result: the facade returns the instance of the generated class that the expression selects
    // or creates, and a message of another type is a failed conversion.
    [Fact]
    [Trait("Requirement", "+R-10")]
    public void MessageResult_IsTheInstanceOfItsClass()
    {
        Proto3.TestAllTypes message = Proto3.TestAllTypes.Parser.ParseFrom(ProtobufExample.Full().ToByteArray());
        CelBindings own = new CelBindings().Add("x", message).Add("e", new Proto3.TestAllTypes()).Add("n", new Proto3.NestedTestAllTypes());

        Proto3.TestAllTypes.Types.NestedMessage nested = environment.Compile<Proto3.TestAllTypes.Types.NestedMessage>("x.standalone_message")
            .GetProgramOrThrow().Evaluate(own, cancellationToken: CancellationToken.None).GetValueOrThrow();
        Proto3.TestAllTypes same = environment.Compile<Proto3.TestAllTypes>("x")
            .GetProgramOrThrow().Evaluate(own, cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.Equal(6, nested.Bb);
        Assert.Same(message, same);
    }
}
