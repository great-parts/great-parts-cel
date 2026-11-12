#if NET11_0_OR_GREATER
using Great.Parts.Cel.Diagnostics;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Decimals.DecimalEnvironment;

namespace Great.Parts.Cel.Tests.Library.Decimals;

// The units of the set decimal where a call ends in an error: the fixed unit and the part of the arguments are charged before the
// function runs, so the error is reached with a limit equal to the cost of the call and with one unit less the evaluation stops at
// the limit instead. Each row gives the units of the whole expression: a read of a variable is 1, a literal 0.
public sealed class DecimalCostTests
{
    private const string Largest = "9.999999999999999999999999999999999E+6144";

    // x / y with a zero divisor: the two reads and the call, 3.
    [Fact]
    public static void DivisionByZero_IsReachedWithTheUnitsOfTheCall()
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1")).Add("y", Of("0"));

        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(Evaluate("x / y", bindings, 3).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("x / y", bindings, 2).Errors).Code);
    }

    // The largest value times 10 does not fit the 34 digits and the range: the two reads and the call, 3.
    [Fact]
    public static void Overflow_IsReachedWithTheUnitsOfTheCall()
    {
        CelBindings bindings = new CelBindings().Add("x", Of(Largest)).Add("y", Of("10"));

        Assert.Equal(CelErrorCode.Overflow, Assert.Single(Evaluate("x * y", bindings, 3).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("x * y", bindings, 2).Errors).Code);
    }

    // decimal(double) is 4 for the digits it finds, and the read of the variable 1; a NaN is refused after the 4 are charged.
    [Fact]
    public static void DoubleThatIsNoNumber_IsReachedWithTheFourUnitsOfTheConversion()
    {
        CelBindings bindings = new CelBindings().Add("x", double.NaN);

        Assert.Equal(CelErrorCode.ConversionRange, Assert.Single(Evaluate("decimal(x)", bindings, 5).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("decimal(x)", bindings, 4).Errors).Code);
    }

    // decimal(string) is 1 and ⌈L/10⌉ for the length of the string, charged before the string is read, so a string that does not parse
    // and one whose exponent is out of the range have been paid for: 'abc' and '1e-6200' are 3 and 7 characters, 1 unit each.
    [Theory]
    [InlineData("decimal('abc')", CelErrorCode.InvalidArgument)]
    [InlineData("decimal('1e-6200')", CelErrorCode.ConversionRange)]
    public static void StringThatIsRefused_IsReachedAfterItsLengthIsCharged(string expression, CelErrorCode code)
    {
        Assert.Equal(code, Assert.Single(Evaluate(expression, null, 2).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, 1).Errors).Code);
    }

    // int(x), uint(x) and double(x) are 1 unit and the read of x: a value out of the range is reached with 2.
    [Theory]
    [InlineData("int(x)")]
    [InlineData("uint(x)")]
    [InlineData("double(x)")]
    public static void ValueOutOfTheRangeOfANumber_IsReachedWithTheUnitOfTheConversion(string expression)
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1e400"));

        Assert.Equal(CelErrorCode.ConversionRange, Assert.Single(Evaluate(expression, bindings, 2).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, bindings, 1).Errors).Code);
    }
}
#endif
