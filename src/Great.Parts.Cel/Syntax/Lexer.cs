using System;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Splits the text of an expression into tokens by the lexis of the CEL language definition, once, from left to
/// right. White space and comments separate tokens and yield none.
/// </summary>
/// <remarks>
/// The lexer covers the whole lexis, so only the parser decides which constructs are accepted. A character outside
/// the lexis, or a string without its closing quote, yields a token of the kind <see cref="TokenKind.Error"/>, and
/// <see cref="Error"/> holds the <see cref="CelErrorCode.SyntaxError"/> for it.
/// </remarks>
internal sealed class Lexer
{
    // Operators, the two-character ones before the one-character ones that start them.
    private static readonly (string Text, TokenKind Kind)[] Operators =
    [
        ("<=", TokenKind.LessOrEqual),
        (">=", TokenKind.GreaterOrEqual),
        ("==", TokenKind.Equal),
        ("!=", TokenKind.NotEqual),
        ("&&", TokenKind.LogicalAnd),
        ("||", TokenKind.LogicalOr),
        ("(", TokenKind.LeftParenthesis),
        (")", TokenKind.RightParenthesis),
        ("[", TokenKind.LeftBracket),
        ("]", TokenKind.RightBracket),
        ("{", TokenKind.LeftBrace),
        ("}", TokenKind.RightBrace),
        (",", TokenKind.Comma),
        (":", TokenKind.Colon),
        (".", TokenKind.Dot),
        ("?", TokenKind.Question),
        ("+", TokenKind.Plus),
        ("-", TokenKind.Minus),
        ("*", TokenKind.Star),
        ("/", TokenKind.Slash),
        ("%", TokenKind.Percent),
        ("!", TokenKind.Exclamation),
        ("<", TokenKind.Less),
        (">", TokenKind.Greater),
    ];

    private readonly string _text;
    private readonly LineTable _lines;
    private int _position;

    /// <summary>
    /// Creates a lexer positioned at the start of the text.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="lines">The line table of <paramref name="text"/>, for the positions of errors.</param>
    internal Lexer(string text, LineTable lines)
    {
        _text = text;
        _lines = lines;
    }

    /// <summary>
    /// The syntax error of the last token of the kind <see cref="TokenKind.Error"/>, or <see langword="null"/> before
    /// the first such token.
    /// </summary>
    internal CelError? Error { get; private set; }

