using System;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// The reading of the fields of a message through the provider, beyond the rows of the table that the examples fix: the boundaries of
// the 64-bit numbers in single, repeated and map fields, the order of insertion of a map field for each of the six key kinds, the
// lookup of a key that the kind of the key cannot hold, bytes that are the memory of the byte string, an enumeration number that the
// enumeration does not declare, wrappers inside repeated and map fields, Timestamp and Duration at their bounds and beyond, and a
// message field that is not set, which reads as one empty message shared by every read.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-1")]
public sealed class FieldReadingTests
{
    [Fact]
    public static void Int64AndUInt64_AtTheirBoundaries_ReadExactly()
    {
        var message = new Proto3.TestAllTypes
        {
            SingleInt64 = long.MaxValue,
            RepeatedInt64 = { long.MinValue, long.MaxValue },
            RepeatedUint64 = { 0, ulong.MaxValue },
            MapInt64Int64 = { [long.MinValue] = long.MaxValue },
            MapUint64Uint64 = { [ulong.MaxValue] = ulong.MaxValue },
        };

        Assert.True(Reading.Read("x.single_int64", message).TryGetInt64(out long single));
        Assert.True(Reading.Read("x.repeated_int64[0]", message).TryGetInt64(out long repeatedMin));
        Assert.True(Reading.Read("x.repeated_int64[1]", message).TryGetInt64(out long repeatedMax));
        Assert.True(Reading.Read("x.repeated_uint64[1]", message).TryGetUInt64(out ulong repeatedUInt));
        Assert.True(Reading.Read("x.map_int64_int64.map(k, k)[0]", message).TryGetInt64(out long key));
        Assert.True(Reading.Read("x.map_int64_int64.map(k, x.map_int64_int64[k])[0]", message).TryGetInt64(out long value));
        Assert.True(Reading.Read("x.map_uint64_uint64[18446744073709551615u]", message).TryGetUInt64(out ulong mapped));
        Assert.Equal(long.MaxValue, single);
        Assert.Equal(long.MinValue, repeatedMin);
        Assert.Equal(long.MaxValue, repeatedMax);
        Assert.Equal(ulong.MaxValue, repeatedUInt);
        Assert.Equal(long.MinValue, key);
        Assert.Equal(long.MaxValue, value);
        Assert.Equal(ulong.MaxValue, mapped);
    }

    // A map field is read in the order of insertion whatever its key kind, never in the order of the keys or of their hash codes.
    [Fact]
    public static void MapField_KeepsTheOrderOfInsertionForEveryKeyKind()
    {
        var message = new Proto3.TestAllTypes
        {
            MapInt32Int32 = { [3] = 1, [1] = 2, [2] = 3 },
            MapInt64Int64 = { [30] = 1, [10] = 2, [20] = 3 },
            MapUint32Uint32 = { [30u] = 1, [10u] = 2, [20u] = 3 },
            MapUint64Uint64 = { [30ul] = 1, [10ul] = 2, [20ul] = 3 },
            MapStringString = { ["b"] = "x", ["c"] = "y", ["a"] = "z" },
            MapBoolBool = { [true] = false, [false] = true },
        };

        Assert.True(Reading.IsTrue("x.map_int32_int32.map(k, k) == [3, 1, 2]", message));
        Assert.True(Reading.IsTrue("x.map_int64_int64.map(k, k) == [30, 10, 20]", message));
        Assert.True(Reading.IsTrue("x.map_uint32_uint32.map(k, k) == [30u, 10u, 20u]", message));
        Assert.True(Reading.IsTrue("x.map_uint64_uint64.map(k, k) == [30u, 10u, 20u]", message));
        Assert.True(Reading.IsTrue("x.map_string_string.map(k, k) == ['b', 'c', 'a']", message));
        Assert.True(Reading.IsTrue("x.map_bool_bool.map(k, k) == [true, false]", message));
        Assert.True(Reading.IsTrue("x.map_string_string.map(k, x.map_string_string[k]) == ['x', 'y', 'z']", message));
    }

    // A key that the kind of the key of the field cannot hold is not in the map, and is no error.
    [Fact]
    public static void MapLookup_OfAKeyBeyondTheKindOfTheKey_FindsNothing()
    {
        var message = new Proto3.TestAllTypes
        {
            MapInt32Bool = { [3] = true },
            MapUint32String = { [7] = "a" },
        };

        Assert.True(Reading.IsTrue("3 in x.map_int32_bool", message));
        Assert.False(Reading.IsTrue("3000000000 in x.map_int32_bool", message));
        Assert.True(Reading.IsTrue("7u in x.map_uint32_string", message));
        Assert.False(Reading.IsTrue("4294967296u in x.map_uint32_string", message));
    }

