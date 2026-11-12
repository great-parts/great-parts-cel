using System;
using System.Collections.Generic;
using Cel.Expr;
using Google.Protobuf;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using Any = Google.Protobuf.WellKnownTypes.Any;
using BoolValue = Google.Protobuf.WellKnownTypes.BoolValue;
using BytesValue = Google.Protobuf.WellKnownTypes.BytesValue;
using DoubleValue = Google.Protobuf.WellKnownTypes.DoubleValue;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using FloatValue = Google.Protobuf.WellKnownTypes.FloatValue;
using Int32Value = Google.Protobuf.WellKnownTypes.Int32Value;
using Int64Value = Google.Protobuf.WellKnownTypes.Int64Value;
using JsonList = Google.Protobuf.WellKnownTypes.ListValue;
using JsonStruct = Google.Protobuf.WellKnownTypes.Struct;
using JsonValue = Google.Protobuf.WellKnownTypes.Value;
using Proto2Message = Cel.Expr.Conformance.Proto2.TestAllTypes;
using Proto3Message = Cel.Expr.Conformance.Proto3.TestAllTypes;
using ScopedExtensions = Cel.Expr.Conformance.Proto2.Proto2ExtensionScopedMessage.Extensions;
using StringValue = Google.Protobuf.WellKnownTypes.StringValue;
using TopLevelExtensions = Cel.Expr.Conformance.Proto2.TestAllTypesExtensionsExtensions;
using Type = Cel.Expr.Type;
using UInt32Value = Google.Protobuf.WellKnownTypes.UInt32Value;
using UInt64Value = Google.Protobuf.WellKnownTypes.UInt64Value;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Synthetic;

// Messages, Any and the provider of the cases in the runner (docs/design/conformance.md, "Messages, Any and the provider of the
// cases"): the known types in both directions, an Any that holds an Any, a message of the table with its description, the absent
// resolver, the comparison of two messages, each row of TypeConversion, and the provider with the extensions in every case.
public sealed class ProtobufConversionTests
{
    private const string Proto2Name = "cel.expr.conformance.proto2.TestAllTypes";

    private const string Proto3Name = "cel.expr.conformance.proto3.TestAllTypes";

    private static readonly Func<Any, CelValue?> Resolver = ConformanceMessages.Resolve;

    private static readonly string[] ExtensionNames =
    [
        "cel.expr.conformance.proto2.int32_ext",
        "cel.expr.conformance.proto2.nested_ext",
        "cel.expr.conformance.proto2.test_all_types_ext",
        "cel.expr.conformance.proto2.nested_enum_ext",
        "cel.expr.conformance.proto2.repeated_test_all_types",
        "cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.int64_ext",
        "cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.message_scoped_nested_ext",
        "cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.nested_enum_ext",
        "cel.expr.conformance.proto2.Proto2ExtensionScopedMessage.message_scoped_repeated_test_all_types",
    ];

    // ---- known types, in both directions ----

    [Fact]
    public static void Wrappers_AreTheValueTheyWrap_InBothDirections()
    {
        (IMessage Message, Value Expected)[] wrappers =
        [
            (new Int32Value { Value = -7 }, new Value { Int64Value = -7 }),
            (new Int64Value { Value = long.MinValue }, new Value { Int64Value = long.MinValue }),
            (new UInt32Value { Value = uint.MaxValue }, new Value { Uint64Value = uint.MaxValue }),
            (new UInt64Value { Value = ulong.MaxValue }, new Value { Uint64Value = ulong.MaxValue }),
            (new FloatValue { Value = 1.5f }, new Value { DoubleValue = 1.5 }),
            (new DoubleValue { Value = -0.25 }, new Value { DoubleValue = -0.25 }),
            (new BoolValue { Value = true }, new Value { BoolValue = true }),
            (new StringValue { Value = "abc" }, new Value { StringValue = "abc" }),
            (new BytesValue { Value = ByteString.CopyFrom(1, 2, 3) }, new Value { BytesValue = ByteString.CopyFrom(1, 2, 3) }),
        ];

        foreach ((IMessage message, Value expected) in wrappers)
        {
            Assert.Equal(expected, ValueConversion.ToExprValue(ValueConversion.ToCelValue(Pack(message), null)));
        }
    }

