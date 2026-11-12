using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// has(m.f) on protobuf messages (the type mapping page, the rule of presence of the table of protobuf messages): in proto2
// the field is set, whatever its value — the default of the field, zero and the empty string included; in proto3 a
// scalar that has explicit presence (optional) is set when it was set, any other scalar is set when its value is not the
// zero of its type, a oneof member and a message field are set when they were set, a repeated or a map field when it
// has an element, and a wrapper, a Struct, a ListValue, a Timestamp, an Any and a Value when they were set. A field that is
// not set reads as its default, a proto2 field as its own default when the file has one. A name that the message does not
// have is NoSuchField (2004) at type checking, at the selection or at the "(" of has(), and EvalNoSuchField (3001) when
// the check is not made or the operand is dyn. The values are those of cel-go v0.32.0 (a probe of the oracle with the test
// messages of the suite, 2026-11-11); the explicit default of a proto2 field is a set field there too.
// Variables: x, e as the field rows have them (x with every field set, e with none); n an empty NestedTestAllTypes; o1 has
// the oneof member single_nested_message set, o2 single_nested_enum set to its zero; p2e is a proto2 TestAllTypes with no field set,
// p2d one whose single_int32 and single_bool are set to their defaults (-32 and true) and single_string to the empty
// string, p2z one whose single_int32 is set to 0, p2r one with a repeated field of one element.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-1")]
public sealed class ProtobufPresenceTests
{
    private const string Proto3Scalars = """
        has(e.single_int32) => false
        has(x.single_int32) => true
        has(TestAllTypes{single_int32: 5}.single_int32) => true
        has(TestAllTypes{single_int32: 0}.single_int32) => false
        has(TestAllTypes{single_uint64: 0u}.single_uint64) => false
        has(TestAllTypes{single_uint64: 1u}.single_uint64) => true
        has(TestAllTypes{single_double: 0.0}.single_double) => false
        has(TestAllTypes{single_double: 0.5}.single_double) => true
        has(TestAllTypes{single_bool: false}.single_bool) => false
        has(TestAllTypes{single_bool: true}.single_bool) => true
        has(TestAllTypes{single_string: ''}.single_string) => false
        has(TestAllTypes{single_string: 'a'}.single_string) => true
        has(TestAllTypes{single_bytes: b''}.single_bytes) => false
        has(TestAllTypes{single_bytes: b'a'}.single_bytes) => true
        has(TestAllTypes{standalone_enum: 0}.standalone_enum) => false
        has(TestAllTypes{standalone_enum: 1}.standalone_enum) => true
        """;

    private const string Proto3Optional = """
        has(e.optional_bool) => false
        has(e.optional_string) => false
        has(TestAllTypes{optional_bool: false}.optional_bool) => true
        has(TestAllTypes{optional_string: ''}.optional_string) => true
        has(x.optional_null_value) => true
        has(e.optional_null_value) => false
        """;

    // A member of a oneof is set when it was set, whatever its value, and no other member is; reading a member that is not set gives its default.
    private const string Proto3Oneof = """
        has(o1.single_nested_message) => true
        has(o1.single_nested_enum) => false
        has(o2.single_nested_enum) => true
        has(o2.single_nested_message) => false
        o2.single_nested_enum => 0
        o1.single_nested_enum => 0
        has(TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{}}.single_nested_message) => true
        has(TestAllTypes{single_nested_enum: 0}.single_nested_enum) => true
        has(TestAllTypes{oneof_bool: false}.oneof_bool) => true
        has(TestAllTypes{oneof_bool: false}.oneof_msg) => false
        has(e.oneof_bool) => false
        """;

    private const string Proto3Messages = """
        has(e.standalone_message) => false
        has(TestAllTypes{standalone_message: TestAllTypes.NestedMessage{}}.standalone_message) => true
        has(TestAllTypes{standalone_message: null}.standalone_message) => false
        has(e.standalone_message.bb) => false
        has(n.child) => false
        has(NestedTestAllTypes{child: NestedTestAllTypes{}}.child) => true
        has(n.child.child) => false
        """;

