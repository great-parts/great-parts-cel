using Xunit;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// Presence of the fields of messages that were made as instances and not by a literal, beyond the rows that the examples fix: the
// floating point numbers of proto3, where only the bit pattern of +0.0 is the default.
[Trait("Requirement", "F-2")]
public sealed class PresenceTests
{
    // A number is the default of its type only when its bit pattern is that of +0.0: -0.0 and a NaN are set.
    [Fact]
    public static void Proto3FloatingPointNumber_IsSetUnlessItIsPositiveZero()
    {
        var zero = new Proto3.TestAllTypes { SingleDouble = 0.0, SingleFloat = 0.0f };
        var negativeZero = new Proto3.TestAllTypes { SingleDouble = -0.0, SingleFloat = -0.0f };
        var notANumber = new Proto3.TestAllTypes { SingleDouble = double.NaN, SingleFloat = float.NaN };

        Assert.False(Reading.IsTrue("has(x.single_double) || has(x.single_float)", zero));
        Assert.True(Reading.IsTrue("has(x.single_double) && has(x.single_float)", negativeZero));
        Assert.True(Reading.IsTrue("has(x.single_double) && has(x.single_float)", notANumber));
    }
}
