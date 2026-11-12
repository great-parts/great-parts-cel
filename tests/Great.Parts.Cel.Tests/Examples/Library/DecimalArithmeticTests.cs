#if NET11_0_OR_GREATER
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The arithmetic of the set decimal, version 1 (the page of the extension set decimal, "The 18 overloads", "What Decimal128 does"):
// the overloads add_decimal, subtract_decimal, multiply_decimal, divide_decimal and negate_decimal, that is _+_, _-_, _*_, _/_ and
// -_ on two decimals or one. The result is exact when it has at most 34 digits and rounded to 34 digits half to even when it has
// more; its exponent (the cohort) is that of IEEE 754-2019: the sum and the difference have the smaller exponent, the product the
// sum of the exponents, the quotient the exponent nearest to e1 - e2 that keeps it exact (so 6 / 2 is 3 and 1.00 / 4 is 0.25),
// and 34 digits when it is not exact. A row that shows the exponent is written as string(x). The sign of a zero: a sum or a
// difference is a negative zero only when both operands of the sum are, a product and a quotient follow the signs, the negation
// of a zero is a negative zero that keeps the exponent. The range is the exponent from -6176 to 6111: a result above it whose
// coefficient can be padded with zeros is clamped (1E+6111 * 1E+1 is 10E+6111, text 1.0E+6112), a result that cannot is the
// error Overflow (3006, "decimal overflow") at the operator, and a result below it is rounded away to a zero or a subnormal value
// without an error. A divisor that is zero, of any cohort and any sign, is DivisionByZero (3004, "division by zero") at the
// operator, also for 0.0m / 0.0m, which Decimal128 itself would give as a NaN. The values are those of the rules of IEEE 754-2019
// (there is no decimal in cel-go). Rows are those of ExtensionRows; Max34 is 34 nines, so Max34e6111m is the largest value.
[Trait("Requirement", "F-5")]
[Trait("Requirement", "F-1")]
public sealed class DecimalArithmeticTests
{
    private const string Max34 = DecimalExample.Max34;

    private const string Add = """
        string(1.50m + 1.5m) => '3.00'
        string(0.1m + 0.2m) => '0.3'
        string(1m + 2m) => '3'
        string(1e2m + 1m) => '101'
        string(1.5m + 2.5m) => '4.0'
        string(123.45m + 0.55m) => '124.00'
        string(1.5m + 2m) => '3.5'
        string(-0m + -0m) => '-0'
        string(-0m + 0m) => '0'
        string(0m + -0m) => '0'
        string(1e6111m + 1e6111m) => '2E+6111'
        dyn(1.5m) + dyn(2.5m) == 4m => true
        string(dyn(1.5m) + dyn(2.5m)) => '4.0'
        """;

    private const string Subtract = """
        string(2.5m - 1.5m) => '1.0'
        string(1.5m - 1.5m) => '0.0'
        string(0.0m - 0.0m) => '0.0'
        string(0m - 0m) => '0'
        string(-0m - 0m) => '-0'
        string(0m - -0m) => '0'
        string(1.5m - 2.5m) => '-1.0'
        string(0.1m + 0.2m - 0.3m) => '0.0'
        string(1e2m - 1m) => '99'
        """;

    private const string Multiply = """
        string(1.5m * 2m) => '3.0'
        string(1.0m * 1.0m) => '1.00'
        string(0.1m * 0.1m) => '0.01'
        string(1.5m * 2.5m) => '3.75'
        string(0.1m * 3m) => '0.3'
        string(-0m * 5m) => '-0'
        string(0m * -5m) => '-0'
        string(0m * 5m) => '0'
        string(-0m * -5m) => '0'
        string(1e2m * 1e3m) => '1E+5'
        """;

    private const string Divide = """
        string(1m / 3m) => '0.3333333333333333333333333333333333'
        string(1.0m / 3.0m) => '0.3333333333333333333333333333333333'
        string(2m / 3m) => '0.6666666666666666666666666666666667'
        string(-1m / 3m) => '-0.3333333333333333333333333333333333'
        string(1m / 7m) => '0.1428571428571428571428571428571429'
        string(6m / 2m) => '3'
        string(1.00m / 4m) => '0.25'
        string(10m / 4m) => '2.5'
        string(4.0m / 2m) => '2.0'
        string(1.5m / 1.5m) => '1'
        string(1m / 8m) => '0.125'
        string(0m / 5m) => '0'
        string(0.00m / 5m) => '0.00'
        string(1e2m / 1e1m) => '1E+1'
        string(10m / 3m * 3m) => '9.999999999999999999999999999999999'
        """;

    private const string Negate = """
        string(-(1.50m)) => '-1.50'
        string(-(-(1.5m))) => '1.5'
        string(-(0m)) => '-0'
        string(-(-0m)) => '0'
        string(-(0.0m)) => '-0.0'
        string(-(-0.0m)) => '0.0'
        string(-dyn(1.5m)) => '-1.5'
        -(1.5m) + 1.5m == 0m => true
        """;