    // A bytes value is the memory of the byte string: nothing is copied, in a single field, in an element and in a map value.
    [Fact]
    public static void Bytes_AreTheMemoryOfTheByteString()
    {
        var message = new Proto3.TestAllTypes
        {
            SingleBytes = ByteString.CopyFrom(1, 2, 3),
            RepeatedBytes = { ByteString.CopyFrom(4, 5), ByteString.CopyFrom(6) },
            MapBoolBytes = { [true] = ByteString.CopyFrom(7, 8, 9, 10) },
        };

        Assert.True(Reading.Read("x.single_bytes", message).TryGetBytes(out ReadOnlyMemory<byte> single));
        Assert.True(Reading.Read("x.repeated_bytes[1]", message).TryGetBytes(out ReadOnlyMemory<byte> element));
        Assert.True(Reading.Read("x.map_bool_bytes[true]", message).TryGetBytes(out ReadOnlyMemory<byte> mapped));
        Assert.True(single.Equals(message.SingleBytes.Memory));
        Assert.True(element.Equals(message.RepeatedBytes[1].Memory));
        Assert.True(mapped.Equals(message.MapBoolBytes[true].Memory));
    }

    // The number of an enumeration value is read whether the enumeration declares it or not.
    [Fact]
    public static void EnumerationNumber_ThatTheEnumerationDoesNotDeclare_ReadsAsItsNumber()
    {
        var message = new Proto3.TestAllTypes
        {
            StandaloneEnum = (Proto3.TestAllTypes.Types.NestedEnum)77,
            RepeatedNestedEnum = { (Proto3.TestAllTypes.Types.NestedEnum)(-5), Proto3.TestAllTypes.Types.NestedEnum.Baz },
            MapBoolEnum = { [false] = (Proto3.TestAllTypes.Types.NestedEnum)1000 },
        };

        Assert.True(Reading.IsTrue("x.standalone_enum == 77", message));
        Assert.True(Reading.IsTrue("x.repeated_nested_enum[0] == -5 && x.repeated_nested_enum[1] == 2", message));
        Assert.True(Reading.IsTrue("x.map_bool_enum[false] == 1000", message));
    }

    // A value of a null enumeration in a container is the int 0, as it is in a field.
    [Fact]
    public static void NullValue_InRepeatedAndMapFields_IsTheIntZero()
    {
        var message = new Proto3.TestAllTypes
        {
            RepeatedNullValue = { NullValue.NullValue },
            MapBoolNullValue = { [true] = NullValue.NullValue },
        };

        Assert.True(Reading.IsTrue("x.repeated_null_value[0] == 0 && type(x.repeated_null_value[0]) == int", message));
        Assert.True(Reading.IsTrue("x.map_bool_null_value[true] == 0", message));
    }

    // A wrapper that holds the default of its kind is a value, not null, also as an element and as a map value.
    [Fact]
    public static void Wrappers_InRepeatedAndMapFields_ReadTheirValues()
    {
        var message = new Proto3.TestAllTypes
        {
            RepeatedInt32Wrapper = { 0, 7 },
            RepeatedStringWrapper = { string.Empty },
            RepeatedBytesWrapper = { ByteString.Empty },
            MapBoolInt64Wrapper = { [true] = 0L },
        };

        Assert.True(Reading.IsTrue("x.repeated_int32_wrapper[0] == 0 && x.repeated_int32_wrapper[1] == 7", message));
        Assert.True(Reading.IsTrue("x.repeated_string_wrapper[0] == '' && x.repeated_bytes_wrapper[0] == b''", message));
        Assert.True(Reading.IsTrue("x.map_bool_int64_wrapper[true] == 0", message));
    }

    [Fact]
    public static void TimestampAndDuration_AtTheirBounds_ReadAsTheirValues()
    {
        var latest = new Proto3.TestAllTypes
        {
            SingleTimestamp = new Timestamp { Seconds = 253_402_300_799, Nanos = 999_999_999 },
            SingleDuration = new Duration { Seconds = 9_223_372_036, Nanos = 854_775_807 },
        };
        var earliest = new Proto3.TestAllTypes
        {
            SingleTimestamp = new Timestamp { Seconds = -62_135_596_800 },
            SingleDuration = new Duration { Seconds = -9_223_372_036, Nanos = -854_775_808 },
        };

        Assert.True(Reading.IsTrue("x.single_timestamp == timestamp('9999-12-31T23:59:59.999999999Z')", latest));
        Assert.True(Reading.IsTrue("x.single_duration == duration('9223372036854775807ns')", latest));
        Assert.True(Reading.IsTrue("x.single_timestamp == timestamp('0001-01-01T00:00:00Z')", earliest));
        Assert.True(Reading.IsTrue("x.single_duration == duration('-9223372036854775808ns')", earliest));
    }

