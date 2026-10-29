using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// "<expression> → <values>": the values of the literals of the expression in the order of the text, separated by
// " | ", or "-" when it has none. A minus right before an integer or floating literal belongs to the literal, as
// where an operand is expected. A string shows printable ASCII characters other than the backslash as they are and
// every other UTF-16 code unit as \uXXXX; bytes show hexadecimal digits; an invalid literal shows the offset of its
// error and the message. In the expressions of LiteralTable, "<XXXX>" is the UTF-16 code unit XXXX: a lone surrogate in
// the text of a literal, raw or not, is invalid, and a pair is one character.
public sealed class LiteralsTests
{
    // Every expression of the cel-spec test files basic and plumbing, in the order of the files.
    private const string CelSpecExpressions = """""""
        0 → int 0
        0u → uint 0
        0U → uint 0
        0.0 → double 0
        0e+0 → double 0
        '' → string ""
        "" → string ""
        r"" → string ""
        b"" → bytes []
        false → bool false
        null → null
        [] → -
        {} → -
        r"""""" → string ""
        r'''''' → string ""
        42 → int 42
        123456789u → uint 123456789
        123456789U → uint 123456789
        -9223372036854775808 → int -9223372036854775808
        -2.3e+1 → double -23
        "!" → string "!"
        '\'' → string "'"
        b'ÿ' → bytes [C3BF]
        b'\000\xff' → bytes [00FF]
        [-1] → int -1
        {"k":"v"} → string "k" | string "v"
        true → bool true
        0x55555555 → int 1431655765
        -0x55555555 → int -1431655765
        0x55555555u → uint 1431655765
        0x55555555U → uint 1431655765
        "\u270c" → string "\u270C"
        "\U0001f431" → string "\uD83D\uDC31"
        "\a\b\f\n\r\t\v\"\'\\" → string "\u0007\u0008\u000C\u000A\u000D\u0009\u000B"'\u005C"
        x → -
        x → -
        x || true → bool true
        1 + 1 → int 1 | int 1
        f_unknown(17) → int 17
        false → bool false
        true → bool true
        null → null
        17 → int 17
        1 / 0 → int 1 | int 0
        {"k1":"v1","k":"v"} → string "k1" | string "v1" | string "k" | string "v"
        [17, 'pancakes'] → int 17 | string "pancakes"
        'foo' → string "foo"
        """"""";