    [Fact]
    public static void Struct_IsAMapOfStringsToValues()
    {
        JsonStruct message = new()
        {
            Fields =
            {
                ["a"] = JsonValue.ForNumber(1.5),
                ["b"] = JsonValue.ForString("x"),
                ["c"] = JsonValue.ForStruct(new JsonStruct { Fields = { ["d"] = JsonValue.ForBool(true) } }),
            },
        };

        Value written = ValueConversion.ToExprValue(ValueConversion.ToCelValue(Pack(message), null));

        Assert.Equal(3, written.MapValue.Entries.Count);
        Assert.Equal(1.5, Entry(written, "a").DoubleValue);
        Assert.Equal("x", Entry(written, "b").StringValue);
        Assert.True(Entry(Entry(written, "c"), "d").BoolValue);
    }

    [Fact]
    public static void ListValue_IsAListOfValues()
    {
        JsonList message = new() { Values = { JsonValue.ForNumber(2), JsonValue.ForString("y"), JsonValue.ForNull() } };

        Value written = ValueConversion.ToExprValue(ValueConversion.ToCelValue(Pack(message), null));

        Assert.Equal(3, written.ListValue.Values.Count);
        Assert.Equal(2.0, written.ListValue.Values[0].DoubleValue);
        Assert.Equal("y", written.ListValue.Values[1].StringValue);
        Assert.Equal(Value.KindOneofCase.NullValue, written.ListValue.Values[2].KindCase);
    }

    [Fact]
    public static void Value_IsTheKindThatIsSet_AndNullWhenNoneIs()
    {
        Assert.Equal(Value.KindOneofCase.NullValue, Written(JsonValue.ForNull()).KindCase);
        Assert.Equal(-26.375, Written(JsonValue.ForNumber(-26.375)).DoubleValue);
        Assert.Equal("bar", Written(JsonValue.ForString("bar")).StringValue);
        Assert.True(Written(JsonValue.ForBool(true)).BoolValue);
        Assert.Equal(Value.KindOneofCase.MapValue, Written(JsonValue.ForStruct(new JsonStruct())).KindCase);
        Assert.Equal(Value.KindOneofCase.ListValue, Written(JsonValue.ForList()).KindCase);
        Assert.Equal(Value.KindOneofCase.NullValue, Written(new JsonValue()).KindCase);
    }

    [Fact]
    public static void AnyThatHoldsAnAny_IsUnpackedAgain()
    {
        Any inner = Any.Pack(Any.Pack(new Int32Value { Value = 3 }));

        Value written = ValueConversion.ToExprValue(ValueConversion.ToCelValue(Pack(inner), null));

        Assert.Equal(3, written.Int64Value);
    }

    // ---- a message of the table ----

    [Fact]
    public static void MessageOfTheTable_IsAnObjectValueWithItsDescription()
    {
        Proto3Message message = new() { SingleInt32 = 17, SingleDouble = double.NaN };

        CelValue value = ValueConversion.ToCelValue(Pack(message), Resolver);

        Assert.True(value.TryGetObject(out object? instance));
        Assert.Equal(message, instance);
        Value written = ValueConversion.ToExprValue(value);
        Assert.True(Rule.AreEqual(Pack(message), written));
        Assert.Equal(message, written.ObjectValue.Unpack<Proto3Message>());
    }

    [Fact]
    public static void NestedAndImportedMessages_AreInTheTable()
    {
        Assert.True(ValueConversion.ToCelValue(Pack(new Proto3Message.Types.NestedMessage { Bb = 5 }), Resolver).TryGetObject(out object? nested));
        Assert.Equal(5, Assert.IsType<Proto3Message.Types.NestedMessage>(nested).Bb);
        Assert.True(ValueConversion.ToCelValue(Pack(new Empty()), Resolver).TryGetObject(out object? empty));
        Assert.IsType<Empty>(empty);
    }