    // Beyond the range of CEL the field reads as ConversionRange, whose text names the seconds, or the nanoseconds when the seconds fit.
    [Fact]
    public static void Timestamp_BeyondTheRange_IsConversionRange()
    {
        AssertTimestampError(new Timestamp { Seconds = 253_402_300_800 }, "253402300800 is out of the range of timestamp");
        AssertTimestampError(new Timestamp { Seconds = -62_135_596_801 }, "-62135596801 is out of the range of timestamp");
        AssertTimestampError(new Timestamp { Seconds = 5, Nanos = 1_000_000_000 }, "1000000000 is out of the range of timestamp");
        AssertTimestampError(new Timestamp { Seconds = 5, Nanos = -1 }, "-1 is out of the range of timestamp");
    }

    // The nanoseconds of the wrong sign or beyond a second are named by the error, a total that does not fit 64 bits by the seconds.
    [Fact]
    public static void Duration_BeyondTheRange_IsConversionRange()
    {
        AssertDurationError(new Duration { Seconds = 9_223_372_036, Nanos = 854_775_808 }, "9223372036 is out of the range of duration");
        AssertDurationError(new Duration { Seconds = 9_223_372_037 }, "9223372037 is out of the range of duration");
        AssertDurationError(new Duration { Seconds = -9_223_372_037 }, "-9223372037 is out of the range of duration");
        AssertDurationError(new Duration { Seconds = 1, Nanos = -1 }, "-1 is out of the range of duration");
        AssertDurationError(new Duration { Seconds = -1, Nanos = 1 }, "1 is out of the range of duration");
        AssertDurationError(new Duration { Nanos = 1_000_000_000 }, "1000000000 is out of the range of duration");
    }

    // The error of one element is made when the element is read: the list and its other elements read as usual.
    [Fact]
    public static void ElementThatIsOutOfRange_FailsWhenItIsRead()
    {
        var message = new Proto3.TestAllTypes
        {
            RepeatedTimestamp = { new Timestamp { Seconds = 1 }, new Timestamp { Seconds = 253_402_300_800 } },
            MapBoolDuration = { [true] = new Duration { Seconds = 9_223_372_037 }, [false] = new Duration { Seconds = 2 } },
        };

        Assert.True(Reading.IsTrue("size(x.repeated_timestamp) == 2 && x.repeated_timestamp[0] == timestamp(1)", message));
        Assert.True(Reading.IsTrue("x.map_bool_duration[false] == duration('2s')", message));
        Assert.Equal(CelErrorCode.ConversionRange, Reading.ReadError("x.repeated_timestamp[1]", message).Code);
        Assert.Equal(CelErrorCode.ConversionRange, Reading.ReadError("x.map_bool_duration[true]", message).Code);
    }

    // A message field that is not set reads as one empty message of its class, the same instance every time and in every message, at
    // every depth; the facade returns that instance.
    [Fact]
    public static void UnsetMessageField_ReadsAsTheSameEmptyMessage()
    {
        var first = new Proto3.TestAllTypes();
        var second = new Proto3.TestAllTypes { SingleInt32 = 4 };

        var one = Reading.Message<Proto3.TestAllTypes.Types.NestedMessage>("x.standalone_message", first);
        var two = Reading.Message<Proto3.TestAllTypes.Types.NestedMessage>("x.standalone_message", second);
        var child = Reading.Message<Proto3.NestedTestAllTypes>("n.child", n: new Proto3.NestedTestAllTypes());
        var grandchild = Reading.Message<Proto3.NestedTestAllTypes>("n.child.child", n: new Proto3.NestedTestAllTypes());

        Assert.Same(one, two);
        Assert.Equal(new Proto3.TestAllTypes.Types.NestedMessage(), one);
        Assert.Same(child, grandchild);
        Assert.Equal(new Proto3.NestedTestAllTypes(), child);
    }

    private static void AssertTimestampError(Timestamp timestamp, string text)
    {
        CelError error = Reading.ReadError("x.single_timestamp", new Proto3.TestAllTypes { SingleTimestamp = timestamp });

        Assert.Equal((CelErrorCode.ConversionRange, text), (error.Code, error.Message));
    }

    private static void AssertDurationError(Duration duration, string text)
    {
        CelError error = Reading.ReadError("x.single_duration", new Proto3.TestAllTypes { SingleDuration = duration });

        Assert.Equal((CelErrorCode.ConversionRange, text), (error.Code, error.Message));
    }
}
