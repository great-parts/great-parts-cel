using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The values of time as CEL values: the kinds and the runtime types, CEL equality and ordering, the refusal of a map key,
// and the check against a declared type, at any depth.
public sealed class TimeValueRulesTests
{
    private static readonly CelValue Stamp = CelValue.FromTimestamp(new CelTimestamp(1_700_000_000, 250_000_000));
    private static readonly CelValue Span = CelValue.FromDuration(new CelDuration(90_000_000_000));

    [Fact]
    public static void ValuesOfTime_AreReadOnlyByTheMethodOfTheirKind()
    {
        Assert.Equal(CelValueKind.Timestamp, Stamp.Kind);
        Assert.Equal(CelValueKind.Duration, Span.Kind);
        Assert.True(Stamp.TryGetTimestamp(out CelTimestamp timestamp));
        Assert.Equal((1_700_000_000L, 250_000_000), (timestamp.Seconds, timestamp.Nanoseconds));
        Assert.True(Span.TryGetDuration(out CelDuration duration));
        Assert.Equal(90_000_000_000L, duration.TotalNanoseconds);

        Assert.False(Stamp.TryGetDuration(out CelDuration noDuration));
        Assert.Equal(default, noDuration);
        Assert.False(Span.TryGetTimestamp(out CelTimestamp noTimestamp));
        Assert.Equal(default, noTimestamp);
        Assert.False(Stamp.TryGetInt64(out _) || Stamp.TryGetUInt64(out _) || Stamp.TryGetDouble(out _) || Span.TryGetInt64(out _));
        Assert.False(CelValue.FromInt64(5).TryGetTimestamp(out _));
        Assert.False(CelValue.Null.TryGetDuration(out _));
    }

    [Fact]
    public static void ValuesOfTime_AtTheLimits_KeepTheirPayload()
    {
        var first = new CelTimestamp(-62_135_596_800, 0);
        var last = new CelTimestamp(253_402_300_799, 999_999_999);
        var before = new CelTimestamp(-1, 999_999_999);

        foreach (CelTimestamp value in new[] { first, last, before, default })
        {
            Assert.True(CelValue.FromTimestamp(value).TryGetTimestamp(out CelTimestamp read));
            Assert.Equal(value, read);
        }

        foreach (long nanoseconds in new[] { long.MinValue, long.MaxValue, -1L, 0L, 1L })
        {
            Assert.True(CelValue.FromDuration(new CelDuration(nanoseconds)).TryGetDuration(out CelDuration read));
            Assert.Equal(nanoseconds, read.TotalNanoseconds);
        }
    }

    [Fact]
    public static void DotNetEquality_ComparesTheKindAndThePayload()
    {
        CelValue sameStamp = CelValue.FromTimestamp(new CelTimestamp(1_700_000_000, 250_000_000));
        CelValue otherNanos = CelValue.FromTimestamp(new CelTimestamp(1_700_000_000, 250_000_001));
        CelValue sameSpan = CelValue.FromDuration(new CelDuration(90_000_000_000));

        Assert.True(Stamp == sameStamp && Stamp.Equals((object)sameStamp) && Stamp != otherNanos);
        Assert.True(Span == sameSpan && Span != CelValue.FromDuration(new CelDuration(1)));
        Assert.Equal(Stamp.GetHashCode(), sameStamp.GetHashCode());
        Assert.Equal(Span.GetHashCode(), sameSpan.GetHashCode());
        Assert.True(Stamp != Span);

        // A duration of N nanoseconds is not a timestamp of N seconds, and neither is an integer.
        Assert.True(CelValue.FromDuration(new CelDuration(5)) != CelValue.FromInt64(5));
        Assert.True(CelValue.FromTimestamp(new CelTimestamp(5, 0)) != CelValue.FromDuration(new CelDuration(5)));
        Assert.True(CelValue.FromTimestamp(default) != CelValue.Null);
    }

