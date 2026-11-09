using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.TimeCalls;

namespace Great.Parts.Cel.Tests.Library;

// The eight relations of time on values: each of the four relations on a moment and on a duration, for a smaller, an equal and
// a greater second operand; the nanoseconds of a moment decide when the seconds are equal.
public sealed class TimeOrderingOverloadTests
{
    // The identifier of the relation for each kind, and the outcomes for the operands (1, 2), (2, 2) and (3, 2).
    [Theory]
    [InlineData("less_timestamp", true, false, false)]
    [InlineData("less_equals_timestamp", true, true, false)]
    [InlineData("greater_timestamp", false, false, true)]
    [InlineData("greater_equals_timestamp", false, true, true)]
    public static void RelationsOfMoments_FollowTheOrder(string id, bool below, bool equal, bool above)
    {
        Assert.Equal((below, equal, above), (Check(id, Ts(1), Ts(2)), Check(id, Ts(2), Ts(2)), Check(id, Ts(3), Ts(2))));
    }

    [Theory]
    [InlineData("less_duration", true, false, false)]
    [InlineData("less_equals_duration", true, true, false)]
    [InlineData("greater_duration", false, false, true)]
    [InlineData("greater_equals_duration", false, true, true)]
    public static void RelationsOfDurations_FollowTheOrder(string id, bool below, bool equal, bool above)
    {
        Assert.Equal((below, equal, above), (Check(id, Du(-1), Du(2)), Check(id, Du(2), Du(2)), Check(id, Du(long.MaxValue), Du(2))));
    }

    [Fact]
    public static void NanosecondsOfAMoment_DecideWhenTheSecondsAreEqual()
    {
        Assert.True(Check("less_timestamp", Ts(5, 1), Ts(5, 2)));
        Assert.False(Check("less_timestamp", Ts(5, 2), Ts(5, 2)));
        Assert.True(Check("greater_equals_timestamp", Ts(5, 999_999_999), Ts(5, 999_999_998)));
        Assert.True(Check("less_timestamp", Ts(-1, 999_999_999), Ts(0)));
    }

    [Fact]
    public static void ExtremeDurations_AreOrdered()
    {
        Assert.True(Check("less_duration", Du(long.MinValue), Du(long.MaxValue)));
        Assert.True(Check("greater_equals_duration", Du(long.MinValue), Du(long.MinValue)));
    }

    [Theory]
    [InlineData("less_timestamp")]
    [InlineData("less_equals_timestamp")]
    [InlineData("greater_timestamp")]
    [InlineData("greater_equals_timestamp")]
    public static void RelationsOfMoments_RejectOtherKinds(string id)
    {
        AssertNoMatchingOverload(id, Ts(1), Du(1));
        AssertNoMatchingOverload(id, Int(1), Int(2));
    }

    [Theory]
    [InlineData("less_duration")]
    [InlineData("less_equals_duration")]
    [InlineData("greater_duration")]
    [InlineData("greater_equals_duration")]
    public static void RelationsOfDurations_RejectOtherKinds(string id)
    {
        AssertNoMatchingOverload(id, Du(1), Ts(1));
        AssertNoMatchingOverload(id, Int(1), Int(2));
    }

    private static bool Check(string id, CelValue left, CelValue right)
    {
        CelValue result = Call(id, left, right);
        Assert.True(result.TryGetBoolean(out bool value), result.Kind.ToString());
        return value;
    }
}
