using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// The first error of a filter that does not follow the grammar or the limits.
/// </summary>
/// <param name="Code">The code of the error.</param>
/// <param name="Message">The message, in English.</param>
/// <param name="Start">The offset of the first code unit of the token at which parsing stopped.</param>
/// <param name="End">The offset after the last code unit of the token; the same as <paramref name="Start"/> at the end of the text.</param>
internal sealed record FilterError(CelErrorCode Code, string Message, int Start, int End);

/// <summary>
/// The result of parsing a filter: the tree, or the first error.
/// </summary>
/// <param name="Root">The tree; <see langword="null"/> with an error.</param>
/// <param name="Error">The error; <see langword="null"/> with a tree.</param>
internal readonly record struct FilterParseResult(FilterExpr? Root, FilterError? Error);

/// <summary>
/// Parses a filter of AIP-160 into a <see cref="FilterExpr"/> tree by recursive descent that follows the grammar rule by rule.
/// </summary>
/// <remarks>
/// <para>
/// The grammar functions are <c>ParseExpression</c> (sequences joined by <c>AND</c>), <c>ParseSequence</c> (factors separated by white
/// space), <c>ParseFactor</c> (terms joined by <c>OR</c>), <c>ParseTerm</c> (<c>NOT</c> or a minus), <c>ParseSimple</c>,
/// <c>ParseRestriction</c>, <c>ParseComparable</c> and <c>ParseArg</c>. Recursion happens only through parentheses, of a group and of
/// a call, and the depth of the open parentheses is limited, so a text of any length uses a bounded stack. Each function returns
/// <see langword="null"/> after the first error, which parsing keeps and stops at.
/// </para>
/// <para>
/// A keyword is a text equal to <c>AND</c>, <c>OR</c> or <c>NOT</c>, case-sensitive: <c>AND</c> and <c>OR</c> need white space on
/// both sides, <c>NOT</c> after it. After a dot and in the name of a function the same words are names.
/// </para>
/// </remarks>
internal sealed class FilterParser
{
    /// <summary>The depth of open parentheses that a filter can reach.</summary>
    internal const int MaxDepth = 250;

    private readonly string _text;
    private readonly FilterLexer _lexer;

    // The token being read and, when it was asked for, the one after it.
    private FilterToken _token;
    private FilterToken? _ahead;

    private int _depth;
    private FilterError? _error;

    private FilterParser(string text)
    {
        _text = text;
        _lexer = new FilterLexer(text);
        _token = _lexer.Next();
    }

    /// <summary>
    /// Parses a filter.
    /// </summary>
    /// <param name="text">The text, of the length that a filter may have.</param>
    /// <returns>The tree, or the first error.</returns>
    internal static FilterParseResult Parse(string text)
    {
        FilterParser parser = new(text);
        FilterExpr? root = parser.ParseFilter();
        return parser._error is { } error ? new FilterParseResult(null, error) : new FilterParseResult(root, null);
    }

    private FilterExpr? ParseFilter()
    {
        if (_token.Kind == FilterTokenKind.End)
        {
            return new FilterEmpty();
        }

        FilterExpr? expression = ParseExpression();
        if (expression is not null && _token.Kind != FilterTokenKind.End)
        {
            return Fail("the end of the filter");
        }

        return expression;
    }

    // expression = sequence { WS "AND" WS sequence }
    private FilterExpr? ParseExpression() => ParseJoin(isOr: false);

    // sequence = factor { WS factor }
    private FilterExpr? ParseSequence()
    {
        FilterExpr? first = ParseFactor();
        if (first is null || !StartsFactor())
        {
            return first;
        }

        List<FilterExpr> operands = [first];
        while (StartsFactor())
        {
            if (!_token.SpaceBefore)
            {
                return Fail("white space between two restrictions");
            }

            FilterExpr? next = ParseFactor();
            if (next is null)
            {
                return null;
            }

            operands.Add(next);
        }

        return new FilterJoin(FilterJoinKind.Sequence, operands, []);
    }

    // Whether the token after a factor starts another factor of the sequence. Anything that is neither the end of the sequence nor the
    // start of a factor is an error, which the caller finds when it expects its own token.
    private bool StartsFactor() =>
        _token.Kind is FilterTokenKind.Text or FilterTokenKind.String or FilterTokenKind.Minus or FilterTokenKind.LeftParen
        && !IsKeyword("AND");

    // factor = term { WS "OR" WS term }
    private FilterExpr? ParseFactor() => ParseJoin(isOr: true);

