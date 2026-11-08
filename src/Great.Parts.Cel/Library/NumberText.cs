using System;
using System.Globalization;
using System.Numerics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// Numbers to text and text to numbers, without the culture of the process: the forms of cel-go v0.32.0,
// which formats a double as Go %g and parses with strconv of Go 1.27.3.
internal static class NumberText
{
    // The longest text of a double: a sign, 17 digits, a point and the exponent e+308, or a plain decimal
    // with the exponent -4 and 17 digits.
    internal const int MaxDoubleLength = 32;

    // The longest text of an integer: a sign and 20 digits.
    internal const int MaxIntegerLength = 21;

    // The outcome of parsing a text.
    internal enum ParseResult
    {
        Parsed,

        // The text is not of the forms of the type.
        Invalid,

        // The text is of the forms, but its number is outside the range of the type.
        OutOfRange,
    }

    // Writes a double as Go %g with the shortest digits that read back to the same double: the exponent form
    // d.ddde±XX when the exponent of the first digit is below -4 or from 6, otherwise the plain decimal;
    // -0, +Inf, -Inf and NaN as written. Returns the number of characters written.
    internal static int FormatDouble(double value, Span<char> destination)
    {
        if (double.IsNaN(value))
        {
            return Copy("NaN", destination);
        }

        if (double.IsInfinity(value))
        {
            return Copy(value > 0 ? "+Inf" : "-Inf", destination);
        }

        Span<char> digits = stackalloc char[MaxDoubleLength];
        int count = DoubleTextRules.ShortestDigits(value, digits, out int point);
        ReadOnlySpan<char> significant = digits[..count];
        int written = 0;
        if (double.IsNegative(value))
        {
            destination[written++] = '-';
        }

        if (significant is ['0'])
        {
            destination[written] = '0';
            return written + 1;
        }

        int exponent = point - 1;
        return exponent < -4 || exponent >= 6
            ? written + WriteExponentForm(significant, exponent, destination[written..])
            : written + WritePlainForm(significant, point, destination[written..]);
    }

    // The text of a double, for messages.
    internal static string FormatDouble(double value)
    {
        Span<char> text = stackalloc char[MaxDoubleLength];
        return new string(text[..FormatDouble(value, text)]);
    }

    // The nearest double to a uint, ties to even: halving with the lowest bit kept as a sticky bit rounds
    // only once.
    internal static double ToDouble(ulong value) =>
        value <= long.MaxValue ? (long)value : 2.0 * (long)((value >> 1) | (value & 1));

    // [+-]?digits, the whole text, ASCII digits only.
    internal static ParseResult ParseInt64(ReadOnlySpan<char> text, out long value)
    {
        value = 0;
        bool negative = false;
        if (text.Length > 0 && text[0] is '+' or '-')
        {
            negative = text[0] == '-';
            text = text[1..];
        }

        ParseResult magnitude = ParseUInt64(text, out ulong absolute);
        if (magnitude != ParseResult.Parsed)
        {
            return magnitude;
        }

        if (negative ? absolute > (ulong)long.MaxValue + 1 : absolute > long.MaxValue)
        {
            return ParseResult.OutOfRange;
        }

        value = negative ? unchecked(-(long)absolute) : (long)absolute;
        return ParseResult.Parsed;
    }

    // digits, the whole text, ASCII digits only and no sign.
    internal static ParseResult ParseUInt64(ReadOnlySpan<char> text, out ulong value)
    {
        value = 0;
        if (text.IsEmpty)
        {
            return ParseResult.Invalid;
        }

        bool overflow = false;
        foreach (char c in text)
        {
            uint digit = (uint)(c - '0');
            if (digit > 9)
            {
                return ParseResult.Invalid;
            }

            if (value > (ulong.MaxValue - digit) / 10)
            {
                overflow = true;
            }

            value = unchecked((value * 10) + digit);
        }

        return overflow ? ParseResult.OutOfRange : ParseResult.Parsed;
    }