    [Fact]
    public static void CelEquality_TimeEqualsTimeOfItsKindOnly_AndNeverFails()
    {
        Assert.True(IsTrue(CelValue.CelEquals(Stamp, CelValue.FromTimestamp(new CelTimestamp(1_700_000_000, 250_000_000)))));
        Assert.False(IsTrue(CelValue.CelEquals(Stamp, CelValue.FromTimestamp(new CelTimestamp(1_700_000_000, 250_000_001)))));
        Assert.False(IsTrue(CelValue.CelEquals(Stamp, CelValue.FromTimestamp(new CelTimestamp(1_700_000_001, 250_000_000)))));
        Assert.True(IsTrue(CelValue.CelEquals(Span, CelValue.FromDuration(new CelDuration(90_000_000_000)))));
        Assert.False(IsTrue(CelValue.CelEquals(Span, CelValue.FromDuration(new CelDuration(-90_000_000_000)))));

        foreach (CelValue other in new[] { CelValue.Null, CelValue.FromInt64(90_000_000_000), CelValue.FromDouble(9e10), CelValue.FromString("90s"), CelValue.FromBoolean(true) })
        {
            CelValue result = CelValue.CelEquals(Span, other);

            Assert.Equal(CelValueKind.Bool, result.Kind);
            Assert.False(IsTrue(result));
        }

        Assert.False(IsTrue(CelValue.CelEquals(CelValue.FromTimestamp(new CelTimestamp(5, 0)), CelValue.FromDuration(new CelDuration(5)))));
    }

    [Fact]
    public static void CelEquality_OfTime_CostsOneUnit_AndInAListOneUnitPerListAndPerPair()
    {
        var meter = new RecordingMeter();

        Assert.Equal(ValueEquality.Equal, EqualityRules.AreEqual(Stamp, Stamp, meter, out _));
        Assert.Equal(ValueEquality.NotEqual, EqualityRules.AreEqual(Span, CelValue.FromDuration(default), meter, out _));
        Assert.Equal(ValueEquality.NotEqual, EqualityRules.AreEqual(Span, Stamp, meter, out _));
        Assert.Equal([1UL, 1UL, 1UL], meter.Charges);

        var lists = new RecordingMeter();
        CelValue left = CelValue.FromList(CelList.Create(Stamp, Span));
        CelValue right = CelValue.FromList(CelList.Create(Stamp, Span));

        Assert.Equal(ValueEquality.Equal, EqualityRules.AreEqual(left, right, lists, out _));
        Assert.Equal(3UL, lists.Total);
    }

    [Fact]
    public static void Ordering_OfTimestamps_IsBySecondsThenNanoseconds()
    {
        CelValue early = CelValue.FromTimestamp(new CelTimestamp(-10, 999_999_999));
        CelValue epoch = CelValue.FromTimestamp(default);
        CelValue later = CelValue.FromTimestamp(new CelTimestamp(0, 1));

        Assert.Equal(ValueOrdering.Less, OrderingRules.Compare(early, epoch));
        Assert.Equal(ValueOrdering.Less, OrderingRules.Compare(epoch, later));
        Assert.Equal(ValueOrdering.Greater, OrderingRules.Compare(later, early));
        Assert.Equal(ValueOrdering.Equal, OrderingRules.Compare(later, CelValue.FromTimestamp(new CelTimestamp(0, 1))));
    }

    [Fact]
    public static void Ordering_OfDurations_IsByTheSignedLength()
    {
        CelValue negative = CelValue.FromDuration(new CelDuration(long.MinValue));
        CelValue zero = CelValue.FromDuration(default);
        CelValue large = CelValue.FromDuration(new CelDuration(long.MaxValue));

        Assert.Equal(ValueOrdering.Less, OrderingRules.Compare(negative, zero));
        Assert.Equal(ValueOrdering.Less, OrderingRules.Compare(zero, large));
        Assert.Equal(ValueOrdering.Greater, OrderingRules.Compare(large, negative));
        Assert.Equal(ValueOrdering.Equal, OrderingRules.Compare(zero, CelValue.FromDuration(default)));
    }

