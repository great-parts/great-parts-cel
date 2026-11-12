using Google.Protobuf;
using Google.Protobuf.Reflection;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// The presence property that the provider relies on: FieldDescriptor.HasPresence exists in the Google.Protobuf that the package
// uses and answers as the table of presence assumes — true for a proto3 optional scalar and enumeration (NullValue is an
// enumeration), for a message field, a wrapper and a member of a oneof; false for a proto3 scalar without optional, for a
// repeated and a map field; true for every singular field of proto2. The assembly of Google.Protobuf is in the output of the test.
// A failing assertion is a change of the rule of presence, not of the test.
[Trait("Requirement", "F-2")]
public sealed class FieldDescriptorPresenceTests
{
    private static readonly string[] Proto3WithPresence =
    [
        "optional_bool",
        "optional_string",
        "optional_null_value",
        "single_nested_message",
        "single_nested_enum",
        "oneof_bool",
        "standalone_message",
        "single_any",
        "single_struct",
        "single_value",
        "single_int64_wrapper",
        "single_timestamp",
    ];

    private static readonly string[] Proto3WithoutPresence =
    [
        "single_int32",
        "single_uint64",
        "single_double",
        "single_bool",
        "single_string",
        "single_bytes",
        "standalone_enum",
        "null_value",
        "repeated_int32",
        "repeated_nested_message",
        "map_int32_bool",
        "map_string_string",
    ];

    private static readonly string[] Proto2WithPresence =
    [
        "single_int32",
        "single_double",
        "single_string",
        "standalone_enum",
        "standalone_message",
        "single_int64_wrapper",
        "single_nested_enum",
    ];

    private static readonly string[] Proto2WithoutPresence =
    [
        "repeated_int32",
        "repeated_nested_message",
        "map_int32_bool",
    ];

    [Fact]
    public static void Proto3_HasPresenceIsTrueForOptionalMessageWrapperAndOneofFields()
    {
        TestContext.Current.TestOutputHelper?.WriteLine("Google.Protobuf: " + typeof(IMessage).Assembly.FullName);
        MessageDescriptor message = Proto3.TestAllTypes.Descriptor;

        foreach (string name in Proto3WithPresence)
        {
            Assert.True(message.FindFieldByName(name).HasPresence, "proto3 field " + name);
        }
    }

    [Fact]
    public static void Proto3_HasPresenceIsFalseForScalarsWithoutOptionalAndForContainers()
    {
        MessageDescriptor message = Proto3.TestAllTypes.Descriptor;

        foreach (string name in Proto3WithoutPresence)
        {
            Assert.False(message.FindFieldByName(name).HasPresence, "proto3 field " + name);
        }
    }

    [Fact]
    public static void Proto2_HasPresenceIsTrueForEverySingularField()
    {
        MessageDescriptor message = Proto2.TestAllTypes.Descriptor;

        foreach (string name in Proto2WithPresence)
        {
            Assert.True(message.FindFieldByName(name).HasPresence, "proto2 field " + name);
        }

        foreach (string name in Proto2WithoutPresence)
        {
            Assert.False(message.FindFieldByName(name).HasPresence, "proto2 field " + name);
        }
    }
}
