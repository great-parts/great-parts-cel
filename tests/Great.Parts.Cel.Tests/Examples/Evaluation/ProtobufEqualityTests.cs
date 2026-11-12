using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The equality of protobuf messages (the type mapping page, the rule of equality of the table of protobuf messages): two
// messages of one description are equal when each field is set in both or in neither and the values that are set are equal
// in CEL; a scalar of proto3 that holds its zero is not set, so it equals a message that has not set it, while a
// proto3 optional field, a message field and a wrapper that were set to an empty or zero value differ from a message that
// did not set them. A NaN field makes a message unequal to itself, and the generated Equals is not used. An Any is
// unpacked before it is compared, so two Any whose bytes differ and whose messages are equal are equal; the unknown fields,
// which the description does not have, are not compared: two messages that differ only in unknown fields are equal, which
// differs from cel-go v0.32.0, where they are unequal (a by-design difference of the library). Messages of two descriptions
// are not equal, and a message is not equal to a value of another kind: a literal of two types that cannot be equal is
// NoMatchingOverload at type checking, as cel-go has it, and through dyn the answer is false. The values are those of cel-go
// v0.32.0 (a probe of the oracle, 2026-11-11), except the two rows of the unknown fields. Variables, all of TestAllTypes: e has
// no field set; nan1 and nan2 a NaN in single_double; ma and mb an Any of the message NestedMessage{bb: 5} whose bytes are
// 08 05 and 08 01 08 05 (the field written twice, the last wins); u1 and u2 the unknown field 10041 with 7, u3 the same with 8.
// p2e is an empty proto2 TestAllTypes.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-2")]
public sealed class ProtobufEqualityTests
{
    private const string Values = """
        TestAllTypes{} == e => true
        TestAllTypes{single_int32: 1} == TestAllTypes{single_int32: 1} => true
        TestAllTypes{single_int32: 1} != TestAllTypes{single_int32: 2} => true
        TestAllTypes{single_int32: 0} == TestAllTypes{} => true
        TestAllTypes{optional_bool: false} == TestAllTypes{} => false
        TestAllTypes{standalone_message: TestAllTypes.NestedMessage{}} == TestAllTypes{} => false
        TestAllTypes{single_int64_wrapper: 0} == TestAllTypes{} => false
        TestAllTypes{repeated_int32: [1]} == TestAllTypes{repeated_int32: [1]} => true
        TestAllTypes{repeated_int32: [1]} == TestAllTypes{repeated_int32: [1, 2]} => false
        TestAllTypes{map_string_int64: {'a': 1}} == TestAllTypes{map_string_int64: {'a': 1}} => true
        TestAllTypes{single_struct: {'a': 1}} == TestAllTypes{single_struct: {'a': 1}} => true
        TestAllTypes{single_any: TestAllTypes.NestedMessage{bb: 1}} == TestAllTypes{single_any: TestAllTypes.NestedMessage{bb: 1}} => true
        TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 5}} == TestAllTypes{single_nested_message: TestAllTypes.NestedMessage{bb: 5}} => true
        [e] == [TestAllTypes{}] => true
        {'k': e} == {'k': TestAllTypes{}} => true
        e in [TestAllTypes{}] => true
        e == null => false
        ma == mb => true
        u1 == u2 => true
        u1 == u3 => true
        u1 == e => true
        nan1 == nan1 => false
        nan1 == nan2 => false
        nan1 != nan1 => true
        [nan1] == [nan1] => false
        dyn(e) == dyn(NestedTestAllTypes{}) => false
        dyn(e) == dyn(p2e) => false
        dyn(e) == 1 => false
        dyn(e) == {} => false
        """;

    private const string Errors = """
        Check | TestAllTypes{} ^== NestedTestAllTypes{} | NoMatchingOverload | found no matching overload for '_==_' applied to '(cel.expr.conformance.proto3.TestAllTypes, cel.expr.conformance.proto3.NestedTestAllTypes)'
        Check | e ^== 1 | NoMatchingOverload | found no matching overload for '_==_' applied to '(cel.expr.conformance.proto3.TestAllTypes, int)'
        """;

    private readonly CelEnvironment environment;

    private readonly CelBindings bindings;

    public ProtobufEqualityTests()
    {
        CelProtobufTypes messages = new CelProtobufTypesBuilder("test messages")
            .AddFile(Proto3.TestAllTypesReflection.Descriptor)
            .AddFile(Proto2.TestAllTypesReflection.Descriptor)
            .Build();
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(messages).UseContainer(ProtobufExample.Container3);
        foreach (string name in new[] { "e", "nan1", "nan2", "ma", "mb", "u1", "u2", "u3" })
        {
            builder.AddVariable<Proto3.TestAllTypes>(name);
        }

        environment = builder.AddVariable<Proto2.TestAllTypes>("p2e").Build().GetEnvironmentOrThrow();
        string url = "type.googleapis.com/" + ProtobufExample.NestedName3;
        bindings = new CelBindings()
            .Add("e", new Proto3.TestAllTypes())
            .Add("nan1", new Proto3.TestAllTypes { SingleDouble = double.NaN })
            .Add("nan2", new Proto3.TestAllTypes { SingleDouble = double.NaN })
            .Add("ma", new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = url, Value = ByteString.CopyFrom(0x08, 0x05) } })
            .Add("mb", new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = url, Value = ByteString.CopyFrom(0x08, 0x01, 0x08, 0x05) } })
            .Add("u1", Proto3.TestAllTypes.Parser.ParseFrom(new byte[] { 0xc8, 0xf3, 0x04, 0x07 }))
            .Add("u2", Proto3.TestAllTypes.Parser.ParseFrom(new byte[] { 0xc8, 0xf3, 0x04, 0x07 }))
            .Add("u3", Proto3.TestAllTypes.Parser.ParseFrom(new byte[] { 0xc8, 0xf3, 0x04, 0x08 }))
            .Add("p2e", new Proto2.TestAllTypes());
    }

    [Fact]
    public void Messages_AreEqualByTheirSetFields()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Values, bindings));
    }

    [Fact]
    public void MessagesOfTwoTypes_AreNoMatchingOverloadAtTypeChecking()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, Errors, bindings));
    }

    // Messages that the application builds and binds are compared by their fields too, not by reference.
    [Fact]
    public void MessagesBoundByTheApplication_AreEqualByTheirFields()
    {
        Proto3.TestAllTypes left = new() { SingleInt32 = 1 };
        Proto3.TestAllTypes same = new() { SingleInt32 = 1 };
        Proto3.TestAllTypes other = new() { SingleInt32 = 2 };
        CelBindings own = new CelBindings().Add("a", left).Add("b", same).Add("c", other);
        CelEnvironment two = ProtobufExample.Builder3()
            .AddVariable<Proto3.TestAllTypes>("a")
            .AddVariable<Proto3.TestAllTypes>("b")
            .AddVariable<Proto3.TestAllTypes>("c")
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Empty(ExtensionRows.CheckValues(two, "a == b => true\na == c => false\na != c => true", own));
    }
}
