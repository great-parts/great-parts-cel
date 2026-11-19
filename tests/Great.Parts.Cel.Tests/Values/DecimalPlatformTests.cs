using System;
#if NET11_0_OR_GREATER
using System.Numerics;
using Great.Parts.Cel.Tests.Examples.Library;
#endif
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The platform of the set decimal: one value type for each target, a Decimal128 on net11.0 and a System.Decimal on
// net10.0, behind CelValue.FromDecimal and TryGetDecimal, and the few operations of DecimalPlatform that differ by target. The rules
// over the parts are those of DecimalValueTests (net11.0) and of the examples of the set; this file holds what the examples do not
// cover: the System.Decimal in and out on both targets, the read of a value that System.Decimal holds exactly, and on net10.0 the
// quotient with the exponent of IEEE 754 where the platform loses it, the single rounding of a text and the 96 bits of the literal.
public sealed class DecimalPlatformTests
{
    [Fact]
    public static void FromDecimal_KeepsTheDigitsTheScaleAndTheSignOfAZero_OnBothTargets()
    {
        decimal[] values =
        [
            1.50m, 1.5m, 0m, 0.00m, decimal.Negate(0m), decimal.Negate(0.0m), decimal.MaxValue, decimal.MinValue,
            0.0000000000000000000000000001m, 7.9228162514264337593543950335m, 100m, -12.34m,
        ];

        foreach (decimal value in values)
        {
            CelValue held = CelValue.FromDecimal(value);

            Assert.Equal(CelValueKind.Decimal, held.Kind);
            Assert.True(held.TryGetDecimal(out decimal back));
            Assert.Equal(decimal.GetBits(value), decimal.GetBits(back));
        }
    }

    [Fact]
    public static void TryGetDecimal_OfAnotherKind_IsFalse_WithZero()
    {
        foreach (CelValue other in new[] { CelValue.Null, CelValue.FromInt64(1), CelValue.FromDouble(1.5), CelValue.FromString("1.5") })
        {
            Assert.False(other.TryGetDecimal(out decimal number));
            Assert.Equal(0m, number);
        }
    }

    [Fact]
    public static void EqualityAndHash_AreByTheValue_NotByTheScale()
    {
        CelValue first = CelValue.FromDecimal(1.50m);
        CelValue second = CelValue.FromDecimal(1.5m);

        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(CelValue.FromDecimal(decimal.Negate(0m)).Equals(CelValue.FromDecimal(0m)));
        Assert.Equal(CelValue.FromDecimal(decimal.Negate(0m)).GetHashCode(), CelValue.FromDecimal(0m).GetHashCode());
        Assert.False(first.Equals(CelValue.FromDecimal(1.51m)));
        Assert.False(first.Equals(CelValue.FromDouble(1.5)));
    }

#if NET11_0_OR_GREATER
    // A Decimal128 that equals a System.Decimal exactly is read as one, with its scale where it has one, and a positive exponent
    // multiplied in; a value that System.Decimal does not hold exactly — more than 96 bits, an exponent below -28, a value above the
    // range — is false with zero, and TryGetDecimal128 reads it.
    [Fact]
    public static void TryGetDecimal_OnNet11_IsTrueOnlyForAValueThatSystemDecimalHoldsExactly()
    {
        Assert.True(CelValue.FromDecimal128(DecimalExample.Make(150, -2)).TryGetDecimal(out decimal scaled));
        Assert.Equal(2, scaled.Scale);
        Assert.Equal(1.50m, scaled);
        Assert.True(CelValue.FromDecimal128(DecimalExample.Make(1, 2)).TryGetDecimal(out decimal hundred));
        Assert.Equal(100m, hundred);
        Assert.True(CelValue.FromDecimal128(DecimalExample.Make(0, 5)).TryGetDecimal(out decimal zero));
        Assert.Equal(0m, zero);
        Assert.True(CelValue.FromDecimal128(DecimalExample.Make(1, -28)).TryGetDecimal(out decimal smallest));
        Assert.Equal(0.0000000000000000000000000001m, smallest);
        Assert.True(CelValue.FromDecimal128(DecimalExample.Make(UInt128.Parse("79228162514264337593543950335", System.Globalization.CultureInfo.InvariantCulture), 0)).TryGetDecimal(out decimal largest));
        Assert.Equal(decimal.MaxValue, largest);

        foreach (Decimal128 refused in new[]
        {
            DecimalExample.Make(1, -29),
            DecimalExample.Make(1, 29),
            DecimalExample.Make(UInt128.Parse("79228162514264337593543950336", System.Globalization.CultureInfo.InvariantCulture), 0),
            DecimalExample.Make(UInt128.Parse(DecimalExample.Max34, System.Globalization.CultureInfo.InvariantCulture), 0),
            DecimalExample.Make(15, -29),
            DecimalExample.Make(1, 6111),
        })
        {
            Assert.False(CelValue.FromDecimal128(refused).TryGetDecimal(out decimal none));
            Assert.Equal(0m, none);
            Assert.True(CelValue.FromDecimal128(refused).TryGetDecimal128(out Decimal128 exact));
            Assert.Equal(refused, exact);
        }
    }

