using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// The equality of two messages by the algorithm of the package, beyond the rows that the examples fix: -0.0 against 0.0 for a
// field with and without explicit presence, lists, maps, nested messages, wrappers, the JSON-like fields, the time fields, oneof
// members, proto2 extensions, an Any whose content unpacks and one whose content cannot be compared as a message, and the cost of
// two messages that hold Any messages and extensions.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "R-3")]
public sealed class MessageEqualityTests
{
    private const string TypeUrl3 = "type.googleapis.com/cel.expr.conformance.proto3.TestAllTypes";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ProtobufExample.TypesAll)
        .UseContainer(ProtobufExample.Container3)
        .AddVariable<Proto3.TestAllTypes>("a")
        .AddVariable<Proto3.TestAllTypes>("b")
        .AddVariable<Proto2.TestAllTypes>("p")
        .AddVariable<Proto2.TestAllTypes>("q")
        .AddVariable<Proto3.NestedTestAllTypes>("na")
        .AddVariable<Proto3.NestedTestAllTypes>("nb")
        .Build()
        .GetEnvironmentOrThrow();

    // A field without explicit presence at its default is absent, and -0.0 is not the default: it is present, and unequal to 0.0; in
    // proto2 both are present and -0.0 == 0.0.
    [Fact]
    public static void NegativeZero_IsPresentInProto3_AndEqualToZeroInProto2()
    {
        Assert.False(Equal(new Proto3.TestAllTypes { SingleDouble = -0.0 }, new Proto3.TestAllTypes { SingleDouble = 0.0 }));
        Assert.True(Equal(new Proto3.TestAllTypes { SingleDouble = -0.0 }, new Proto3.TestAllTypes { SingleDouble = -0.0 }));
        Assert.True(Equal(new Proto3.TestAllTypes { SingleDouble = 0.0 }, new Proto3.TestAllTypes()));
        Assert.True(Equal(new Proto2.TestAllTypes { SingleDouble = -0.0 }, new Proto2.TestAllTypes { SingleDouble = 0.0 }));
        Assert.False(Equal(new Proto2.TestAllTypes { SingleDouble = 0.0 }, new Proto2.TestAllTypes()));
    }

    [Fact]
    public static void RepeatedAndMapFields_AreComparedByCountAndElements()
    {
        Assert.False(Equal(new Proto3.TestAllTypes { RepeatedInt32 = { 1, 2 } }, new Proto3.TestAllTypes { RepeatedInt32 = { 2, 1 } }));
        Assert.True(Equal(
            new Proto3.TestAllTypes { RepeatedNestedMessage = { ProtobufExample.Nested(1) } },
            new Proto3.TestAllTypes { RepeatedNestedMessage = { ProtobufExample.Nested(1) } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { RepeatedNestedMessage = { ProtobufExample.Nested(1) } },
            new Proto3.TestAllTypes { RepeatedNestedMessage = { ProtobufExample.Nested(2) } }));
        Assert.True(Equal(
            new Proto3.TestAllTypes { RepeatedNestedEnum = { (Proto3.TestAllTypes.Types.NestedEnum)77 } },
            new Proto3.TestAllTypes { RepeatedNestedEnum = { (Proto3.TestAllTypes.Types.NestedEnum)77 } }));
        Assert.True(Equal(
            new Proto3.TestAllTypes { MapStringInt64 = { ["a"] = 1, ["b"] = 2 } },
            new Proto3.TestAllTypes { MapStringInt64 = { ["b"] = 2, ["a"] = 1 } }));
        Assert.False(Equal(new Proto3.TestAllTypes { MapStringInt64 = { ["a"] = 1 } }, new Proto3.TestAllTypes { MapStringInt64 = { ["a"] = 2 } }));
        Assert.False(Equal(new Proto3.TestAllTypes { MapStringInt64 = { ["a"] = 1 } }, new Proto3.TestAllTypes { MapStringInt64 = { ["b"] = 1 } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { MapStringInt64 = { ["a"] = 1 } },
            new Proto3.TestAllTypes { MapStringInt64 = { ["a"] = 1, ["b"] = 1 } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { MapInt64NestedType = { [1] = new Proto3.NestedTestAllTypes { Payload = new Proto3.TestAllTypes { SingleInt32 = 1 } } } },
            new Proto3.TestAllTypes { MapInt64NestedType = { [1] = new Proto3.NestedTestAllTypes { Payload = new Proto3.TestAllTypes { SingleInt32 = 2 } } } }));
    }

    // Nested messages are compared at any depth, the difference at the bottom included.
    [Fact]
    public static void NestedMessages_AreComparedToTheBottom()
    {
        Assert.True(Equal(Chain(20, 1), Chain(20, 1)));
        Assert.False(Equal(Chain(20, 1), Chain(20, 2)));
        Assert.False(Equal(Chain(20, 1), Chain(19, 1)));
    }

    // A wrapper set to the default is unequal to one that is not set, and two set to the same value are equal.
    [Fact]
    public static void Wrappers_AreComparedByTheirValueAndTheirPresence()
    {
        Assert.True(Equal(new Proto3.TestAllTypes { SingleInt64Wrapper = 0 }, new Proto3.TestAllTypes { SingleInt64Wrapper = 0 }));
        Assert.False(Equal(new Proto3.TestAllTypes { SingleStringWrapper = "a" }, new Proto3.TestAllTypes { SingleStringWrapper = "b" }));
        Assert.True(Equal(new Proto3.TestAllTypes { SingleBytesWrapper = ByteString.CopyFrom(1) }, new Proto3.TestAllTypes { SingleBytesWrapper = ByteString.CopyFrom(1) }));
        Assert.False(Equal(new Proto3.TestAllTypes { SingleFloatWrapper = float.NaN }, new Proto3.TestAllTypes { SingleFloatWrapper = float.NaN }));
        Assert.True(Equal(new Proto3.TestAllTypes { RepeatedInt32Wrapper = { 1, 2 } }, new Proto3.TestAllTypes { RepeatedInt32Wrapper = { 1, 2 } }));
        Assert.False(Equal(new Proto3.TestAllTypes { RepeatedInt32Wrapper = { 1, 2 } }, new Proto3.TestAllTypes { RepeatedInt32Wrapper = { 1, 3 } }));
    }

    // Value, Struct and ListValue are compared as the JSON-like values they read as: a NaN is equal to nothing, a message that holds
    // one is unequal even to a copy of itself.
    [Fact]
    public static void JsonLikeFields_AreComparedByWhatTheyReadAs()
    {
        Assert.True(Equal(new Proto3.TestAllTypes { SingleValue = Value.ForNumber(1) }, new Proto3.TestAllTypes { SingleValue = Value.ForNumber(1) }));
        Assert.False(Equal(new Proto3.TestAllTypes { SingleValue = Value.ForNumber(1) }, new Proto3.TestAllTypes { SingleValue = Value.ForString("1") }));
        Assert.True(Equal(new Proto3.TestAllTypes { SingleValue = Value.ForNull() }, new Proto3.TestAllTypes { SingleValue = Value.ForNull() }));
        Assert.False(Equal(new Proto3.TestAllTypes { SingleValue = Value.ForNull() }, new Proto3.TestAllTypes()));
        Assert.True(Equal(
            new Proto3.TestAllTypes { SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(1) } } },
            new Proto3.TestAllTypes { SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(1) } } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(1) } } },
            new Proto3.TestAllTypes { SingleStruct = new Struct { Fields = { ["b"] = Value.ForNumber(1) } } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(1) } } },
            new Proto3.TestAllTypes { SingleStruct = new Struct { Fields = { ["a"] = Value.ForNumber(2) } } }));
        Assert.True(Equal(
            new Proto3.TestAllTypes { ListValue = new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } } },
            new Proto3.TestAllTypes { ListValue = new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { ListValue = new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } } },
            new Proto3.TestAllTypes { ListValue = new ListValue { Values = { Value.ForString("a"), Value.ForNumber(1) } } }));

        var withNaN = new Proto3.TestAllTypes { SingleValue = Value.ForNumber(double.NaN) };
        Assert.False(Equal(withNaN, withNaN.Clone()));
    }

    [Fact]
    public static void TimeFields_AreComparedBySecondsAndNanoseconds()
    {
        Assert.True(Equal(
            new Proto3.TestAllTypes { SingleTimestamp = new Timestamp { Seconds = 5, Nanos = 6 } },
            new Proto3.TestAllTypes { SingleTimestamp = new Timestamp { Seconds = 5, Nanos = 6 } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { SingleTimestamp = new Timestamp { Seconds = 5, Nanos = 6 } },
            new Proto3.TestAllTypes { SingleTimestamp = new Timestamp { Seconds = 5, Nanos = 7 } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { SingleDuration = new Duration { Seconds = 5 } },
            new Proto3.TestAllTypes { SingleDuration = new Duration { Seconds = 6 } }));
        Assert.False(Equal(new Proto3.TestAllTypes { SingleDuration = new Duration() }, new Proto3.TestAllTypes()));
    }

    // A oneof member that is set is present also at its default, and another member of the same oneof is another field.
    [Fact]
    public static void OneofMembers_AreComparedByWhichIsSet()
    {
        Assert.False(Equal(new Proto3.TestAllTypes { OneofBool = false }, new Proto3.TestAllTypes()));
        Assert.True(Equal(new Proto3.TestAllTypes { OneofBool = false }, new Proto3.TestAllTypes { OneofBool = false }));
        Assert.False(Equal(new Proto3.TestAllTypes { OneofBool = true }, new Proto3.TestAllTypes { OneofMsg = ProtobufExample.Nested(1) }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { SingleNestedMessage = ProtobufExample.Nested(1) },
            new Proto3.TestAllTypes { SingleNestedEnum = Proto3.TestAllTypes.Types.NestedEnum.Foo }));
    }

    // The registered extensions are compared after the declared fields: a value, a message, a repeated extension, and an extension
    // that only one of the messages has.
    [Fact]
    public static void Extensions_AreComparedWithTheOtherFields()
    {
        Proto2.TestAllTypes same = ProtobufExample.WithExtensions();
        Proto2.TestAllTypes otherValue = ProtobufExample.WithExtensions();
        otherValue.SetExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext, 8);
        Proto2.TestAllTypes otherNested = ProtobufExample.WithExtensions();
        otherNested.SetExtension(Proto2.TestAllTypesExtensionsExtensions.NestedExt, new Proto2.TestAllTypes { SingleInt32 = 10 });
        Proto2.TestAllTypes longer = ProtobufExample.WithExtensions();
        longer.GetOrInitializeExtension(Proto2.TestAllTypesExtensionsExtensions.RepeatedTestAllTypes).Add(new Proto2.TestAllTypes { SingleInt32 = 3 });
        Proto2.TestAllTypes withoutExtension = new() { SingleInt32 = 5 };

        Assert.True(Equal(ProtobufExample.WithExtensions(), same));
        Assert.False(Equal(ProtobufExample.WithExtensions(), otherValue));
        Assert.False(Equal(ProtobufExample.WithExtensions(), otherNested));
        Assert.False(Equal(ProtobufExample.WithExtensions(), longer));
        Assert.False(Equal(ProtobufExample.WithExtensions(), withoutExtension));
        Assert.True(Equal(OnlyInt32Extension(7), OnlyInt32Extension(7)));
        Assert.False(Equal(OnlyInt32Extension(7), OnlyInt32Extension(0)));
        Assert.False(Equal(OnlyInt32Extension(0), new Proto2.TestAllTypes()));
        Assert.True(Equal(OnlyInt32Extension(0), OnlyInt32Extension(0)));
    }

    // Both Any messages are unpacked and the two results compared by CEL equality: the numbers of different wrappers are equal, a
    // number and a string are not, two messages are compared as messages, so a NaN makes them unequal though their bytes are equal.
    [Fact]
    public static void AnyThatUnpacks_IsComparedByWhatItHolds()
    {
        Assert.True(Equal(Holding(new Int64Value { Value = 1 }), Holding(new DoubleValue { Value = 1.0 })));
        Assert.True(Equal(Holding(new Int64Value { Value = 1 }), Holding(new UInt64Value { Value = 1 })));
        Assert.False(Equal(Holding(new Int64Value { Value = 1 }), Holding(new StringValue { Value = "1" })));
        Assert.False(Equal(Holding(new Int64Value { Value = 1 }), Holding(new Int64Value { Value = 2 })));
        Assert.True(Equal(
            Holding(new Proto3.TestAllTypes { SingleInt32 = 3 }),
            Holding(new Proto3.TestAllTypes { SingleInt32 = 3 })));
        Assert.False(Equal(
            Holding(new Proto3.TestAllTypes { SingleInt32 = 3 }),
            Holding(new Proto3.TestAllTypes { SingleInt32 = 4 })));
        Assert.False(Equal(
            Holding(new Proto3.TestAllTypes { SingleDouble = double.NaN }),
            Holding(new Proto3.TestAllTypes { SingleDouble = double.NaN })));
        Assert.False(Equal(Holding(ProtobufExample.Nested(1)), Holding(new Proto3.TestAllTypes { SingleInt32 = 1 })));
        Assert.True(Equal(
            Holding(new Struct { Fields = { ["a"] = Value.ForNumber(1) } }),
            Holding(new Struct { Fields = { ["a"] = Value.ForNumber(1) } })));
        Assert.True(Equal(
            new Proto3.TestAllTypes { RepeatedAny = { Any.Pack(new Int64Value { Value = 1 }) } },
            new Proto3.TestAllTypes { RepeatedAny = { Any.Pack(new DoubleValue { Value = 1.0 }) } }));
        Assert.False(Equal(
            new Proto3.TestAllTypes { MapStringAny = { ["k"] = Any.Pack(new Int64Value { Value = 1 }) } },
            new Proto3.TestAllTypes { MapStringAny = { ["k"] = Any.Pack(new Int64Value { Value = 2 }) } }));
    }

    // Two Any messages whose contents are the same message written with its fields in another order are equal, since both unpack.
    [Fact]
    public static void AnyWithTheSameMessageInAnotherByteOrder_IsEqual()
    {
        var inOrder = new Any { TypeUrl = TypeUrl3, Value = ByteString.CopyFrom(0x08, 0x01, 0x10, 0x02) };
        var reversed = new Any { TypeUrl = TypeUrl3, Value = ByteString.CopyFrom(0x10, 0x02, 0x08, 0x01) };

        Assert.True(Equal(new Proto3.TestAllTypes { SingleAny = inOrder }, new Proto3.TestAllTypes { SingleAny = reversed }));
        Assert.True(Equal(new Proto3.TestAllTypes { SingleAny = inOrder }, new Proto3.TestAllTypes { SingleAny = Any.Pack(new Proto3.TestAllTypes { SingleInt32 = 1, SingleInt64 = 2 }) }));
    }

    // When either Any cannot be unpacked — an address that no type has, content that does not parse — the two are equal when their
    // addresses are equal and their contents equal byte for byte; the comparison is never an error.
    [Fact]
    public static void AnyThatDoesNotUnpack_IsComparedByAddressAndBytes()
    {
        Any Unknown(string url, params byte[] content) => new() { TypeUrl = url, Value = ByteString.CopyFrom(content) };

        Assert.True(Equal(Single(Unknown("type.googleapis.com/acme.A", 1, 2)), Single(Unknown("type.googleapis.com/acme.A", 1, 2))));
        Assert.False(Equal(Single(Unknown("type.googleapis.com/acme.A", 1, 2)), Single(Unknown("type.googleapis.com/acme.A", 1, 3))));
        Assert.False(Equal(Single(Unknown("type.googleapis.com/acme.A", 1, 2)), Single(Unknown("type.googleapis.com/acme.B", 1, 2))));
        Assert.True(Equal(Single(new Any()), Single(new Any())));
        Assert.True(Equal(Single(Unknown("NoSlash", 5)), Single(Unknown("NoSlash", 5))));
        Assert.True(Equal(Single(Unknown(TypeUrl3, 0xff, 0xff)), Single(Unknown(TypeUrl3, 0xff, 0xff))));
        Assert.False(Equal(Single(Unknown(TypeUrl3, 0xff, 0xff)), Single(Unknown(TypeUrl3, 0xff, 0xfe))));
        Assert.False(Equal(Single(Unknown(TypeUrl3, 0xff, 0xff)), Single(Any.Pack(new Proto3.TestAllTypes()))));
    }

    // The cost is 1 and one for every ten of the sizes of both messages in bytes of the wire format, whatever the messages hold: Any
    // messages, extensions, unknown fields.
    [Fact]
    public static void Cost_IsCountedInBytesWhateverTheMessagesHold()
    {
        Proto3.TestAllTypes withAny = new() { SingleAny = Any.Pack(ProtobufExample.Nested(1)), RepeatedAny = { Any.Pack(new Int64Value { Value = 9 }) } };
        Proto2.TestAllTypes withExtensions = ProtobufExample.WithExtensions();
        Proto3.TestAllTypes withUnknown = WithUnknownField(withAny, 1);

        AssertCost("a == b", new CelBindings().Add("a", withAny).Add("b", withUnknown), withAny.CalculateSize() + withUnknown.CalculateSize());
        AssertCost("p == q", new CelBindings().Add("p", withExtensions).Add("q", ProtobufExample.WithExtensions()), withExtensions.CalculateSize() * 2);
    }

    private static void AssertCost(string expression, CelBindings bindings, int bytes)
    {
        ulong cost = 2 + 1 + (ulong)((bytes + 9) / 10);
        CelProgram<CelValue> program = Environment.Compile<CelValue>(expression).GetProgramOrThrow();

        Assert.True(program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None).IsSuccess);
        Assert.Equal(
            CelErrorCode.CostLimitExceeded,
            Assert.Single(program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None).Errors).Code);
    }

    // The message and a field 9999 with the varint value (below 128) that no class of the test messages declares: it stays among the
    // unknown fields of the parsed message.
    private static Proto3.TestAllTypes WithUnknownField(Proto3.TestAllTypes message, byte value)
    {
        byte[] known = message.ToByteArray();
        byte[] bytes = new byte[known.Length + 4];
        known.CopyTo(bytes, 0);
        bytes[^4] = 0xF8;
        bytes[^3] = 0xF0;
        bytes[^2] = 0x04;
        bytes[^1] = value;
        return Proto3.TestAllTypes.Parser.ParseFrom(bytes);
    }

    private static Proto3.NestedTestAllTypes Chain(int depth, int bottom)
    {
        var message = new Proto3.NestedTestAllTypes { Payload = new Proto3.TestAllTypes { SingleInt32 = bottom } };
        for (int level = 1; level < depth; level++)
        {
            message = new Proto3.NestedTestAllTypes { Child = message };
        }

        return message;
    }

    private static Proto2.TestAllTypes OnlyInt32Extension(int value)
    {
        var message = new Proto2.TestAllTypes();
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext, value);
        return message;
    }

    private static Proto3.TestAllTypes Holding(IMessage message) => new() { SingleAny = Any.Pack(message) };

    private static Proto3.TestAllTypes Single(Any any) => new() { SingleAny = any };

    private static bool Equal(Proto3.TestAllTypes left, Proto3.TestAllTypes right) => Compare("a == b", new CelBindings().Add("a", left).Add("b", right));

    private static bool Equal(Proto2.TestAllTypes left, Proto2.TestAllTypes right) => Compare("p == q", new CelBindings().Add("p", left).Add("q", right));

    private static bool Equal(Proto3.NestedTestAllTypes left, Proto3.NestedTestAllTypes right) => Compare("na == nb", new CelBindings().Add("na", left).Add("nb", right));

    private static bool Compare(string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool equal));
        return equal;
    }
}
