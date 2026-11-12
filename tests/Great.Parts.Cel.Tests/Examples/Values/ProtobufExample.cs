using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Protobuf;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The messages and the environments of the examples of protobuf messages. The messages are the test messages of the
// cel-spec suite, compiled into the test project: cel.expr.conformance.proto3.TestAllTypes with its nested types and
// NestedTestAllTypes, and the proto2 TestAllTypes with the extensions of its extension file. The expressions of the
// examples use the container of the proto3 package, so TestAllTypes is the proto3 message there; the proto2 messages
// are named in full.
internal static class ProtobufExample
{
    internal const string Container3 = "cel.expr.conformance.proto3";

    internal const string Container2 = "cel.expr.conformance.proto2";

    internal const string TypeName3 = "cel.expr.conformance.proto3.TestAllTypes";

    internal const string NestedName3 = "cel.expr.conformance.proto3.TestAllTypes.NestedMessage";

    // The provider of the proto3 messages; the files that TestAllTypes imports come with it.
    internal static CelProtobufTypes Types3 { get; } = new CelProtobufTypesBuilder("proto3 test messages")
        .AddFile(Proto3.TestAllTypesReflection.Descriptor)
        .Build();

    // The provider of the proto2 messages with every extension of the extension file, in both forms: package scoped
    // and message scoped, one value and repeated.
    internal static CelProtobufTypes Types2 { get; } = new CelProtobufTypesBuilder("proto2 test messages")
        .AddFile(Proto2.TestAllTypesReflection.Descriptor)
        .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.NestedExt)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.NestedEnumExt)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes)
        .AddProtoExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.Int64Ext)
        .Build();

    // One provider of the messages of both packages and the extensions: the environments that need a proto3 message and a
    // proto2 message with extensions at once. Two providers of protobuf cannot share an environment (the well-known types
    // are in both), so a test that needs both builds one.
    internal static CelProtobufTypes TypesAll { get; } = new CelProtobufTypesBuilder("all test messages")
        .AddFile(Proto3.TestAllTypesReflection.Descriptor)
        .AddFile(Proto2.TestAllTypesReflection.Descriptor)
        .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.NestedExt)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.NestedEnumExt)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes)
        .AddProtoExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.Int64Ext)
        .Build();

    // An environment of the proto3 messages, in the container of their package.
    internal static CelEnvironmentBuilder Builder3() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(Types3).UseContainer(Container3);

    internal static Proto3.TestAllTypes.Types.NestedMessage Nested(int bb) => new() { Bb = bb };

    // A message with every kind of field set to a value that tells it from the default, and from another field of its
    // kind: the boundaries of the 64-bit integers, a float that is not exact in binary, a string with a non-ASCII
    // letter, an explicit zero in the wrappers and the proto3 optional fields, and the well-known types.
    internal static Proto3.TestAllTypes Full() => new()
    {
        SingleInt32 = -5,
        SingleInt64 = long.MinValue,
        SingleUint32 = uint.MaxValue,
        SingleUint64 = ulong.MaxValue,
        SingleSint32 = -7,
        SingleSint64 = -8,
        SingleFixed32 = 9,
        SingleFixed64 = 10,
        SingleSfixed32 = -11,
        SingleSfixed64 = -12,
        SingleFloat = 0.1f,
        SingleDouble = 0.1,
        SingleBool = true,
        SingleString = "héllo",
        SingleBytes = ByteString.CopyFrom(1, 2, 3),
        OptionalBool = false,
        OptionalString = "o",
        SingleAny = Any.Pack(Nested(5)),
        SingleDuration = new Duration { Seconds = 3723, Nanos = 500_000_000 },
        SingleTimestamp = new Timestamp { Seconds = 1_704_164_645, Nanos = 123_456_789 },
        SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(1), ["b"] = Value.ForList(Value.ForString("x")) } },
        SingleValue = Value.ForNumber(1.5),
        SingleInt64Wrapper = 7,
        SingleInt32Wrapper = 0,
        SingleDoubleWrapper = 2.5,
        SingleFloatWrapper = 1.5f,
        SingleUint64Wrapper = 8,
        SingleUint32Wrapper = 9,
        SingleStringWrapper = "w",
        SingleBoolWrapper = false,
        SingleBytesWrapper = ByteString.CopyFrom(4),
        ListValue = new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } },
        OptionalNullValue = NullValue.NullValue,
        StandaloneMessage = Nested(6),
        StandaloneEnum = Proto3.TestAllTypes.Types.NestedEnum.Bar,
        RepeatedInt32 = { 1, 2, 3 },
        RepeatedString = { "b", "a" },
        RepeatedBytes = { ByteString.CopyFrom(1) },
        RepeatedNestedMessage = { Nested(7), Nested(8) },
        RepeatedNestedEnum = { Proto3.TestAllTypes.Types.NestedEnum.Baz, Proto3.TestAllTypes.Types.NestedEnum.Foo },
        MapStringInt64 = { ["b"] = 2, ["a"] = 1 },
        MapUint32String = { [7] = "a" },
        MapBoolString = { [true] = "t" },
        MapInt32Bool = { [3] = true },
        MapInt64NestedType = { [1] = new Proto3.NestedTestAllTypes { Payload = new Proto3.TestAllTypes { SingleInt32 = 3 } } },
    };

    // A proto2 message with an extension of every kind set: int32_ext 7, nested_ext a message with single_int32 9,
    // nested_enum_ext BAR, repeated_test_all_types two messages with single_int32 1 and 2, and the message scoped
    // int64_ext 8.
    internal static Proto2.TestAllTypes WithExtensions()
    {
        var message = new Proto2.TestAllTypes { SingleInt32 = 5 };
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext, 7);
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.NestedExt, new Proto2.TestAllTypes { SingleInt32 = 9 });
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.NestedEnumExt, Proto2.TestAllTypes.Types.NestedEnum.Bar);
        message.SetExtension(Proto2.Proto2ExtensionScopedMessage.Extensions.Int64Ext, 8L);
        RepeatedField<Proto2.TestAllTypes> repeated = message.GetOrInitializeExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes);
        repeated.Add(new Proto2.TestAllTypes { SingleInt32 = 1 });
        repeated.Add(new Proto2.TestAllTypes { SingleInt32 = 2 });
        return message;
    }
}