    [Fact]
    public static void FromDecimal_OnNet11_IsTheDecimal128OfTheSameDigitsAndScale()
    {
        Assert.True(CelValue.FromDecimal(1.50m).TryGetDecimal128(out Decimal128 number));
        Assert.Equal((false, (UInt128)150, -2), DecimalExample.Parts(number));
        Assert.True(CelValue.FromDecimal(decimal.Negate(0.00m)).TryGetDecimal128(out Decimal128 negativeZero));
        Assert.Equal((true, UInt128.Zero, -2), DecimalExample.Parts(negativeZero));
    }
#else
    // The quotient of System.Decimal does not keep the ideal exponent of IEEE 754: the code gives it where the quotient is exact and
    // the platform holds the exponent, and leaves the quotient that is not exact to the platform (28 places at most, rounded).
    [Theory]
    [MemberData(nameof(Quotients))]
    public static void Divide_GivesTheIdealExponentOfAnExactQuotient_AndThePlatformQuotientOtherwise(decimal dividend, decimal divisor, string text)
    {
        Assert.True(DecimalPlatform.TryDivide(dividend, divisor, out decimal quotient));

        Assert.Equal(text, Text(quotient));
    }

    public static TheoryData<decimal, decimal, string> Quotients() => new()
    {
        { 6.0m, 2m, "3.0" },
        { 6m, 2m, "3" },
        { 1.00m, 4m, "0.25" },
        { 1m, 4m, "0.25" },
        { 1m, 0.01m, "100" },
        { 1.0m, 0.1m, "10" },
        { 100.0m, 4m, "25.0" },
        { 0.0m, 0.1m, "0" },
        { 0.00m, 5m, "0.00" },
        { decimal.Negate(0.0m), 5m, "-0.0" },
        { 0m, decimal.Negate(5m), "-0" },
        { 1m, 3m, "0.3333333333333333333333333333" },
        { 2m, 3m, "0.6666666666666666666666666667" },
        { 10m, 3m, "3.3333333333333333333333333333" },
        { 10000000000000000000000000000m, 3m, "3333333333333333333333333333.3" },
        { 1m, 0.0000000000000000000000000001m, "10000000000000000000000000000" },
        { 0.0000000000000000000000000001m, 0.0000000000000000000000000001m, "1" },
        { decimal.MaxValue, 5m, "15845632502852867518708790067" },
        { decimal.MaxValue, decimal.MaxValue, "1" },
        { 0.0000000000000000000000000001m, 10m, "0E-28" },
        { 0.00000000000000001m, 0.0000000000000000549755813888m, "0.1818989403545856475830078125" },
    };

    [Fact]
    public static void Divide_OfAQuotientThatDoesNotFit_IsAnOverflowOfThePlatform()
    {
        Assert.False(DecimalPlatform.TryDivide(decimal.MaxValue, 0.1m, out _));
        Assert.False(DecimalPlatform.TryDivide(decimal.MinValue, 0.5m, out _));
        Assert.False(DecimalPlatform.TryAdd(decimal.MaxValue, 1m, out _));
        Assert.False(DecimalPlatform.TrySubtract(decimal.MinValue, 1m, out _));
        Assert.False(DecimalPlatform.TryMultiply(decimal.MaxValue, 2m, out _));
    }

