#if NET11_0_OR_GREATER
using System;
using System.Globalization;
using System.Numerics;

namespace Great.Parts.Cel.Properties.Decimals;

// The exact reference of the arithmetic of decimal128 (IEEE 754-2019): the sign, the coefficient and the exponent of a value
// are kept apart and every operation is done exactly on BigInteger and then rounded once into the format. It is written from the
// rules and shares no code with the library and no call of the parsing, the formatting or the arithmetic of Decimal128.
//
// The rules:
// - a coefficient has at most 34 digits and an exponent is from -6176 to 6111;
// - a result is rounded to 34 digits, half to even, and below the exponent -6176 the digits are rounded away, a zero without
//   an error;
// - a result of an exponent above 6111 is clamped when its coefficient can be padded with zeros to the exponent 6111 and still
//   has 34 digits; otherwise it is an overflow;
// - the exponent of a sum and of a difference is the smaller of the two, of a product the sum, of a quotient the preferred
//   exponent (the first minus the second) when the quotient is exact and the exponent of 34 digits when it is not;
// - a zero result: the sum of two zeros is negative only when both are, the sum of opposite non-zero values is positive, the
//   product and the quotient follow the signs, a negation keeps the exponent.
internal static class ReferenceDecimal
{
    internal const int Digits = 34;
    internal const int MinExponent = -6176;
    internal const int MaxExponent = 6111;

    internal static readonly BigInteger MaxCoefficient = Pow10(Digits) - 1;

    internal static BigInteger Pow10(int power) => BigInteger.Pow(10, power);

    // The number of decimal digits of a non-negative number; none for zero.
    internal static int DigitCount(BigInteger value) =>
        value.IsZero ? 0 : value.ToString(CultureInfo.InvariantCulture).Length;

    // The quotient of two positive numbers rounded to an integer, half to even.
    internal static BigInteger RoundDivide(BigInteger numerator, BigInteger denominator)
    {
        BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
        int half = (remainder * 2).CompareTo(denominator);
        return half > 0 || (half == 0 && !quotient.IsEven) ? quotient + 1 : quotient;
    }

    // Rounds an exact value into decimal128 and gives the value, or null for an overflow.
    internal static RefDecimal? Finish(bool negative, BigInteger coefficient, int exponent)
    {
        int dropped = Math.Max(DigitCount(coefficient) - Digits, MinExponent - exponent);
        if (dropped > 0)
        {
            coefficient = RoundDivide(coefficient, Pow10(dropped));
            exponent += dropped;
            if (coefficient > MaxCoefficient)
            {
                // The rounding carried into a 35th digit: 10^34 is 10^33 with one more exponent.
                coefficient /= 10;
                exponent++;
            }
        }

        if (exponent > MaxExponent)
        {
            BigInteger padded = coefficient * Pow10(exponent - MaxExponent);
            if (padded > MaxCoefficient)
            {
                return null;
            }

            return new RefDecimal(negative, padded, MaxExponent);
        }

        return new RefDecimal(negative, coefficient, exponent);
    }

    internal static RefDecimal? Add(RefDecimal left, RefDecimal right)
    {
        int exponent = Math.Min(left.Exponent, right.Exponent);
        BigInteger sum = (Signed(left) * Pow10(left.Exponent - exponent)) + (Signed(right) * Pow10(right.Exponent - exponent));
        bool negative = sum.IsZero ? left.Negative && right.Negative : sum.Sign < 0;
        return Finish(negative, BigInteger.Abs(sum), exponent);
    }

    internal static RefDecimal? Subtract(RefDecimal left, RefDecimal right) => Add(left, Negate(right));

    internal static RefDecimal? Multiply(RefDecimal left, RefDecimal right) =>
        Finish(left.Negative != right.Negative, left.Coefficient * right.Coefficient, left.Exponent + right.Exponent);

