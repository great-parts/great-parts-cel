#if NET11_0_OR_GREATER
using System;
using System.Numerics;

namespace Great.Parts.Cel.Values;

// A finite decimal128 value taken apart: the value is Coefficient × 10^Exponent, negated when Negative is set. The coefficient is
// from 0 to 10^34 − 1 and the exponent from −6176 to 6111; the coefficient with its trailing zeros is the cohort, so 1.50 is 150
// and −2 and 1.5 is 15 and −1, and a zero has a sign and an exponent too. The parts are read from and written to the bits of the
// binary integer encoding of IEEE 754-2019 that Decimal128.EncodeBinary and DecodeBinary give and take: the sign in bit 127, the
// exponent plus 6176 in bits 113 to 126 and the coefficient in bits 0 to 112. A NaN and an infinity are never passed: every
// caller has tested Decimal128.IsFinite.
internal readonly record struct DecimalParts(bool Negative, UInt128 Coefficient, int Exponent)
{
    // The most digits of a coefficient.
    internal const int MaxDigits = 34;

    internal const int MinExponent = -6176;

    internal const int MaxExponent = 6111;

    private const int ExponentBias = 6176;

    private const int CoefficientBits = 113;

    private const int SignShift = 127;

    // The mark of a value that a System.Decimal made, kept in the reference field of CelValue: only the text of an error reads it.
    internal static readonly object SystemDecimalOrigin = new();

    // The bits 0 to 112 of the encoding.
    private static readonly UInt128 CoefficientMask = new(0x0001_FFFF_FFFF_FFFFUL, ulong.MaxValue);

    // The powers of ten from 10^0 to 10^38, the largest that a UInt128 holds.
    internal static readonly UInt128[] Powers = CreatePowers();

    // 10^34 − 1.
    internal static UInt128 MaxCoefficient => Powers[MaxDigits] - UInt128.One;

    // The parts of a finite value.
    internal static DecimalParts From(Decimal128 value) => FromBits(Decimal128.EncodeBinary(value));

    // The parts of a finite value by its bits. A coefficient above 10^34 − 1, which only raw bits can have, is a zero, as it is for
    // Decimal128; so is the form whose combination field starts with 11, with the exponent of bits 111 to 124.
    internal static DecimalParts FromBits(UInt128 bits)
    {
        ulong high = (ulong)(bits >> 64);
        bool negative = (high >> 63) != 0;
        if (((high >> 61) & 3) == 3)
        {
            return new DecimalParts(negative, UInt128.Zero, (int)((high >> 47) & 0x3FFF) - ExponentBias);
        }

        UInt128 coefficient = bits & CoefficientMask;
        return new DecimalParts(negative, coefficient > MaxCoefficient ? UInt128.Zero : coefficient, (int)((high >> 49) & 0x3FFF) - ExponentBias);
    }

    // The bits of the canonical encoding; the parts are in range.
    internal UInt128 ToBits() =>
        (Negative ? UInt128.One << SignShift : UInt128.Zero) | ((UInt128)(uint)(Exponent + ExponentBias) << CoefficientBits) | Coefficient;

    internal Decimal128 ToDecimal128() => Decimal128.DecodeBinary(ToBits());

    // The number of decimal digits of a coefficient, 1 for zero, by the position of the highest bit and one comparison.
    internal static int DigitCount(UInt128 coefficient)
    {
        if (coefficient == UInt128.Zero)
        {
            return 1;
        }

        int estimate = (((int)UInt128.Log2(coefficient) + 1) * 1233) >> 12;
        return estimate - (coefficient < Powers[estimate] ? 1 : 0) + 1;
    }

    // The coefficient without its last count digits, rounded half to even by the digits that go: a tie goes up exactly when the kept
    // part is odd. More than 38 digits leave nothing but zero, since a coefficient has at most 34.
    internal static UInt128 DropDigits(UInt128 coefficient, int count)
    {
        if (count >= Powers.Length)
        {
            return UInt128.Zero;
        }

        UInt128 power = Powers[count];
        (UInt128 kept, UInt128 gone) = UInt128.DivRem(coefficient, power);
        UInt128 half = power >> 1;
        return gone > half || (gone == half && (kept & UInt128.One) != UInt128.Zero) ? kept + UInt128.One : kept;
    }

    private static UInt128[] CreatePowers()
    {
        var powers = new UInt128[39];
        powers[0] = UInt128.One;
        for (int index = 1; index < powers.Length; index++)
        {
            powers[index] = powers[index - 1] * 10;
        }

        return powers;
    }
}
#endif
