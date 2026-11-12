#if NET11_0_OR_GREATER
using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Decimals.DecimalEnvironment;

namespace Great.Parts.Cel.Tests.Library.Decimals;

// The functions of the set decimal where the values come from the application and not from literals of the text: a divisor that is a
// zero in an encoding that no literal gives, the largest values of System.Decimal against int and uint, a string that fails to parse
// held by reference, and the sets whose functions refuse a decimal (encoders and strings).
public sealed class DecimalFunctionTests
{
    private const string StringClause =
        "invalid argument to 'format': error during formatting: string clause can only be used on strings, bools, bytes, ints, doubles, "
        + "maps, lists, types, durations, and timestamps, was given decimal";

    private const string DecimalClause =
        "invalid argument to 'format': error during formatting: decimal clause can only be used on ints, uints, and doubles, was given decimal";

    // A divisor that Decimal128 reads as a zero is a zero for the test of the divisor, whatever its sign, its exponent or its bits.
    [Theory]
    [InlineData(-6176, false)]
    [InlineData(-6176, true)]
    [InlineData(0, true)]
    [InlineData(6111, false)]
    [InlineData(6111, true)]
    public static void ZeroDivisorOfAnyExponentAndSign_IsDivisionByZeroAtTheOperator(int exponent, bool negative)
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1")).Add("y", Make(UInt128.Zero, exponent, negative));

        CelError error = Assert.Single(Evaluate("x / y", bindings).Errors);

        Assert.Equal((CelErrorCode.DivisionByZero, "division by zero", 2), (error.Code, error.Message, error.Location.Offset));
    }

    // A coefficient above 10^34 - 1 is a pattern that IEEE 754 reads as a zero: the divisor is a zero, not a number that
    // Decimal128 turns into a NaN, which would be Overflow.
    [Fact]
    public static void ZeroDivisorThatIsAPatternOfTooManyDigits_IsDivisionByZero()
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1")).Add("y", Make(DecimalParts.Powers[DecimalParts.MaxDigits], 0));

        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(Evaluate("x / y", bindings).Errors).Code);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(Evaluate("y / y", bindings).Errors).Code);
    }

    // The error of a conversion holds the value and writes it when the message is read: the largest and the least System.Decimal are
    // 29 digits, which an int and a uint do not hold.
    [Theory]
    [InlineData("int(x)", 3, 1, "79228162514264337593543950335 is out of the range of int")]
    [InlineData("uint(x)", 4, 1, "79228162514264337593543950335 is out of the range of uint")]
    [InlineData("int(x)", 3, -1, "-79228162514264337593543950335 is out of the range of int")]
    [InlineData("uint(x)", 4, -1, "-79228162514264337593543950335 is out of the range of uint")]
    public static void SystemDecimalBeyondAnInteger_IsConversionRangeWithItsDigits(string expression, int offset, int sign, string message)
    {
        CelBindings bindings = new CelBindings().Add("x", sign > 0 ? decimal.MaxValue : decimal.MinValue);

        CelError error = Assert.Single(Evaluate(expression, bindings).Errors);

        Assert.Equal((CelErrorCode.ConversionRange, message, offset), (error.Code, error.Message, error.Location.Offset));
    }

    // The string that does not parse is held by reference and written whole when the message is read.
    [Fact]
    public static void StringThatDoesNotParse_IsNamedWholeInTheMessage()
    {
        string text = new('a', 1000);

        CelError error = Assert.Single(Evaluate("decimal(text)", new CelBindings().Add("text", text)).Errors);

        Assert.Equal(
            (CelErrorCode.InvalidArgument, "invalid argument to 'decimal': cannot parse " + text + " as decimal"),
            (error.Code, error.Message));
    }

    // A decimal is no JSON number: json.encode refuses it alone and inside a list and a map, as it refuses any value that has no JSON.
    [Theory]
    [InlineData("json.encode(x)")]
    [InlineData("json.encode([1, x])")]
    [InlineData("json.encode({'k': x})")]
    public static void JsonEncodeOfADecimal_IsInvalidArgument(string expression)
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1.5"));

        CelError error = Assert.Single(Evaluate(expression, bindings).Errors);

        Assert.Equal((CelErrorCode.InvalidArgument, "invalid argument to 'json.encode': unsupported type conversion"), (error.Code, error.Message));
    }

    // The clauses of format take no decimal in this version; the text names the type.
    [Theory]
    [InlineData("'%s'.format([x])", StringClause)]
    [InlineData("'%d'.format([x])", DecimalClause)]
    public static void FormatOfADecimal_IsInvalidArgumentThatNamesTheType(string expression, string message)
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1.5"));

        CelError error = Assert.Single(Evaluate(expression, bindings).Errors);

        Assert.Equal((CelErrorCode.InvalidArgument, message), (error.Code, error.Message));
    }

    // The way to a text of a decimal for those functions is string(x), whose text is the value of the cohort.
    [Fact]
    public static void StringOfADecimal_IsTheWayIntoFormat()
    {
        CelBindings bindings = new CelBindings().Add("x", Of("1.50"));

        Assert.True(ValueOf("'%s'.format([string(x)])", bindings).TryGetString(out string? text));
        Assert.Equal("1.50", text);
    }
}
#endif