    // The rule of both joins: operands separated by a keyword, with white space before and after it. The operands of AND are
    // sequences, those of OR are terms.
    private FilterExpr? ParseJoin(bool isOr)
    {
        string keyword = isOr ? "OR" : "AND";
        FilterExpr? first = ParseOperand(isOr);
        if (first is null || !IsKeyword(keyword))
        {
            return first;
        }

        List<FilterExpr> operands = [first];
        List<(int, int)> keywords = [];
        while (IsKeyword(keyword))
        {
            if (!_token.SpaceBefore)
            {
                return Fail("white space before " + keyword);
            }

            keywords.Add((_token.Start, _token.End));
            Advance();
            if (!ValueFollows(keyword))
            {
                return null;
            }

            FilterExpr? next = ParseOperand(isOr);
            if (next is null)
            {
                return null;
            }

            operands.Add(next);
        }

        return new FilterJoin(isOr ? FilterJoinKind.Or : FilterJoinKind.And, operands, keywords);
    }

    private FilterExpr? ParseOperand(bool isOr) => isOr ? ParseTerm() : ParseSequence();

    // After AND or OR: white space and then a term; the end of the text is an error at the end.
    private bool ValueFollows(string keyword)
    {
        if (_token.Kind == FilterTokenKind.End || !_token.SpaceBefore)
        {
            Fail("a term after " + keyword + " and white space between them");
            return false;
        }

        return true;
    }

    // term = [ "NOT" WS | "-" ] simple
    private FilterExpr? ParseTerm()
    {
        FilterToken mark = _token;
        bool negation;
        if (mark.Kind == FilterTokenKind.Text && IsKeywordText(mark))
        {
            if (IsCallAhead())
            {
                return ParseSimple();
            }

            if (!IsKeyword("NOT"))
            {
                return Fail("a restriction");
            }

            negation = true;
            Advance();
            if (_token.Kind == FilterTokenKind.End || !_token.SpaceBefore)
            {
                return Fail("a term after NOT and white space between them");
            }
        }
        else
        {
            negation = mark.Kind == FilterTokenKind.Minus && !IsSignedNumberAhead();
            if (negation)
            {
                Advance();
                if (_token.SpaceBefore)
                {
                    return Fail("a term right after the minus");
                }
            }
        }

        FilterExpr? operand = ParseSimple();
        return negation && operand is not null ? new FilterNot(operand, mark.Start, mark.End) : operand;
    }

    // simple = restriction | composite
    private FilterExpr? ParseSimple() => _token.Kind == FilterTokenKind.LeftParen ? ParseGroup() : ParseRestriction();

    // composite = "(" expression ")"
    private FilterExpr? ParseGroup()
    {
        FilterToken open = _token;
        if (!Enter(open))
        {
            return null;
        }

        Advance();
        if (_token.Kind == FilterTokenKind.RightParen)
        {
            return Fail("an expression between the parentheses");
        }

        FilterExpr? inner = ParseExpression();
        if (inner is null)
        {
            return null;
        }

        if (_token.Kind != FilterTokenKind.RightParen)
        {
            return Fail("')'");
        }

        FilterToken close = _token;
        Advance();
        _depth--;
        return new FilterGroup(inner, open.Start, close.End);
    }

    // restriction = comparable [ comparator arg ]
    private FilterExpr? ParseRestriction()
    {
        FilterExpr? left = ParseComparable();
        if (left is null || _token.Kind != FilterTokenKind.Comparator)
        {
            return left;
        }

        FilterToken comparator = _token;
        Advance();
        FilterExpr? right = ParseArg();
        return right is null ? null : new FilterRestriction(left, comparator.Comparator, comparator.Start, comparator.End, right);
    }

    // arg = comparable | composite
    private FilterExpr? ParseArg()
    {
        return _token.Kind switch
        {
            FilterTokenKind.LeftParen => ParseGroup(),
            FilterTokenKind.Text or FilterTokenKind.String or FilterTokenKind.Minus => ParseComparable(),
            _ => Fail("a value"),
        };
    }

    // comparable = member | function; member = value { "." field }; function = name { "." name } "(" [ argList ] ")".
    // A minus directly before the first text is the sign of a number and belongs to the first part.
    private FilterExpr? ParseComparable()
    {
        int signStart = -1;
        if (_token.Kind == FilterTokenKind.Minus)
        {
            signStart = _token.Start;
            Advance();
            if (_token.Kind != FilterTokenKind.Text || _token.SpaceBefore)
            {
                return Fail("a number right after the minus");
            }
        }

        if (_token.Kind is not (FilterTokenKind.Text or FilterTokenKind.String))
        {
            return Fail("a value");
        }

        if (signStart < 0 && _token.Kind == FilterTokenKind.Text && IsKeywordText(_token) && !IsCallAhead())
        {
            return Fail("a value, not the keyword " + Slice(_token.Start, _token.End));
        }

        List<FilterSegment>? segments = ReadSegments(signStart);
        if (segments is null)
        {
            return null;
        }

        if (_token.Kind == FilterTokenKind.LeftParen && !_token.SpaceBefore)
        {
            return signStart >= 0 ? Fail("a restriction; a number is not a function") : ParseCall(segments);
        }

        return new FilterMember(segments, Slice(segments[0].Start, segments[^1].End));
    }

