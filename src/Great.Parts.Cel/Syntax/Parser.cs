using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Parses the text of an expression by recursive descent over its tokens, with one token of lookahead.
/// </summary>
/// <remarks>
/// Parsing stops at the first <see cref="CelErrorCode.SyntaxError"/>; an invalid literal is reported as
/// <see cref="CelErrorCode.InvalidLiteral"/> and parsing goes on. The parser has no limits of length and nesting yet,
/// so it must not receive untrusted text.
/// </remarks>
internal sealed class Parser
{
    // The binary operators by precedence level, from the lowest. Operators of one level associate to the left; the
    // operators of a balanced level form a balanced tree instead.
    private static readonly (TokenKind Operator, string Function, bool Balanced)[] Levels =
    [
        (TokenKind.LogicalOr, "_||_", true),
        (TokenKind.Plus, "_+_", false),
        (TokenKind.Slash, "_/_", false),
    ];

    private readonly string _text;
    private readonly LineTable _lines;
    private readonly Lexer _lexer;
    private readonly NodeFactory _nodes = new();
    private readonly ImmutableArray<CelError>.Builder _errors = ImmutableArray.CreateBuilder<CelError>();
    private Token _token;

    // After the first syntax error every further token is the end of the text and no further error is reported.
    private bool _stopped;

    private Parser(string text)
    {
        _text = text;
        _lines = new LineTable(text);
        _lexer = new Lexer(text, _lines);
        _token = _lexer.Next();
    }

    /// <summary>
    /// Parses the text of an expression.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The parsed expression, or the errors of the stage <see cref="CelErrorStage.Parse"/>.</returns>
    internal static ParseResult Parse(string text)
    {
        var parser = new Parser(text);
        ExprNode root = parser.ParseExpression();
        if (parser._token.Kind != TokenKind.End)
        {
            parser.Fail("the end of the text");
        }

        return parser._errors.Count == 0
            ? new ParseResult(parser._nodes.Build(root, parser._lines))
            : new ParseResult(parser._errors.ToImmutable());
    }

    private ExprNode ParseExpression() => ParseLevel(0);

    // The operands of one level are the expressions of the next higher level; above the last level, unary ones.
    private ExprNode ParseLevel(int level)
    {
        if (level == Levels.Length)
        {
            return ParseUnary();
        }

        (TokenKind op, string function, bool balanced) = Levels[level];
        ExprNode first = ParseLevel(level + 1);
        if (_token.Kind != op)
        {
            return first;
        }

        List<ExprNode> terms = [first];
        List<int> offsets = [];
        while (_token.Kind == op)
        {
            offsets.Add(_token.Offset);
            Advance();
            terms.Add(ParseLevel(level + 1));
        }

        if (balanced)
        {
            return Balance(function, terms, offsets, 0, offsets.Count - 1);
        }

        ExprNode result = first;
        for (int i = 0; i < offsets.Count; i++)
        {
            result = Binary(function, result, terms[i + 1], offsets[i]);
        }

        return result;
    }

    // The balanced tree of the operators low to high over the terms low to high + 1, the shape that cel-go gives to
    // chains of && and ||: the operator in the middle is the root.
    private CallNode Balance(string function, List<ExprNode> terms, List<int> offsets, int low, int high)
    {
        int middle = (low + high + 1) / 2;
        ExprNode left = middle == low ? terms[middle] : Balance(function, terms, offsets, low, middle - 1);
        ExprNode right = middle == high ? terms[middle + 1] : Balance(function, terms, offsets, middle + 1, high);
        return Binary(function, left, right, offsets[middle]);
    }

    private CallNode Binary(string function, ExprNode left, ExprNode right, int offset) =>
        _nodes.Call(function, null, [left, right], offset);

    // A run of minuses before a primary, folded as in cel-go: an even run gives the primary itself, an odd run one -_ at
    // the offset of the first minus. A single minus right before an integer or floating literal belongs to the literal.
    private ExprNode ParseUnary()
    {
        int minus = _token.Offset;
        int count = 0;
        while (_token.Kind == TokenKind.Minus)
        {
            count++;
            Advance();
        }

        if (count == 1 && _token.Kind is TokenKind.IntLiteral or TokenKind.DoubleLiteral)
        {
            return ParseLiteral(minus);
        }

        ExprNode operand = ParsePrimary();
        return count % 2 == 0 ? operand : _nodes.Call("-_", null, [operand], minus);
    }

