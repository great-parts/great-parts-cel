#if NET11_0_OR_GREATER
using System;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The parts of a decimal and the bits they are read from: the sign, the coefficient and the exponent of the binary integer encoding
// of IEEE 754-2019, which Decimal128.EncodeBinary gives; the count of digits and the rounding of a coefficient that the conversions
// and the parser share.
public sealed class DecimalPartsTests
{
    [Fact]
    public static void Parts_SurviveTheBitsAndTheDecimal128_AtTheEdgesOfEveryField()
    {
        UInt128 max = DecimalParts.MaxCoefficient;
        DecimalParts[] cases =
        [
            new(false, 150UL, -2),
            new(true, 0UL, 0),
            new(false, 0UL, DecimalParts.MaxExponent),
            new(true, 0UL, DecimalParts.MinExponent),
            new(false, 1UL, DecimalParts.MinExponent),
            new(true, max, DecimalParts.MaxExponent),
            new(false, max, DecimalParts.MinExponent),
        ];

        foreach (DecimalParts parts in cases)
        {
            Assert.Equal(parts, DecimalParts.FromBits(parts.ToBits()));
            Assert.Equal(parts, DecimalParts.From(parts.ToDecimal128()));
        }
    }

    [Fact]
    public static void BitsOfTheEncoding_AreTheSignTheExponentPlus6176AndTheCoefficient()
    {
        UInt128 bits = new DecimalParts(true, 1_234UL, 5).ToBits();

        Assert.Equal(UInt128.One, bits >> 127);
        Assert.Equal((UInt128)(5 + 6176), (bits >> 113) & (UInt128)0x3FFF);
        Assert.Equal((UInt128)1_234UL, bits & ((UInt128.One << 113) - UInt128.One));
    }

    // A coefficient above 10^34 - 1 can only come from raw bits; Decimal128 and DecimalParts both read it as a zero, with the exponent.
    [Fact]
    public static void CoefficientAbove34Digits_IsReadAsAZero_WithItsExponent()
    {
        UInt128 bits = ((UInt128)(uint)(10 + 6176) << 113) | DecimalParts.Powers[DecimalParts.MaxDigits];

        Assert.Equal(new DecimalParts(false, UInt128.Zero, 10), DecimalParts.FromBits(bits));
        Assert.Equal(new DecimalParts(true, UInt128.Zero, 10), DecimalParts.FromBits(bits | (UInt128.One << 127)));
    }

    // The combination field that starts with 11 is, by IEEE 754, a zero whose exponent is in the bits 111 to 124.
    [Fact]
    public static void CombinationFieldThatStartsWith11_IsAZero_WithTheExponentOfItsOwnBits()
    {
        ulong high = (3UL << 61) | ((ulong)(5 + 6176) << 47);
        var bits = new UInt128(high, 12_345UL);

        Assert.Equal(new DecimalParts(false, UInt128.Zero, 5), DecimalParts.FromBits(bits));
        Assert.Equal(new DecimalParts(true, UInt128.Zero, 5), DecimalParts.FromBits(bits | (UInt128.One << 127)));
    }

    [Fact]
    public static void DigitCount_IsOneForZeroAndExactAtEveryPowerOfTen()
    {
        Assert.Equal(1, DecimalParts.DigitCount(UInt128.Zero));
        for (int digits = 1; digits < DecimalParts.Powers.Length; digits++)
        {
            UInt128 power = DecimalParts.Powers[digits - 1];

            Assert.Equal(digits, DecimalParts.DigitCount(power));
            Assert.Equal(digits, DecimalParts.DigitCount((power * 10) - UInt128.One));
        }

        Assert.Equal(1, DecimalParts.DigitCount(9UL));
        Assert.Equal(39, DecimalParts.DigitCount(UInt128.MaxValue));
        Assert.Equal(DecimalParts.MaxDigits, DecimalParts.DigitCount(DecimalParts.MaxCoefficient));
        Assert.Equal(DecimalParts.MaxDigits + 1, DecimalParts.DigitCount(DecimalParts.MaxCoefficient + UInt128.One));
    }

    // Half to even by the digits that go: a tie goes up exactly when the kept part is odd.
    [Fact]
    public static void DropDigits_RoundsHalfToEven()
    {
        (ulong Coefficient, int Count, ulong Expected)[] cases =
        [
            (25, 1, 2),
            (35, 1, 4),
            (15, 1, 2),
            (5, 1, 0),
            (7, 1, 1),
            (151, 2, 2),
            (150, 2, 2),
            (250, 2, 2),
            (249, 2, 2),
            (251, 2, 3),
            (99, 1, 10),
            (4_999, 3, 5),
            (500, 3, 0),
            (501, 3, 1),
        ];

        foreach ((ulong coefficient, int count, ulong expected) in cases)
        {
            Assert.Equal((UInt128)expected, DecimalParts.DropDigits(coefficient, count));
        }
    }

    [Fact]
    public static void DropDigits_OfMoreDigitsThanThereAre_LeavesAZeroOrOne()
    {
        Assert.Equal(UInt128.One, DecimalParts.DropDigits(DecimalParts.Powers[33] * 5 + UInt128.One, DecimalParts.MaxDigits));
        Assert.Equal(UInt128.Zero, DecimalParts.DropDigits(DecimalParts.Powers[33] * 5, DecimalParts.MaxDigits));
        Assert.Equal(UInt128.Zero, DecimalParts.DropDigits(DecimalParts.MaxCoefficient, 35));
        Assert.Equal(UInt128.Zero, DecimalParts.DropDigits(DecimalParts.MaxCoefficient, 38));
        Assert.Equal(UInt128.Zero, DecimalParts.DropDigits(DecimalParts.MaxCoefficient, 39));
        Assert.Equal(UInt128.Zero, DecimalParts.DropDigits(DecimalParts.MaxCoefficient, 1_000));
    }
}
#endif