    [Fact]
    public static void MessageWithAnExtension_IsParsedWithTheRegistry()
    {
        Proto2Message message = new() { SingleInt32 = 1 };
        message.SetExtension(TopLevelExtensions.Int32Ext, 7);
        message.SetExtension(ScopedExtensions.Int64Ext, 9L);
        message.GetOrInitializeExtension(TopLevelExtensions.RepeatedTestAllTypes).Add(new Proto2Message { SingleInt32 = 2 });

        CelValue value = ValueConversion.ToCelValue(Pack(message), Resolver);

        Assert.True(value.TryGetObject(out object? instance));
        Proto2Message read = Assert.IsType<Proto2Message>(instance);
        Assert.Equal(7, read.GetExtension(TopLevelExtensions.Int32Ext));
        Assert.Equal(9L, read.GetExtension(ScopedExtensions.Int64Ext));
        Assert.Equal(2, Assert.Single(read.GetExtension(TopLevelExtensions.RepeatedTestAllTypes)).SingleInt32);
    }

    [Fact]
    public static void MessageWithoutAResolver_IsNotImplemented()
    {
        Assert.Throws<NotImplementedException>(() => ValueConversion.ToCelValue(Pack(new Proto3Message { SingleInt32 = 1 }), null));
    }

    [Fact]
    public static void AnyOfAnUnknownMessage_IsNotImplemented()
    {
        Value unknown = new() { ObjectValue = new Any { TypeUrl = "type.googleapis.com/acme.Unknown", Value = ByteString.CopyFrom(8, 1) } };

        Assert.Null(ConformanceMessages.Resolve(unknown.ObjectValue));
        Assert.Throws<NotImplementedException>(() => ValueConversion.ToCelValue(unknown, Resolver));
    }

    // ---- the comparison with the expected value ----

    [Fact]
    public static void Messages_WithMapsInDifferentOrders_AreEqual()
    {
        Proto3Message first = new() { MapBoolBool = { [true] = false, [false] = true } };
        Proto3Message second = new() { MapBoolBool = { [false] = true, [true] = false } };

        Assert.NotEqual(first.ToByteString(), second.ToByteString());
        Assert.True(Rule.AreEqual(Pack(first), Pack(second)));
        Assert.False(Rule.AreEqual(Pack(first), Pack(new Proto3Message { MapBoolBool = { [true] = true, [false] = true } })));
    }

    [Fact]
    public static void Messages_WithAnExtension_AreComparedWithTheExtension()
    {
        Proto2Message first = new();
        first.SetExtension(TopLevelExtensions.Int32Ext, 7);
        Proto2Message same = new();
        same.SetExtension(TopLevelExtensions.Int32Ext, 7);
        Proto2Message other = new();
        other.SetExtension(TopLevelExtensions.Int32Ext, 8);

        Assert.True(Rule.AreEqual(Pack(first), Pack(same)));
        Assert.False(Rule.AreEqual(Pack(first), Pack(other)));
        Assert.False(Rule.AreEqual(Pack(first), Pack(new Proto2Message())));
    }

    [Fact]
    public static void Messages_WithANaN_AreEqual_AsTheRuleNeeds()
    {
        Assert.True(Rule.AreEqual(Pack(new Proto3Message { SingleDouble = double.NaN }), Pack(new Proto3Message { SingleDouble = double.NaN })));
        Assert.False(Rule.AreEqual(Pack(new Proto3Message { SingleDouble = double.NaN }), Pack(new Proto3Message { SingleDouble = 1.0 })));
    }

    [Fact]
    public static void MessagesOfOtherTypes_AreNotEqual_AndAnUnknownTypeIsComparedByBytes()
    {
        Assert.False(Rule.AreEqual(Pack(new Proto3Message()), Pack(new Proto2Message())));

        Value first = new() { ObjectValue = new Any { TypeUrl = "type.googleapis.com/acme.Unknown", Value = ByteString.CopyFrom(8, 1) } };
        Value same = new() { ObjectValue = new Any { TypeUrl = "type.googleapis.com/acme.Unknown", Value = ByteString.CopyFrom(8, 1) } };
        Value other = new() { ObjectValue = new Any { TypeUrl = "type.googleapis.com/acme.Unknown", Value = ByteString.CopyFrom(8, 2) } };

        Assert.True(Rule.AreEqual(first, same));
        Assert.False(Rule.AreEqual(first, other));
    }

