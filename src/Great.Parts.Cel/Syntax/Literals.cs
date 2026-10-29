using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The values of literal tokens. The conversion never uses the culture of the process.
/// </summary>
internal static class Literals
{
    // The escapes of one character after the backslash and the characters they denote, at the same index.
    private const string SimpleEscapes = "abfnrtv\\?\"'`";
    private const string SimpleEscapeValues = "\a\b\f\n\r\t\v\\?\"'`";

    /// <summary>
    /// Converts a literal token to its value.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="token">
    /// A token of the kind <see cref="TokenKind.IntLiteral"/>, <see cref="TokenKind.UIntLiteral"/>,
    /// <see cref="TokenKind.DoubleLiteral"/>, <see cref="TokenKind.StringLiteral"/>, <see cref="TokenKind.BytesLiteral"/>,
    /// <see cref="TokenKind.True"/>, <see cref="TokenKind.False"/> or <see cref="TokenKind.Null"/>.
    /// </param>
    /// <param name="minusOffset">
    /// The offset of a minus that belongs to an integer or floating literal, where an operand is expected; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <param name="lines">The line table of <paramref name="text"/>.</param>
    /// <param name="value">The value when the method returns <see langword="true"/>; otherwise <c>null</c>.</param>
    /// <param name="error">
    /// When the method returns <see langword="false"/>, the error <see cref="CelErrorCode.InvalidLiteral"/> at the
    /// literal, the minus included; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the literal has a value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="token"/> is not a literal.</exception>
    internal static bool TryConvert(
        string text,
        Token token,
        int? minusOffset,
        LineTable lines,
        out CelValue value,
        [NotNullWhen(false)] out CelError? error)
    {
        error = null;
        if (token.Kind is TokenKind.True or TokenKind.False or TokenKind.Null)
        {
            value = token.Kind == TokenKind.Null ? CelValue.Null : CelValue.FromBoolean(token.Kind == TokenKind.True);
            return true;
        }

        ReadOnlySpan<char> span = text.AsSpan(token.Offset, token.Length);
        bool negative = minusOffset.HasValue;
        (string kind, string? detail) = token.Kind switch
        {
            TokenKind.IntLiteral => ("int", ConvertInt(span, negative, out value)),
            TokenKind.UIntLiteral => ("uint", ConvertUInt(span[..^1], out value)),
            TokenKind.DoubleLiteral => ("double", ConvertDouble(span, negative, out value)),
            TokenKind.StringLiteral => ("string", ConvertText(span, bytes: false, out value)),
            TokenKind.BytesLiteral => ("bytes", ConvertText(span[1..], bytes: true, out value)),
            _ => throw new ArgumentOutOfRangeException(nameof(token), token.Kind, "The token is not a literal."),
        };

        if (detail is not null)
        {
            value = default;
            error = new CelError(
                CelErrorCode.InvalidLiteral,
                CelErrorStage.Parse,
                "invalid " + kind + " literal: " + detail,
                lines.GetLocation(minusOffset ?? token.Offset),
                sourceLocation: null,
                exception: null);
        }

        return detail is null;
    }

    // The magnitude of a negative literal can reach 2^63, the magnitude of long.MinValue.
    private static string? ConvertInt(ReadOnlySpan<char> digits, bool negative, out CelValue value)
    {
        bool inRange = TryParseMagnitude(digits, out ulong magnitude)
            && magnitude <= (negative ? 1UL << 63 : long.MaxValue);
        value = CelValue.FromInt64(unchecked(negative ? (long)(0 - magnitude) : (long)magnitude));
        return inRange ? null : "out of range";
    }

    private static string? ConvertUInt(ReadOnlySpan<char> digits, out CelValue value)
    {
        bool inRange = TryParseMagnitude(digits, out ulong magnitude);
        value = CelValue.FromUInt64(magnitude);
        return inRange ? null : "out of range";
    }

    // Decimal digits, or 0x and hexadecimal digits; false when the number does not fit in 64 bits.
    private static bool TryParseMagnitude(ReadOnlySpan<char> digits, out ulong magnitude) =>
        digits.StartsWith("0x", StringComparison.Ordinal)
            ? ulong.TryParse(digits[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out magnitude)
            : ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out magnitude);

    // The parser of .NET rounds correctly and gives an infinity on overflow, which is an error, as in cel-go.
    private static string? ConvertDouble(ReadOnlySpan<char> literal, bool negative, out CelValue value)
    {
        double number = double.Parse(
            literal,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture);
        value = CelValue.FromDouble(negative ? -number : number);
        return double.IsInfinity(number) ? "the value overflows to infinity" : null;
    }