    [Fact]
    public static void Ordering_BetweenTimeAndAnotherKind_HasNoOrdering()
    {
        Assert.Equal(ValueOrdering.None, OrderingRules.Compare(Stamp, Span));
        Assert.Equal(ValueOrdering.None, OrderingRules.Compare(Span, Stamp));
        Assert.Equal(ValueOrdering.None, OrderingRules.Compare(Stamp, CelValue.FromInt64(1_700_000_000)));
        Assert.Equal(ValueOrdering.None, OrderingRules.Compare(CelValue.FromDouble(1), Span));
        Assert.Equal(ValueOrdering.None, OrderingRules.Compare(Stamp, CelValue.Null));
    }

    [Fact]
    public static void Time_IsNoMapKey_AndHasNoHash()
    {
        Assert.False(KeyHashRules.IsKeyKind(CelValueKind.Timestamp));
        Assert.False(KeyHashRules.IsKeyKind(CelValueKind.Duration));
        Assert.False(KeyHashRules.TryGetHash(Stamp, out int hash));
        Assert.Equal(0, hash);
        Assert.False(KeyHashRules.TryGetHash(Span, out _));
        Assert.Throws<ArgumentException>(() => CelMap.Create([new KeyValuePair<CelValue, CelValue>(Stamp, CelValue.FromInt64(1))]));
        Assert.Throws<ArgumentException>(() => CelMap.Create([new KeyValuePair<CelValue, CelValue>(Span, CelValue.FromInt64(1))]));

        CelMap map = CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromInt64(1), Stamp)]);

        Assert.False(map.TryGetValue(Stamp, out _));
        Assert.False(map.TryGetValue(Span, out _));
        Assert.True(map.TryGetValue(CelValue.FromInt64(1), out CelValue found));
        Assert.Equal(Stamp, found);
        Assert.False(EqualityRules.KeysEqual(Stamp, Stamp));
    }

    [Fact]
    public static void RuntimeType_OfTime_IsTheTypeOfTheKind_WrittenShortInMessages()
    {
        Assert.Equal(CelType.Timestamp, ValueTypeRules.TypeOf(Stamp));
        Assert.Equal(CelType.Duration, ValueTypeRules.TypeOf(Span));
        Assert.Equal("timestamp", ValueTypeNames.Of(CelValueKind.Timestamp));
        Assert.Equal("duration", ValueTypeNames.Of(CelValueKind.Duration));
        Assert.Equal(("google.protobuf.Timestamp", CelTypeKind.Timestamp, "timestamp"), (CelType.Timestamp.Name, CelType.Timestamp.Kind, CelType.Timestamp.ToString()));
        Assert.Equal(("google.protobuf.Duration", CelTypeKind.Duration, "duration"), (CelType.Duration.Name, CelType.Duration.Kind, CelType.Duration.ToString()));
        Assert.Equal("type(timestamp)", CelType.TypeOf(CelType.Timestamp).ToString());
        Assert.Equal("list(duration)", CelType.ListOf(CelType.Duration).ToString());
        Assert.Same(CelType.Timestamp, CelType.Timestamp);
        Assert.NotEqual(CelType.Timestamp, CelType.Duration);
        Assert.Empty(CelType.Timestamp.Parameters);
    }

    [Fact]
    public static void Fits_ATimestampFitsItsTypeAndDyn_AndNothingElse()
    {
        Assert.True(ValueTypeRules.Fits(Stamp, CelType.Timestamp, null).IsFit);
        Assert.True(ValueTypeRules.Fits(Stamp, CelType.Dyn, null).IsFit);
        Assert.True(ValueTypeRules.Fits(Span, CelType.Duration, null).IsFit);
        Assert.True(ValueTypeRules.Fits(Span, CelType.TypeParameter("T"), null).IsFit);

        foreach (CelType other in new[] { CelType.Duration, CelType.Int, CelType.String, CelType.Null, CelType.WrapperOf(CelType.Int), CelType.TypeOf(CelType.Dyn) })
        {
            Assert.False(ValueTypeRules.Fits(Stamp, other, null).IsFit);
        }

        Assert.False(ValueTypeRules.Fits(Span, CelType.Timestamp, null).IsFit);
        Assert.Equal("duration", ValueTypeRules.MismatchType(Span, CelType.Timestamp));
        Assert.Equal("timestamp", ValueTypeRules.MismatchType(Stamp, CelType.Int));
        Assert.Null(ValueTypeRules.MismatchType(Stamp, CelType.Timestamp));
    }

    // A null value fits neither type of time, although a static null is assignable to them.
    [Fact]
    public static void Fits_NullFitsNeitherTimestampNorDuration()
    {
        Assert.False(ValueTypeRules.Fits(CelValue.Null, CelType.Timestamp, null).IsFit);
        Assert.False(ValueTypeRules.Fits(CelValue.Null, CelType.Duration, null).IsFit);
        Assert.Equal("null_type", ValueTypeRules.MismatchType(CelValue.Null, CelType.Timestamp));
    }

    [Fact]
    public static void Fits_AtDepth_ChecksEachElementAndEachValue()
    {
        CelValue list = CelValue.FromList(CelList.Create(Stamp, CelValue.FromTimestamp(default)));
        CelValue mixed = CelValue.FromList(CelList.Create(Stamp, Span));
        CelValue map = CelValue.FromMap(CelMap.Create([
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), Span),
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("b"), Span),
        ]));
        CelValue badMap = CelValue.FromMap(CelMap.Create([
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), Span),
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("b"), Stamp),
        ]));

        Assert.True(ValueTypeRules.Fits(list, CelType.ListOf(CelType.Timestamp), null).IsFit);
        Assert.False(ValueTypeRules.Fits(mixed, CelType.ListOf(CelType.Timestamp), null).IsFit);
        Assert.Equal("list(duration)", ValueTypeRules.MismatchType(mixed, CelType.ListOf(CelType.Timestamp)));
        Assert.True(ValueTypeRules.Fits(mixed, CelType.ListOf(CelType.Dyn), null).IsFit);
        Assert.True(ValueTypeRules.Fits(map, CelType.MapOf(CelType.String, CelType.Duration), null).IsFit);
        Assert.False(ValueTypeRules.Fits(badMap, CelType.MapOf(CelType.String, CelType.Duration), null).IsFit);
        Assert.Equal("map(string, timestamp)", ValueTypeRules.MismatchType(badMap, CelType.MapOf(CelType.String, CelType.Duration)));

        var meter = new RecordingMeter();
        Assert.True(ValueTypeRules.Fits(list, CelType.ListOf(CelType.Timestamp), meter).IsFit);
        Assert.Equal(2UL, meter.Total);
    }

    [Fact]
    public static void CreateSorted_OrdersTimestampsAndDurations_AndRefusesTheTwoKindsTogether()
    {
        var stamps = new List<CelTimestamp> { new(5, 0), new(-3, 1), new(5, 1) };

        CelList sorted = CelList.CreateSorted(stamps, CelValue.FromTimestamp);

        Assert.Equal([new CelTimestamp(-3, 1), new CelTimestamp(5, 0), new CelTimestamp(5, 1)], Timestamps(sorted));
        Assert.Equal(
            [-5L, 0L, 7L],
            ReadDurations(CelList.CreateSorted(new List<long> { 7, -5, 0 }, static ns => CelValue.FromDuration(new CelDuration(ns)))));
        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(new List<CelValue> { Stamp, Span }, static value => value));
        Assert.Throws<ArgumentException>(() => CelList.CreateSorted(new List<CelTimestamp> { new(5, 0), new(5, 0) }, CelValue.FromTimestamp));
    }

    private static bool IsTrue(CelValue value) => value.TryGetBoolean(out bool result) && result;

    private static List<CelTimestamp> Timestamps(CelList list)
    {
        var result = new List<CelTimestamp>();
        for (int index = 0; index < list.Count; index++)
        {
            Assert.True(list[index].TryGetTimestamp(out CelTimestamp timestamp));
            result.Add(timestamp);
        }

        return result;
    }

    private static List<long> ReadDurations(CelList list)
    {
        var result = new List<long>();
        for (int index = 0; index < list.Count; index++)
        {
            Assert.True(list[index].TryGetDuration(out CelDuration duration));
            result.Add(duration.TotalNanoseconds);
        }

        return result;
    }
}