    [Fact]
    public static void ObjectValuesInAListAndAMap_AreComparedAsMessages()
    {
        Proto3Message first = new() { MapBoolBool = { [true] = false, [false] = true } };
        Proto3Message second = new() { MapBoolBool = { [false] = true, [true] = false } };
        Value left = new() { ListValue = new ListValue { Values = { Pack(first) } } };
        Value right = new() { ListValue = new ListValue { Values = { Pack(second) } } };

        Assert.True(Rule.AreEqual(left, right));
    }

    // ---- the rows of TypeConversion ----

    [Fact]
    public static void WellKnownAny_IsTheTypeAny_InBothDirections()
    {
        CelType type = TypeConversion.ToCelType(new Type { WellKnown = Type.Types.WellKnownType.Any });

        Assert.Equal(CelTypeKind.Any, type.Kind);
        Assert.Equal(Type.Types.WellKnownType.Any, TypeConversion.ToExprType(type).WellKnown);
        Assert.Equal(CelTypeKind.Any, TypeConversion.ToCelType(new Type { MessageType = "google.protobuf.Any" }).Kind);
    }

    [Fact]
    public static void MessageTypeOfAMessage_IsTheObjectTypeOfItsName_InBothDirections()
    {
        CelType type = TypeConversion.ToCelType(new Type { MessageType = Proto3Name });

        Assert.Equal(CelTypeKind.Object, type.Kind);
        Assert.Equal(Proto3Name, type.Name);
        Assert.Equal(Proto3Name, TypeConversion.ToExprType(type).MessageType);
    }

    [Fact]
    public static void MessageTypeOfAKnownName_IsTheTypeThatProtobufMapsItTo()
    {
        Assert.Equal(CelTypeKind.Wrapper, TypeConversion.ToCelType(new Type { MessageType = "google.protobuf.Int32Value" }).Kind);
        Assert.Equal(CelTypeKind.Dyn, TypeConversion.ToCelType(new Type { MessageType = "google.protobuf.Value" }).Kind);
        Assert.Equal(CelTypeKind.Map, TypeConversion.ToCelType(new Type { MessageType = "google.protobuf.Struct" }).Kind);
        Assert.Equal(CelTypeKind.List, TypeConversion.ToCelType(new Type { MessageType = "google.protobuf.ListValue" }).Kind);
        Assert.Equal(Type.Types.PrimitiveType.Int64, TypeConversion.ToExprType(TypeConversion.ToCelType(new Type { MessageType = "google.protobuf.Int32Value" })).Wrapper);
    }

    [Fact]
    public static void KindsWithoutARow_AreNotImplemented()
    {
        Assert.Throws<NotImplementedException>(() => TypeConversion.ToCelType(new Type { AbstractType = new Type.Types.AbstractType { Name = "optional_type" } }));
        Assert.Throws<NotImplementedException>(() => TypeConversion.ToCelType(new Type { WellKnown = Type.Types.WellKnownType.Unspecified }));
    }

    // ---- the provider of every case ----

    [Fact]
    public static void Provider_DescribesTheTestMessagesAndTheNineExtensions()
    {
        Assert.Contains(ConformanceMessages.Provider.ObjectTypes, type => type.Name == Proto2Name);
        Assert.Contains(ConformanceMessages.Provider.ObjectTypes, type => type.Name == Proto3Name);
        CelObjectType proto2 = Assert.Single(ConformanceMessages.Provider.ObjectTypes, type => type.Name == Proto2Name);

        foreach (string name in ExtensionNames)
        {
            Assert.True(proto2.TryGetField(name, out CelObjectField? field), name);
            Assert.NotNull(field);
        }
    }

