using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The decimal of each target: one set and one code of rules, the value Decimal128 on net11.0 (34 digits, exponent −6176…6111, the
// cohort of IEEE kept) and System.Decimal on net10.0 (96 bits of coefficient, exponent −28…0: a positive exponent is not kept).
// Each row has the outcome of both targets; where they differ, it differs only in the text of an equal value, the
// precision of an inexact result, an error for a value out of the range of the platform. Equality and order do not depend on the
// text. The other examples of decimal hold the rows whose outcome is the same on both targets, and the rows of 34 digits and of the
// exponents of decimal128, which are net11.0 only.
[Trait("Requirement", "F-18")]
[Trait("Requirement", "R-9")]
public sealed class DecimalTargetTests
{
    private static readonly CelEnvironment Environment = DecimalExample.Create();

    // The text of an equal value: a positive exponent is kept only on net11.0.
    [Theory]
#if NET11_0_OR_GREATER
    [InlineData("1e2m", "1E+2")]
    [InlineData("1e3m", "1E+3")]
    [InlineData("2.5E+3M", "2.5E+3")]
    [InlineData("1m / 0.01m", "1E+2")]
    [InlineData("decimal('1e30')", "1E+30")]
    [InlineData("decimal(1e300)", "1E+300")]
    [InlineData("decimal(1e6)", "1E+6")]
#else
    [InlineData("1e2m", "100")]
    [InlineData("1e3m", "1000")]
    [InlineData("2.5E+3M", "2500")]
    [InlineData("1m / 0.01m", "100")]
    [InlineData("decimal(1e6)", "1000000")]
#endif
    [InlineData("decimal(100.0)", "100")]
    [InlineData("100m", "100")]
    [InlineData("6.0m / 2m", "3.0")]
    [InlineData("6m / 2m", "3")]
    [InlineData("1.00m / 4m", "0.25")]
    [InlineData("1m / 4m", "0.25")]
    [InlineData("1.50m + 1.5m", "3.00")]
    [InlineData("1e-28m", "1E-28")]
    [InlineData("0.0000005m", "5E-7")]
    [InlineData("79228162514264337593543950335m", "79228162514264337593543950335")]
    [InlineData("-0.0m", "-0.0")]
    public static void Text(string expression, string text)
    {
        Assert.Equal(text, Evaluate<string>("string(" + expression + ")"));
    }

    // An inexact result is rounded half to even to the precision of the platform: 34 digits, or 28 places on net10.0.
    [Theory]
#if NET11_0_OR_GREATER
    [InlineData("1m / 3m", "0.3333333333333333333333333333333333")]
    [InlineData("2m / 3m", "0.6666666666666666666666666666666667")]
    [InlineData("0.0000000000000000000000000001m * 0.1m", "1E-29")]
    [InlineData("decimal('0.12345678901234567890123456789')", "0.12345678901234567890123456789")]
    [InlineData("decimal('1e-30')", "1E-30")]
    [InlineData("decimal(1e-30)", "1E-30")]
#else
    [InlineData("1m / 3m", "0.3333333333333333333333333333")]
    [InlineData("2m / 3m", "0.6666666666666666666666666667")]
    [InlineData("0.0000000000000000000000000001m * 0.1m", "0E-28")]
    [InlineData("decimal('0.12345678901234567890123456789')", "0.1234567890123456789012345679")]
    [InlineData("decimal('1e-30')", "0E-28")]
    [InlineData("decimal(1e-30)", "0E-28")]
#endif
    public static void Precision(string expression, string text)
    {
        Assert.Equal(text, Evaluate<string>("string(" + expression + ")"));
    }

    // Equal values are equal on both targets, whatever their text; the order is by value.
    [Theory]
    [InlineData("1e2m == 100m")]
    [InlineData("1e2m == 100.00m")]
    [InlineData("0.1m + 0.2m == 0.3m")]
    [InlineData("1m / 3m * 3m < 1m")]
    [InlineData("-0m == 0m")]
    [InlineData("int(1e2m) == 100")]
    [InlineData("decimal(string(1e2m)) == 1e2m")]
    public static void EqualityAndOrder_DoNotDependOnTheTarget(string expression)
    {
        Assert.True(Evaluate<bool>(expression));
    }

    // A literal that the platform does not hold exactly is InvalidLiteral at the literal, not a rounded value.
    [Theory]
#if NET11_0_OR_GREATER
    [InlineData("12345678901234567890123456789012345m", "invalid decimal literal: more than 34 significant digits")]
    [InlineData("1e6112m", "invalid decimal literal: exponent out of the range of decimal128")]
#else
    [InlineData("0.1234567890123456789012345678901m", "invalid decimal literal: more significant digits than System.Decimal holds")]
    [InlineData("79228162514264337593543950336m", "invalid decimal literal: more significant digits than System.Decimal holds")]
    [InlineData("1e-29m", "invalid decimal literal: exponent out of the range of System.Decimal")]
    [InlineData("1.0e-28m", "invalid decimal literal: exponent out of the range of System.Decimal")]
    [InlineData("1e29m", "invalid decimal literal: value out of the range of System.Decimal")]
#endif
    public static void LiteralOutsideThePlatform_IsInvalidLiteral(string literal, string message)
    {
        ArgumentNullException.ThrowIfNull(literal);
        CelError error = Assert.Single(Environment.Compile<CelValue>("1m == " + literal).Errors);

        Assert.Equal(CelErrorCode.InvalidLiteral, error.Code);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
        Assert.Equal(6, error.Location.Offset);
        Assert.Equal(literal.Length, error.Span.Length);
        Assert.Equal(message, error.Message);
    }

    // The same literals are values on net11.0: an error on net10.0 only where the platform cannot hold the value.
    [Theory]
    [InlineData("1e-29m")]
    [InlineData("1e29m")]
    [InlineData("79228162514264337593543950336m")]
    [InlineData("0.1234567890123456789012345678901m")]
    public static void LiteralOfNet11_IsAValueThereOnly(string literal)
    {
        CelCompileResult<CelValue> result = Environment.Compile<CelValue>(literal);

#if NET11_0_OR_GREATER
        Assert.True(result.IsSuccess);
#else
        Assert.Equal(CelErrorCode.InvalidLiteral, Assert.Single(result.Errors).Code);
#endif
    }

    // A result out of the range of the platform: Overflow for arithmetic, ConversionRange for a conversion to decimal.
    [Theory]
    [InlineData("79228162514264337593543950335m + 1m")]
    [InlineData("79228162514264337593543950335m * 2m")]
    [InlineData("decimal('1e30')")]
    [InlineData("decimal(1e300)")]
    public static void ResultOutOfTheRangeOfNet10(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

#if NET11_0_OR_GREATER
        Assert.True(result.IsSuccess);
#else
        Assert.Equal(expression.StartsWith("decimal(", StringComparison.Ordinal) ? CelErrorCode.ConversionRange : CelErrorCode.Overflow, Assert.Single(result.Errors).Code);
#endif
    }

    // System.Decimal of the application: exact on both targets, the scale kept; read back with TryGetDecimal(out decimal).
    [Fact]
    public void SystemDecimal_InAndOut()
    {
        CelValue value = CelValue.FromDecimal(1.50m);

        Assert.True(value.TryGetDecimal(out decimal back));
        Assert.Equal(1.50m, back);
        Assert.Equal(2, back.Scale);
        CelEnvironment withPrice = DecimalExample.Builder().AddVariable<decimal>("p").Build().GetEnvironmentOrThrow();
        decimal sum = withPrice.Compile<decimal>("p + p").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("p", 1.50m), cancellationToken: CancellationToken.None).GetValueOrThrow();
        Assert.Equal(3.00m, sum);
        Assert.Equal(2, sum.Scale);
    }

    private static T Evaluate<T>(string text) =>
        Environment.Compile<T>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}
