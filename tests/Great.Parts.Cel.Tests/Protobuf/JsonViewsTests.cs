using Google.Protobuf.WellKnownTypes;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// Struct, Value and ListValue read as the JSON-like map, value and list of CEL, beyond the rows that the examples fix: a Value with no
// kind and one that holds null, the numbers of a Value including a NaN, an infinity and a number that a double holds exactly only up
// to 2^53, a Struct that keeps the order of insertion, nesting deeper than three levels, and Value messages inside repeated fields.
[Trait("Requirement", "F-2")]
public sealed class JsonViewsTests
{
    [Fact]
    public static void Value_WithNoKindOrANullKind_ReadsNull()
    {
        var noKind = new Proto3.TestAllTypes { SingleValue = new Value() };
        var nullKind = new Proto3.TestAllTypes { SingleValue = Value.ForNull() };

        Assert.True(Reading.IsTrue("x.single_value == null && has(x.single_value)", noKind));
        Assert.True(Reading.IsTrue("x.single_value == null && has(x.single_value)", nullKind));
    }

    [Fact]
    public static void Value_Number_IsADoubleWhateverItsValue()
    {
        var notANumber = new Proto3.TestAllTypes { SingleValue = Value.ForNumber(double.NaN) };
        var infinity = new Proto3.TestAllTypes { SingleValue = Value.ForNumber(double.PositiveInfinity) };
        var large = new Proto3.TestAllTypes { SingleValue = Value.ForNumber(9_007_199_254_740_992.0) };

        Assert.True(Reading.Read("x.single_value", notANumber).TryGetDouble(out double nan));
        Assert.True(double.IsNaN(nan));
        Assert.True(Reading.Read("x.single_value", infinity).TryGetDouble(out double positive));
        Assert.True(double.IsPositiveInfinity(positive));
        Assert.True(Reading.IsTrue("x.single_value == 9007199254740992.0 && type(x.single_value) == double", large));
    }

    [Fact]
    public static void Value_Kinds_ReadAsTheirCelValues()
    {
        var message = new Proto3.TestAllTypes
        {
            RepeatedValue =
            {
                Value.ForNumber(1),
                Value.ForNull(),
                Value.ForString("a"),
                Value.ForBool(true),
                Value.ForList(Value.ForNumber(2)),
                Value.ForStruct(new Struct { Fields = { ["k"] = Value.ForString("v") } }),
            },
        };

        Assert.True(Reading.IsTrue("x.repeated_value[0] == 1.0 && x.repeated_value[1] == null && x.repeated_value[2] == 'a'", message));
        Assert.True(Reading.IsTrue("x.repeated_value[3] == true && x.repeated_value[4][0] == 2.0 && x.repeated_value[5].k == 'v'", message));
    }

    // The entries of a Struct are in the order of insertion.
    [Fact]
    public static void Struct_KeepsTheOrderOfInsertion()
    {
        var message = new Proto3.TestAllTypes
        {
            SingleStruct = new Struct { Fields = { ["z"] = Value.ForNumber(1), ["a"] = Value.ForNumber(2), ["m"] = Value.ForNumber(3) } },
        };

        Assert.True(Reading.IsTrue("x.single_struct.map(k, k) == ['z', 'a', 'm']", message));
        Assert.True(Reading.IsTrue("size(x.single_struct) == 3 && 'm' in x.single_struct && !('q' in x.single_struct)", message));
    }

    // The nesting of structs, values and lists is walked level by level, with no limit of three.
    [Fact]
    public static void NestedStructAndList_AreReadBeyondThreeLevels()
    {
        Value level = Value.ForString("deep");
        foreach (string key in new[] { "e", "d", "c", "b", "a" })
        {
            level = Value.ForStruct(new Struct { Fields = { [key] = level } });
        }

        Value list = Value.ForNumber(4);
        for (int depth = 0; depth < 5; depth++)
        {
            list = Value.ForList(list);
        }

        var message = new Proto3.TestAllTypes { SingleStruct = level.StructValue, ListValue = list.ListValue };

        Assert.True(Reading.IsTrue("x.single_struct.a.b.c.d.e == 'deep'", message));
        Assert.True(Reading.IsTrue("x.list_value[0][0][0][0][0] == 4.0", message));
    }

    [Fact]
    public static void ListValue_InARepeatedField_ReadsItsElements()
    {
        var message = new Proto3.TestAllTypes
        {
            RepeatedListValue = { new ListValue { Values = { Value.ForNumber(1), Value.ForString("b") } }, new ListValue() },
            RepeatedStruct = { new Struct { Fields = { ["a"] = Value.ForBool(false) } } },
        };

        Assert.True(Reading.IsTrue("size(x.repeated_list_value) == 2 && x.repeated_list_value[0][1] == 'b' && size(x.repeated_list_value[1]) == 0", message));
        Assert.True(Reading.IsTrue("x.repeated_struct[0].a == false", message));
    }
}
