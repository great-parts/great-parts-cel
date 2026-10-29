using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// A node of a generated expression. A literal carries its value and the form in which it is printed; parentheses that
/// precedence needs are added by the printer, <see cref="Parenthesized"/> adds them around any node at random.
/// </summary>
internal abstract record Expr
{
    /// <summary>Whether the node is printed in parentheses although precedence does not need them.</summary>
    internal bool Parenthesized { get; init; }
}

/// <summary>An <c>int</c> literal. A negative value is printed as <c>-</c> and its magnitude.</summary>
/// <param name="Value">The value.</param>
/// <param name="Form">The form of the magnitude.</param>
internal sealed record IntLiteral(long Value, IntegerForm Form) : Expr;

/// <summary>A <c>uint</c> literal.</summary>
/// <param name="Value">The value.</param>
/// <param name="Form">The form of the digits.</param>
/// <param name="Suffix"><c>u</c> or <c>U</c>.</param>
internal sealed record UintLiteral(ulong Value, IntegerForm Form, char Suffix) : Expr;

/// <summary>
/// A <c>double</c> literal: a finite value, printed as the shortest decimal of its magnitude; a negative value, <c>-0.0</c>
/// included, as <c>-</c> and its magnitude.
/// </summary>
/// <param name="Value">The value.</param>
/// <param name="Form">The notation.</param>
internal sealed record DoubleLiteral(double Value, DoubleForm Form) : Expr;

/// <summary>A <c>bool</c> literal.</summary>
/// <param name="Value">The value.</param>
internal sealed record BoolLiteral(bool Value) : Expr;

/// <summary>The literal <c>null</c>.</summary>
internal sealed record NullLiteral : Expr;

/// <summary>A <c>string</c> literal.</summary>
/// <param name="Value">The value: Unicode scalar values only.</param>
/// <param name="Form">The quotes, the raw prefix and the form of each code point.</param>
internal sealed record StringLiteral(string Value, TextForm Form) : Expr;

/// <summary>A <c>bytes</c> literal.</summary>
/// <param name="Value">The value.</param>
/// <param name="Prefix"><c>b</c> or <c>B</c>.</param>
/// <param name="Form">The quotes, the raw prefix and the form of each unit; see <see cref="TextForm"/>.</param>
internal sealed record BytesLiteral(IReadOnlyList<byte> Value, char Prefix, TextForm Form) : Expr;

/// <summary>An identifier: a variable of the manifest.</summary>
/// <param name="Name">The name of the variable.</param>
internal sealed record Ident(string Name) : Expr;

/// <summary>A call of a function of the profile.</summary>
/// <param name="Function">The function: one of the constants of <see cref="ProfileTables"/>.</param>
/// <param name="Args">The arguments.</param>
internal sealed record Call(string Function, IReadOnlyList<Expr> Args) : Expr;

/// <summary>A list literal.</summary>
/// <param name="Elements">The elements.</param>
/// <param name="TrailingComma">Whether a comma follows the last element, or stands alone without elements: <c>[,]</c>.</param>
internal sealed record ListLiteral(IReadOnlyList<Expr> Elements, bool TrailingComma) : Expr;

/// <summary>A map literal.</summary>
/// <param name="Entries">The entries in their order.</param>
/// <param name="TrailingComma">Whether a comma follows the last entry, or stands alone without entries: <c>{,}</c>.</param>
internal sealed record MapLiteral(IReadOnlyList<MapEntry> Entries, bool TrailingComma) : Expr;

/// <summary>An entry of a map literal.</summary>
/// <param name="Key">The key: a literal of <c>int</c>, <c>uint</c>, <c>bool</c> or <c>string</c>.</param>
/// <param name="Value">The value.</param>
internal sealed record MapEntry(Expr Key, Expr Value);

/// <summary>The form of the digits of an integer literal.</summary>
internal enum IntegerForm
{
    /// <summary>Decimal digits.</summary>
    DecimalDigits,

    /// <summary><c>0x</c> and lower case hexadecimal digits.</summary>
    HexLower,

    /// <summary><c>0x</c> and upper case hexadecimal digits.</summary>
    HexUpper,
}

/// <summary>The notation of a <c>double</c> literal; the digits are always the shortest decimal that reads back to the value.</summary>
internal enum DoubleForm
{
    /// <summary>Without an exponent: <c>1500.0</c>, <c>0.015</c>.</summary>
    Plain,

    /// <summary>With the exponent <c>e</c> after the first digit: <c>1.5e3</c>, <c>1.5e-2</c>.</summary>
    Exponent,

    /// <summary>
    /// With a leading <c>.</c>: <c>.015</c> without an exponent when the value is below 1, otherwise with the exponent
    /// <c>E+</c>: <c>.15E+4</c>.
    /// </summary>
    LeadingDot,
}

/// <summary>The quotes of a string or bytes literal.</summary>
internal enum Quote
{
    /// <summary><c>'…'</c>.</summary>
    Apostrophe,

    /// <summary><c>"…"</c>.</summary>
    QuotationMark,

    /// <summary><c>'''…'''</c>.</summary>
    TripleApostrophe,

    /// <summary><c>"""…"""</c>.</summary>
    TripleQuotationMark,
}

/// <summary>How a unit of a string or bytes literal is printed.</summary>
internal enum Escape
{
    /// <summary>As itself: one code point, or in bytes the UTF-8 bytes of one code point.</summary>
    Literal,

    /// <summary>A character escape: <c>\a \b \f \n \r \t \v \\ \' \" \` \?</c>.</summary>
    Character,

    /// <summary><c>\x</c> and two lower case hexadecimal digits: a code point up to U+00FF, or one byte.</summary>
    Hex,

    /// <summary><c>\</c> and three octal digits: a code point up to U+00FF, or one byte.</summary>
    Octal,

    /// <summary><c>\u</c> and four hexadecimal digits: a code point up to U+FFFF; strings only.</summary>
    UnicodeShort,

    /// <summary><c>\U</c> and eight hexadecimal digits: any code point; strings only.</summary>
    UnicodeLong,
}

/// <summary>
/// The form of a string or bytes literal. A unit of a string is a code point. A unit of bytes is one byte, or, printed as
/// <see cref="Escape.Literal"/>, the UTF-8 bytes of one code point.
/// </summary>
/// <param name="Quote">The quotes.</param>
/// <param name="Raw">The raw prefix <c>r</c> or <c>R</c>, or <see langword="null"/>; a raw literal prints every unit as itself.</param>
/// <param name="Units">The form of each unit, in order.</param>
internal sealed record TextForm(Quote Quote, char? Raw, IReadOnlyList<Escape> Units);
