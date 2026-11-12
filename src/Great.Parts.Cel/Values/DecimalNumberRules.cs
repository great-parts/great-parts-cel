#if NET11_0_OR_GREATER
using System;

namespace Great.Parts.Cel.Values;

// int, uint and System.Decimal to and from the parts of a decimal. The conversions to the integers truncate toward zero and then
// check the range; the conversions to and from System.Decimal are exact when the value fits and round half to even when it does
// not. A magnitude is found by at most one multiplication or one division of UInt128, never by a loop over the digits.
internal static class DecimalNumberRules
{
    // A coefficient of System.Decimal is 96 bits.
    private static readonly UInt128 MaxSystemCoefficient = (UInt128.One << 96) - UInt128.One;

    // The most digits and the largest scale of System.Decimal.
    private const int SystemDigits = 29;

    private const int SystemScale = 28;

    // The magnitude of the truncated value is below 10^20 when it can fit a long or a ulong.
    private const int IntegerDigits = 20;

    // An int is exact: the magnitude of the number, exponent 0.
    internal static DecimalParts FromInt64(long value) =>
        value < 0 ? new DecimalParts(true, (ulong)~value + 1UL, 0) : new DecimalParts(false, (ulong)value, 0);

    internal static DecimalParts FromUInt64(ulong value) => new(false, value, 0);

    // The value truncated toward zero as an int; false when it is outside the range, so −9223372036854775808.9 is the least int and
    // −9223372036854775809 is not.
    internal static bool ToInt64(DecimalParts value, out long result)
    {
        result = 0;
        if (!TryTruncate(value, out UInt128 magnitude))
        {
            return false;
        }

        if (!value.Negative)
        {
            result = unchecked((long)magnitude);
            return magnitude <= (ulong)long.MaxValue;
        }

        result = unchecked((long)(0UL - (ulong)magnitude));
        return magnitude <= 1UL << 63;
    }

    // The value truncated toward zero as a uint; false when it is above the range or is negative after the truncation: −0.5 and −0 are
    // 0, −1.5 is out of range. The sign is tested after the truncation, unlike uint(double), which tests it before.
    internal static bool ToUInt64(DecimalParts value, out ulong result)
    {
        result = 0;
        if (!TryTruncate(value, out UInt128 magnitude) || magnitude > ulong.MaxValue || (value.Negative && magnitude != UInt128.Zero))
        {
            return false;
        }

        result = (ulong)magnitude;
        return true;
    }

    // The parts of a System.Decimal, exactly: the 96 bits are the coefficient, minus the scale is the exponent, and the sign and the
    // scale of a zero are kept. A System.Decimal has at most 29 digits and a scale of at most 28, which 34 digits hold.
    internal static DecimalParts FromSystemDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        _ = decimal.GetBits(value, bits);
        var coefficient = new UInt128((uint)bits[2], ((ulong)(uint)bits[1] << 32) | (uint)bits[0]);
        return new DecimalParts(bits[3] < 0, coefficient, -((bits[3] >> 16) & 0xFF));
    }

    // The System.Decimal of the value: exact when it fits, otherwise rounded half to even to a scale of at most 28 and a coefficient of
    // 96 bits; false when even the integer part does not fit. A negative zero and the scale of a zero are kept.
    internal static bool ToSystemDecimal(DecimalParts value, out decimal result)
    {
        result = 0m;
        UInt128 coefficient = value.Coefficient;
        int scale = 0;
        if (value.Exponent >= 0)
        {
            if (coefficient != UInt128.Zero)
            {
                if (DecimalParts.DigitCount(coefficient) + value.Exponent > SystemDigits)
                {
                    return false;
                }

                coefficient *= DecimalParts.Powers[value.Exponent];
            }
        }
        else if (!TryScale(value, out coefficient, out scale))
        {
            return false;
        }

        if (coefficient > MaxSystemCoefficient)
        {
            return false;
        }

        result = new decimal(
            unchecked((int)(uint)coefficient),
            unchecked((int)(uint)(coefficient >> 32)),
            unchecked((int)(uint)(coefficient >> 64)),
            value.Negative,
            (byte)scale);
        return true;
    }

    // The magnitude of the value without its fraction; false when it is at least 10^20, far above the range of both integers.
    private static bool TryTruncate(DecimalParts value, out UInt128 magnitude)
    {
        magnitude = UInt128.Zero;
        UInt128 coefficient = value.Coefficient;
        if (coefficient == UInt128.Zero)
        {
            return true;
        }

        int exponent = value.Exponent;
        if (exponent >= 0)
        {
            if (DecimalParts.DigitCount(coefficient) + exponent > IntegerDigits)
            {
                return false;
            }

            magnitude = coefficient * DecimalParts.Powers[exponent];
            return true;
        }

        if (-exponent < DecimalParts.Powers.Length)
        {
            magnitude = coefficient / DecimalParts.Powers[-exponent];
        }

        return true;
    }

    // The coefficient and the scale of a System.Decimal for a negative exponent: the digits beyond a scale of 28 go, rounded, and while
    // the coefficient is above 96 bits one more digit goes, rounded from the original coefficient; when more digits would go than the
    // scale has, the integer part is too large.
    private static bool TryScale(DecimalParts value, out UInt128 coefficient, out int scale)
    {
        int source = -value.Exponent;
        int dropped = Math.Max(source - SystemScale, 0);
        coefficient = dropped == 0 ? value.Coefficient : DecimalParts.DropDigits(value.Coefficient, dropped);
        while (coefficient > MaxSystemCoefficient)
        {
            if (++dropped > source)
            {
                scale = 0;
                return false;
            }

            coefficient = DecimalParts.DropDigits(value.Coefficient, dropped);
        }

        scale = source - dropped;
        return true;
    }
}
#endif
