using System;
using System.Globalization;

namespace Great.Parts.Cel.Values;

// The digits of the text of a double, shared by the text form of the library (string(double), %g of cel-go) and
// the printer of syntax trees: the shortest decimal digits that read back to the same double.
internal static class DoubleTextRules
{
    // The size of the buffer for the digits: 17 digits at most, with room for the scratch text of the search.
    internal const int BufferLength = 32;

    // The most significant digits that the shortest text of a double needs.
    private const int MaxSignificantDigits = 17;

    // The bits of the fraction of a double: all zero for a power of two and for zero.
    private const ulong FractionBits = 0x000F_FFFF_FFFF_FFFFUL;

    // The length of the exact text of a double with the format E800: a digit, a point, 800 digits and E-ddd.
    private const int ExactLength = 807;

    // The shortest digits of a finite double, by magnitude, that round-trip, without leading and trailing zeros,
    // and the position of the decimal point counted from the first of them; zero is the single digit 0. The buffer
    // holds at least BufferLength characters. Returns the number of digits.
    internal static int ShortestDigits(double value, Span<char> digits, out int point)
    {
        // .NET writes them with or without an exponent: 1E+21, 1.5E-05, 0.0001, 123456.
        Span<char> text = stackalloc char[BufferLength];
        _ = Math.Abs(value).TryFormat(text, out int length, "R", CultureInfo.InvariantCulture);
        text = text[..length];
        int count = 0;
        point = -1;
        int position = 0;
        for (; position < text.Length && text[position] != 'E'; position++)
        {
            if (text[position] == '.')
            {
                point = count;
            }
            else
            {
                digits[count++] = text[position];
            }
        }

        point = (point < 0 ? count : point)
            + (position < text.Length ? int.Parse(text[(position + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : 0);

        // Leading zeros move the point; trailing zeros are not digits of the shortest form.
        int first = 0;
        while (first < count - 1 && digits[first] == '0')
        {
            first++;
            point--;
        }

        while (count - 1 > first && digits[count - 1] == '0')
        {
            count--;
        }

        digits[first..count].CopyTo(digits);
        count -= first;

        // For a power of two the gap to the double below is half the gap above, and .NET takes the wider gap on both
        // sides: a few powers get digits that read back to the double below, such as 2.980232238769531E-08 for 2^-25.
        return (BitConverter.DoubleToUInt64Bits(value) & FractionBits) == 0 && !ReadsBack(digits[..count], point, Math.Abs(value))
            ? ShortestOfPowerOfTwo(Math.Abs(value), digits, count, out point)
            : count;
    }

    // Whether the digits, with the decimal point at its position, read back to the double.
    private static bool ReadsBack(ReadOnlySpan<char> digits, int point, double value)
    {
        Span<char> text = stackalloc char[BufferLength];
        text[0] = '.';
        digits.CopyTo(text[1..]);
        int length = digits.Length + 1;
        text[length++] = 'e';
        _ = point.TryFormat(text[length..], out int exponentLength, default, CultureInfo.InvariantCulture);
        return double.Parse(text[..(length + exponentLength)], NumberStyles.Float, CultureInfo.InvariantCulture) == value;
    }

    // The digits that Go chooses for a positive power of two: the fewest, from the given count on, of the two roundings
    // of the exact value, down and up, that read back; of two that both do, the closer one, and at one half the even one.
    // Seventeen digits always read back, so the search ends there at the latest.
    private static int ShortestOfPowerOfTwo(double value, Span<char> digits, int count, out int point)
    {
        // The exact value, d.dddE-ddd: no double has more than 767 significant digits. The digits after the point move
        // next to the first one.
        Span<char> exact = new char[ExactLength];
        _ = value.TryFormat(exact, out int length, "E800", CultureInfo.InvariantCulture);
        int exponent = exact[..length].LastIndexOf('E');
        point = int.Parse(exact[(exponent + 1)..length], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) + 1;
        exact[2..exponent].CopyTo(exact[1..]);
        ReadOnlySpan<char> significant = exact[..(exponent - 1)];
        Span<char> up = stackalloc char[MaxSignificantDigits];
        for (; ; count++)
        {
            significant[..count].CopyTo(digits);
            int upPoint = RoundUp(digits[..count], point, up, out int upCount);
            bool downReadsBack = ReadsBack(digits[..count], point, value);
            if (ReadsBack(up[..upCount], upPoint, value) && (!downReadsBack || IsCloserUp(significant[count..], digits[count - 1])))
            {
                up[..upCount].CopyTo(digits);
                point = upPoint;
                return upCount;
            }

            if (downReadsBack)
            {
                return TrimZeros(digits, count);
            }
        }
    }

    // The digits plus one unit of their last digit, without trailing zeros, and the position of their decimal point.
    private static int RoundUp(ReadOnlySpan<char> digits, int point, Span<char> up, out int count)
    {
        digits.CopyTo(up);
        int position = digits.Length - 1;
        while (position >= 0 && up[position] == '9')
        {
            up[position--] = '0';
        }

        if (position < 0)
        {
            up[0] = '1';
            count = 1;
            return point + 1;
        }

        up[position]++;
        count = TrimZeros(up, digits.Length);
        return point;
    }

    // Whether the exact digits that a rounding drops are above one half, or one half after an odd last digit.
    private static bool IsCloserUp(ReadOnlySpan<char> rest, char last)
    {
        if (rest[0] != '5')
        {
            return rest[0] > '5';
        }

        foreach (char digit in rest[1..])
        {
            if (digit != '0')
            {
                return true;
            }
        }

        return (last - '0') % 2 == 1;
    }

    // The number of the digits without trailing zeros, at least one.
    private static int TrimZeros(ReadOnlySpan<char> digits, int count)
    {
        while (count > 1 && digits[count - 1] == '0')
        {
            count--;
        }

        return count;
    }
}
