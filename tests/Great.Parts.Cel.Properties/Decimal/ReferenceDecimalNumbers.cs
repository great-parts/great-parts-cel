#if NET11_0_OR_GREATER
using System;
using System.Numerics;

namespace Great.Parts.Cel.Properties.Decimals;

// The conversions of a decimal value to and from the number types, written from the rules: the integers by truncation toward
// zero, the double by the nearest value with an exact comparison, System.Decimal by exact rounding. Every one works on the
// parts of a value, and none calls a conversion or a parsing of Decimal128.
internal static class ReferenceDecimalNumbers
{
    private static readonly BigInteger SystemDecimalLimit = (BigInteger.One << 96) - 1;

    private static readonly BigInteger DoubleSignificandLimit = BigInteger.One << 53;

    private static readonly BigInteger DoubleNormalLimit = BigInteger.One << 52;

    internal static RefDecimal FromInt64(long value) =>
        new(value < 0, BigInteger.Abs(value), 0);

    internal static RefDecimal FromUInt64(ulong value) =>
        new(false, value, 0);

    // The integer part: the digits after the point are dropped, whatever they are, and the sign stays.
    private static BigInteger Truncate(RefDecimal value)
    {
        BigInteger magnitude = value.Exponent >= 0
            ? value.Coefficient * ReferenceDecimal.Pow10(value.Exponent)
            : value.Coefficient / ReferenceDecimal.Pow10(-value.Exponent);
        return value.Negative ? -magnitude : magnitude;
    }

    internal static long? ToInt64(RefDecimal value)
    {
        BigInteger integer = Truncate(value);
        return integer >= long.MinValue && integer <= long.MaxValue ? (long)integer : null;
    }

    // A negative value that truncates to zero is zero, so uint(-0.5m) is 0u.
    internal static ulong? ToUInt64(RefDecimal value)
    {
        BigInteger integer = Truncate(value);
        return integer >= 0 && integer <= ulong.MaxValue ? (ulong)integer : null;
    }

    // The double nearest to the exact value, half to even, as its bits; null when the nearest is infinite.
    internal static long? ToDoubleBits(RefDecimal value)
    {
        long sign = value.Negative ? long.MinValue : 0;
        if (value.IsZero)
        {
            return sign;
        }

        BigInteger numerator = value.Coefficient * ReferenceDecimal.Pow10(Math.Max(value.Exponent, 0));
        BigInteger denominator = ReferenceDecimal.Pow10(Math.Max(-value.Exponent, 0));
        int unit = (int)Math.Max(FloorLog2(numerator, denominator) - 52, -1074);
        BigInteger significand = ReferenceDecimal.RoundDivide(Shifted(numerator, -unit), Shifted(denominator, unit));
        if (significand == DoubleSignificandLimit)
        {
            significand >>= 1;
            unit++;
        }

        if (significand < DoubleNormalLimit)
        {
            // A subnormal value, or a value that vanishes: the significand is the whole field.
            return sign | (long)significand;
        }

        long biased = unit + 52 + 1023;
        return biased >= 2047 ? null : sign | (biased << 52) | (long)(significand - DoubleNormalLimit);
    }

    private static BigInteger Shifted(BigInteger value, int shift) => shift >= 0 ? value << shift : value;

    // The largest k with 2^k not above numerator / denominator.
    private static long FloorLog2(BigInteger numerator, BigInteger denominator)
    {
        long estimate = numerator.GetBitLength() - denominator.GetBitLength();
        BigInteger scaledNumerator = estimate >= 0 ? numerator : numerator << (int)-estimate;
        BigInteger scaledDenominator = estimate >= 0 ? denominator << (int)estimate : denominator;
        return scaledNumerator >= scaledDenominator ? estimate : estimate - 1;
    }

    // The parts of a System.Decimal as the decimal value: the 96 bits of the coefficient, the exponent minus the scale, and the
    // sign, a zero included.
    internal static RefDecimal FromSystemDecimal(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger coefficient = ((BigInteger)(uint)bits[2] << 64) | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        return new RefDecimal(bits[3] < 0, coefficient, -((bits[3] >> 16) & 0xFF));
    }

    // The value as the parts of a System.Decimal, or null when it is out of its range. The coefficient has at most 96 bits and
    // the scale is from 0 to 28; a value with more places is rounded half to even from its exact digits, and a value whose
    // rounded coefficient does not fit loses one more place, from the exact digits again.
    internal static RefDecimal? ToSystemDecimal(RefDecimal value)
    {
        if (value.Exponent >= 0)
        {
            BigInteger whole = value.Coefficient * ReferenceDecimal.Pow10(value.Exponent);
            return whole > SystemDecimalLimit ? null : new RefDecimal(value.Negative, whole, 0);
        }

        int places = -value.Exponent;
        int scale = Math.Min(places, 28);
        BigInteger rounded = Rounded(value.Coefficient, places - scale);
        while (rounded > SystemDecimalLimit && scale >= 0)
        {
            scale--;
            rounded = Rounded(value.Coefficient, places - scale);
        }

        return scale < 0 ? null : new RefDecimal(value.Negative, rounded, -scale);
    }

    private static BigInteger Rounded(BigInteger coefficient, int droppedDigits) =>
        ReferenceDecimal.RoundDivide(coefficient, ReferenceDecimal.Pow10(droppedDigits));
}
#endif