    // The quotient of a divisor that is not a zero.
    internal static RefDecimal? Divide(RefDecimal dividend, RefDecimal divisor)
    {
        bool negative = dividend.Negative != divisor.Negative;
        int preferred = dividend.Exponent - divisor.Exponent;
        int scale = TerminatingScale(dividend.Coefficient, divisor.Coefficient);
        if (scale >= 0)
        {
            // The quotient is exact: the preferred exponent, or the closest one at which it has an integer coefficient.
            return Finish(negative, dividend.Coefficient * Pow10(scale) / divisor.Coefficient, preferred - scale);
        }

        return DivideInexact(negative, dividend.Coefficient, divisor.Coefficient, preferred);
    }

    // The number of decimal places of the exact quotient of two coefficients, or -1 when it has no finite decimal expansion.
    private static int TerminatingScale(BigInteger dividend, BigInteger divisor)
    {
        if (dividend.IsZero)
        {
            return 0;
        }

        BigInteger rest = divisor / BigInteger.GreatestCommonDivisor(dividend, divisor);
        int twos = 0;
        while (rest.IsEven)
        {
            rest /= 2;
            twos++;
        }

        int fives = 0;
        while ((rest % 5).IsZero)
        {
            rest /= 5;
            fives++;
        }

        return rest.IsOne ? Math.Max(twos, fives) : -1;
    }

    // A quotient that is not exact: 34 digits, and at the exponent -6176 at the lowest, rounded once from the exact value.
    private static RefDecimal? DivideInexact(bool negative, BigInteger dividend, BigInteger divisor, int preferred)
    {
        int scale = 0;
        BigInteger scaled = dividend;
        BigInteger smallest = Pow10(Digits - 1);
        while (scaled / divisor < smallest)
        {
            scaled *= 10;
            scale++;
        }

        int exponent = Math.Max(preferred - scale, MinExponent);
        int shift = preferred - exponent;
        BigInteger quotient = RoundDivide(dividend * Pow10(Math.Max(shift, 0)), divisor * Pow10(Math.Max(-shift, 0)));
        if (quotient > MaxCoefficient)
        {
            quotient /= 10;
            exponent++;
        }

        return Finish(negative, quotient, exponent);
    }

    // The negation changes the sign of a zero too and keeps the exponent.
    internal static RefDecimal Negate(RefDecimal value) => value with { Negative = !value.Negative };

    // The comparison by value: the cohort and the sign of a zero do not matter.
    internal static int Compare(RefDecimal left, RefDecimal right)
    {
        int exponent = Math.Min(left.Exponent, right.Exponent);
        BigInteger first = Signed(left) * Pow10(left.Exponent - exponent);
        BigInteger second = Signed(right) * Pow10(right.Exponent - exponent);
        return first.CompareTo(second);
    }

    private static BigInteger Signed(RefDecimal value) => value.Negative ? -value.Coefficient : value.Coefficient;

    // The binary integer encoding (BID): the sign in bit 127, the exponent plus 6176 in bits 113 to 126, the coefficient in bits 0 to 112.
    internal static UInt128 ToBits(RefDecimal value) =>
        (value.Negative ? UInt128.One << 127 : UInt128.Zero)
        | ((UInt128)(uint)(value.Exponent - MinExponent) << 113)
        | (UInt128)value.Coefficient;

    // Reads the bits of a finite value, or gives null for an infinity and a NaN. A coefficient above 10^34 - 1 is a zero; the
    // two forms that start with the bits 11 after the sign are a zero whose exponent is in bits 111 to 124.
    internal static RefDecimal? FromBits(UInt128 bits)
    {
        bool negative = (bits >> 127) != UInt128.Zero;
        uint combination = (uint)((bits >> 122) & (UInt128)0x1F);
        if (combination >= 0x1E)
        {
            return null;
        }

        if (combination >= 0x18)
        {
            return new RefDecimal(negative, BigInteger.Zero, (int)((bits >> 111) & (UInt128)0x3FFF) + MinExponent);
        }

        BigInteger coefficient = (BigInteger)(bits & ((UInt128.One << 113) - UInt128.One));
        return new RefDecimal(negative, coefficient > MaxCoefficient ? BigInteger.Zero : coefficient, (int)((bits >> 113) & (UInt128)0x3FFF) + MinExponent);
    }
}
#endif
