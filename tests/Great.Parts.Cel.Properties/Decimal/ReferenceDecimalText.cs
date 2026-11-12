#if NET11_0_OR_GREATER
using System;
using System.Globalization;
using System.Numerics;

namespace Great.Parts.Cel.Properties.Decimals;

internal enum ParseOutcome
{
    Parsed,
    NotANumber,
    ExponentOutOfRange,
}

internal readonly record struct ParsedDecimal(ParseOutcome Outcome, RefDecimal Value);

// The text of a decimal value, written from the rules and not from the library: the to-scientific-string of the decimal
// arithmetic specification, the reading of a string by the grammar with the rounding to 34 digits and the range of the exponent,
// and the layout of a double (the shortest digits, placed as the %g verb of Go places them).
internal static class ReferenceDecimalText
{
    // A written exponent beyond this magnitude is out of range whatever the digits are, so it stops growing.
    private const long ExponentCap = 1_000_000_000;

    // The to-scientific-string: the digits of the coefficient with a point when the exponent is not positive and the adjusted
    // exponent is at least -6, and the form with E otherwise; a minus for a negative value, a zero included.
    internal static string Format(RefDecimal value)
    {
        string digits = value.Coefficient.ToString(CultureInfo.InvariantCulture);
        int adjusted = value.Exponent + digits.Length - 1;
        string sign = value.Negative ? "-" : string.Empty;
        return value.Exponent <= 0 && adjusted >= -6
            ? sign + Plain(digits, value.Exponent)
            : sign + Scientific(digits, adjusted);
    }

    private static string Plain(string digits, int exponent)
    {
        if (exponent == 0)
        {
            return digits;
        }

        int integerDigits = digits.Length + exponent;
        return integerDigits > 0
            ? digits[..integerDigits] + "." + digits[integerDigits..]
            : "0." + new string('0', -integerDigits) + digits;
    }

    private static string Scientific(string digits, int adjusted)
    {
        string mantissa = digits.Length > 1 ? digits[..1] + "." + digits[1..] : digits;
        return mantissa + "E" + (adjusted < 0 ? "-" : "+") + Math.Abs(adjusted).ToString(CultureInfo.InvariantCulture);
    }

    // The grammar: an optional sign, then digits with an optional point and digits, or a point and digits, then an optional
    // e or E with an optional sign and digits. ASCII digits only, and nothing else in the text.
    internal static ParsedDecimal Parse(string text)
    {
        int position = 0;
        bool negative = ReadSign(text, ref position);
        string integer = ReadDigits(text, ref position);
        string fraction = string.Empty;
        if (position < text.Length && text[position] == '.')
        {
            position++;
            fraction = ReadDigits(text, ref position);
        }

        bool valid = integer.Length + fraction.Length > 0;
        long written = 0;
        if (valid && position < text.Length && text[position] is 'e' or 'E')
        {
            position++;
            bool exponentNegative = ReadSign(text, ref position);
            string exponentDigits = ReadDigits(text, ref position);
            valid = exponentDigits.Length > 0;
            written = ExponentValue(exponentDigits, exponentNegative);
        }

        return valid && position == text.Length
            ? Round(negative, integer + fraction, written - fraction.Length)
            : new ParsedDecimal(ParseOutcome.NotANumber, default);
    }

    private static bool ReadSign(string text, ref int position)
    {
        if (position < text.Length && text[position] is '+' or '-')
        {
            position++;
            return text[position - 1] == '-';
        }

        return false;
    }

    private static string ReadDigits(string text, ref int position)
    {
        int start = position;
        while (position < text.Length && text[position] is >= '0' and <= '9')
        {
            position++;
        }

        return text[start..position];
    }

    private static long ExponentValue(string digits, bool negative)
    {
        long value = 0;
        foreach (char digit in digits)
        {
            value = Math.Min((value * 10) + (digit - '0'), ExponentCap);
        }

        return negative ? -value : value;
    }

    // The digits and the exponent as written, rounded to 34 digits half to even, then the range: an exponent outside it after the
    // rounding is an error, a zero included, since a text is refused rather than changed.
    private static ParsedDecimal Round(bool negative, string digits, long exponent)
    {
        BigInteger coefficient = FromDigits(digits);
        int excess = ReferenceDecimal.DigitCount(coefficient) - ReferenceDecimal.Digits;
        if (excess > 0)
        {
            coefficient = ReferenceDecimal.RoundDivide(coefficient, ReferenceDecimal.Pow10(excess));
            exponent += excess;
            if (coefficient > ReferenceDecimal.MaxCoefficient)
            {
                coefficient /= 10;
                exponent++;
            }
        }

        return exponent < ReferenceDecimal.MinExponent || exponent > ReferenceDecimal.MaxExponent
            ? new ParsedDecimal(ParseOutcome.ExponentOutOfRange, default)
            : new ParsedDecimal(ParseOutcome.Parsed, new RefDecimal(negative, coefficient, (int)exponent));
    }

    internal static BigInteger FromDigits(string digits)
    {
        BigInteger value = BigInteger.Zero;
        foreach (char digit in digits)
        {
            value = (value * 10) + (digit - '0');
        }

        return value;
    }

    // The value of a finite double as a decimal: the shortest digits that read back to the double, in the layout of %g. With the
    // adjusted exponent x of the first digit, a value with x below -4 or at least 6 keeps the digits and the exponent that the
    // E form has; otherwise the plain form, which for a value of an integer written without a point is the integer and the
    // exponent 0, and for a value with a fraction the digits and the exponent that puts the point in place. A zero has the digit
    // 0, the exponent 0 and the sign of the double.
    internal static RefDecimal FromDouble(double value)
    {
        bool negative = double.IsNegative(value);
        if (value == 0)
        {
            return new RefDecimal(negative, BigInteger.Zero, 0);
        }

        (BigInteger digits, int scale) = ShortestDigits(Math.Abs(value));
        int count = ReferenceDecimal.DigitCount(digits);
        int firstExponent = count + scale - 1;
        bool plainInteger = scale >= 0 && firstExponent >= -4 && firstExponent < 6;
        return plainInteger
            ? new RefDecimal(negative, digits * ReferenceDecimal.Pow10(scale), 0)
            : new RefDecimal(negative, digits, scale);
    }

    // The shortest digits of a positive double without trailing zeros, and the exponent of the last of them.
    private static (BigInteger Digits, int Scale) ShortestDigits(double value)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        int marker = text.IndexOf('E', StringComparison.Ordinal);
        string mantissa = marker < 0 ? text : text[..marker];
        int exponent = marker < 0 ? 0 : int.Parse(text[(marker + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        int point = mantissa.IndexOf('.', StringComparison.Ordinal);
        string fraction = point < 0 ? string.Empty : mantissa[(point + 1)..];
        BigInteger digits = FromDigits(point < 0 ? mantissa : mantissa[..point] + fraction);
        int scale = exponent - fraction.Length;
        while ((digits % 10).IsZero)
        {
            digits /= 10;
            scale++;
        }

        return (digits, scale);
    }
}
#endif
