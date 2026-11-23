using System;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The classes of ASCII characters that the grammars of the formats name, and the one search that the scanners share. A unit above
// U+007F is in no class, so every scanner rejects it wherever the format is ASCII.
internal static class FormatChars
{
    internal static bool IsDigit(char c) => c is >= '0' and <= '9';

    internal static bool IsLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    internal static bool IsAlphanumeric(char c) => IsDigit(c) || IsLetter(c);

    internal static bool IsHex(char c) => IsDigit(c) || c is (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    // The value of a hexadecimal digit; the argument is one.
    internal static int HexValue(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;

    // unreserved of RFC 3986: ALPHA, DIGIT, '-', '.', '_' and '~'.
    internal static bool IsUnreserved(char c) => IsAlphanumeric(c) || c is '-' or '.' or '_' or '~';

    // sub-delims of RFC 3986.
    internal static bool IsSubDelimiter(char c) =>
        c is '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=';

    // The index of the first unit equal to the value, or -1.
    internal static int IndexOf<TCounter>(ReadOnlySpan<char> text, char value, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        for (int i = 0; i < text.Length; i++)
        {
            counter.Step();
            if (text[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    // The index of the last unit equal to the value, or -1.
    internal static int LastIndexOf<TCounter>(ReadOnlySpan<char> text, char value, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        for (int i = text.Length - 1; i >= 0; i--)
        {
            counter.Step();
            if (text[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    // Whether every unit is an ASCII digit; true for the empty text.
    internal static bool AllDigits<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        foreach (char c in text)
        {
            counter.Step();
            if (!IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
