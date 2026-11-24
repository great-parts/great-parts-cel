namespace Great.Parts.Cel.Filtering;

/// <summary>
/// The kinds of the tokens of a filter. White space is not a token: a token knows whether white space is right before it.
/// </summary>
internal enum FilterTokenKind
{
    /// <summary>The end of the text; its range is empty.</summary>
    End,

    /// <summary>A run of characters that none of the other tokens starts with or contains, such as a name, a number or a keyword.</summary>
    Text,

    /// <summary>A string in double or single quotes, with its unescaped value.</summary>
    String,

    /// <summary>The dot of a member or a function name.</summary>
    Dot,

    /// <summary>An opening parenthesis.</summary>
    LeftParen,

    /// <summary>A closing parenthesis.</summary>
    RightParen,

    /// <summary>The comma between arguments.</summary>
    Comma,

    /// <summary>A comparator, which <see cref="FilterToken.Comparator"/> tells.</summary>
    Comparator,

    /// <summary>A minus at the start of a term or of an argument, which is the negation of a term or the sign of a number.</summary>
    Minus,

    /// <summary>Text that is not a token of the language, such as a string that is not closed; <see cref="FilterToken.Detail"/> tells why.</summary>
    Invalid,
}

/// <summary>
/// The comparators of a filter.
/// </summary>
internal enum FilterComparator
{
    /// <summary><c>=</c>.</summary>
    Equal,

    /// <summary><c>!=</c>.</summary>
    NotEqual,

    /// <summary><c>&lt;</c>.</summary>
    Less,

    /// <summary><c>&lt;=</c>.</summary>
    LessOrEqual,

    /// <summary><c>&gt;</c>.</summary>
    Greater,

    /// <summary><c>&gt;=</c>.</summary>
    GreaterOrEqual,

    /// <summary><c>:</c>, the has operator.</summary>
    Has,
}

/// <summary>
/// The value of a string of a filter and where its escaped <c>*</c> characters are.
/// </summary>
/// <param name="value">The value, with the escapes removed.</param>
/// <param name="escapedStars">The indices in <paramref name="value"/> of the <c>*</c> characters that were written <c>\*</c>.</param>
internal sealed class FilterString(string value, int[] escapedStars)
{
    /// <summary>The value, with the escapes removed.</summary>
    internal string Value { get; } = value;

    /// <summary>The indices in <see cref="Value"/> of the <c>*</c> characters that were written <c>\*</c>: they are not wildcards.</summary>
    internal int[] EscapedStars { get; } = escapedStars;
}

/// <summary>
/// A token of a filter with its range in UTF-16 code units of the text.
/// </summary>
internal readonly struct FilterToken
{
    internal FilterToken(
        FilterTokenKind kind,
        int start,
        int end,
        bool spaceBefore,
        FilterComparator comparator = default,
        FilterString? text = null,
        string? detail = null)
    {
        Kind = kind;
        Start = start;
        End = end;
        SpaceBefore = spaceBefore;
        Comparator = comparator;
        Quoted = text;
        Detail = detail;
    }

    /// <summary>The kind of the token.</summary>
    internal FilterTokenKind Kind { get; }

    /// <summary>The offset of the first code unit.</summary>
    internal int Start { get; }

    /// <summary>The offset after the last code unit.</summary>
    internal int End { get; }

    /// <summary>Whether white space is right before the token.</summary>
    internal bool SpaceBefore { get; }

    /// <summary>The comparator of a <see cref="FilterTokenKind.Comparator"/>.</summary>
    internal FilterComparator Comparator { get; }

    /// <summary>The value of a <see cref="FilterTokenKind.String"/>.</summary>
    internal FilterString? Quoted { get; }

    /// <summary>The message of an <see cref="FilterTokenKind.Invalid"/> token.</summary>
    internal string? Detail { get; }
}
