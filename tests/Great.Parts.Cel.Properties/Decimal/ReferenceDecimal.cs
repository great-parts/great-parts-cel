using System;
using System.Globalization;
using System.Numerics;

namespace Great.Parts.Cel.Properties.Decimals;

// The exact reference of the arithmetic of the platform decimal: the sign, the coefficient and the exponent of a value are kept
// apart and every operation is done exactly on BigInteger and then rounded once into the format. It is written from the rules and
// shares no code with the library and no call of the parsing, the formatting or the arithmetic of Decimal128 or System.Decimal. The
// format is that of the target: decimal128 on net11.0 (IEEE 754-2019), System.Decimal on net10.0.
//
// The rules of net11.0:
// - a coefficient has at most 34 digits and an exponent is from -6176 to 6111;
// - a result is rounded to 34 digits, half to even, and below the exponent -6176 the digits are rounded away, a zero without
//   an error;
// - a result of an exponent above 6111 is clamped when its coefficient can be padded with zeros to the exponent 6111 and still
//   has 34 digits; otherwise it is an overflow;
// - the exponent of a sum and of a difference is the smaller of the two, of a product the sum, of a quotient the preferred
//   exponent (the first minus the second) when the quotient is exact and the exponent of 34 digits when it is not;
// - a zero result: the sum of two zeros is negative only when both are, the sum of opposite non-zero values is positive, the
//   product and the quotient follow the signs, a negation keeps the exponent.
//
// The rules of net10.0 differ in the format and in the quotient:
// - a coefficient is at most 2^96 - 1 and an exponent is from -28 to 0, so a positive exponent is not kept: a result whose exponent
//   is above 0 is padded with zeros to the exponent 0, and it is an overflow when the coefficient then does not fit;
// - a result is rounded once from the exact value, half to even, to the smallest exponent from -28 up at which the coefficient
//   fits 96 bits;
// - an exact quotient has the preferred exponent or the nearest one that the format holds; one that is not exact has the smallest
//   exponent from -28 up at which its coefficient fits 96 bits, less the zeros that its rounding left at the end (the platform drops
//   them), and a zero of the exponent -28.
// - a zero result of a sum is negative only when both operands are, of a product it has the sum of the scales, down to 28.
internal static class ReferenceDecimal
{
#if NET11_0_OR_GREATER
    internal const int Digits = 34;
    internal const int MinExponent = -6176;
    internal const int MaxExponent = 6111;

    internal static readonly BigInteger MaxCoefficient = Pow10(Digits) - 1;
#else
    // The most digits of a coefficient: 2^96 - 1 has 29.
    internal const int Digits = 29;
    internal const int MinExponent = -28;
    internal const int MaxExponent = 0;

    internal static readonly BigInteger MaxCoefficient = (BigInteger.One << 96) - 1;
#endif

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
        // The digits dropped are those below the lowest exponent and those that the coefficient has beyond its largest value; each
        // count is rounded from the exact coefficient, so a carry into one more digit is the next count, not a second rounding.
        // The count starts below which no rounding can fit the largest coefficient (a number of one digit more than it is above it), so
        // a coefficient of thousands of digits, which a sum of distant exponents makes, does not take a step for each digit.
        int dropped = Math.Max(MinExponent - exponent, Math.Max(DigitCount(coefficient) - DigitCount(MaxCoefficient) - 1, 0));
        BigInteger rounded = dropped > 0 ? RoundDivide(coefficient, Pow10(dropped)) : coefficient;
        while (rounded > MaxCoefficient)
        {
            dropped++;
            rounded = RoundDivide(coefficient, Pow10(dropped));
        }

        coefficient = rounded;
        exponent += dropped;

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
            BigInteger exact = dividend.Coefficient * Pow10(scale) / divisor.Coefficient;
#if !NET11_0_OR_GREATER
            // Exact as a number, but System.Decimal may have to round it: a value below the scale 28 or beyond 96 bits is an inexact quotient
            // of the format, whose digits lose the zeros that the rounding left.
            if (!HoldsExactly(exact, preferred - scale))
            {
                return DivideInexact(negative, dividend.Coefficient, divisor.Coefficient, preferred);
            }
#endif
            return Finish(negative, exact, preferred - scale);
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

#if !NET11_0_OR_GREATER
    // Whether the digits of a coefficient at an exponent are held by the format without rounding: the digits that the exponent below -28
    // and the 96 bits drop are all zeros.
    private static bool HoldsExactly(BigInteger coefficient, int exponent)
    {
        int dropped = Math.Max(MinExponent - exponent, Math.Max(DigitCount(coefficient) - DigitCount(MaxCoefficient) - 1, 0));
        while (coefficient / Pow10(dropped) > MaxCoefficient)
        {
            dropped++;
        }

        return (coefficient % Pow10(dropped)).IsZero;
    }

    // A quotient that is not exact: rounded once from the exact value at the smallest exponent from -28 up where it fits 96 bits, and
    // then, as System.Decimal does, without the zeros that the rounding left at the end of the digits (a zero stays at the exponent -28).
    private static RefDecimal? DivideInexact(bool negative, BigInteger dividend, BigInteger divisor, int preferred)
    {
        for (int exponent = MinExponent; ; exponent++)
        {
            int shift = preferred - exponent;
            BigInteger quotient = RoundDivide(dividend * Pow10(Math.Max(shift, 0)), divisor * Pow10(Math.Max(-shift, 0)));
            if (quotient <= MaxCoefficient)
            {
                int kept = exponent;
                while (kept < 0 && !quotient.IsZero && quotient % 10 == 0)
                {
                    quotient /= 10;
                    kept++;
                }

                return Finish(negative, quotient, kept);
            }
        }
    }
#else
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
#endif

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

#if !NET11_0_OR_GREATER
    // The System.Decimal of a value that Finish gave: 96 bits of coefficient and a scale from 0 to 28.
    internal static decimal ToDecimal(RefDecimal value) =>
        new(
            unchecked((int)(uint)(value.Coefficient & uint.MaxValue)),
            unchecked((int)(uint)((value.Coefficient >> 32) & uint.MaxValue)),
            unchecked((int)(uint)((value.Coefficient >> 64) & uint.MaxValue)),
            value.Negative,
            (byte)-value.Exponent);

    // The value that the target holds in the place of any value: a value of an exponent above 0 or below -28, or of a coefficient
    // above 2^96 - 1, is made one that System.Decimal holds, by the rule of the arithmetic, and the largest value when it is out of
    // the range. The generators of the properties make values of decimal128 and pass them through here.
    internal static RefDecimal Fit(RefDecimal value) => Round(value) ?? new RefDecimal(value.Negative, MaxCoefficient, 0);

    // The value rounded once into the format of the target by the rule of the arithmetic, half to even; null when it is out of the
    // range. It is the value of a conversion to decimal: of the shortest digits of a double, of a text.
    internal static RefDecimal? Round(RefDecimal value) => Finish(value.Negative, value.Coefficient, value.Exponent);
#else
    // The value that the target holds in the place of any value: on net11.0 the value itself.
    internal static RefDecimal Fit(RefDecimal value) => value;

    // The value rounded into the format of the target: on net11.0 a value of the shortest digits of a double is held as it is.
    internal static RefDecimal? Round(RefDecimal value) => value;

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
#endif
}
