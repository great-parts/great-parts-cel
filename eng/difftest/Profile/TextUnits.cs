using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The rules of the units of string and bytes literals, shared by the generator, which chooses only allowed forms, and the
/// printer, which refuses any other form. A delimiting quote is never printed as itself, also inside triple quotes, where
/// it could close the literal early. A carriage return (U+000D) is never printed as itself either, in any quotes, only as an
/// escape, so a literal that holds one is never raw.
/// </summary>
internal static class TextUnits
{
    /// <summary>The letter of the character escape of a code point, or <see langword="null"/> when it has none.</summary>
    /// <param name="codePoint">The code point.</param>
    internal static char? CharacterEscape(int codePoint) => codePoint switch
    {
        0x07 => 'a',
        0x08 => 'b',
        0x0C => 'f',
        0x0A => 'n',
        0x0D => 'r',
        0x09 => 't',
        0x0B => 'v',
        '\\' => '\\',
        '\'' => '\'',
        '"' => '"',
        '`' => '`',
        '?' => '?',
        _ => null,
    };

    /// <summary>The text of the quotes.</summary>
    /// <param name="quote">The quotes.</param>
    internal static string Delimiter(Quote quote) => quote switch
    {
        Quote.Apostrophe => "'",
        Quote.QuotationMark => "\"",
        Quote.TripleApostrophe => "'''",
        Quote.TripleQuotationMark => "\"\"\"",
        _ => throw new ArgumentOutOfRangeException(nameof(quote), quote, "Not a form of quotes."),
    };

    /// <summary>
    /// Whether a code point may stand as itself between the quotes: not the delimiting quote, never a carriage return, a line
    /// feed only inside triple quotes, and a backslash only in a raw literal.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <param name="quote">The quotes.</param>
    /// <param name="raw">Whether the literal is raw.</param>
    internal static bool CanBeLiteral(int codePoint, Quote quote, bool raw)
    {
        string delimiter = Delimiter(quote);
        if (codePoint == delimiter[0])
        {
            return false;
        }

        // A carriage return is only escaped, in any quotes: cel-go turns a carriage return in the source of a literal into a
        // line feed, while the library keeps it.
        if (codePoint == '\r')
        {
            return false;
        }

        if (delimiter.Length == 1 && codePoint == '\n')
        {
            return false;
        }

        return raw || codePoint != '\\';
    }

    /// <summary>The forms that a code point of a string literal may take.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <param name="quote">The quotes.</param>
    /// <param name="raw">Whether the literal is raw: then only <see cref="Escape.Literal"/>, where allowed.</param>
    internal static List<Escape> StringForms(int codePoint, Quote quote, bool raw)
    {
        List<Escape> forms = [];
        if (CanBeLiteral(codePoint, quote, raw))
        {
            forms.Add(Escape.Literal);
        }

        if (raw)
        {
            return forms;
        }

        if (CharacterEscape(codePoint) is not null)
        {
            forms.Add(Escape.Character);
        }

        if (codePoint <= 0xFF)
        {
            forms.Add(Escape.Hex);
            forms.Add(Escape.Octal);
        }

        if (codePoint <= 0xFFFF)
        {
            forms.Add(Escape.UnicodeShort);
        }

        forms.Add(Escape.UnicodeLong);
        return forms;
    }

    /// <summary>
    /// The forms that the unit of a bytes literal at a position may take. <see cref="Escape.Literal"/> takes the code point
    /// whose UTF-8 bytes start there; the other forms take one byte.
    /// </summary>
    /// <param name="bytes">The value.</param>
    /// <param name="index">The position of the unit.</param>
    /// <param name="quote">The quotes.</param>
    /// <param name="raw">Whether the literal is raw: then only <see cref="Escape.Literal"/>, where allowed.</param>
    internal static List<Escape> BytesForms(IReadOnlyList<byte> bytes, int index, Quote quote, bool raw)
    {
        List<Escape> forms = [];
        if (LiteralCodePoint(bytes, index, out int codePoint, out _) && CanBeLiteral(codePoint, quote, raw))
        {
            forms.Add(Escape.Literal);
        }

        if (raw)
        {
            return forms;
        }

        if (bytes[index] < 0x80 && CharacterEscape(bytes[index]) is not null)
        {
            forms.Add(Escape.Character);
        }

        forms.Add(Escape.Hex);
        forms.Add(Escape.Octal);
        return forms;
    }

    /// <summary>Decodes the code point whose UTF-8 bytes start at a position.</summary>
    /// <param name="bytes">The value.</param>
    /// <param name="index">The position.</param>
    /// <param name="codePoint">The code point.</param>
    /// <param name="length">The number of its bytes.</param>
    /// <returns><see langword="false"/> when the bytes at the position are not well-formed UTF-8.</returns>
    internal static bool LiteralCodePoint(IReadOnlyList<byte> bytes, int index, out int codePoint, out int length)
    {
        Span<byte> window = stackalloc byte[4];
        int count = Math.Min(4, bytes.Count - index);
        for (int i = 0; i < count; i++)
        {
            window[i] = bytes[index + i];
        }

        OperationStatus status = Rune.DecodeFromUtf8(window[..count], out Rune rune, out length);
        codePoint = rune.Value;
        return status == OperationStatus.Done;
    }
}
