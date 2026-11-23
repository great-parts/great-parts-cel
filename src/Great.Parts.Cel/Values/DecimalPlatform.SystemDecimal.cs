#if !NET11_0_OR_GREATER
using System;
using System.Numerics;

namespace Great.Parts.Cel.Values;

// The platform of the set decimal on net10.0: the value is a System.Decimal, 96 bits of coefficient and the exponents −28 to 0, so a
// positive exponent is not kept. The sum, the difference, the product and the order are those of System.Decimal, which rounds to the
// nearest, half to even, when a result is inexact (checked by the reference of the tests); the quotient gets the ideal exponent of
// IEEE 754 where the platform loses it. An overflow of the platform is the only failure of the arithmetic.
internal static partial class DecimalPlatform
{
    // The most digits of a coefficient of System.Decimal.
    internal const int MaxDigits = 29;

    // The smallest exponent, the scale 28.
    internal const int MinExponent = -28;

    // 2^96 − 1.
    private static readonly UInt128 MaxCoefficient = DecimalNumberRules.MaxSystemCoefficient;

    // The .NET type of the value, which the error of a value that the environment cannot have names.
    internal static readonly Type ClrType = typeof(decimal);

    internal static DecimalParts ToParts(PlatformDecimal value) => DecimalNumberRules.FromSystemDecimal(value);

    // The parts are in the range of System.Decimal: a coefficient of at most 96 bits and an exponent from −28 to 0.
    internal static PlatformDecimal FromParts(DecimalParts parts) =>
        DecimalNumberRules.MakeSystemDecimal(parts.Negative, parts.Coefficient, -parts.Exponent);

    // The value of parts that a double gave, rounded half to even to the scale 28 and 96 bits; false when the integer part does not fit.
    internal static bool TryRound(DecimalParts parts, out PlatformDecimal result) => DecimalNumberRules.ToSystemDecimal(parts, out result);

    // The result of round or quantize, whose exponent is the one asked for: a positive exponent is folded into the coefficient, since
    // the platform keeps none, and a zero then has the exponent 0 and keeps its sign. false, which is Overflow, when the coefficient
    // is then above 96 bits. No exponent below -28 comes here, since none is asked for.
    internal static bool TryFromRounded(DecimalParts parts, out PlatformDecimal result)
    {
        UInt128 coefficient = parts.Coefficient;
        result = 0m;
        if (parts.Exponent > 0 && coefficient != UInt128.Zero)
        {
            if (parts.Exponent >= DecimalParts.Powers.Length || coefficient > MaxCoefficient / DecimalParts.Powers[parts.Exponent])
            {
                return false;
            }

            coefficient *= DecimalParts.Powers[parts.Exponent];
        }

        if (coefficient > MaxCoefficient)
        {
            return false;
        }

        result = DecimalNumberRules.MakeSystemDecimal(parts.Negative, coefficient, -Math.Min(parts.Exponent, 0));
        return true;
    }

    // false for an overflow: Overflow.
    internal static bool TryAdd(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        try
        {
            // The platform gives the sign of the first operand to a sum of two zeros; IEEE 754 gives a minus only when both are minus.
            result = Zero(left + right, decimal.IsNegative(left) && decimal.IsNegative(right));
            return true;
        }
        catch (OverflowException)
        {
            result = 0m;
            return false;
        }
    }

    internal static bool TrySubtract(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        try
        {
            result = Zero(left - right, decimal.IsNegative(left) && !decimal.IsNegative(right));
            return true;
        }
        catch (OverflowException)
        {
            result = 0m;
            return false;
        }
    }

    internal static bool TryMultiply(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        try
        {
            // The platform gives a zero product the scale 0; IEEE 754 gives it the sum of the exponents, down to -28, and the sign of the signs.
            result = left * right;
            if (result == 0m)
            {
                result = DecimalNumberRules.MakeSystemDecimal(
                    decimal.IsNegative(left) != decimal.IsNegative(right),
                    UInt128.Zero,
                    Math.Min(left.Scale + right.Scale, -MinExponent));
            }

            return true;
        }
        catch (OverflowException)
        {
            result = 0m;
            return false;
        }
    }

    // The divisor is not a zero: the caller has refused it. The quotient of the platform, with the ideal exponent where it is exact.
    internal static bool TryDivide(PlatformDecimal left, PlatformDecimal right, out PlatformDecimal result)
    {
        try
        {
            decimal quotient = left / right;
            result = IdealQuotient(left, right, quotient);
            return true;
        }
        catch (OverflowException)
        {
            result = 0m;
            return false;
        }
    }

    internal static PlatformDecimal Negate(PlatformDecimal value) => decimal.Negate(value);

    // A zero result of a sum or a difference with the sign that IEEE 754 gives it; any other result as it is.
    private static decimal Zero(decimal result, bool negative) =>
        result == 0m && decimal.IsNegative(result) != negative ? decimal.Negate(result) : result;

    // The four 32-bit words of decimal.GetBits packed in two: the payload of CelValue.
    internal static void ToWords(PlatformDecimal value, out ulong low, out ulong high)
    {
        Span<int> bits = stackalloc int[4];
        _ = decimal.GetBits(value, bits);
        low = (uint)bits[0] | ((ulong)(uint)bits[1] << 32);
        high = (uint)bits[2] | ((ulong)(uint)bits[3] << 32);
    }

