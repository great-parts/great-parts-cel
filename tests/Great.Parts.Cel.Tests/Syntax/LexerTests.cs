using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

public sealed class LexerTests
{
    // "<text> → <tokens>": every token as "<kind>@<offset>:<length>", the end of the text last.
    private const string Tokens = """""""""""
        x → Identifier@0:1 End@1:0
        _a1 B_2 z9_ → Identifier@0:3 Identifier@4:3 Identifier@8:3 End@11:0
        true false null in → True@0:4 False@5:5 Null@11:4 In@16:2 End@18:0
        trueish in2 if nullx → Identifier@0:7 Identifier@8:3 Identifier@12:2 Identifier@15:5 End@20:0
        b r R B bx rb → Identifier@0:1 Identifier@2:1 Identifier@4:1 Identifier@6:1 Identifier@8:2 Identifier@11:2 End@13:0
        0 123 → IntLiteral@0:1 IntLiteral@2:3 End@5:0
        0x1aF 0x9 0x → IntLiteral@0:5 IntLiteral@6:3 IntLiteral@10:1 Identifier@11:1 End@12:0
        0X1 0xg 1x2 → IntLiteral@0:1 Identifier@1:2 IntLiteral@4:1 Identifier@5:2 IntLiteral@8:1 Identifier@9:2 End@11:0
        1u 2U 0x1u 0x2U → UIntLiteral@0:2 UIntLiteral@3:2 UIntLiteral@6:4 UIntLiteral@11:4 End@15:0
        1.5 .5 0.25 → DoubleLiteral@0:3 DoubleLiteral@4:2 DoubleLiteral@7:4 End@11:0
        1e3 1E3 2.3e+1 2e-1 → DoubleLiteral@0:3 DoubleLiteral@4:3 DoubleLiteral@8:6 DoubleLiteral@15:4 End@19:0
        1. → IntLiteral@0:1 Dot@1:1 End@2:0
        1.e3 → IntLiteral@0:1 Dot@1:1 Identifier@2:2 End@4:0
        1e 1e+ 1.5u → IntLiteral@0:1 Identifier@1:1 IntLiteral@3:1 Identifier@4:1 Plus@5:1 DoubleLiteral@7:3 Identifier@10:1 End@11:0
        a.b . → Identifier@0:1 Dot@1:1 Identifier@2:1 Dot@4:1 End@5:0
        "a" 'b' → StringLiteral@0:3 StringLiteral@4:3 End@7:0
        "" '' → StringLiteral@0:2 StringLiteral@3:2 End@5:0
        "'" '"' → StringLiteral@0:3 StringLiteral@4:3 End@7:0
        """a"b""" '''x''x''' → StringLiteral@0:9 StringLiteral@10:10 End@20:0
        """""" '''''' → StringLiteral@0:6 StringLiteral@7:6 End@13:0
        r"a" R'b' r"""c""" → StringLiteral@0:4 StringLiteral@5:4 StringLiteral@10:8 End@18:0
        b"a" B'b' br"c" BR'd' bR'''e''' → BytesLiteral@0:4 BytesLiteral@5:4 BytesLiteral@10:5 BytesLiteral@16:5 BytesLiteral@22:9 End@31:0
        rb"a" → Identifier@0:2 StringLiteral@2:3 End@5:0
        "a\"b" '\'' → StringLiteral@0:6 StringLiteral@7:4 End@11:0
        "a\\" "b" → StringLiteral@0:5 StringLiteral@6:3 End@9:0
        """a\"""" → StringLiteral@0:9 End@9:0
        r"\" r'\\' → StringLiteral@0:4 StringLiteral@5:5 End@10:0
        r"""\""" → StringLiteral@0:8 End@8:0
        ( ) [ ] { } , : ? . → LeftParenthesis@0:1 RightParenthesis@2:1 LeftBracket@4:1 RightBracket@6:1 LeftBrace@8:1 RightBrace@10:1 Comma@12:1 Colon@14:1 Question@16:1 Dot@18:1 End@19:0
        + - * / % ! → Plus@0:1 Minus@2:1 Star@4:1 Slash@6:1 Percent@8:1 Exclamation@10:1 End@11:0
        < <= > >= == != && || → Less@0:1 LessOrEqual@2:2 Greater@5:1 GreaterOrEqual@7:2 Equal@10:2 NotEqual@13:2 LogicalAnd@16:2 LogicalOr@19:2 End@21:0
        <<=!!-1 → Less@0:1 LessOrEqual@1:2 Exclamation@3:1 Exclamation@4:1 Minus@5:1 IntLiteral@6:1 End@7:0
        1/2 → IntLiteral@0:1 Slash@1:1 IntLiteral@2:1 End@3:0
        x // comment → Identifier@0:1 End@12:0
        x / / y → Identifier@0:1 Slash@2:1 Slash@4:1 Identifier@6:1 End@7:0
        'a' 'b → StringLiteral@0:3 Error@4:2 End@6:0
        1 # = & | 2 → IntLiteral@0:1 Error@2:1 Error@4:1 Error@6:1 Error@8:1 IntLiteral@10:1 End@11:0
        'a\' → Error@0:4 End@4:0
        """a"" → Error@0:6 End@6:0
        """ → Error@0:3 End@3:0
        """a\""" → Error@0:8 End@8:0
        """a""""" → StringLiteral@0:7 StringLiteral@7:2 End@9:0
        """""""""" → StringLiteral@0:6 Error@6:4 End@10:0
        """"""""""";

    public static TheoryData<string, string> TokenCases => CaseTable.Parse(Tokens);

    [Theory]
    [MemberData(nameof(TokenCases))]
    public static void Text_GivesItsTokens(string text, string tokens)
    {
        Assert.Equal(tokens, Format(text));
    }

    // Separators and line breaks, which the table of one-line cases cannot hold.
    [Theory]
    [InlineData(" \t\n\f\r1", "IntLiteral@5:1 End@6:0")]
    [InlineData("1 // c\n2", "IntLiteral@0:1 IntLiteral@7:1 End@8:0")]
    [InlineData("1 // c\r2", "IntLiteral@0:1 End@8:0")]
    [InlineData("'''a\nb''' \"\"\"\r\n\"\"\"", "StringLiteral@0:9 StringLiteral@10:8 End@18:0")]
    [InlineData("'a\nc'", "Error@0:2 Identifier@3:1 Error@4:1 End@5:0")]
    [InlineData("\"a\\\nc\"", "Error@0:3 Identifier@4:1 Error@5:1 End@6:0")]
    [InlineData("\"a\r\"", "Error@0:2 Error@3:1 End@4:0")]
    [InlineData("r'\\\n'", "Error@0:3 Error@4:1 End@5:0")]
    [InlineData("\0", "Error@0:1 End@1:0")]
    public static void TextWithControlCharacters_GivesItsTokens(string text, string tokens)
    {
        Assert.Equal(tokens, Format(text));
    }

    [Theory]
    [InlineData("1 # 2", 2, 1, 3, "syntax error: unexpected character '#'")]
    [InlineData("'abc", 0, 1, 1, "syntax error: unterminated string literal")]
    [InlineData("x\n  =", 4, 2, 3, "syntax error: unexpected character '='")]
    [InlineData("[1,\r\n&", 5, 2, 1, "syntax error: unexpected character '&'")]
    [InlineData("1 | 2", 2, 1, 3, "syntax error: unexpected character '|'")]
    [InlineData("a + \U0001F600", 4, 1, 5, "syntax error: unexpected character '\U0001F600'")]
    [InlineData("\uD83D", 0, 1, 1, "syntax error: unexpected character '\uD83D'")]
    [InlineData("1 + b'abc", 4, 1, 5, "syntax error: unterminated string literal")]
    [InlineData("r'''abc''", 0, 1, 1, "syntax error: unterminated string literal")]
    [InlineData("'a\nb'", 0, 1, 1, "syntax error: unterminated string literal")]
    public static void LexicalError_IsSyntaxErrorAtItsPlace(string text, int offset, int line, int column, string message)
    {
        var lexer = new Lexer(text, new LineTable(text));

        Token token = lexer.Next();
        while (token.Kind is not (TokenKind.Error or TokenKind.End))
        {
            Assert.Null(lexer.Error);
            token = lexer.Next();
        }

        Assert.Equal(TokenKind.Error, token.Kind);

        CelError? error = lexer.Error;
        Assert.NotNull(error);
        Assert.Equal(offset, token.Offset);
        Assert.Equal(
            (CelErrorCode.SyntaxError, CelErrorStage.Parse, message, offset, line, column),
            (error.Code, error.Stage, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Equal(((string?)null, (CelSourceLocation?)null, (Exception?)null), (error.Location.SourceName, error.SourceLocation, error.Exception));
    }

    [Fact]
    public static void LaterError_ReplacesTheEarlierOne()
    {
        const string text = "# 'a";
        var lexer = new Lexer(text, new LineTable(text));

        _ = lexer.Next();
        _ = lexer.Next();

        CelError? error = lexer.Error;
        Assert.NotNull(error);
        Assert.Equal(("syntax error: unterminated string literal", 2), (error.Message, error.Location.Offset));
    }

    [Fact]
    public static void End_IsReturnedAgain()
    {
        const string text = "x ";
        var lexer = new Lexer(text, new LineTable(text));

        _ = lexer.Next();

        Assert.Equal(new Token(TokenKind.End, 2, 0), lexer.Next());
        Assert.Equal(new Token(TokenKind.End, 2, 0), lexer.Next());
    }

    private static string Format(string text)
    {
        var lexer = new Lexer(text, new LineTable(text));
        var tokens = new List<string>();
        Token token;
        do
        {
            token = lexer.Next();
            tokens.Add(string.Create(CultureInfo.InvariantCulture, $"{token.Kind}@{token.Offset}:{token.Length}"));
        }
        while (token.Kind != TokenKind.End);

        return string.Join(' ', tokens);
    }
}
