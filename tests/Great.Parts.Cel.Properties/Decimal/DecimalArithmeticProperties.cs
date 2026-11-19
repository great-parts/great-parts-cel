using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Properties.Decimals;

// The arithmetic and the comparison of decimal against the exact reference, on random values and on the boundaries: on net11.0 34
// and 35 digits, the extreme exponents and subnormal values, on net10.0 28 and 29 digits, 2^96 - 1 and 2^96 and the exponent -28;
// on both the zeros of both signs, 1 / 3, overflow, a result that vanishes and a quotient with its cohort. Each property compares
// every case with no tolerance: the sign, the coefficient and the exponent of a result, or the error code exactly when the
// reference has no value (Overflow, DivisionByZero).
public sealed class DecimalArithmeticProperties
{
    private const ulong ArithmeticSeed = 20261201;
    private const ulong ComparisonSeed = 20261202;
    private const int Cases = 3000;

    private static readonly CelProgram<PlatformDecimal> Sum = DecimalHarness.Program<PlatformDecimal>("a + b");
    private static readonly CelProgram<PlatformDecimal> Difference = DecimalHarness.Program<PlatformDecimal>("a - b");
    private static readonly CelProgram<PlatformDecimal> Product = DecimalHarness.Program<PlatformDecimal>("a * b");
    private static readonly CelProgram<PlatformDecimal> Quotient = DecimalHarness.Program<PlatformDecimal>("a / b");
    private static readonly CelProgram<PlatformDecimal> Negation = DecimalHarness.Program<PlatformDecimal>("-a");

    private static readonly CelProgram<bool> Less = DecimalHarness.Program<bool>("a < b");
    private static readonly CelProgram<bool> LessOrEqual = DecimalHarness.Program<bool>("a <= b");
    private static readonly CelProgram<bool> Greater = DecimalHarness.Program<bool>("a > b");
    private static readonly CelProgram<bool> GreaterOrEqual = DecimalHarness.Program<bool>("a >= b");
    private static readonly CelProgram<bool> Equal = DecimalHarness.Program<bool>("a == b");
    private static readonly CelProgram<bool> NotEqual = DecimalHarness.Program<bool>("a != b");

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Arithmetic_EqualsTheReference() =>
        PropertyLoop.Check(nameof(Arithmetic_EqualsTheReference), DecimalGenerators.Pairs, ArithmeticSeed, Cases, ArithmeticViolation, pair => pair.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Arithmetic_EqualsTheReferenceOnTheBoundaries() =>
        DecimalHarness.CheckAll(nameof(Arithmetic_EqualsTheReferenceOnTheBoundaries), DecimalGenerators.EdgePairs(), ArithmeticViolation, pair => pair.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Comparison_EqualsTheReference() =>
        PropertyLoop.Check(nameof(Comparison_EqualsTheReference), DecimalGenerators.Pairs, ComparisonSeed, Cases, ComparisonViolation, pair => pair.ToString());

    [Fact]
    [Trait("Requirement", "F-5")]
    public void Comparison_EqualsTheReferenceOnTheBoundaries() =>
        DecimalHarness.CheckAll(nameof(Comparison_EqualsTheReferenceOnTheBoundaries), DecimalGenerators.EdgePairs(), ComparisonViolation, pair => pair.ToString());

    private static string? ArithmeticViolation(RefPair pair)
    {
        RefDecimal left = pair.First;
        RefDecimal right = pair.Second;
        CelBindings bindings = DecimalHarness.Bind(pair);

        // A divisor that is a zero, of any sign and cohort, is DivisionByZero before the division, a zero dividend included.
        string quotient = right.IsZero
            ? DecimalHarness.ExpectedError(CelErrorCode.DivisionByZero)
            : DecimalHarness.Expected(ReferenceDecimal.Divide(left, right), CelErrorCode.Overflow);
        (string Operation, CelProgram<PlatformDecimal> Program, string Expected)[] checks =
        [
            ("a + b", Sum, DecimalHarness.Expected(ReferenceDecimal.Add(left, right), CelErrorCode.Overflow)),
            ("a - b", Difference, DecimalHarness.Expected(ReferenceDecimal.Subtract(left, right), CelErrorCode.Overflow)),
            ("a * b", Product, DecimalHarness.Expected(ReferenceDecimal.Multiply(left, right), CelErrorCode.Overflow)),
            ("a / b", Quotient, quotient),
            ("-a", Negation, ReferenceDecimal.Negate(left).ToString()),
        ];

        foreach ((string operation, CelProgram<PlatformDecimal> program, string expected) in checks)
        {
            if (DecimalHarness.Differs(operation, expected, DecimalHarness.Observe(program.Evaluate(bindings), DecimalHarness.Parts)) is { } difference)
            {
                return difference + " for " + pair;
            }
        }

        return null;
    }

    private static string? ComparisonViolation(RefPair pair)
    {
        int order = ReferenceDecimal.Compare(pair.First, pair.Second);
        CelBindings bindings = DecimalHarness.Bind(pair);
        (string Operation, CelProgram<bool> Program, bool Expected)[] checks =
        [
            ("a < b", Less, order < 0),
            ("a <= b", LessOrEqual, order <= 0),
            ("a > b", Greater, order > 0),
            ("a >= b", GreaterOrEqual, order >= 0),
            ("a == b", Equal, order == 0),
            ("a != b", NotEqual, order != 0),
        ];

        foreach ((string operation, CelProgram<bool> program, bool expected) in checks)
        {
            if (DecimalHarness.Differs(operation, DecimalHarness.Truth(expected), DecimalHarness.Observe(program.Evaluate(bindings), DecimalHarness.Truth)) is { } difference)
            {
                return difference;
            }
        }

        return null;
    }
}