    // A text is rounded once, half to even, from its original digits: the 34 digits that a reading keeps are cut, not rounded, so the
    // digits beyond them still decide a tie. 0.1111111111111111111111111111 and 4999999 round down at the scale 28, but 34 digits
    // rounded first would end in a tie that goes up.
    [Theory]
    [InlineData("0.11111111111111111111111111114999999", "0.1111111111111111111111111111")]
    [InlineData("0.11111111111111111111111111115000000", "0.1111111111111111111111111112")]
    [InlineData("0.11111111111111111111111111115000001", "0.1111111111111111111111111112")]
    [InlineData("0.1111111111111111111111111111500000000000000000000000000000001", "0.1111111111111111111111111112")]
    [InlineData("0.1111111111111111111111111112500000000000000000000000000000000", "0.1111111111111111111111111112")]
    [InlineData("0.1111111111111111111111111112500000000000000000000000000000001", "0.1111111111111111111111111113")]
    [InlineData("79228162514264337593543950335.4999999999", "79228162514264337593543950335")]
    [InlineData("7922816251426433759354395033.55", "7922816251426433759354395034")]
    [InlineData("-0.00000000000000000000000000005", "-0E-28")]
    [InlineData("0e-99", "0E-28")]
    [InlineData("1e-400", "0E-28")]
    public static void TryParse_OfAString_RoundsOnceFromTheOriginalDigits(string text, string expected)
    {
        Assert.Equal(DecimalParseResult.Parsed, DecimalTextRules.TryParse(text, exact: false, out DecimalParts parts));

        Assert.Equal(expected, Format(parts));
    }

    [Theory]
    [InlineData("79228162514264337593543950335.5")]
    [InlineData("79228162514264337593543950336")]
    [InlineData("1e29")]
    [InlineData("1e99999999999")]
    [InlineData("123456789012345678901234567890")]
    public static void TryParse_OfAStringWhoseIntegerPartDoesNotFit96Bits_IsOutOfTheRange(string text) =>
        Assert.Equal(DecimalParseResult.ExponentOutOfRange, DecimalTextRules.TryParse(text, exact: false, out _));

    // The literal is exact and refused when System.Decimal does not hold it: more than 29 digits or a coefficient of 2^96 and more, an
    // exponent below -28 (the cohort written counts), a positive exponent whose value does not fit; a positive exponent that fits is
    // multiplied in, and a zero has the exponent 0.
    [Theory]
    [InlineData("12345678901234567890123456789", "Parsed", "12345678901234567890123456789")]
    [InlineData("79228162514264337593543950335", "Parsed", "79228162514264337593543950335")]
    [InlineData("79228162514264337593543950336", "TooManyDigits", "")]
    [InlineData("123456789012345678901234567890", "TooManyDigits", "")]
    [InlineData("0.1234567890123456789012345678901", "TooManyDigits", "")]
    [InlineData("1e-28", "Parsed", "1E-28")]
    [InlineData("1e-29", "ExponentOutOfRange", "")]
    [InlineData("1.0e-28", "ExponentOutOfRange", "")]
    [InlineData("0e-29", "ExponentOutOfRange", "")]
    [InlineData("1e28", "Parsed", "10000000000000000000000000000")]
    [InlineData("7e28", "Parsed", "70000000000000000000000000000")]
    [InlineData("8e28", "ValueOutOfRange", "")]
    [InlineData("1e29", "ValueOutOfRange", "")]
    [InlineData("1e99999999999", "ValueOutOfRange", "")]
    [InlineData("0e3", "Parsed", "0")]
    [InlineData("0e99999999999", "Parsed", "0")]
    [InlineData("1000e-3", "Parsed", "1.000")]
    public static void TryParse_OfALiteral_IsExactOrRefused(string text, string outcome, string expected)
    {
        Assert.Equal(outcome, DecimalTextRules.TryParse(text, exact: true, out DecimalParts parts).ToString());

        if (outcome == nameof(DecimalParseResult.Parsed))
        {
            Assert.Equal(expected, Format(parts));
        }
    }

    private static string Text(decimal value) => Format(DecimalPlatform.ToParts(value));

    private static string Format(DecimalParts parts)
    {
        Span<char> text = stackalloc char[DecimalTextRules.BufferLength];
        return new string(text[..DecimalTextRules.Format(parts, text)]);
    }
#endif
}