    internal static PlatformDecimal FromWords(ulong low, ulong high)
    {
        ReadOnlySpan<int> bits =
        [
            unchecked((int)(uint)low),
            unchecked((int)(uint)(low >> 32)),
            unchecked((int)(uint)high),
            unchecked((int)(uint)(high >> 32)),
        ];
        return new decimal(bits);
    }

    // A System.Decimal of the application is the value itself.
    internal static PlatformDecimal FromSystemDecimal(decimal value) => value;

    internal static bool TryToSystemDecimal(PlatformDecimal value, out decimal result)
    {
        result = value;
        return true;
    }

    internal static bool TryToSystemDecimalExact(PlatformDecimal value, out decimal result) => TryToSystemDecimal(value, out result);

    // The detail of InvalidLiteral for a literal that System.Decimal does not hold exactly.
    internal static string LiteralDetail(DecimalParseResult result) => result switch
    {
        DecimalParseResult.TooManyDigits => "more significant digits than System.Decimal holds",
        DecimalParseResult.ExponentOutOfRange => "exponent out of the range of System.Decimal",
        _ => "value out of the range of System.Decimal",
    };

    // System.Decimal does not keep the ideal exponent of a quotient (6.0 / 2 is 3, not 3.0). When the quotient q of the platform is
    // exact, the exact value can be stored at the exponents from lo to hi: hi is the exponent of q without its trailing zeros, but not
    // above 0, and lo is the smallest exponent, not below −28, at which the coefficient still fits 96 bits. The result has the exponent
    // of IEEE 754, e₁ − e₂, or the nearest one of the range. An inexact quotient is the platform's: at most 28 places, rounded.
    private static decimal IdealQuotient(decimal left, decimal right, decimal quotient)
    {
        DecimalParts dividend = ToParts(left);
        DecimalParts divisor = ToParts(right);
        DecimalParts found = ToParts(quotient);
        bool negative = dividend.Negative != divisor.Negative;
        if (!IsExact(dividend, divisor, found))
        {
            // A quotient that vanishes is a zero of the scale 28 with the sign of the signs, as a result below the exponent -28 is.
            return found.Coefficient == UInt128.Zero ? DecimalNumberRules.MakeSystemDecimal(negative, UInt128.Zero, -MinExponent) : quotient;
        }

        UInt128 coefficient = found.Coefficient;
        int exponent = found.Exponent;
        int highest = 0;
        int lowest = MinExponent;
        if (coefficient != UInt128.Zero)
        {
            while (coefficient % 10 == UInt128.Zero)
            {
                coefficient /= 10;
                exponent++;
            }

            highest = Math.Min(exponent, 0);
            int room = 0;
            for (UInt128 grown = coefficient * 10; room < exponent - MinExponent && grown <= MaxCoefficient; grown *= 10)
            {
                room++;
            }

            lowest = exponent - room;
        }
        else
        {
            exponent = 0;
        }

        int ideal = Math.Clamp(dividend.Exponent - divisor.Exponent, lowest, highest);
        return DecimalNumberRules.MakeSystemDecimal(negative, coefficient * DecimalParts.Powers[exponent - ideal], -ideal);
    }

    // Whether the quotient q is the exact value of dividend / divisor: q · divisor equals the dividend, compared as numbers of up to
    // 256 bits, since the product of two coefficients of 96 bits does not fit 128.
    private static bool IsExact(DecimalParts dividend, DecimalParts divisor, DecimalParts quotient)
    {
        (UInt128 productHigh, UInt128 productLow) = Multiply(quotient.Coefficient, divisor.Coefficient);
        int shift = quotient.Exponent + divisor.Exponent - dividend.Exponent;
        if (shift >= 0)
        {
            if (productHigh != UInt128.Zero)
            {
                return false;
            }

            (UInt128 scaledHigh, UInt128 scaledLow) = Multiply(productLow, DecimalParts.Powers[shift]);
            return scaledHigh == UInt128.Zero && scaledLow == dividend.Coefficient;
        }

        if (-shift >= DecimalParts.Powers.Length)
        {
            // The power of ten does not fit a UInt128: two coefficients of a large scale; so rare that BigInteger is fine.
            return ((BigInteger)quotient.Coefficient * (BigInteger)divisor.Coefficient) == (BigInteger)dividend.Coefficient * BigInteger.Pow(10, -shift);
        }

        (UInt128 targetHigh, UInt128 targetLow) = Multiply(dividend.Coefficient, DecimalParts.Powers[-shift]);
        return productHigh == targetHigh && productLow == targetLow;
    }

    // The product of two numbers of 128 bits in 256: the high half and the low half.
    private static (UInt128 High, UInt128 Low) Multiply(UInt128 left, UInt128 right)
    {
        ulong left0 = (ulong)left;
        ulong left1 = (ulong)(left >> 64);
        ulong right0 = (ulong)right;
        ulong right1 = (ulong)(right >> 64);
        UInt128 low = Wide(left0, right0);
        UInt128 cross1 = Wide(left0, right1);
        UInt128 cross2 = Wide(left1, right0);
        UInt128 high = Wide(left1, right1);
        UInt128 cross = cross1 + cross2;
        if (cross < cross1)
        {
            high += UInt128.One << 64;
        }

        UInt128 shifted = cross << 64;
        UInt128 sum = low + shifted;
        if (sum < low)
        {
            high += UInt128.One;
        }

        return (high + (cross >> 64), sum);
    }

    private static UInt128 Wide(ulong left, ulong right)
    {
        ulong high = Math.BigMul(left, right, out ulong low);
        return new UInt128(high, low);
    }
}
#endif
