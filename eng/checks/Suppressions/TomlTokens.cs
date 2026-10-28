using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The kind of a TOML token.</summary>
internal enum TomlTokenKind
{
    /// <summary>A comment, from <c>#</c> to the end of the line.</summary>
    Comment,

    /// <summary>A string in any of the four forms; the text is the content without the quotes.</summary>
    String,

    /// <summary>A bare word: a key, a number, a date or a boolean.</summary>
    Bare,

    /// <summary>One of <c>[ ] { } , =</c>.</summary>
    Punctuation,
}

/// <summary>A token of a TOML text.</summary>
/// <param name="Kind">The kind of the token.</param>
/// <param name="Offset">The offset of the first character in the text, in UTF-16 code units.</param>
/// <param name="Length">The length of the token as written, quotes included.</param>
/// <param name="Text">The content of a string, the word of a bare token, the comment with its <c>#</c>, or the punctuation character.</param>
internal readonly record struct TomlToken(TomlTokenKind Kind, int Offset, int Length, string Text);

/// <summary>
/// Cuts a TOML text into tokens, as much as the checks need: the structure of tables, keys and arrays, the strings that hold
/// a <c>#</c>, and the comments. The text is not validated; a malformed text gives tokens that the readers skip.
/// </summary>
internal static class TomlTokens
{
    private const string Punctuation = "[]{},=";

    /// <summary>Cuts the text into tokens.</summary>
    /// <param name="text">The text of a TOML file.</param>
    internal static List<TomlToken> Read(string text)
    {
        List<TomlToken> tokens = [];
        int index = 0;
        while (index < text.Length)
        {
            char current = text[index];
            if (char.IsWhiteSpace(current))
            {
                index++;
            }
            else if (current == '#')
            {
                int end = EndOfLine(text, index);
                tokens.Add(new TomlToken(TomlTokenKind.Comment, index, end - index, text[index..end]));
                index = end;
            }
            else if (current is '"' or '\'')
            {
                index = ReadString(text, index, tokens);
            }
            else if (Punctuation.Contains(current, StringComparison.Ordinal))
            {
                tokens.Add(new TomlToken(TomlTokenKind.Punctuation, index, 1, current.ToString()));
                index++;
            }
            else
            {
                int start = index;
                while (index < text.Length && !EndsWord(text[index]))
                {
                    index++;
                }

                tokens.Add(new TomlToken(TomlTokenKind.Bare, start, index - start, text[start..index]));
            }
        }

        return tokens;
    }

    private static bool EndsWord(char character) =>
        char.IsWhiteSpace(character) || character is '#' or '"' or '\'' || Punctuation.Contains(character, StringComparison.Ordinal);

    private static int EndOfLine(string text, int from)
    {
        int end = text.AsSpan(from).IndexOfAny('\r', '\n');
        return end < 0 ? text.Length : from + end;
    }

    // A string of one line ends at its closing quote or, when it is not closed, at the end of the line. A multi-line string
    // (three quotes) ends at the next three quotes. A basic string (double quotes) skips the character after a backslash.
    private static int ReadString(string text, int start, List<TomlToken> tokens)
    {
        char quote = text[start];
        bool multiline = text.AsSpan(start).StartsWith(new string(quote, 3));
        int open = multiline ? 3 : 1;
        int index = start + open;
        while (index < text.Length && !ClosesString(text, index, quote, multiline))
        {
            index += quote == '"' && text[index] == '\\' ? 2 : 1;
        }

        int contentEnd = Math.Min(index, text.Length);
        int end = index < text.Length && text[index] == quote ? Math.Min(index + open, text.Length) : contentEnd;
        tokens.Add(new TomlToken(TomlTokenKind.String, start, end - start, text[(start + open)..contentEnd]));
        return end;
    }

    private static bool ClosesString(string text, int index, char quote, bool multiline)
    {
        if (!multiline)
        {
            return text[index] == quote || text[index] is '\r' or '\n';
        }

        return text.AsSpan(index).StartsWith(new string(quote, 3));
    }
}
