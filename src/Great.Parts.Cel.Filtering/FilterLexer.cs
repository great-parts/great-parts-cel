using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// Reads the tokens of a filter one at a time, with their ranges in UTF-16 code units.
/// </summary>
/// <remarks>
/// White space is a space, a tab, a carriage return or a line feed. A text is a run of characters other than white space, a
/// dot, parentheses, a comma, the characters of a comparator, and quotes. A minus is a token of its own only where a term or an
/// argument can start - at the start of the text, after white space, an opening parenthesis, a comma or a comparator;
/// elsewhere it is a character of a text. A backslash in a string makes the next character literal.
/// </remarks>
internal sealed class FilterLexer
{
    // The characters that end a text: white space and the characters of the other tokens.
    private static readonly SearchValues<char> TextEnds = SearchValues.Create(" \t\r\n.(),:=<>!\"'");

    private readonly string _text;
    private int _position;

    // The kind of the token before; at the start a comma, which a term or an argument follows as at the start of the text.
    private FilterTokenKind _previous = FilterTokenKind.Comma;

    internal FilterLexer(string text)
    {
        _text = text;
    }

    /// <summary>
    /// Reads the next token; at the end of the text, always an <see cref="FilterTokenKind.End"/>.
    /// </summary>
    /// <returns>The token.</returns>
    internal FilterToken Next()
    {
        int before = _position;
        while (_position < _text.Length && _text[_position] is ' ' or '\t' or '\r' or '\n')
        {
            _position++;
        }

        bool space = _position > before;
        FilterToken token = _position >= _text.Length
            ? new FilterToken(FilterTokenKind.End, _position, _position, space)
            : Read(_text[_position], space);
        _previous = token.Kind;
        return token;
    }

    private FilterToken Read(char c, bool space)
    {
        int start = _position;
        FilterTokenKind single = c switch
        {
            '.' => FilterTokenKind.Dot,
            '(' => FilterTokenKind.LeftParen,
            ')' => FilterTokenKind.RightParen,
            ',' => FilterTokenKind.Comma,
            _ => FilterTokenKind.Text,
        };

        if (single != FilterTokenKind.Text)
        {
            return Single(single, space);
        }

        return c switch
        {
            '=' or '<' or '>' or '!' or ':' => ReadComparator(c, space),
            '"' or '\'' => ReadString(c, space),
            '-' when space || _previous is FilterTokenKind.LeftParen or FilterTokenKind.Comma or FilterTokenKind.Comparator =>
                Single(FilterTokenKind.Minus, space),
            _ => ReadText(start, space),
        };
    }

    private FilterToken Single(FilterTokenKind kind, bool space)
    {
        int start = _position++;
        return new FilterToken(kind, start, _position, space);
    }

    private FilterToken ReadText(int start, bool space)
    {
        int length = _text.AsSpan(start).IndexOfAny(TextEnds);
        _position = length < 0 ? _text.Length : start + length;
        return new FilterToken(FilterTokenKind.Text, start, _position, space);
    }

    private FilterToken ReadComparator(char c, bool space)
    {
        int start = _position;
        bool equals = start + 1 < _text.Length && _text[start + 1] == '=';
        (FilterComparator? comparator, int length) = (c, equals) switch
        {
            ('=', _) => ((FilterComparator?)FilterComparator.Equal, 1),
            (':', _) => (FilterComparator.Has, 1),
            ('<', true) => (FilterComparator.LessOrEqual, 2),
            ('<', false) => (FilterComparator.Less, 1),
            ('>', true) => (FilterComparator.GreaterOrEqual, 2),
            ('>', false) => (FilterComparator.Greater, 1),
            ('!', true) => (FilterComparator.NotEqual, 2),
            _ => (null, 1),
        };

        _position += length;
        return comparator is { } known
            ? new FilterToken(FilterTokenKind.Comparator, start, _position, space, known)
            : new FilterToken(FilterTokenKind.Invalid, start, _position, space, detail: "'!' is not an operator; negation is NOT or a minus, inequality is !=");
    }

    // A string runs to the same quote; a backslash makes the next character literal, and a backslash before a star is remembered so
    // that the star is not a wildcard.
    private FilterToken ReadString(char quote, bool space)
    {
        int start = _position;
        StringBuilder value = new();
        List<int>? stars = null;
        int i = start + 1;
        while (i < _text.Length)
        {
            char c = _text[i];
            if (c == quote)
            {
                _position = i + 1;
                return new FilterToken(FilterTokenKind.String, start, _position, space, text: new FilterString(value.ToString(), stars is null ? [] : [.. stars]));
            }

            if (c == '\\' && i + 1 < _text.Length)
            {
                i++;
                c = _text[i];
                if (c == '*')
                {
                    (stars ??= []).Add(value.Length);
                }
            }
            else if (c == '\\')
            {
                break;
            }

            value.Append(c);
            i++;
        }

        _position = _text.Length;
        return new FilterToken(FilterTokenKind.Invalid, start, _position, space, detail: "the string is not closed");
    }
}