    private const string Proto3Collections = """
        has(e.repeated_int32) => false
        has(TestAllTypes{repeated_int32: []}.repeated_int32) => false
        has(TestAllTypes{repeated_int32: [0]}.repeated_int32) => true
        has(e.map_string_int64) => false
        has(TestAllTypes{map_string_int64: {}}.map_string_int64) => false
        has(TestAllTypes{map_string_int64: {'a': 0}}.map_string_int64) => true
        """;

    private const string Proto3WellKnown = """
        has(e.single_int64_wrapper) => false
        has(TestAllTypes{single_int64_wrapper: 0}.single_int64_wrapper) => true
        has(TestAllTypes{single_string_wrapper: ''}.single_string_wrapper) => true
        has(TestAllTypes{single_int64_wrapper: null}.single_int64_wrapper) => false
        has(e.single_struct) => false
        has(TestAllTypes{single_struct: {}}.single_struct) => true
        has(e.list_value) => false
        has(TestAllTypes{list_value: []}.list_value) => true
        has(e.single_timestamp) => false
        has(TestAllTypes{single_timestamp: timestamp(0)}.single_timestamp) => true
        has(e.single_duration) => false
        has(e.single_any) => false
        has(x.single_any) => true
        has(e.single_value) => false
        has(x.single_value) => true
        has(x.single_struct.a) => true
        has(x.single_struct.zz) => false
        """;

    private const string Proto2Rows = """
        has(p2e.single_int32) => false
        has(p2d.single_int32) => true
        has(p2z.single_int32) => true
        has(p2d.single_string) => true
        has(p2d.single_bool) => true
        has(p2e.single_bool) => false
        has(p2e.standalone_message) => false
        has(p2e.single_int64_wrapper) => false
        has(p2e.repeated_int32) => false
        has(p2r.repeated_int32) => true
        has(p2e.map_string_int64) => false
        p2e.single_int32 => -32
        p2e.single_string => 'empty'
        p2e.single_bytes => b'none'
        p2e.single_bool => true
        p2e.single_float => 3.0
        p2e.single_sint32 => 0
        p2z.single_int32 => 0
        p2d.single_string => ''
        """;

    private readonly CelEnvironment environment;

    private readonly CelBindings bindings;

    public ProtobufPresenceTests()
    {
        CelProtobufTypes messages = new CelProtobufTypesBuilder("test messages")
            .AddFile(Proto3.TestAllTypesReflection.Descriptor)
            .AddFile(Proto2.TestAllTypesReflection.Descriptor)
            .Build();
        environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(messages)
            .UseContainer(ProtobufExample.Container3)
            .AddVariable<Proto3.TestAllTypes>("x")
            .AddVariable<Proto3.TestAllTypes>("e")
            .AddVariable<Proto3.NestedTestAllTypes>("n")
            .AddVariable<Proto3.TestAllTypes>("o1")
            .AddVariable<Proto3.TestAllTypes>("o2")
            .AddVariable<Proto2.TestAllTypes>("p2e")
            .AddVariable<Proto2.TestAllTypes>("p2d")
            .AddVariable<Proto2.TestAllTypes>("p2z")
            .AddVariable<Proto2.TestAllTypes>("p2r")
            .Build()
            .GetEnvironmentOrThrow();
        bindings = new CelBindings()
            .Add("x", ProtobufExample.Full())
            .Add("e", new Proto3.TestAllTypes())
            .Add("n", new Proto3.NestedTestAllTypes())
            .Add("o1", new Proto3.TestAllTypes { SingleNestedMessage = ProtobufExample.Nested(5) })
            .Add("o2", new Proto3.TestAllTypes { SingleNestedEnum = Proto3.TestAllTypes.Types.NestedEnum.Foo })
            .Add("p2e", new Proto2.TestAllTypes())
            .Add("p2d", new Proto2.TestAllTypes { SingleInt32 = -32, SingleString = string.Empty, SingleBool = true })
            .Add("p2z", new Proto2.TestAllTypes { SingleInt32 = 0 })
            .Add("p2r", new Proto2.TestAllTypes { RepeatedInt32 = { 0 } });
    }

