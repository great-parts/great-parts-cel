using System;
using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Policy.Yaml;

// The tag of a plain scalar that has no tag of its own, by the table of resolve.go of go.yaml.in/yaml/v3 v3.0.4: null, bool, int,
// float and timestamp by the form of the text, else str. The forms are the same as in that library, so that a scalar is a string,
// a number or a timestamp for a policy exactly when it is for cel-go.
internal static class YamlResolver
{
    // Returns the long tag of an untagged plain scalar with this text.
    internal static string ResolvePlain(string text)
    {
        if (text.Length == 0)
        {
            return YamlNode.NullTag;
        }

        // The first character tells which forms are possible: a sign, a digit, a dot, or a letter of a special word.
        char first = text[0];
        bool word = first is 'y' or 'Y' or 'n' or 'N' or 't' or 'T' or 'f' or 'F' or 'o' or 'O' or '~';
        bool number = first is (>= '0' and <= '9') or '+' or '-';
        if (!(word || number || first == '.'))
        {
            return YamlNode.StrTag;
        }

        string? special = Special(text);
        if (special is not null)
        {
            return special;
        }

        if (first == '.')
        {
            return IsDotFloat(text) ? YamlNode.FloatTag : YamlNode.StrTag;
        }

        return number ? ResolveNumber(text) : YamlNode.StrTag;
    }

    // The words that have a tag of their own.
    private static string? Special(string text) => text switch
    {
        "true" or "True" or "TRUE" or "false" or "False" or "FALSE" => YamlNode.BoolTag,
        "~" or "null" or "Null" or "NULL" => YamlNode.NullTag,
        ".nan" or ".NaN" or ".NAN" or ".inf" or ".Inf" or ".INF" or "+.inf" or "+.Inf" or "+.INF" or "-.inf" or "-.Inf" or "-.INF"
            => YamlNode.FloatTag,
        _ => null,
    };

    // A text that starts with a sign or a digit: a timestamp, an integer, a float, else a string.
    private static string ResolveNumber(string text)
    {
        if (IsTimestamp(text))
        {
            return YamlNode.TimestampTag;
        }

        string plain = RemoveUnderscores(text);
        if (IsInteger(plain))
        {
            return YamlNode.IntTag;
        }

        return IsDecimalFloat(plain) ? YamlNode.FloatTag : YamlNode.StrTag;
    }

