using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// The 24 ordering overloads across int, uint and double that EnableCrossTypeNumericComparisons makes candidates for static types: the four
// operators on the six ordered pairs of types, each on the values at the edges of int64 and uint64, at 2^63 and 2^64 and next to them,
// and on NaN and the infinities. The rule is that of a dyn operand: int and uint are compared exactly, and an integer with a double as the
// integer rounded to the nearest double, so 9223372036854775807 and 9223372036854775808.0 are equal. The expected result is computed apart
// from the library, by BigInteger for two integers and by the conversion of C# for an integer and a double.
[Trait("Requirement", "F-29")]
public sealed class CrossTypeNumericOverloadTests
{
    private static readonly string[] Operators = ["<", "<=", ">", ">="];

    private static readonly string[] Names = ["i", "u", "d"];

    private static readonly string[] NaNExpressions = ["i < d", "i <= d", "i > d", "i >= d", "d < u", "d <= u", "d > u", "d >= u"];

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("i", CelType.Int)
        .AddVariable("u", CelType.UInt)
        .AddVariable("d", CelType.Double)
        .EnableCrossTypeNumericComparisons()
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly long[] Ints = [long.MinValue, long.MinValue + 1, -1, 0, 1, long.MaxValue - 1, long.MaxValue];

    private static readonly ulong[] UInts = [0, 1, (ulong)long.MaxValue, (ulong)long.MaxValue + 1, ulong.MaxValue - 1, ulong.MaxValue];

    private static readonly double[] Doubles =
    [
        double.NaN,
        double.NegativeInfinity,
        double.PositiveInfinity,
        -9223372036854775808.0,
        -9223372036854774784.0,
        9223372036854774784.0,
        9223372036854775808.0,
        9223372036854777856.0,
        18446744073709549568.0,
        18446744073709551616.0,
        0.0,
        -0.0,
        0.5,
        -0.5,
        1.0,
        -1.0,
        1.5,
        2.5,
    ];

    public static TheoryData<string, string> Overloads
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach ((string expression, string op) in Table())
            {
                data.Add(expression, op);
            }

            return data;
        }
    }

    [Fact]
    public static void TheTable_HasTwentyFourOverloads()
    {
        Assert.Equal(24, Table().Count);
        Assert.Equal(24, Table().Select(row => row.Expression).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Overloads))]
    public static void Overload_AgreesWithTheExactComparison_OnTheEdges(string expression, string op)
    {
        CelProgram<bool> program = Environment.Compile<bool>(expression).GetProgramOrThrow();
        string left = expression[..1];
        string right = expression[^1..];
        int pairs = 0;

        foreach (object x in Values(left))
        {
            foreach (object y in Values(right))
            {
                CelBindings bindings = Bind(Bind(new CelBindings(), left, x), right, y);

                bool actual = program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

                Assert.True(Holds(Compare(x, y), op) == actual, expression + " with " + Show(x) + " and " + Show(y) + " gave " + actual);
                pairs++;
            }
        }

        Assert.True(pairs >= Ints.Length * UInts.Length);
    }

    // Neighbours that a double cannot tell apart from the integer next to it: 2^63 - 1 and 2^63, 2^64 - 1 and 2^64. Two integers are
    // ordered exactly; an integer and a double are equal when the integer rounds to the double.
    [Theory]
    [InlineData("i < u", "9223372036854775807", "9223372036854775808", true)]
    [InlineData("u > i", "9223372036854775808", "9223372036854775807", true)]
    [InlineData("i < d", "9223372036854775807", "9223372036854775808.0", false)]
    [InlineData("i <= d", "9223372036854775807", "9223372036854775808.0", true)]
    [InlineData("d > i", "9223372036854775808.0", "9223372036854775807", false)]
    [InlineData("u < d", "18446744073709551615", "18446744073709551616.0", false)]
    [InlineData("d >= u", "18446744073709551616.0", "18446744073709551615", true)]
    [InlineData("i <= d", "9223372036854775807", "9223372036854774784.0", false)]
    public static void NeighboursOfTheEdges_AreOrderedAsTheRuleSays(string expression, string first, string second, bool expected)
    {
        CelBindings bindings = BindText(BindText(new CelBindings(), expression[..1], first), expression[^1..], second);

        Assert.Equal(expected, Environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public static void WithNaN_EveryOrderingIsFalse()
    {
        foreach (string expression in NaNExpressions)
        {
            CelProgram<bool> program = Environment.Compile<bool>(expression).GetProgramOrThrow();
            CelBindings bindings = new CelBindings().Add("i", 0L).Add("u", 0UL).Add("d", double.NaN);

            Assert.False(program.Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow(), expression);
        }
    }

    private static List<(string Expression, string Operator)> Table()
    {
        var rows = new List<(string, string)>();
        foreach (string left in Names)
        {
            foreach (string right in Names.Where(name => name != left))
            {
                foreach (string op in Operators)
                {
                    rows.Add((left + " " + op + " " + right, op));
                }
            }
        }

        return rows;
    }

    private static CelBindings Bind(CelBindings bindings, string name, object value) => value switch
    {
        long signed => bindings.Add(name, signed),
        ulong unsigned => bindings.Add(name, unsigned),
        _ => bindings.Add(name, (double)value),
    };

    private static CelBindings BindText(CelBindings bindings, string name, string text) => name switch
    {
        "i" => bindings.Add(name, long.Parse(text, CultureInfo.InvariantCulture)),
        "u" => bindings.Add(name, ulong.Parse(text, CultureInfo.InvariantCulture)),
        _ => bindings.Add(name, double.Parse(text, CultureInfo.InvariantCulture)),
    };

    private static IEnumerable<object> Values(string name) => name switch
    {
        "i" => Ints.Cast<object>(),
        "u" => UInts.Cast<object>(),
        _ => Doubles.Cast<object>(),
    };

    private static string Show(object value) => string.Create(CultureInfo.InvariantCulture, $"{value.GetType().Name} {value}");

    // The sign of the comparison of x with y, or null when one of them is NaN.
    private static int? Compare(object x, object y)
    {
        if (x is not double && y is not double)
        {
            return ToInteger(x).CompareTo(ToInteger(y));
        }

        double left = ToDouble(x);
        double right = ToDouble(y);
        return double.IsNaN(left) || double.IsNaN(right) ? null : left.CompareTo(right);
    }

    private static BigInteger ToInteger(object value) => value is long signed ? signed : (ulong)value;

    // An integer rounded to the nearest double, which is what the conversion of C# does.
    private static double ToDouble(object value) => value switch
    {
        double number => number,
        long signed => signed,
        _ => (ulong)value,
    };

    private static bool Holds(int? sign, string op) => sign is { } s && op switch
    {
        "<" => s < 0,
        "<=" => s <= 0,
        ">" => s > 0,
        _ => s >= 0,
    };
}