    // The parts of a dotted name or a number; after the first part, a dot with no white space before or after it and a part.
    private List<FilterSegment>? ReadSegments(int signStart)
    {
        List<FilterSegment> segments = [Segment(signStart)];
        Advance();
        while (_token.Kind == FilterTokenKind.Dot)
        {
            if (_token.SpaceBefore)
            {
                Fail("no white space before '.'");
                return null;
            }

            Advance();
            if (_token.SpaceBefore || _token.Kind is not (FilterTokenKind.Text or FilterTokenKind.String))
            {
                Fail("a name after '.'");
                return null;
            }

            segments.Add(Segment(-1));
            Advance();
        }

        return segments;
    }

    // The part that the current token is; with a sign, it starts at the sign.
    private FilterSegment Segment(int signStart)
    {
        int start = signStart >= 0 ? signStart : _token.Start;
        return _token.Quoted is { } quoted
            ? new FilterSegment(quoted.Value, start, _token.End, quoted)
            : new FilterSegment(Slice(start, _token.End), start, _token.End, null);
    }

    // The arguments of a call whose name is the dotted parts; the current token is the opening parenthesis.
    private FilterCall? ParseCall(List<FilterSegment> name)
    {
        FilterToken open = _token;
        foreach (FilterSegment part in name)
        {
            if (part.Quoted is not null)
            {
                Fail("a name of a function; a string is not one");
                return null;
            }
        }

        if (!Enter(open))
        {
            return null;
        }

        Advance();
        List<FilterExpr> arguments = [];
        if (_token.Kind != FilterTokenKind.RightParen)
        {
            while (true)
            {
                FilterExpr? argument = ParseArg();
                if (argument is null)
                {
                    return null;
                }

                arguments.Add(argument);
                if (_token.Kind != FilterTokenKind.Comma)
                {
                    break;
                }

                Advance();
            }
        }

        if (_token.Kind != FilterTokenKind.RightParen)
        {
            Fail("',' or ')'");
            return null;
        }

        int end = _token.End;
        Advance();
        _depth--;
        return new FilterCall(name, arguments, end);
    }

    // Opens a level of parentheses; false after the error of the limit, at the opening parenthesis.
    private bool Enter(FilterToken open)
    {
        if (_depth >= MaxDepth)
        {
            _error ??= new FilterError(CelErrorCode.NestingTooDeep, "parentheses are nested deeper than the limit of 250", open.Start, open.End);
            return false;
        }

        _depth++;
        return true;
    }

    private void Advance()
    {
        _token = _ahead ?? _lexer.Next();
        _ahead = null;
    }

    private FilterToken Peek() => _ahead ??= _lexer.Next();

    // Whether the token after the current one is the opening parenthesis of a call, right after it.
    private bool IsCallAhead() => Peek() is { Kind: FilterTokenKind.LeftParen, SpaceBefore: false };

    // Whether the token after the current minus is a text right after it that starts with a digit: the minus is the sign of a number.
    private bool IsSignedNumberAhead()
    {
        FilterToken next = Peek();
        return next.Kind == FilterTokenKind.Text && !next.SpaceBefore && next.Start < _text.Length && char.IsAsciiDigit(_text[next.Start]);
    }

    private bool IsKeyword(string keyword) => _token.Kind == FilterTokenKind.Text && IsKeywordText(_token, keyword);

    private bool IsKeywordText(FilterToken token) =>
        IsKeywordText(token, "AND") || IsKeywordText(token, "OR") || IsKeywordText(token, "NOT");

    private bool IsKeywordText(FilterToken token, string keyword) =>
        token.End - token.Start == keyword.Length && _text.AsSpan(token.Start, keyword.Length).SequenceEqual(keyword);

    private string Slice(int start, int end) => _text.Substring(start, end - start);

    // Records the first error at the current token: what was expected and what was found. A token that is not of the language
    // gives its own message.
    private FilterExpr? Fail(string expected)
    {
        if (_error is not null)
        {
            return null;
        }

        string message = _token.Kind switch
        {
            FilterTokenKind.Invalid => _token.Detail!,
            FilterTokenKind.End => "expected " + expected + " but found the end of the filter",
            _ => "expected " + expected + " but found '" + Slice(_token.Start, _token.End) + "'",
        };
        _error = new FilterError(CelErrorCode.FilterSyntaxError, message, _token.Start, _token.End);
        return null;
    }
}