    private static string RemoveUnderscores(string text)
    {
        if (!text.AsSpan().Contains('_'))
        {
            return text;
        }

        var plain = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c != '_')
            {
                plain.Append(c);
            }
        }

        return plain.ToString();
    }

    // ---- Integers ----

    // Whether the text is an integer that fits in 64 bits, signed, or unsigned when it has no sign: the forms of ParseInt and ParseUint
    // with the base 0 of Go: decimal, 0x, 0o, 0b, and 0 and digits for octal.
    private static bool IsInteger(string plain)
    {
        int position = 0;
        char sign = plain[0];
        if (sign is '+' or '-')
        {
            position = 1;
        }

        (int radix, int digitsStart) = Radix(plain, position);
        if (!TryMagnitude(plain, digitsStart, radix, out ulong magnitude))
        {
            return false;
        }

        return sign switch
        {
            '-' => magnitude <= 1UL << 63,
            '+' => magnitude <= long.MaxValue,
            _ => true,
        };
    }

    // The base of the digits and where they start: a prefix 0x, 0o or 0b, or the 0 of an octal number.
    private static (int Radix, int Start) Radix(string text, int start)
    {
        if (start >= text.Length || text[start] != '0')
        {
            return (10, start);
        }

        if (text.Length - start >= 3)
        {
            switch (text[start + 1])
            {
                case 'x' or 'X':
                    return (16, start + 2);
                case 'o' or 'O':
                    return (8, start + 2);
                case 'b' or 'B':
                    return (2, start + 2);
                default:
                    break;
            }
        }

        return (8, start + 1);
    }

    // The value of the digits, which must be valid in the base and fit in 64 bits; no digits is zero only for the octal "0".
    private static bool TryMagnitude(string text, int start, int radix, out ulong value)
    {
        value = 0;
        if (start >= text.Length)
        {
            return radix == 8 && start == text.Length && text.Length > 0 && text[start - 1] == '0';
        }

        for (int i = start; i < text.Length; i++)
        {
            int digit = DigitValue(text[i]);
            if (digit < 0 || digit >= radix)
            {
                return false;
            }

            if (value > (ulong.MaxValue - (ulong)digit) / (ulong)radix)
            {
                return false;
            }

            value = (value * (ulong)radix) + (ulong)digit;
        }

        return true;
    }

    private static int DigitValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };

    // ---- Floats ----

    // The form [-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)? whose value is not beyond the range of a double.
    private static bool IsDecimalFloat(string plain)
    {
        int i = 0;
        if (plain[0] is '+' or '-')
        {
            i = 1;
        }

        int integerDigits = CountDigits(plain, i);
        i += integerDigits;
        int fractionDigits = 0;
        if (i < plain.Length && plain[i] == '.')
        {
            i++;
            fractionDigits = CountDigits(plain, i);
            i += fractionDigits;
        }

        if (integerDigits == 0 && fractionDigits == 0)
        {
            return false;
        }

        if (!TryExponent(plain, ref i))
        {
            return false;
        }

        return i == plain.Length && InRange(plain);
    }

    // The form of ParseFloat for a text that starts with a dot: the dot, digits, an exponent; underscores between digits.
    private static bool IsDotFloat(string text)
    {
        if (text.Length < 2 || text[1] is < '0' or > '9')
        {
            return false;
        }

        int i = 1;
        if (!SkipDigitsWithUnderscores(text, ref i))
        {
            return false;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            i++;
            if (i < text.Length && text[i] is '+' or '-')
            {
                i++;
            }

            if (i >= text.Length || !SkipDigitsWithUnderscores(text, ref i))
            {
                return false;
            }
        }

        return i == text.Length && InRange(RemoveUnderscores(text));
    }

    // Digits with single underscores between them; false when an underscore is not between two digits.
    private static bool SkipDigitsWithUnderscores(string text, ref int i)
    {
        bool previousDigit = false;
        while (i < text.Length)
        {
            char c = text[i];
            if (c is >= '0' and <= '9')
            {
                previousDigit = true;
            }
            else if (c == '_' && previousDigit && i + 1 < text.Length && text[i + 1] is >= '0' and <= '9')
            {
                previousDigit = false;
            }
            else
            {
                break;
            }

            i++;
        }

        return previousDigit;
    }

    private static int CountDigits(string text, int start)
    {
        int i = start;
        while (i < text.Length && text[i] is >= '0' and <= '9')
        {
            i++;
        }

        return i - start;
    }

    // An optional exponent: e or E, an optional sign and digits.
    private static bool TryExponent(string text, ref int i)
    {
        if (i >= text.Length || text[i] is not ('e' or 'E'))
        {
            return true;
        }

        i++;
        if (i < text.Length && text[i] is '+' or '-')
        {
            i++;
        }

        int digits = CountDigits(text, i);
        i += digits;
        return digits > 0;
    }

    // A text with the form of a float that is beyond the range of a double does not parse in Go: it stays a string.
    private static bool InRange(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsInfinity(value);

    // ---- Timestamps ----

    // The forms of time.Parse that go.yaml.in/yaml/v3 tries: "2006-1-2T15:4:5.999999999Z07:00", the same with a lower-case t,
    // "2006-1-2 15:4:5.999999999" without a zone, and "2006-1-2"; the numbers have one or two digits, except the year.
    private static bool IsTimestamp(string text)
    {
        int i = 0;
        while (i < text.Length && text[i] is >= '0' and <= '9')
        {
            i++;
        }

        if (i != 4 || i == text.Length || text[i] != '-')
        {
            return false;
        }

        int year = (text[0] - '0') * 1000 + (text[1] - '0') * 100 + (text[2] - '0') * 10 + (text[3] - '0');
        i++;
        if (!TryNumber(text, ref i, 1, 12, out int month) || !Expect(text, ref i, '-') || !TryNumber(text, ref i, 1, DaysIn(month, year), out _))
        {
            return false;
        }

        if (i == text.Length)
        {
            return true;
        }

        char separator = text[i++];
        if (separator is not ('T' or 't' or ' ') || !TryTime(text, ref i))
        {
            return false;
        }

        return separator == ' ' ? i == text.Length : TryZone(text, i);
    }

    // The time of the day: hours, minutes and seconds with a fraction of the seconds, which may be left out.
    private static bool TryTime(string text, ref int i)
    {
        if (!TryNumber(text, ref i, 0, 23, out _) || !Expect(text, ref i, ':')
            || !TryNumber(text, ref i, 0, 59, out _) || !Expect(text, ref i, ':')
            || !TryNumber(text, ref i, 0, 59, out _))
        {
            return false;
        }

        if (i + 1 < text.Length && text[i] is '.' or ',' && text[i + 1] is >= '0' and <= '9')
        {
            i++;
            while (i < text.Length && text[i] is >= '0' and <= '9')
            {
                i++;
            }
        }

        return true;
    }

    // The zone of a timestamp: Z, or a sign with two digits of hours, a colon and two digits of minutes.
    private static bool TryZone(string text, int start)
    {
        if (text.Length - start == 1 && text[start] == 'Z')
        {
            return true;
        }

        if (text.Length - start != 6 || text[start] is not ('+' or '-') || text[start + 3] != ':')
        {
            return false;
        }

        int hours = TwoDigits(text, start + 1);
        int minutes = TwoDigits(text, start + 4);
        return hours is >= 0 and <= 24 && minutes is >= 0 and <= 60;
    }

    private static int TwoDigits(string text, int at) =>
        text[at] is >= '0' and <= '9' && text[at + 1] is >= '0' and <= '9' ? ((text[at] - '0') * 10) + (text[at + 1] - '0') : -1;

    // One or two digits whose value is in a range.
    private static bool TryNumber(string text, ref int i, int min, int max, out int value)
    {
        value = 0;
        if (i >= text.Length || text[i] is < '0' or > '9')
        {
            return false;
        }

        value = text[i++] - '0';
        if (i < text.Length && text[i] is >= '0' and <= '9')
        {
            value = (value * 10) + (text[i++] - '0');
        }

        return value >= min && value <= max;
    }

    private static bool Expect(string text, ref int i, char c)
    {
        if (i < text.Length && text[i] == c)
        {
            i++;
            return true;
        }

        return false;
    }

    private static int DaysIn(int month, int year) => month switch
    {
        2 => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0) ? 29 : 28,
        4 or 6 or 9 or 11 => 30,
        _ => 31,
    };
}