    // ---- cases through the library: the provider and the set protos are in every environment ----

    [Fact]
    public static void BindingOfAMessage_IsReadByItsDeclaredType()
    {
        Dictionary<string, ExprValue> bindings = new() { ["x"] = new ExprValue { Value = Pack(new Proto3Message { SingleInt32 = 17 }) } };

        CaseOutcome outcome = Evaluate("x.single_int32", [Variable("x", Proto3Name)], bindings);

        Assert.Null(outcome.Exception);
        Assert.Empty(outcome.Errors);
        Assert.Equal(17, outcome.Value?.Int64Value);
    }

    [Fact]
    public static void ProtosSet_ReadsAnExtensionOfAProto2Message()
    {
        Proto2Message message = new();
        message.SetExtension(TopLevelExtensions.Int32Ext, 7);
        Dictionary<string, ExprValue> bindings = new() { ["x"] = new ExprValue { Value = Pack(message) } };

        CaseOutcome outcome = Evaluate("proto.getExt(x, cel.expr.conformance.proto2.int32_ext)", [Variable("x", Proto2Name)], bindings);

        Assert.Null(outcome.Exception);
        Assert.Empty(outcome.Errors);
        Assert.Equal(7, outcome.Value?.Int64Value);
    }

    [Fact]
    public static void BindingOfAnAnyThatHoldsAnAny_IsTheMessageInside()
    {
        Any packed = Any.Pack(Any.Pack(new Proto3Message { SingleInt32 = 150 }));
        Dictionary<string, ExprValue> bindings = new() { ["x"] = new ExprValue { Value = Pack(packed) } };
        Decl variable = new() { Name = "x", Ident = new Decl.Types.IdentDecl { Type = new Type { WellKnown = Type.Types.WellKnownType.Any } } };

        CaseOutcome outcome = Evaluate("x", [variable], bindings);

        Assert.Null(outcome.Exception);
        Assert.Empty(outcome.Errors);
        Assert.Equal(Type.Types.WellKnownType.Any, outcome.ResultType?.WellKnown);
        Assert.NotNull(outcome.Value);
        Assert.True(Rule.AreEqual(Pack(new Proto3Message { SingleInt32 = 150 }), outcome.Value));
    }

    [Fact]
    public static void ResultOfAMessageLiteral_IsWrittenAsAnAnyOfItsType()
    {
        CaseOutcome outcome = Evaluate("cel.expr.conformance.proto3.TestAllTypes{single_int32: 3}", [], new Dictionary<string, ExprValue>());

        Assert.Null(outcome.Exception);
        Assert.Empty(outcome.Errors);
        Assert.Equal(Proto3Name, outcome.ResultType?.MessageType);
        Assert.NotNull(outcome.Value);
        Assert.True(Rule.AreEqual(Pack(new Proto3Message { SingleInt32 = 3 }), outcome.Value));
    }

    private static Value Pack(IMessage message) => new() { ObjectValue = Any.Pack(message) };

    // The value of the entry with a string key; the order of the entries is not part of what the tests check.
    private static Value Entry(Value map, string key) =>
        Assert.Single(map.MapValue.Entries, entry => entry.Key.StringValue == key).Value;

    private static Value Written(JsonValue message) => ValueConversion.ToExprValue(ValueConversion.ToCelValue(Pack(message), null));

    private static Decl Variable(string name, string messageType) => new()
    {
        Name = name,
        Ident = new Decl.Types.IdentDecl { Type = new Type { MessageType = messageType } },
    };

    private static CaseOutcome Evaluate(string expression, IReadOnlyList<Decl> declarations, IReadOnlyDictionary<string, ExprValue> bindings) =>
        new LibraryEvaluator().Evaluate(new ConformanceCase(
            "f/s/t",
            "f.textproto",
            1,
            1,
            expression,
            new CaseEnvironment("", declarations, false),
            bindings,
            CheckMode.Check,
            new Expectation(ExpectationKind.Value, "none", new Value { BoolValue = true }, null)));
}
