namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The kind of a token of the lexis of the CEL language definition.
/// </summary>
internal enum TokenKind
{
    /// <summary>The end of the text; the lexer returns it again on every further call.</summary>
    End,

    /// <summary>A character outside the lexis or a string without its closing quote; the lexer holds the error.</summary>
    Error,

    /// <summary>An identifier, reserved words such as <c>if</c> included.</summary>
    Identifier,

    /// <summary>An escaped identifier: a name between backticks, such as <c>`a-b`</c>.</summary>
    EscapedIdentifier,

    /// <summary>An integer literal, decimal or hexadecimal, without a sign.</summary>
    IntLiteral,

    /// <summary>An unsigned integer literal, with the suffix <c>u</c> or <c>U</c>.</summary>
    UIntLiteral,

    /// <summary>A floating literal: with a fraction, an exponent or both.</summary>
    DoubleLiteral,

    /// <summary>
    /// A <c>decimal</c> literal: a floating or decimal integer literal with the suffix <c>m</c> or <c>M</c>, without a
    /// sign.
    /// </summary>
    DecimalLiteral,

    /// <summary>A string literal with its prefix and quotes.</summary>
    StringLiteral,

    /// <summary>A bytes literal with its prefix and quotes.</summary>
    BytesLiteral,

    /// <summary>The keyword <c>true</c>.</summary>
    True,

    /// <summary>The keyword <c>false</c>.</summary>
    False,

    /// <summary>The keyword <c>null</c>.</summary>
    Null,

    /// <summary>The keyword <c>in</c>.</summary>
    In,

    /// <summary><c>(</c></summary>
    LeftParenthesis,

    /// <summary><c>)</c></summary>
    RightParenthesis,

    /// <summary><c>[</c></summary>
    LeftBracket,

    /// <summary><c>]</c></summary>
    RightBracket,

    /// <summary><c>{</c></summary>
    LeftBrace,

    /// <summary><c>}</c></summary>
    RightBrace,

    /// <summary><c>,</c></summary>
    Comma,

    /// <summary><c>:</c></summary>
    Colon,

    /// <summary><c>.</c></summary>
    Dot,

    /// <summary><c>?</c></summary>
    Question,

    /// <summary><c>+</c></summary>
    Plus,

    /// <summary><c>-</c>, unary or binary.</summary>
    Minus,

    /// <summary><c>*</c></summary>
    Star,

    /// <summary><c>/</c></summary>
    Slash,

    /// <summary><c>%</c></summary>
    Percent,

    /// <summary><c>!</c></summary>
    Exclamation,

    /// <summary><c>&lt;</c></summary>
    Less,

    /// <summary><c>&lt;=</c></summary>
    LessOrEqual,

    /// <summary><c>&gt;</c></summary>
    Greater,

    /// <summary><c>&gt;=</c></summary>
    GreaterOrEqual,

    /// <summary><c>==</c></summary>
    Equal,

    /// <summary><c>!=</c></summary>
    NotEqual,

    /// <summary><c>&amp;&amp;</c></summary>
    LogicalAnd,

    /// <summary><c>||</c></summary>
    LogicalOr,
}