    // The twelve texts of Go strconv.ParseBool.
    internal static bool TryParseBool(string text, out bool value)
    {
        switch (text)
        {
            case "1" or "t" or "T" or "true" or "TRUE" or "True":
                value = true;
                return true;
            case "0" or "f" or "F" or "false" or "FALSE" or "False":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    // The forms of Go strconv.ParseFloat(s, 64), the whole text: a decimal, a hexadecimal with a binary
    // exponent, _ between digits, inf, infinity with or without a sign and nan without a sign, regardless of
    // case. The result is the nearest double, ties to even; a finite text beyond the largest double is out
    // of range.
    internal static ParseResult ParseDouble(ReadOnlySpan<char> text, out double value)
    {
        value = 0;
        if (TryParseSpecial(text, out value))
        {
            return ParseResult.Parsed;
        }

        int start = text.Length > 0 && text[0] is '+' or '-' ? 1 : 0;
        bool hexadecimal = text.Length - start > 2 && text[start] == '0' && (text[start + 1] | 0x20) == 'x';
        if (!IsFloatForm(text, start, hexadecimal, out bool underscores) || (underscores && !UnderscoresAllowed(text)))
        {
            return ParseResult.Invalid;
        }

        value = hexadecimal ? ParseHexadecimal(text, start) : ParseDecimal(text, underscores);
        return double.IsInfinity(value) ? ParseResult.OutOfRange : ParseResult.Parsed;
    }

    // d.ddde±XX with at least two digits of exponent.
    private static int WriteExponentForm(ReadOnlySpan<char> significant, int exponent, Span<char> destination)
    {
        int written = 0;
        destination[written++] = significant[0];
        if (significant.Length > 1)
        {
            destination[written++] = '.';
            significant[1..].CopyTo(destination[written..]);
            written += significant.Length - 1;
        }

        destination[written++] = 'e';
        destination[written++] = exponent < 0 ? '-' : '+';
        int magnitude = Math.Abs(exponent);
        if (magnitude < 10)
        {
            destination[written++] = '0';
        }

        _ = magnitude.TryFormat(destination[written..], out int exponentLength, default, CultureInfo.InvariantCulture);
        return written + exponentLength;
    }

    // The digits with the point at its position: 0.000ddd, ddd000 or dd.d.
    private static int WritePlainForm(ReadOnlySpan<char> significant, int point, Span<char> destination)
    {
        if (point <= 0)
        {
            destination[0] = '0';
            destination[1] = '.';
            destination.Slice(2, -point).Fill('0');
            significant.CopyTo(destination[(2 - point)..]);
            return 2 - point + significant.Length;
        }

        if (point >= significant.Length)
        {
            significant.CopyTo(destination);
            destination[significant.Length..point].Fill('0');
            return point;
        }

        significant[..point].CopyTo(destination);
        destination[point] = '.';
        significant[point..].CopyTo(destination[(point + 1)..]);
        return significant.Length + 1;
    }

    private static int Copy(string text, Span<char> destination)
    {
        text.CopyTo(destination);
        return text.Length;
    }

    // inf and infinity with an optional sign, nan without one, regardless of case, as the whole text.
    private static bool TryParseSpecial(ReadOnlySpan<char> text, out double value)
    {
        value = 0;
        ReadOnlySpan<char> word = text.Length > 0 && text[0] is '+' or '-' ? text[1..] : text;
        if (word.Equals("inf", StringComparison.OrdinalIgnoreCase) || word.Equals("infinity", StringComparison.OrdinalIgnoreCase))
        {
            value = text[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            return true;
        }

        if (word.Length == text.Length && word.Equals("nan", StringComparison.OrdinalIgnoreCase))
        {
            value = double.NaN;
            return true;
        }

        return false;
    }

    // The syntax of Go readFloat after the sign, skipping _: digits with at most one point and at least one
    // digit, then an exponent — e with decimal digits for a decimal, p required for a hexadecimal.
    private static bool IsFloatForm(ReadOnlySpan<char> text, int start, bool hexadecimal, out bool underscores)
    {
        int position = ScanMantissa(text, hexadecimal ? start + 2 : start, hexadecimal, out bool digits, out underscores);
        if (!digits)
        {
            return false;
        }

        if (position == text.Length)
        {
            return !hexadecimal;
        }

        if ((text[position] | 0x20) != (hexadecimal ? 'p' : 'e'))
        {
            return false;
        }

        bool exponentForm = IsExponent(text[(position + 1)..], out bool exponentUnderscores);
        underscores |= exponentUnderscores;
        return exponentForm;
    }

    // The digits of a mantissa with at most one point; returns the position after them.
    private static int ScanMantissa(ReadOnlySpan<char> text, int position, bool hexadecimal, out bool digits, out bool underscores)
    {
        digits = false;
        underscores = false;
        bool point = false;
        for (; position < text.Length; position++)
        {
            char c = text[position];
            if (c == '_')
            {
                underscores = true;
            }
            else if (c == '.' && !point)
            {
                point = true;
            }
            else if (char.IsAsciiDigit(c) || (hexadecimal && char.IsAsciiHexDigit(c)))
            {
                digits = true;
            }
            else
            {
                break;
            }
        }

        return position;
    }

    // [+-]? digit (digit | _)*, the rest of the text after e or p.
    private static bool IsExponent(ReadOnlySpan<char> text, out bool underscores)
    {
        underscores = false;
        if (text.Length > 0 && text[0] is '+' or '-')
        {
            text = text[1..];
        }

        if (text.IsEmpty || !char.IsAsciiDigit(text[0]))
        {
            return false;
        }

        foreach (char c in text)
        {
            if (c == '_')
            {
                underscores = true;
            }
            else if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    // Go underscoreOK: _ only between two digits, or between the prefix 0x and a digit.
    private static bool UnderscoresAllowed(ReadOnlySpan<char> text)
    {
        if (text.Length > 0 && text[0] is '+' or '-')
        {
            text = text[1..];
        }

        // The last character class: '0' a digit or the prefix, '_' an underscore, '!' anything else.
        char saw = '^';
        bool hexadecimal = false;
        int position = 0;
        if (text.Length >= 2 && text[0] == '0' && (text[1] | 0x20) is 'b' or 'o' or 'x')
        {
            position = 2;
            saw = '0';
            hexadecimal = (text[1] | 0x20) == 'x';
        }

        for (; position < text.Length; position++)
        {
            char c = text[position];
            if (char.IsAsciiDigit(c) || (hexadecimal && char.IsAsciiHexDigit(c)))
            {
                saw = '0';
                continue;
            }

            if (c == '_')
            {
                if (saw != '0')
                {
                    return false;
                }

                saw = '_';
                continue;
            }

            if (saw == '_')
            {
                return false;
            }

            saw = '!';
        }

        return saw != '_';
    }

    // A decimal of the checked form: .NET reads it with the invariant culture and rounds correctly, once the
    // underscores are removed.
    private static double ParseDecimal(ReadOnlySpan<char> text, bool underscores)
    {
        const NumberStyles Styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        if (!underscores)
        {
            return double.Parse(text, Styles, CultureInfo.InvariantCulture);
        }

        Span<char> buffer = text.Length <= 128 ? stackalloc char[128] : new char[text.Length];
        int length = 0;
        foreach (char c in text)
        {
            if (c != '_')
            {
                buffer[length++] = c;
            }
        }

        return double.Parse(buffer[..length], Styles, CultureInfo.InvariantCulture);
    }

    // A hexadecimal of the checked form, rounded to the nearest double, ties to even: the mantissa keeps up to
    // 64 bits, the digits beyond them only their sticky bit.
    private static double ParseHexadecimal(ReadOnlySpan<char> text, int start)
    {
        bool negative = text[0] == '-';
        ulong mantissa = 0;
        long exponent = 0;
        bool sticky = false;
        bool point = false;
        int position = start + 2;
        for (; position < text.Length && (text[position] | 0x20) != 'p'; position++)
        {
            char c = text[position];
            if (c == '_')
            {
                continue;
            }

            if (c == '.')
            {
                point = true;
                continue;
            }

            uint digit = (uint)HexValue(c);
            if (mantissa >> 60 == 0)
            {
                mantissa = (mantissa << 4) | digit;
                exponent -= point ? 4 : 0;
            }
            else
            {
                sticky |= digit != 0;
                exponent += point ? 0 : 4;
            }
        }

        // The binary exponent, saturated far beyond the range of double.
        position++;
        bool negativeExponent = text[position] == '-';
        if (text[position] is '+' or '-')
        {
            position++;
        }

        long binary = 0;
        for (; position < text.Length; position++)
        {
            if (text[position] != '_')
            {
                binary = Math.Min((binary * 10) + (text[position] - '0'), 100_000);
            }
        }

        exponent += negativeExponent ? -binary : binary;
        double magnitude = mantissa == 0 ? 0 : Round(mantissa, exponent, sticky);
        return negative ? -magnitude : magnitude;
    }

    // mantissa · 2^exponent with the sticky bit of dropped nonzero digits, rounded to the precision of a double
    // at its exponent — 53 bits for a normal number, fewer for a subnormal one — ties to even.
    private static double Round(ulong mantissa, long exponent, bool sticky)
    {
        int leading = BitOperations.LeadingZeroCount(mantissa);
        mantissa <<= leading;
        exponent -= leading;

        // The value is mantissa · 2^exponent with the top bit of mantissa at 2^(exponent + 63).
        long top = exponent + 63;
        if (top > 1023)
        {
            return double.PositiveInfinity;
        }

        // Below 2^-1075, half of the least subnormal, the value rounds to zero.
        long kept = top >= -1022 ? 53 : top + 1075;
        if (kept < 0)
        {
            return 0;
        }

        int dropped = (int)(64 - kept);
        ulong quotient = dropped == 64 ? 0 : mantissa >> dropped;
        ulong remainder = dropped == 64 ? mantissa : mantissa & ((1UL << dropped) - 1);
        ulong half = 1UL << (dropped - 1);
        if (remainder > half || (remainder == half && (sticky || (quotient & 1) == 1)))
        {
            quotient++;
        }

        // quotient · 2^(exponent + dropped) is exact: it has the precision of a double at that exponent.
        return Math.ScaleB(quotient, (int)(exponent + dropped));
    }

    private static int HexValue(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}
