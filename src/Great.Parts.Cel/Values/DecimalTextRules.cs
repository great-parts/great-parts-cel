#if NET11_0_OR_GREATER
using System;
using System.Globalization;

namespace Great.Parts.Cel.Values;

// The outcome of reading the text of a decimal.
internal enum DecimalParseResult
{
    Parsed,

    // The text is not of the grammar.
    NotANumber,

    // More than 34 significant digits, in the exact mode.
    TooManyDigits,

    // After the rounding to 34 digits the exponent is outside −6176 to 6111; a zero is not an exception.
    ExponentOutOfRange,
}

// The text of a decimal: the reading of a literal and of a string, the text of a value, and the conversions to and from double. The
// code uses no culture, no BigInteger and no parsing or formatting of Decimal128; the work buffers are on the stack.
internal static class DecimalTextRules
{
    // The size of the buffer for the text of a value: at most 42 characters.
    internal const int BufferLength = 48;

    // A written exponent stops growing here: it is out of range whatever the digits are, and no length of text brings it back.
    private const long ExponentCap = 100_000_000_000_000;

    private const ulong TenToThe17 = 100_000_000_000_000_000;

    private const int DoubleExactPowers = 22;

    private const ulong TwoToThe53 = 1UL << 53;

    // The powers of ten that a double holds exactly.
    private static readonly double[] DoublePowers =
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11,
        1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
    ];

    // Reads [+|-] (digits [. [digits]] | . digits) [(e|E) [+|-] digits], the whole text: ASCII digits only, no space, no separator,
    // no Infinity or NaN and no hexadecimal form. A literal passes the text of its token without the suffix and the sign, and gives
    // the sign itself. With exact, more than 34 significant digits are TooManyDigits; without it the digits are rounded to 34 half to
    // even. The cohort of 34 digits or fewer is kept as written.
    internal static DecimalParseResult TryParse(ReadOnlySpan<char> text, bool exact, out DecimalParts value)
    {
        value = default;
        if (!TryRead(text, out bool negative, out ReadOnlySpan<char> integer, out ReadOnlySpan<char> fraction, out long written))
        {
            return DecimalParseResult.NotANumber;
        }

        var digits = default(DigitReader);
        digits.Add(integer);
        digits.Add(fraction);
        int dropped = Math.Max(digits.Significant - DecimalParts.MaxDigits, 0);
        if (exact && dropped > 0)
        {
            return DecimalParseResult.TooManyDigits;
        }

        long exponent = written - fraction.Length + dropped;
        UInt128 coefficient = digits.Rounded(dropped, ref exponent);
        if (exponent is < DecimalParts.MinExponent or > DecimalParts.MaxExponent)
        {
            return DecimalParseResult.ExponentOutOfRange;
        }

        value = new DecimalParts(negative, coefficient, (int)exponent);
        return DecimalParseResult.Parsed;
    }

    // The grammar: the sign, the digits of the integer part and of the fraction, and the written exponent, which is 0 when there is
    // none. False when the text is not a number of it.
    private static bool TryRead(
        ReadOnlySpan<char> text,
        out bool negative,
        out ReadOnlySpan<char> integer,
        out ReadOnlySpan<char> fraction,
        out long written)
    {
        int position = 0;
        negative = ReadSign(text, ref position);
        int start = position;
        position = SkipDigits(text, position);
        integer = text[start..position];
        fraction = default;
        written = 0;
        if (position < text.Length && text[position] == '.')
        {
            start = ++position;
            position = SkipDigits(text, position);
            fraction = text[start..position];
        }

        if (integer.IsEmpty && fraction.IsEmpty)
        {
            return false;
        }

        if (position < text.Length && text[position] is 'e' or 'E')
        {
            position = ReadExponent(text, position + 1, out written);
        }

        // ReadExponent gives −1 for an exponent without a digit.
        return position == text.Length;
    }

    // The optional + or -; true for a minus. Moves the position past it.
    private static bool ReadSign(ReadOnlySpan<char> text, ref int position)
    {
        if (position < text.Length && text[position] is '+' or '-')
        {
            return text[position++] == '-';
        }

        return false;
    }

    // Writes the to-scientific-string of the decimal arithmetic specification, with a - for a negative value, a zero included: the plain
    // form when the exponent is not positive and the adjusted exponent is at least −6, otherwise the first digit, a point and the
    // rest, and E with the signed adjusted exponent. The text is at most 42 characters; the buffer is BufferLength. Returns the
    // number of characters written.
    internal static int Format(DecimalParts value, Span<char> destination)
    {
        int count = DecimalParts.DigitCount(value.Coefficient);
        Span<char> digits = stackalloc char[DecimalParts.MaxDigits];
        digits = digits[..count];
        WriteDigits(value.Coefficient, digits);
        int written = 0;
        if (value.Negative)
        {
            destination[written++] = '-';
        }

        int exponent = value.Exponent;
        int adjusted = exponent + count - 1;
        if (exponent <= 0 && adjusted >= -6)
        {
            return written + WritePlain(digits, exponent, destination[written..]);
        }

        destination[written++] = digits[0];
        if (count > 1)
        {
            destination[written++] = '.';
            digits[1..].CopyTo(destination[written..]);
            written += count - 1;
        }

        destination[written++] = 'E';
        destination[written++] = adjusted < 0 ? '-' : '+';
        return written + WriteMagnitude(Math.Abs(adjusted), destination[written..]);
    }

    // The value of a finite double with the digits of string(double), the shortest that read back to the same double, laid out as
    // string(double) lays them out: the exponent form d.ddde±XX when the exponent of the first digit is below −4 or from 6, with the
    // digits as the coefficient; otherwise the plain form, where the digits followed by the zeros that the point asks for are the
    // coefficient with exponent 0 when the point is not before the last digit. A negative zero stays negative. No rounding and no
    // range error is possible: at most 17 digits and an exponent from −340 to 308 come out.
    internal static void FromDouble(double value, out DecimalParts parts)
    {
        bool negative = double.IsNegative(value);
        if (value == 0)
        {
            parts = new DecimalParts(negative, UInt128.Zero, 0);
            return;
        }

        Span<char> text = stackalloc char[DoubleTextRules.BufferLength];
        int count = DoubleTextRules.ShortestDigits(value, text, out int point);
        ulong coefficient = 0;
        for (int index = 0; index < count; index++)
        {
            coefficient = (coefficient * 10) + (uint)(text[index] - '0');
        }

        int first = point - 1;
        if (first is >= -4 and < 6 && point >= count)
        {
            for (int zeros = count; zeros < point; zeros++)
            {
                coefficient *= 10;
            }

            parts = new DecimalParts(negative, coefficient, 0);
            return;
        }

        parts = new DecimalParts(negative, coefficient, point - count);
    }

    // The nearest double to the value, half to even, with the sign of a zero; false when it is beyond the range of double. A
    // coefficient below 2^53 with an exponent of magnitude up to 22 is one multiplication or division of two exact doubles, which
    // is correctly rounded; any other value is read from its text by double.Parse with the invariant culture, which rounds to
    // the nearest the same way.
    internal static bool ToDouble(DecimalParts value, out double result)
    {
        if (value.Coefficient < TwoToThe53 && Math.Abs(value.Exponent) <= DoubleExactPowers)
        {
            double magnitude = (double)(ulong)value.Coefficient;
            magnitude = value.Exponent >= 0 ? magnitude * DoublePowers[value.Exponent] : magnitude / DoublePowers[-value.Exponent];
            result = value.Negative ? -magnitude : magnitude;
            return true;
        }

        Span<char> text = stackalloc char[BufferLength];
        int length = Format(value, text);
        result = double.Parse(
            text[..length],
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture);
        return !double.IsInfinity(result);
    }

    private static int SkipDigits(ReadOnlySpan<char> text, int position)
    {
        while (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            position++;
        }

        return position;
    }

    // The signed exponent that follows the e: [+|-] digits. Returns the position after it, or −1 when there is no digit. A magnitude
    // above the cap stops growing.
    private static int ReadExponent(ReadOnlySpan<char> text, int position, out long exponent)
    {
        exponent = 0;
        bool negative = ReadSign(text, ref position);
        int start = position;
        for (; position < text.Length && char.IsAsciiDigit(text[position]); position++)
        {
            if (exponent < ExponentCap)
            {
                exponent = (exponent * 10) + (text[position] - '0');
            }
        }

        if (position == start)
        {
            return -1;
        }

        if (negative)
        {
            exponent = -exponent;
        }

        return position;
    }

    // The digits of a coefficient, count of them, written from the right: in two steps of at most 17 digits, each a ulong, since a
    // UInt128 division per digit is too slow for a text that a cost counts.
    private static void WriteDigits(UInt128 coefficient, Span<char> destination)
    {
        ulong high;
        ulong low;
        if (coefficient <= ulong.MaxValue)
        {
            ulong small = (ulong)coefficient;
            high = small / TenToThe17;
            low = small % TenToThe17;
        }
        else
        {
            (UInt128 upper, UInt128 lower) = UInt128.DivRem(coefficient, TenToThe17);
            high = (ulong)upper;
            low = (ulong)lower;
        }

        int index = destination.Length;
        int lowCount = high == 0 ? index : 17;
        for (int written = 0; written < lowCount; written++)
        {
            destination[--index] = (char)('0' + (int)(low % 10));
            low /= 10;
        }

        while (high != 0)
        {
            destination[--index] = (char)('0' + (int)(high % 10));
            high /= 10;
        }
    }

    // The plain form for a non-positive exponent: the digits when it is 0, a point inside them when there are more digits than the
    // places after the point, otherwise 0. and the zeros that the places ask for.
    private static int WritePlain(ReadOnlySpan<char> digits, int exponent, Span<char> destination)
    {
        int count = digits.Length;
        if (exponent == 0)
        {
            digits.CopyTo(destination);
            return count;
        }

        int places = -exponent;
        if (count > places)
        {
            int whole = count - places;
            digits[..whole].CopyTo(destination);
            destination[whole] = '.';
            digits[whole..].CopyTo(destination[(whole + 1)..]);
            return count + 1;
        }

        destination[0] = '0';
        destination[1] = '.';
        int zeros = places - count;
        destination.Slice(2, zeros).Fill('0');
        digits.CopyTo(destination[(2 + zeros)..]);
        return 2 + zeros + count;
    }

    // A non-negative number of at most four digits, which the adjusted exponent of a decimal128 is.
    private static int WriteMagnitude(int number, Span<char> destination)
    {
        int count = number < 10 ? 1 : number < 100 ? 2 : number < 1000 ? 3 : 4;
        for (int index = count - 1; index >= 0; index--)
        {
            destination[index] = (char)('0' + (number % 10));
            number /= 10;
        }

        return count;
    }

    // The digits of the integer part and of the fraction as they are read: the first 34 significant digits make the coefficient, and
    // the first digit that goes and whether any later one is not zero decide the rounding. Leading zeros are no digits.
    private struct DigitReader
    {
        internal UInt128 Coefficient;

        // The count of the digits from the first that is not zero, all of them, the dropped ones too.
        internal int Significant;

        private int _firstDropped;

        private bool _laterNonZero;

        // The coefficient of the 34 kept digits rounded half to even by the dropped ones: up when the first dropped digit is above 5,
        // or is 5 and a later one is not zero or the coefficient is odd. A carry that makes 10^34 gives 10^33 and one more exponent.
        internal readonly UInt128 Rounded(int dropped, ref long exponent)
        {
            bool up = _firstDropped > 5 || (_firstDropped == 5 && (_laterNonZero || (Coefficient & UInt128.One) != UInt128.Zero));
            if (dropped == 0 || !up)
            {
                return Coefficient;
            }

            UInt128 carried = Coefficient + UInt128.One;
            if (carried != DecimalParts.Powers[DecimalParts.MaxDigits])
            {
                return carried;
            }

            exponent++;
            return DecimalParts.Powers[DecimalParts.MaxDigits - 1];
        }

        internal void Add(ReadOnlySpan<char> run)
        {
            foreach (char c in run)
            {
                int digit = c - '0';
                if (Significant == 0 && digit == 0)
                {
                    continue;
                }

                Significant++;
                if (Significant <= DecimalParts.MaxDigits)
                {
                    Coefficient = (Coefficient * 10) + (uint)digit;
                }
                else if (Significant == DecimalParts.MaxDigits + 1)
                {
                    _firstDropped = digit;
                }
                else if (digit != 0)
                {
                    _laterNonZero = true;
                }
            }
        }
    }
}
#endif