    // A string or bytes literal from its r or R prefix, if any, to its closing quote.
    private static string? ConvertText(ReadOnlySpan<char> literal, bool bytes, out CelValue value)
    {
        bool raw = literal[0] is 'r' or 'R';
        ReadOnlySpan<char> quoted = raw ? literal[1..] : literal;
        int quoteLength = quoted.StartsWith("\"\"\"", StringComparison.Ordinal)
            || quoted.StartsWith("'''", StringComparison.Ordinal) ? 3 : 1;
        ReadOnlySpan<char> body = quoted[quoteLength..^quoteLength];
        var builder = new TextBuilder(bytes);
        while (!body.IsEmpty)
        {
            int backslash = raw ? -1 : body.IndexOf('\\');
            if (!builder.TryAppendText(backslash < 0 ? body : body[..backslash]))
            {
                value = default;
                return "the text has a lone surrogate";
            }

            if (backslash < 0)
            {
                break;
            }

            body = body[backslash..];
            string? detail = ReadEscape(ref body, bytes, out int unit);
            if (detail is not null)
            {
                value = default;
                return detail;
            }

            builder.AppendUnit(unit);
        }

        value = builder.ToValue();
        return null;
    }

    // One escape sequence at the start of the text: a backslash and at least one more character, because the lexer
    // steps over the character after a backslash. The unit is a code point in a string and a byte in bytes.
    private static string? ReadEscape(ref ReadOnlySpan<char> text, bool bytes, out int unit)
    {
        char letter = text[1];
        int simple = SimpleEscapes.IndexOf(letter, StringComparison.Ordinal);
        if (simple >= 0)
        {
            unit = SimpleEscapeValues[simple];
            text = text[2..];
            return null;
        }

        unit = 0;
        if (bytes && letter is 'u' or 'U')
        {
            return "escape sequence '" + text[..2].ToString() + "' is not allowed in bytes";
        }

        int hexDigits = letter switch
        {
            'x' or 'X' => 2,
            'u' => 4,
            'U' => 8,
            _ => 0,
        };
        if (hexDigits > 0)
        {
            return ReadHexEscape(ref text, hexDigits, bytes, ref unit);
        }

        return letter is >= '0' and <= '3'
            ? ReadOctalEscape(ref text, ref unit)
            : "unknown escape sequence '" + text[..2].ToString() + "'";
    }

    // \x and \X with two hexadecimal digits, \u with four, \U with eight; in a string, the number is a code point.
    private static string? ReadHexEscape(ref ReadOnlySpan<char> text, int digits, bool bytes, ref int unit)
    {
        int length = 2 + digits;
        if (text.Length < length
            || !uint.TryParse(text[2..length], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint number))
        {
            return "escape sequence '" + text[..2].ToString() + "' needs " + (char)('0' + digits) + " hexadecimal digits";
        }

        string sequence = text[..length].ToString();
        text = text[length..];
        if (!bytes && !Rune.IsValid(number))
        {
            return "escape sequence '" + sequence + "' gives a surrogate or a code point above U+10FFFF";
        }

        unit = (int)number;
        return null;
    }

    // Three octal digits from 000 to 377 after the backslash; the first one is already known to be 0 to 3.
    private static string? ReadOctalEscape(ref ReadOnlySpan<char> text, ref int unit)
    {
        if (text.Length < 4 || !IsOctalDigit(text[2]) || !IsOctalDigit(text[3]))
        {
            return "escape sequence '" + text[..2].ToString() + "' needs 3 octal digits";
        }

        unit = ((text[1] - '0') * 64) + ((text[2] - '0') * 8) + (text[3] - '0');
        text = text[4..];
        return null;
    }

    private static bool IsOctalDigit(char c) => c is >= '0' and <= '7';

    // The value of a string or bytes literal: characters of the text as they are, or in UTF-8 in bytes; escape units
    // as code points in a string and as single bytes in bytes. A writer allocates nothing until it is written to, so
    // the one that the literal does not use costs nothing.
    private sealed class TextBuilder(bool bytes)
    {
        private readonly ArrayBufferWriter<char> _chars = new();
        private readonly ArrayBufferWriter<byte> _bytes = new();

        // Appends text without escape sequences; false when it has a lone surrogate, which is not a code point, so the
        // text is not a CEL string. A surrogate pair is one character.
        internal bool TryAppendText(ReadOnlySpan<char> text)
        {
            ReadOnlySpan<char> rest = text;
            while (!rest.IsEmpty)
            {
                if (Rune.DecodeFromUtf16(rest, out _, out int consumed) != OperationStatus.Done)
                {
                    return false;
                }

                rest = rest[consumed..];
            }

            if (bytes)
            {
                _ = Encoding.UTF8.GetBytes(text, _bytes);
            }
            else
            {
                _chars.Write(text);
            }

            return true;
        }

        internal void AppendUnit(int unit)
        {
            if (bytes)
            {
                _bytes.GetSpan(1)[0] = (byte)unit;
                _bytes.Advance(1);
            }
            else
            {
                _chars.Advance(new Rune(unit).EncodeToUtf16(_chars.GetSpan(2)));
            }
        }

        internal CelValue ToValue() =>
            bytes ? CelValue.FromBytes(_bytes.WrittenSpan.ToArray()) : CelValue.FromString(_chars.WrittenSpan.ToString());
    }
}
