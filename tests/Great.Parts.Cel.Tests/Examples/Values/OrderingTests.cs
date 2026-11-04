using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// CEL ordering: numbers of the three kinds on one number line, int with uint exactly, an integer with a
// double as the integer rounded to double within the range of the integer kind; NaN unordered, so every
// comparison with it is false; strings by code points, bytes by unsigned bytes with a prefix first, false
// before true. Ordering across numeric kinds needs an operand of the static type dyn. Every row is the value
// cel-go v0.32.0 gives.
public sealed class OrderingTests
{
    private const string Cases = """
        dyn(1) < 2u => true
        dyn(-1) < 0u => true
        dyn(2u) > 1 => true
        dyn(18446744073709551615u) > 1 => true
        dyn(0u) > -1 => true
        dyn(1) < 1.5 => true
        dyn(1u) < 1.5 => true
        dyn(2u) > 1.5 => true
        dyn(1.0) < 1u => false
        dyn(1.0) <= 1u => true
        dyn(-1.0) < 0u => true
        dyn(9223372036854775807) < 9223372036854775808.0 => false
        dyn(-9223372036854775808) < -9223372036854775809.0 => false
        dyn(18446744073709551615u) < 18446744073709551616.0 => false
        0.0 / 0.0 < 1.0 => false
        0.0 / 0.0 >= 1.0 => false
        'a' < 'b' => true
        'ab' > 'a' => true
        '' < 'a' => true
        'abc' < 'abd' => true
        '￿' < '\U0001F600' => true
        '\U0001F600' > '' => true
        b'a' < b'ab' => true
        b'' < b'\x00' => true
        b'\xff' > b'\x01' => true
        false < true => true
        dyn(true) < dyn(false) => false
        """;

    private readonly CelEnvironment environment =
        new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    public static TheoryData<string, bool> Comparisons
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (string line in Cases.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0))
            {
                string[] parts = line.Split(" => ");
                data.Add(parts[0], parts[1] == "true");
            }

            return data;
        }
    }

    // U+FFFF is before U+1F600 and U+E000 is before U+1F600 by code points, though the UTF-16 code unit of
    // U+1F600 that comes first, U+D83D, is below both.
    [Theory]
    [Trait("Requirement", "F-1")]
    [MemberData(nameof(Comparisons))]
    public void Ordering_FollowsTheLanguageDefinition(string expression, bool expected)
    {
        Assert.Equal(expected, environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // Lists, maps, null and type values have no ordering: no overload takes them.
    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("[1] < [2]", 4)]
    [InlineData("{1: 1} > {1: 1}", 7)]
    [InlineData("type(1) <= type(2)", 8)]
    public void ValuesWithoutOrdering_AreNoMatchingOverload(string expression, int offset)
    {
        CelError error = Assert.Single(environment.Compile<bool>(expression).Errors);

        Assert.Equal((CelErrorCode.NoMatchingOverload, offset), (error.Code, error.Location.Offset));
    }
}