    private const string LiteralTable = """""""
        9223372036854775807 -9223372036854775807 → int 9223372036854775807 | int -9223372036854775807
        0x7fffffffffffffff -0x8000000000000000 → int 9223372036854775807 | int -9223372036854775808
        - 5 → int -5
        9223372036854775808 → error 0 invalid int literal: out of range
        -9223372036854775809 → error 0 invalid int literal: out of range
        0x8000000000000000 → error 0 invalid int literal: out of range
        -0x8000000000000001 → error 0 invalid int literal: out of range
        1 + 18446744073709551616 → int 1 | error 4 invalid int literal: out of range
        0x10000000000000000 → error 0 invalid int literal: out of range
        18446744073709551615u 0xFFFFFFFFFFFFFFFFu → uint 18446744073709551615 | uint 18446744073709551615
        18446744073709551616u → error 0 invalid uint literal: out of range
        0x10000000000000000u → error 0 invalid uint literal: out of range
        1.7976931348623157e308 -1.7976931348623157e308 → double 1.7976931348623157E+308 | double -1.7976931348623157E+308
        1e-400 -0.0 0.1 .5e1 → double 0 | double -0 | double 0.1 | double 5
        9007199254740993.0 → double 9007199254740992
        1e309 → error 0 invalid double literal: the value overflows to infinity
        1 + -1e309 → int 1 | error 4 invalid double literal: the value overflows to infinity
        "\x41\X42\101\177\?\`" → string "ABA\u007F?`"
        "\377\xFF\xff" → string "\u00FF\u00FF\u00FF"
        "\u00e9\uD7FF\uE000\U0001F600\U0010FFFF" → string "\u00E9\uD7FF\uE000\uD83D\uDE00\uDBFF\uDFFF"
        "a\\b" → string "a\u005Cb"
        "é😀" → string "\u00E9\uD83D\uDE00"
        r"\n\q" R'\' → string "\u005Cn\u005Cq" | string "\u005C"
        '''x''x''' """a"b""" '""' → string "x''x" | string "a"b" | string """"
        '''a\'''b''' → string "a'''b"
        b"abc" b"\303\277" B'😀' → bytes [616263] | bytes [C3BF] | bytes [F09F9880]
        b'\n\x41\X42\377' br'\n' BR"""\""" → bytes [0A4142FF] | bytes [5C6E] | bytes [5C]
        true false null → bool true | bool false | null
        '\q' → error 0 invalid string literal: unknown escape sequence '\q'
        'ok' + 'a\qb' → string "ok" | error 7 invalid string literal: unknown escape sequence '\q'
        '\400' → error 0 invalid string literal: unknown escape sequence '\4'
        '\x4' → error 0 invalid string literal: escape sequence '\x' needs 2 hexadecimal digits
        '\X4g' → error 0 invalid string literal: escape sequence '\X' needs 2 hexadecimal digits
        '\u12' → error 0 invalid string literal: escape sequence '\u' needs 4 hexadecimal digits
        '\u+123' → error 0 invalid string literal: escape sequence '\u' needs 4 hexadecimal digits
        '\U0010FFF' → error 0 invalid string literal: escape sequence '\U' needs 8 hexadecimal digits
        '\uD83D' → error 0 invalid string literal: escape sequence '\uD83D' gives a surrogate or a code point above U+10FFFF
        '\uDFFF' → error 0 invalid string literal: escape sequence '\uDFFF' gives a surrogate or a code point above U+10FFFF
        '\UD83DDE03' → error 0 invalid string literal: escape sequence '\UD83DDE03' gives a surrogate or a code point above U+10FFFF
        '\U00110000' → error 0 invalid string literal: escape sequence '\U00110000' gives a surrogate or a code point above U+10FFFF
        '\0' → error 0 invalid string literal: escape sequence '\0' needs 3 octal digits
        '\01' → error 0 invalid string literal: escape sequence '\0' needs 3 octal digits
        '\08' → error 0 invalid string literal: escape sequence '\0' needs 3 octal digits
        '\380' → error 0 invalid string literal: escape sequence '\3' needs 3 octal digits
        b'\u00ff' → error 0 invalid bytes literal: escape sequence '\u' is not allowed in bytes
        b'\U000000ff' → error 0 invalid bytes literal: escape sequence '\U' is not allowed in bytes
        b'\q' → error 0 invalid bytes literal: unknown escape sequence '\q'
        b'\x4' → error 0 invalid bytes literal: escape sequence '\x' needs 2 hexadecimal digits
        '<D800>' → error 0 invalid string literal: the text has a lone surrogate
        '<DC00>' → error 0 invalid string literal: the text has a lone surrogate
        'a<D800>' → error 0 invalid string literal: the text has a lone surrogate
        '<D800>a' → error 0 invalid string literal: the text has a lone surrogate
        '<DC00><D800>' → error 0 invalid string literal: the text has a lone surrogate
        '\n<DC00>' → error 0 invalid string literal: the text has a lone surrogate
        """<D800>""" → error 0 invalid string literal: the text has a lone surrogate
        r'<DC00>' → error 0 invalid string literal: the text has a lone surrogate
        b'<D800>' → error 0 invalid bytes literal: the text has a lone surrogate
        br'<DC00>' → error 0 invalid bytes literal: the text has a lone surrogate
        'ok' + 'x<DC00>' → string "ok" | error 7 invalid string literal: the text has a lone surrogate
        '<D83D><DE00>' r'a<D83D><DE00>\n' → string "\uD83D\uDE00" | string "a\uD83D\uDE00\u005Cn"
        b'<D83D><DE00>' bR'<D83D><DE00>' → bytes [F09F9880] | bytes [F09F9880]
        """"""";

    public static TheoryData<string, string> CelSpecCases => CaseTable.Parse(CelSpecExpressions);

    public static TheoryData<string, string> LiteralCases => CaseTable.Parse(LiteralTable);

    [Fact]
    public static void CelSpecCases_HaveEveryExpressionOfBasicAndPlumbing()
    {
        Assert.Equal(47, CelSpecCases.Count);
    }

    [Theory]
    [MemberData(nameof(CelSpecCases))]
    public static void CelSpecExpression_GivesTheValuesOfItsLiterals(string expression, string values)
    {
        Assert.Equal(values, Format(expression));
    }

    [Theory]
    [MemberData(nameof(LiteralCases))]
    public static void Literal_GivesItsValueOrInvalidLiteral(string expression, string values)
    {
        Assert.Equal(values, Format(CodeUnits(expression)));
    }

    [Theory]
    [InlineData("'\\q'")]
    [InlineData("9223372036854775808")]
    [InlineData("18446744073709551616u")]
    [InlineData("1e309")]
    public static void InvalidLiteral_GivesNull(string expression)
    {
        Assert.Equal(CelValueKind.Null, ConvertLiterals(expression)[0].Value.Kind);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("-")]
    public static void TokenThatIsNoLiteral_Throws(string text)
    {
        Token token = new Lexer(text, new LineTable(text)).Next();

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Literals.TryConvert(text, token, null, new LineTable(text), out _, out _));

        Assert.Equal("token", exception.ParamName);
        Assert.StartsWith("The token is not a literal.", exception.Message, StringComparison.Ordinal);
    }

    private static string Format(string expression)
    {
        List<(CelValue Value, CelError? Error)> literals = ConvertLiterals(expression);
        var parts = new List<string>();
        foreach ((CelValue value, CelError? error) in literals)
        {
            parts.Add(error is null
                ? FormatValue(value)
                : string.Create(CultureInfo.InvariantCulture, $"error {error.Location.Offset} {error.Message}"));
        }

        return parts.Count == 0 ? "-" : string.Join(" | ", parts);
    }

    // The literals of the expression; a minus right before an integer or floating literal belongs to it.
    private static List<(CelValue Value, CelError? Error)> ConvertLiterals(string expression)
    {
        var lines = new LineTable(expression);
        var lexer = new Lexer(expression, lines);
        var literals = new List<(CelValue, CelError?)>();
        Token previous = default;
        for (Token token = lexer.Next(); token.Kind != TokenKind.End; token = lexer.Next())
        {
            if (token.Kind is TokenKind.IntLiteral or TokenKind.UIntLiteral or TokenKind.DoubleLiteral or TokenKind.StringLiteral
                or TokenKind.BytesLiteral or TokenKind.True or TokenKind.False or TokenKind.Null)
            {
                int? minus = previous.Kind == TokenKind.Minus && token.Kind is TokenKind.IntLiteral or TokenKind.DoubleLiteral
                    ? previous.Offset
                    : null;
                bool converted = Literals.TryConvert(expression, token, minus, lines, out CelValue value, out CelError? error);
                Assert.Equal(converted, error is null);
                literals.Add((value, error));
            }

            previous = token;
        }

        return literals;
    }

    private static string FormatValue(CelValue value) => value.Kind switch
    {
        CelValueKind.Null => "null",
        CelValueKind.Bool => value.TryGetBoolean(out bool b) && b ? "bool true" : "bool false",
        CelValueKind.Int => string.Create(CultureInfo.InvariantCulture, $"int {(value.TryGetInt64(out long i) ? i : 0)}"),
        CelValueKind.UInt => string.Create(CultureInfo.InvariantCulture, $"uint {(value.TryGetUInt64(out ulong u) ? u : 0)}"),
        CelValueKind.Double => "double " + (value.TryGetDouble(out double d) ? d : 0).ToString("R", CultureInfo.InvariantCulture),
        CelValueKind.String => "string \"" + Escape(value.TryGetString(out string? s) ? s : "") + "\"",
        CelValueKind.Bytes => "bytes [" + Convert.ToHexString((value.TryGetBytes(out ReadOnlyMemory<byte> m) ? m : default).Span) + "]",
        _ => value.Kind.ToString(),
    };

    // "<XXXX>" in an expression of the table is the UTF-16 code unit XXXX, so that the table can hold lone surrogates.
    private static string CodeUnits(ReadOnlySpan<char> text)
    {
        var builder = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                _ = builder.Append((char)int.Parse(text.Slice(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                i += 5;
            }
            else
            {
                _ = builder.Append(text[i]);
            }
        }

        return builder.ToString();
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder();
        foreach (char c in text)
        {
            _ = c is >= ' ' and <= '~' and not '\\'
                ? builder.Append(c)
                : builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
        }

        return builder.ToString();
    }
}