    // The results that need more than 34 digits are rounded to 34, half to even: a tie goes to the even digit and a result above the
    // tie goes up; a carry out of the 34 digits moves the exponent.
    private const string Rounding = $$"""
        string({{Max34}}m + 1m) => '1.000000000000000000000000000000000E+34'
        string(1234567890123456789012345678901234m + 0.5m) => '1234567890123456789012345678901234'
        string(1234567890123456789012345678901235m + 0.5m) => '1234567890123456789012345678901236'
        string(1234567890123456789012345678901234m + 0.51m) => '1234567890123456789012345678901235'
        string(1234567890123456789012345678901234m * 10m) => '1.234567890123456789012345678901234E+34'
        """;

    // The range of the exponent: the clamp above it, the zero and the subnormal value below it, none of them an error.
    private const string Range = """
        string(1e6111m * 1e1m) => '1.0E+6112'
        string(1e6111m * 1e2m) => '1.00E+6113'
        string(-1e6111m * 1e1m) => '-1.0E+6112'
        string(1e-6176m * 0.1m) => '0E-6176'
        string(1e-6176m / 10m) => '0E-6176'
        string(5e-6176m * 0.5m) => '2E-6176'
        1e-6176m * 0.1m == 0m => true
        """;

    private const string Overflow = $$"""
        Evaluate | {{Max34}}e6111m ^* 10m | Overflow | decimal overflow
        Evaluate | {{Max34}}e6111m ^+ {{Max34}}e6111m | Overflow | decimal overflow
        Evaluate | -{{Max34}}e6111m ^- {{Max34}}e6111m | Overflow | decimal overflow
        Evaluate | -{{Max34}}e6111m ^* 10m | Overflow | decimal overflow
        Evaluate | {{Max34}}e6111m ^/ 0.1m | Overflow | decimal overflow
        Evaluate | 1m + {{Max34}}e6111m ^* 10m | Overflow | decimal overflow
        """;

    private const string DivisionByZero = """
        Evaluate | 1m ^/ 0m | DivisionByZero | division by zero
        Evaluate | 0m ^/ 0m | DivisionByZero | division by zero
        Evaluate | 0.0m ^/ 0.0m | DivisionByZero | division by zero
        Evaluate | 1m ^/ -0.00m | DivisionByZero | division by zero
        Evaluate | 1e2m ^/ 0e3m | DivisionByZero | division by zero
        Evaluate | -1.5m ^/ 0m | DivisionByZero | division by zero
        Evaluate | 1m + 2m ^/ 0m | DivisionByZero | division by zero
        """;

    // The operators work inside the comprehensions and with dyn as everywhere.
    private const string Macros = """
        [1.5m, 2.5m].map(x, x * 2m) => [3m, 5m]
        string([1.5m, 2.5m].map(x, x * 2m)[0]) => '3.0'
        [1.5m, 2.5m].all(x, x > 1m) => true
        [1.5m, 2.5m].exists(x, x > 2m) => true
        [1.5m, 2.5m].exists_one(x, x > 2m) => true
        [1.5m, 2.5m, 3.5m].filter(x, x > 2m) => [2.5m, 3.5m]
        true ? 1.5m : 2.5m => 1.5m
        """;

    private readonly CelEnvironment environment = DecimalExample.Create();

    [Fact]
    public void Add_KeepsTheSmallerExponent_AndTheSignOfTheZeroSum() => Assert.Empty(ExtensionRows.CheckValues(environment, Add));

    [Fact]
    public void Subtract_KeepsTheSmallerExponent_AndTheSignOfTheZeroDifference() => Assert.Empty(ExtensionRows.CheckValues(environment, Subtract));

    [Fact]
    public void Multiply_AddsTheExponents_AndFollowsTheSignsOfTheZero() => Assert.Empty(ExtensionRows.CheckValues(environment, Multiply));

    [Fact]
    public void Divide_GivesTheNearestExponentWhenExact_AndThe34DigitsOtherwise() => Assert.Empty(ExtensionRows.CheckValues(environment, Divide));

    [Fact]
    public void Negate_KeepsTheExponent_AndMakesANegativeZero() => Assert.Empty(ExtensionRows.CheckValues(environment, Negate));

    [Fact]
    [Trait("Requirement", "R-5")]
    public void ResultOfMoreThan34Digits_IsRoundedHalfToEven() => Assert.Empty(ExtensionRows.CheckValues(environment, Rounding));

    [Fact]
    [Trait("Requirement", "R-5")]
    public void ExponentOutOfTheRange_IsClampedOrRoundedAwayWithoutAnError() => Assert.Empty(ExtensionRows.CheckValues(environment, Range));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void ResultThatNoDecimalCanHold_IsOverflowAtTheOperator() => Assert.Empty(ExtensionRows.CheckErrors(environment, Overflow));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void DivisorThatIsZeroOfAnyCohort_IsDivisionByZeroAtTheOperator() => Assert.Empty(ExtensionRows.CheckErrors(environment, DivisionByZero));

    [Fact]
    public void Operators_WorkInsideTheMacros() => Assert.Empty(ExtensionRows.CheckValues(environment, Macros));
}
#endif