    [Fact]
    public void Proto3Scalar_IsSetWhenItsValueIsNotTheZero()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto3Scalars, bindings));
    }

    [Fact]
    public void Proto3Optional_IsSetWhenItWasSet()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto3Optional, bindings));
    }

    [Fact]
    public void OneofMember_IsSetWhenItWasSet()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto3Oneof, bindings));
    }

    [Fact]
    public void MessageField_IsSetWhenItWasSet()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto3Messages, bindings));
    }

    [Fact]
    public void RepeatedAndMapFields_AreSetWhenTheyHaveAnElement()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto3Collections, bindings));
    }

    [Fact]
    public void WellKnownTypes_AreSetWhenTheyWereSet()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto3WellKnown, bindings));
    }

    [Fact]
    public void Proto2Field_IsSetWhenItWasSetWhateverItsValue()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Proto2Rows, bindings));
    }

    // A field the message does not have: the selection at "." and the has() at "(", the text names the field and the type.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("x.missing", 1, "undefined field 'missing' in type 'cel.expr.conformance.proto3.TestAllTypes'")]
    [InlineData("has(x.missing)", 3, "undefined field 'missing' in type 'cel.expr.conformance.proto3.TestAllTypes'")]
    [InlineData("x.Single_Int32", 1, "undefined field 'Single_Int32' in type 'cel.expr.conformance.proto3.TestAllTypes'")]
    [InlineData("n.child.missing", 7, "undefined field 'missing' in type 'cel.expr.conformance.proto3.NestedTestAllTypes'")]
    [InlineData("p2e.optional_bool", 3, "undefined field 'optional_bool' in type 'cel.expr.conformance.proto2.TestAllTypes'")]
    public void FieldThatTheMessageDoesNotHave_IsNoSuchFieldAtTypeChecking(string expression, int offset, string message)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).Errors);

        Assert.Equal((CelErrorCode.NoSuchField, CelErrorStage.Check, offset, message), (error.Code, error.Stage, error.Location.Offset, error.Message));
    }

    // Without the type check, and through dyn, the same name is an error of the evaluation, at the same places. An
    // unknown field of a message is never a missing key, which is the error of a map.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("x.missing", 1)]
    [InlineData("has(x.missing)", 3)]
    [InlineData("n.child.missing", 7)]
    public void FieldThatTheMessageDoesNotHave_IsEvalNoSuchFieldWithoutTheTypeCheck(string expression, int offset)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ParseResult parsed = environment.Parse(expression);
        Assert.True(parsed.IsSuccess);

        CelEvalResult<CelValue> result = environment.EvaluateWithoutTypeCheck(parsed.Expression, bindings);

        CelError error = Assert.Single(result.Errors);
        string type = expression[0] == 'n' ? "cel.expr.conformance.proto3.NestedTestAllTypes" : "cel.expr.conformance.proto3.TestAllTypes";
        Assert.Equal((CelErrorCode.EvalNoSuchField, CelErrorStage.Evaluate, offset, "no such field 'missing' in " + type), (error.Code, error.Stage, error.Location.Offset, error.Message));
    }

    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("dyn(x).missing", 6)]
    [InlineData("has(dyn(x).missing)", 3)]
    public void FieldThatTheMessageDoesNotHave_IsEvalNoSuchFieldThroughDyn(string expression, int offset)
    {
        CelError error = Assert.Single(environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.EvalNoSuchField, CelErrorStage.Evaluate, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("no such field 'missing' in cel.expr.conformance.proto3.TestAllTypes", error.Message);
    }
}