    private ExprNode ParsePrimary()
    {
        switch (_token.Kind)
        {
            case TokenKind.Identifier:
                return ParseIdentifierOrCall();
            case TokenKind.LeftParenthesis:
                Advance();
                ExprNode inner = ParseExpression();
                Expect(TokenKind.RightParenthesis, "')'");
                return inner;
            case TokenKind.LeftBracket:
                return ParseList();
            case TokenKind.LeftBrace:
                return ParseMap();
            default:
                if (IsLiteral(_token.Kind))
                {
                    return ParseLiteral(null);
                }

                // The node only lets parsing unwind: the result has an error and no tree.
                Fail("an operand");
                return _nodes.Constant(CelValue.Null, _token.Offset);
        }
    }

    private static bool IsLiteral(TokenKind kind) =>
        kind is TokenKind.IntLiteral or TokenKind.UIntLiteral or TokenKind.DoubleLiteral or TokenKind.StringLiteral
            or TokenKind.BytesLiteral or TokenKind.True or TokenKind.False or TokenKind.Null;

    private ConstantNode ParseLiteral(int? minus)
    {
        Token token = _token;
        Advance();
        if (!Literals.TryConvert(_text, token, minus, _lines, out CelValue value, out CelError? error))
        {
            _errors.Add(error);
        }

        return _nodes.Constant(value, minus ?? token.Offset);
    }

    // An identifier, or a call of a global function when "(" follows the name: "(" [ExprList] ")".
    private ExprNode ParseIdentifierOrCall()
    {
        Token name = _token;
        Advance();
        string text = _text.Substring(name.Offset, name.Length);
        if (_token.Kind != TokenKind.LeftParenthesis)
        {
            return _nodes.Ident(text, name.Offset);
        }

        int parenthesis = _token.Offset;
        Advance();
        ImmutableArray<ExprNode>.Builder arguments = ImmutableArray.CreateBuilder<ExprNode>();
        if (_token.Kind != TokenKind.RightParenthesis)
        {
            arguments.Add(ParseExpression());
            while (TryAdvance(TokenKind.Comma))
            {
                arguments.Add(ParseExpression());
            }
        }

        Expect(TokenKind.RightParenthesis, "')'");
        return _nodes.Call(text, null, arguments.ToImmutable(), parenthesis);
    }

    // "[" [ExprList] [","] "]"
    private ListNode ParseList()
    {
        int bracket = _token.Offset;
        Advance();
        ImmutableArray<ExprNode>.Builder elements = ImmutableArray.CreateBuilder<ExprNode>();
        ParseItems(TokenKind.RightBracket, "']'", () => elements.Add(ParseExpression()));
        return _nodes.List(elements.ToImmutable(), bracket);
    }

    // "{" [MapInits] [","] "}", where MapInits is Expr ":" Expr {"," Expr ":" Expr}.
    private StructNode ParseMap()
    {
        int brace = _token.Offset;
        Advance();
        ImmutableArray<StructEntry>.Builder entries = ImmutableArray.CreateBuilder<StructEntry>();
        ParseItems(TokenKind.RightBrace, "'}'", () => entries.Add(ParseEntry()));
        return _nodes.Struct(string.Empty, entries.ToImmutable(), brace);
    }

    private StructEntry ParseEntry()
    {
        ExprNode key = ParseExpression();
        int colon = _token.Offset;
        Expect(TokenKind.Colon, "':'");
        ExprNode value = ParseExpression();
        return _nodes.Entry(key, value, colon);
    }

    // The items of a list or a map up to the closing token: items separated by commas, then an optional comma, which
    // the grammar allows even without items.
    private void ParseItems(TokenKind close, string closeName, Action parseItem)
    {
        if (_token.Kind == close || TryAdvance(TokenKind.Comma))
        {
            Expect(close, closeName);
            return;
        }

        do
        {
            parseItem();
        }
        while (TryAdvance(TokenKind.Comma) && _token.Kind != close);

        Expect(close, closeName);
    }

    private void Advance() => _token = _lexer.Next();

    private bool TryAdvance(TokenKind kind)
    {
        if (_token.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
    }

    private void Expect(TokenKind kind, string name)
    {
        if (!TryAdvance(kind))
        {
            Fail(name);
        }
    }

    // Reports the syntax error at the current token and stops parsing: the current token becomes the end of the text,
    // so every construct around it ends without reading further.
    private void Fail(string expected)
    {
        if (_stopped)
        {
            return;
        }

        // The lexer has an error only for the current token: no token after a token of the kind Error is read.
        _errors.Add(_lexer.Error ?? new CelError(
            CelErrorCode.SyntaxError,
            CelErrorStage.Parse,
            "syntax error: expected " + expected + ", found " + Describe(_token),
            _lines.GetLocation(_token.Offset),
            sourceLocation: null,
            exception: null));
        _stopped = true;
        _token = new Token(TokenKind.End, _token.Offset, 0);
    }

    private string Describe(Token token) =>
        token.Kind == TokenKind.End ? "the end of the text" : string.Concat("'", _text.AsSpan(token.Offset, token.Length), "'");
}