    /// <summary>
    /// Reads the next token.
    /// </summary>
    /// <returns>The token; at the end of the text, a token of the kind <see cref="TokenKind.End"/>.</returns>
    internal Token Next()
    {
        SkipWhiteSpaceAndComments();
        int start = _position;
        if (start == _text.Length)
        {
            return new Token(TokenKind.End, start, 0);
        }

        char c = _text[start];
        TokenKind kind;
        if (char.IsAsciiLetter(c) || c == '_')
        {
            kind = ReadWordOrPrefixedString();
        }
        else if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(CharAt(start + 1))))
        {
            kind = ReadNumber();
        }
        else if (c is '"' or '\'')
        {
            kind = ReadString(start, raw: false, TokenKind.StringLiteral);
        }
        else
        {
            kind = ReadOperator();
        }

        return new Token(kind, start, _position - start);
    }

    private void SkipWhiteSpaceAndComments()
    {
        while (_position < _text.Length)
        {
            char c = _text[_position];
            if (c is ' ' or '\t' or '\n' or '\f' or '\r')
            {
                _position++;
            }
            else if (c == '/' && CharAt(_position + 1) == '/')
            {
                // A comment runs to the end of the line; the line feed is white space.
                int length = _text.AsSpan(_position).IndexOf('\n');
                _position = length < 0 ? _text.Length : _position + length;
            }
            else
            {
                return;
            }
        }
    }

    // An identifier, a keyword, or a string with a prefix: b or B for bytes, then r or R for a raw string.
    private TokenKind ReadWordOrPrefixedString()
    {
        int start = _position;
        int position = start;
        bool bytes = _text[position] is 'b' or 'B';
        if (bytes)
        {
            position++;
        }

        bool raw = CharAt(position) is 'r' or 'R';
        if (raw)
        {
            position++;
        }

        if (position > start && CharAt(position) is '"' or '\'')
        {
            _position = position;
            return ReadString(start, raw, bytes ? TokenKind.BytesLiteral : TokenKind.StringLiteral);
        }

        while (_position < _text.Length && (char.IsAsciiLetterOrDigit(_text[_position]) || _text[_position] == '_'))
        {
            _position++;
        }

        return _text.AsSpan(start, _position - start) switch
        {
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            "null" => TokenKind.Null,
            "in" => TokenKind.In,
            _ => TokenKind.Identifier,
        };
    }

    // Digits, or 0x and hexadecimal digits, then u or U; or a floating literal with a fraction, an exponent or both.
    private TokenKind ReadNumber()
    {
        if (CharAt(_position) == '0' && CharAt(_position + 1) == 'x' && char.IsAsciiHexDigit(CharAt(_position + 2)))
        {
            _position += 2;
            SkipHexDigits();
            return ReadUnsignedSuffix();
        }

        SkipDigits();
        bool floating = false;
        if (CharAt(_position) == '.' && char.IsAsciiDigit(CharAt(_position + 1)))
        {
            _position++;
            SkipDigits();
            floating = true;
        }

        if (TryReadExponent())
        {
            floating = true;
        }

        return floating ? TokenKind.DoubleLiteral : ReadUnsignedSuffix();
    }

    private bool TryReadExponent()
    {
        int position = _position;
        if (CharAt(position) is not ('e' or 'E'))
        {
            return false;
        }

        position++;
        if (CharAt(position) is '+' or '-')
        {
            position++;
        }

        if (!char.IsAsciiDigit(CharAt(position)))
        {
            return false;
        }

        _position = position;
        SkipDigits();
        return true;
    }

    private TokenKind ReadUnsignedSuffix()
    {
        if (CharAt(_position) is 'u' or 'U')
        {
            _position++;
            return TokenKind.UIntLiteral;
        }

        return TokenKind.IntLiteral;
    }

    private void SkipDigits()
    {
        while (char.IsAsciiDigit(CharAt(_position)))
        {
            _position++;
        }
    }

    private void SkipHexDigits()
    {
        while (char.IsAsciiHexDigit(CharAt(_position)))
        {
            _position++;
        }
    }

    // A string from its opening quote at the current position; start is the start of the token, its prefix included.
    // Outside raw strings a backslash makes the next character part of the string, so an escaped quote does not close
    // it. A single-quoted form ends at a line break: such a string has no closing quote.
    private TokenKind ReadString(int start, bool raw, TokenKind kind)
    {
        char quote = _text[_position];
        int quoteLength = IsTripleQuote(_position, quote) ? 3 : 1;
        _position += quoteLength;
        bool escaped = false;
        while (_position < _text.Length)
        {
            char c = _text[_position];
            if (quoteLength == 1 && c is '\r' or '\n')
            {
                break;
            }

            if (!escaped && c == quote && (quoteLength == 1 || IsTripleQuote(_position, quote)))
            {
                _position += quoteLength;
                return kind;
            }

            escaped = !escaped && !raw && c == '\\';
            _position++;
        }

        return Fail(start, "unterminated string literal");
    }

    private TokenKind ReadOperator()
    {
        ReadOnlySpan<char> rest = _text.AsSpan(_position);
        foreach ((string text, TokenKind kind) in Operators)
        {
            if (rest.StartsWith(text, StringComparison.Ordinal))
            {
                _position += text.Length;
                return kind;
            }
        }

        int start = _position;
        _position += char.IsSurrogatePair(_text, start) ? 2 : 1;
        return Fail(start, "unexpected character '" + _text[start.._position] + "'");
    }

    private TokenKind Fail(int offset, string detail)
    {
        Error = new CelError(
            CelErrorCode.SyntaxError,
            CelErrorStage.Parse,
            "syntax error: " + detail,
            _lines.GetLocation(offset),
            sourceLocation: null,
            exception: null);
        return TokenKind.Error;
    }

    private bool IsTripleQuote(int position, char quote) =>
        CharAt(position) == quote && CharAt(position + 1) == quote && CharAt(position + 2) == quote;

    // The character at a position, or '\0' past the end of the text; callers compare it only with other characters.
    private char CharAt(int position) => position < _text.Length ? _text[position] : '\0';
}
