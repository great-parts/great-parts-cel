using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Parses the text of an expression by recursive descent over its tokens, with one token of lookahead, within the
/// limits of parsing: the length of the text, the nesting, the depth of the syntax tree, the number of errors and the
/// execution stack. A call of a macro of the syntax options is replaced by its expansion.
/// </summary>
/// <remarks>
/// After a <see cref="CelErrorCode.SyntaxError"/> the parser skips to the next <c>,</c> or closing bracket of the
/// innermost open construct and goes on from there. <see cref="CelErrorCode.InvalidLiteral"/>,
/// <see cref="CelErrorCode.ReservedIdentifier"/>, <see cref="CelErrorCode.InvalidMacroCall"/> and
/// <see cref="CelErrorCode.SyntaxNotEnabled"/> are reported, and parsing goes on as if the construct were valid.
/// Text over the length limit is not parsed; nesting over its limit, too little stack and the 101st error stop
/// parsing. An instance parses one text once.
/// </remarks>
internal sealed class Parser
{
    // The limits of parsing: UTF-8 bytes of the text, the nesting and the depth of the syntax tree, and errors.
    private const int MaxLength = 65_536;
    private const int MaxDepth = 250;
    private const int MaxErrors = 100;

    // Up to this many UTF-16 code units a text is within the length limit: a code unit is at most 3 UTF-8 bytes.
    private const int AlwaysShortLength = MaxLength / 3;

    // The binary level of the comparisons and in; the levels above it are those of + - and * / %.
    private const int RelationLevel = 2;

    // The level of the grammar table of the first binary level, that of ||; the conditional operator is below it.
    private const int FirstBinaryLevel = 2;

    private const string OptionalSyntaxHint = " is not enabled: add the extension set optional";

#if NET11_0_OR_GREATER
    private const string DecimalLiteralNotEnabled =
        "decimal literal is not enabled: enable decimal literals in the environment builder";
#else
    private const string DecimalLiteralNotEnabled = "decimal literal is not enabled: decimal requires .NET 11";
#endif

    // The binary levels from the lowest, below the conditional operator and above it the unary operators. The
    // operators of a level associate to the left; those of a balanced level form a balanced tree instead.
    private static readonly BinaryLevel[] Levels =
    [
        new(balanced: true, [new(TokenKind.LogicalOr, "_||_", "||")]),
        new(balanced: true, [new(TokenKind.LogicalAnd, "_&&_", "&&")]),
        new(
            balanced: false,
            [
                new(TokenKind.Less, "_<_", "<"),
                new(TokenKind.LessOrEqual, "_<=_", "<="),
                new(TokenKind.GreaterOrEqual, "_>=_", ">="),
                new(TokenKind.Greater, "_>_", ">"),
                new(TokenKind.Equal, "_==_", "=="),
                new(TokenKind.NotEqual, "_!=_", "!="),
                new(TokenKind.In, "@in", "in"),
            ]),
        new(balanced: false, [new(TokenKind.Plus, "_+_", "+"), new(TokenKind.Minus, "_-_", "-")]),
        new(balanced: false, [new(TokenKind.Star, "_*_", "*"), new(TokenKind.Slash, "_/_", "/"), new(TokenKind.Percent, "_%_", "%")]),
    ];

    // RESERVED of the language definition without the keywords true, false, null and in, which are not identifiers.
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ReservedWords = FrozenSet
        .Create(
            StringComparer.Ordinal,
            "as", "break", "const", "continue", "else", "for", "function", "if", "import", "let", "loop", "package",
            "namespace", "return", "var", "void", "while")
        .GetAlternateLookup<ReadOnlySpan<char>>();

    private readonly string _text;
    private readonly SyntaxOptions _syntax;
    private readonly NodeFactory _nodes = new();

    // The errors found so far, ordered by offset; errors at one offset keep the order in which they were found.
    private readonly ImmutableArray<CelError>.Builder _errors = ImmutableArray.CreateBuilder<CelError>();

    // The pairs "." IDENT after an identifier, read before the parser knows whether they name a message; used by one
    // name at a time.
    private readonly List<(int Dot, Token Name)> _segments = [];

    // The open bracket constructs from the outermost: the closing token of each and whether a comma separates its
    // items. Their number is the bracket part of the nesting.
    private readonly List<(TokenKind Close, bool Items)> _open = [];

    // The depth of the syntax tree under each node, by id; nodes past the end of the list, the leaves among them, have
    // depth 0.
    private readonly List<int> _depths = [];

    private LineTable? _lines;
    private Lexer? _lexer;
    private Token _token;

    // The enclosing third operands of ?:, right operands of the comparison level, and right operands of the levels of
    // + - and * / %: the other parts of the nesting.
    private int _thirdOperands;
    private int _relationOperands;
    private int _arithmeticOperands;

    // The offset of the first node whose syntax tree is deeper than the limit, or -1.
    private int _deepNodeOffset = -1;

    // After a syntax error the parser has skipped to a token at which a construct goes on or ends; until a token is
    // read, the constructs that see it report no error.
    private bool _recovering;

    // Parsing has stopped: every further token is the end of the text, and no further error is reported.
    private bool _stopped;

    // The error that stopped parsing; it comes after all others.
    private CelError? _stopError;

    /// <summary>
    /// Creates a parser of one text; parsing starts with <see cref="Run"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="syntax">The syntax options.</param>
    internal Parser(string text, SyntaxOptions syntax)
    {
        _text = text;
        _syntax = syntax;
    }

    /// <summary>
    /// Parses the text of an expression.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="syntax">The syntax options; <see langword="null"/> for <see cref="SyntaxOptions.Standard"/>.</param>
    /// <returns>The parsed expression, or the errors of the stage <see cref="CelErrorStage.Parse"/>.</returns>
    internal static ParseResult Parse(string text, SyntaxOptions? syntax = null) =>
        new Parser(text, syntax ?? SyntaxOptions.Standard).Run();

    /// <summary>
    /// Parses the text.
    /// </summary>
    /// <returns>
    /// The parsed expression, or the errors of the stage <see cref="CelErrorStage.Parse"/> ordered by offset, with the
    /// error that stopped parsing last.
    /// </returns>
    internal ParseResult Run()
    {
        // Only the first code unit past the length limit is read, for the line table too.
        LineTable lines = new(_text.AsSpan(0, Math.Min(_text.Length, MaxLength + 1)));
        _lines = lines;
        int pastLimit = OffsetPastLengthLimit(_text);
        if (pastLimit >= 0)
        {
            return new ParseResult([Error(CelErrorCode.ExpressionTooLong, "expression is longer than 65536 UTF-8 bytes", pastLimit)]);
        }

        _lexer = new Lexer(_text, lines);
        _token = _lexer.Next();
        ExprNode root = ParseExpression();
        if (_token.Kind != TokenKind.End)
        {
            Fail("the end of the text");
        }

        // The depth of the syntax tree is checked only when parsing reaches the end of the text, as in cel-go.
        if (_deepNodeOffset >= 0 && !_stopped)
        {
            Report(CelErrorCode.NestingTooDeep, "syntax tree depth exceeds the limit of 250", _deepNodeOffset);
        }

        return _errors.Count == 0 && _stopError is null
            ? new ParseResult(_nodes.Build(root, lines))
            : new ParseResult(Errors(last: null));
    }

    /// <summary>
    /// Finds a binary operator by its function in the grammar table.
    /// </summary>
    /// <param name="function">The function, such as <c>_+_</c>.</param>
    /// <param name="level">
    /// The level of the operator, from 2 for <c>||</c> to 6 for <c>*</c>, <c>/</c> and <c>%</c>; the conditional
    /// operator is at level 1, below them.
    /// </param>
    /// <param name="text">The operator as written, such as <c>+</c> or <c>in</c>.</param>
    /// <returns><see langword="true"/> when the function is that of a binary operator.</returns>
    internal static bool TryGetBinaryOperator(string function, out int level, out string text)
    {
        for (int i = 0; i < Levels.Length; i++)
        {
            if (Levels[i].TextOf(function) is { } found)
            {
                level = i + FirstBinaryLevel;
                text = found;
                return true;
            }
        }

        level = 0;
        text = string.Empty;
        return false;
    }

    /// <summary>
    /// Gives the result of a parse that an unexpected exception interrupted: the errors found before it, then
    /// <see cref="CelErrorCode.InternalError"/> with the exception, at the token the parser was at.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The errors.</returns>
    internal ParseResult Interrupted(Exception exception)
    {
        // Before the first token the offset is 0.
        int offset = _token.Offset;
        CelSourceLocation location = _lines?.GetLocation(offset) ?? new CelSourceLocation(offset, 1, offset + 1);
        var error = new CelError(
            CelErrorCode.InternalError,
            CelErrorStage.Parse,
            "internal error during parsing; the original exception is in CelError.Exception",
            location,
            sourceLocation: null,
            exception);
        return new ParseResult(Errors(error));
    }

    // The offset of the first code unit with which the UTF-8 length of the text passes the limit, or -1. A lone
    // surrogate counts as the 3 bytes of the U+FFFD that replaces it.
    private static int OffsetPastLengthLimit(string text)
    {
        if (text.Length <= AlwaysShortLength)
        {
            return -1;
        }

        int end = Math.Min(text.Length, MaxLength + 1);
        int bytes = 0;
        for (int i = 0; i < end; i++)
        {
            char c = text[i];
            int start = i;
            if (c < 0x80)
            {
                bytes++;
            }
            else if (c < 0x800)
            {
                bytes += 2;
            }
            else if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(text[i + 1]))
            {
                bytes += 4;
                i++;
            }
            else
            {
                bytes += 3;
            }

            if (bytes > MaxLength)
            {
                return start;
            }
        }

        return -1;
    }

    // c ? a : b, where c and a are of the next level and b is an expression, so the operator associates to the right.
    // Every nested sub-expression starts here, so the stack is checked here.
    private ExprNode ParseExpression()
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            Stop(CelErrorCode.InsufficientStack, "insufficient execution stack to continue parsing", _token.Offset);
            return Placeholder();
        }

        ExprNode condition = ParseLevel(0);
        if (_token.Kind != TokenKind.Question)
        {
            return condition;
        }

        int question = _token.Offset;
        Advance();
        ExprNode then = ParseLevel(0);
        Expect(TokenKind.Colon, "':'");
        _thirdOperands++;
        CheckNesting(question);
        ExprNode otherwise = ParseExpression();
        _thirdOperands--;
        return Call("_?_:_", null, [condition, then, otherwise], question, counts: true);
    }

    // The operands of one level are the expressions of the next higher level; above the last level, unary ones.
    private ExprNode ParseLevel(int level)
    {
        if (level == Levels.Length)
        {
            return ParseUnary();
        }

        BinaryLevel binary = Levels[level];
        ExprNode result = ParseLevel(level + 1);
        string? function = binary.FunctionOf(_token.Kind);
        if (function is null)
        {
            return result;
        }

        if (binary.Balanced)
        {
            return ParseBalanced(level, function, result);
        }

        bool relation = level == RelationLevel;
        do
        {
            int offset = _token.Offset;
            Advance();
            ExprNode right = ParseRightOperand(level, relation, offset);
            result = Call(function, null, [result, right], offset, counts: true);
            function = binary.FunctionOf(_token.Kind);
        }
        while (function is not null);

        return result;
    }

    // The right operand of a left-associative level, which adds to the nesting.
    private ExprNode ParseRightOperand(int level, bool relation, int operatorOffset)
    {
        ref int operands = ref relation ? ref _relationOperands : ref _arithmeticOperands;
        operands++;
        CheckNesting(operatorOffset);
        ExprNode right = ParseLevel(level + 1);
        operands--;
        return right;
    }

    // A chain of the single operator of a balanced level after its first term.
    private CallNode ParseBalanced(int level, string function, ExprNode first)
    {
        TokenKind op = _token.Kind;
        List<ExprNode> terms = [first];
        List<int> offsets = [];
        while (_token.Kind == op)
        {
            offsets.Add(_token.Offset);
            Advance();
            terms.Add(ParseLevel(level + 1));
        }

        return Balance(function, terms, offsets, 0, offsets.Count - 1);
    }

    // The balanced tree of the operators low to high over the terms low to high + 1, the shape that cel-go gives to
    // chains of && and ||: the operator in the middle is the root. These calls add no depth to the syntax tree.
    private CallNode Balance(string function, List<ExprNode> terms, List<int> offsets, int low, int high)
    {
        int middle = (low + high + 1) / 2;
        ExprNode left = middle == low ? terms[middle] : Balance(function, terms, offsets, low, middle - 1);
        ExprNode right = middle == high ? terms[middle + 1] : Balance(function, terms, offsets, middle + 1, high);
        return Call(function, null, [left, right], offsets[middle], counts: false);
    }

    // A run of "!" or a run of "-" before a member, folded as in cel-go: an even run gives the member itself, an odd run
    // one call at the offset of the first operator. A single minus right before a number belongs to the number, also
    // after a run of "!"; otherwise a run never mixes the two operators.
    private ExprNode ParseUnary()
    {
        if (_token.Kind is not (TokenKind.Exclamation or TokenKind.Minus))
        {
            return ParseMember();
        }

        TokenKind op = _token.Kind;
        int first = _token.Offset;
        int count = 0;
        while (_token.Kind == op)
        {
            count++;
            Advance();
        }

        ExprNode operand;
        if (op == TokenKind.Minus)
        {
            if (count == 1 && IsNumber(_token.Kind))
            {
                return ParseMember(ParseLiteral(first), -1);
            }

            operand = ParseMember();
            return count % 2 == 0 ? operand : Call("-_", null, [operand], first, counts: false);
        }

        if (_token.Kind == TokenKind.Minus)
        {
            Token minus = _token;
            Advance();
            if (!IsNumber(_token.Kind))
            {
                Fail("an operand", minus);
                return Placeholder();
            }

            operand = ParseMember(ParseLiteral(minus.Offset), -1);
        }
        else
        {
            operand = ParseMember();
        }

        return count % 2 == 0 ? operand : Call("!_", null, [operand], first, counts: false);
    }

    // A primary followed by selections, receiver calls and indexes.
    private ExprNode ParseMember() =>
        _token.Kind is TokenKind.Identifier or TokenKind.Dot ? ParseName() : ParseMember(ParsePrimary(), -1);

    // The selections, receiver calls and indexes after an operand; dot is the offset of a "." already read, or -1.
    private ExprNode ParseMember(ExprNode operand, int dot)
    {
        while (true)
        {
            if (dot >= 0)
            {
                operand = ParseAfterDot(operand, dot);
                dot = -1;
            }
            else if (_token.Kind == TokenKind.Dot)
            {
                dot = _token.Offset;
                Advance();
            }
            else if (_token.Kind == TokenKind.LeftBracket)
            {
                operand = ParseIndex(operand);
            }
            else
            {
                return operand;
            }
        }
    }

    // After the "." of a member: "?" for an optional selection, then the field; a field that is an IDENT and is followed
    // by "(" is the name of a receiver call instead.
    private ExprNode ParseAfterDot(ExprNode operand, int dot)
    {
        bool optional = TryOptional("'.?'");
        Token field = _token;
        if (field.Kind == TokenKind.Identifier)
        {
            Advance();
            if (!optional && _token.Kind == TokenKind.LeftParenthesis)
            {
                return ParseCall(Text(field), operand);
            }
        }
        else if (field.Kind == TokenKind.EscapedIdentifier)
        {
            Advance();
        }
        else
        {
            Fail("a field name");
            return operand;
        }

        string name = FieldName(field);
        return optional
            ? Call("_?._", null, [operand, _nodes.Constant(CelValue.FromString(name), field.Offset)], dot, counts: true)
            : Select(operand, name, dot);
    }

    // "[" ["?"] Expr "]" after an operand.
    private CallNode ParseIndex(ExprNode operand)
    {
        int bracket = _token.Offset;
        Open(TokenKind.RightBracket, items: false, bracket);
        Advance();
        bool optional = TryOptional("'[?'");
        ExprNode index = ParseExpression();
        Expect(TokenKind.RightBracket, "']'");
        Close();
        return Call(optional ? "_[?_]" : "_[_]", null, [operand, index], bracket, counts: true);
    }

    // A name with an optional leading dot and its members. The pairs "." IDENT after the first identifier are read
    // before any node is created: before "{" they name a message; otherwise they are selections, and the last one is
    // the name of a receiver call when "(" follows it. Without pairs, "(" makes the name that of a global call.
    private ExprNode ParseName()
    {
        bool leadingDot = _token.Kind == TokenKind.Dot;
        if (leadingDot)
        {
            Advance();
            if (_token.Kind != TokenKind.Identifier)
            {
                Fail("an identifier");
                return Placeholder();
            }
        }

        Token first = _token;
        Advance();
        _segments.Clear();
        int dot = -1;
        while (_token.Kind == TokenKind.Dot)
        {
            dot = _token.Offset;
            Advance();
            if (_token.Kind != TokenKind.Identifier)
            {
                break;
            }

            _segments.Add((dot, _token));
            dot = -1;
            Advance();
        }

        if (dot < 0 && _token.Kind == TokenKind.LeftBrace)
        {
            return ParseMember(ParseMessage(MessageName(leadingDot, first)), -1);
        }

        if (dot < 0 && _token.Kind == TokenKind.LeftParenthesis)
        {
            if (_segments.Count == 0)
            {
                CheckNotReserved(first);
                return ParseMember(ParseCall(Name(leadingDot, first), null), -1);
            }

            Token function = _segments[^1].Name;
            ExprNode target = Selections(leadingDot, first, _segments.Count - 1);
            return ParseMember(ParseCall(Text(function), target), -1);
        }

        return ParseMember(Selections(leadingDot, first, _segments.Count), dot);
    }

    // The identifier and the first count pairs of the name as selections.
    private ExprNode Selections(bool leadingDot, Token identifier, int count)
    {
        CheckNotReserved(identifier);
        ExprNode node = _nodes.Ident(Name(leadingDot, identifier), identifier.Offset);
        for (int i = 0; i < count; i++)
        {
            (int dot, Token name) = _segments[i];
            node = Select(node, Text(name), dot);
        }

        return node;
    }

    private string MessageName(bool leadingDot, Token first)
    {
        if (!leadingDot && _segments.Count == 0)
        {
            return Text(first);
        }

        var name = new StringBuilder();
        if (leadingDot)
        {
            name.Append('.');
        }

        name.Append(_text, first.Offset, first.Length);
        foreach ((int _, Token segment) in _segments)
        {
            name.Append('.').Append(_text, segment.Offset, segment.Length);
        }

        return name.ToString();
    }

    // "(" [ExprList] ")" of a global call, or of a receiver call with its target. The arguments allow no trailing
    // comma. A call of a macro of the options is replaced by its expansion.
    private ExprNode ParseCall(string function, ExprNode? target)
    {
        int parenthesis = _token.Offset;
        Open(TokenKind.RightParenthesis, items: true, parenthesis);
        Advance();
        ImmutableArray<ExprNode>.Builder arguments = ImmutableArray.CreateBuilder<ExprNode>();
        if (!TryAdvance(TokenKind.RightParenthesis))
        {
            do
            {
                arguments.Add(ParseExpression());
            }
            while (NextItem(TokenKind.RightParenthesis, "')'", trailingComma: false));
        }

        Close();
        ImmutableArray<ExprNode> parsed = arguments.ToImmutable();
        Macro? macro = _syntax.Macros.Find(function, parsed.Length, target is not null);
        return macro is null
            ? Call(function, target, parsed, parenthesis, counts: target is not null)
            : Expand(macro, target, parsed, parenthesis);
    }

    // Replaces a macro call by its expansion and records the call. Arguments of the wrong shape are InvalidMacroCall at
    // the "(" of the call, which then stays a call. The expansion has the depth of the call it is written as.
    private ExprNode Expand(Macro macro, ExprNode? target, ImmutableArray<ExprNode> arguments, int parenthesis)
    {
        bool counts = target is not null;
        string? invalid = macro.Check(arguments);
        if (invalid is not null)
        {
            Report(CelErrorCode.InvalidMacroCall, "invalid call of macro '" + macro.Name + "': " + invalid, parenthesis);
            return Call(macro.Name, target, arguments, parenthesis, counts);
        }

        ExprNode expansion = macro.Expand(_nodes, target, arguments, parenthesis);
        _nodes.RecordMacroCall(expansion, new MacroCall(macro.Name, target, arguments));
        return Record(expansion, ChildDepth(target, arguments), counts, parenthesis);
    }

    private ExprNode ParsePrimary()
    {
        switch (_token.Kind)
        {
            case TokenKind.LeftParenthesis:
                Open(TokenKind.RightParenthesis, items: false, _token.Offset);
                Advance();
                ExprNode inner = ParseExpression();
                Expect(TokenKind.RightParenthesis, "')'");
                Close();
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

                Fail("an operand");
                return Placeholder();
        }
    }

    private static bool IsNumber(TokenKind kind) =>
        kind is TokenKind.IntLiteral or TokenKind.DoubleLiteral or TokenKind.DecimalLiteral;

    private static bool IsLiteral(TokenKind kind) =>
        IsNumber(kind) || kind is TokenKind.UIntLiteral or TokenKind.StringLiteral or TokenKind.BytesLiteral
            or TokenKind.True or TokenKind.False or TokenKind.Null;

    // A literal; minus is the offset of a minus that belongs to it, which is also its position.
    private ConstantNode ParseLiteral(int? minus)
    {
        Token token = _token;
        Advance();
        CelValue value;
        if (token.Kind == TokenKind.DecimalLiteral)
        {
            // The decimal literal has no flag in the syntax options yet; its node only lets parsing go on.
            Report(CelErrorCode.SyntaxNotEnabled, DecimalLiteralNotEnabled, minus ?? token.Offset);
            value = CelValue.Null;
        }
        else if (!Literals.TryConvert(_text, token, minus, _lines!, out value, out CelError? error))
        {
            Report(error);
        }

        return _nodes.Constant(value, minus ?? token.Offset);
    }

    // "[" [ListInit] [","] "]", where an element may have "?" before it.
    private ListNode ParseList()
    {
        int bracket = _token.Offset;
        Open(TokenKind.RightBracket, items: true, bracket);
        Advance();
        ImmutableArray<ExprNode>.Builder elements = ImmutableArray.CreateBuilder<ExprNode>();
        ImmutableArray<int>.Builder? optionalIndices = null;
        if (!TryEndItems(TokenKind.RightBracket, "']'"))
        {
            do
            {
                if (TryOptional("'?'"))
                {
                    optionalIndices ??= ImmutableArray.CreateBuilder<int>();
                    optionalIndices.Add(elements.Count);
                }

                elements.Add(ParseExpression());
            }
            while (NextItem(TokenKind.RightBracket, "']'", trailingComma: true));
        }

        Close();
        ImmutableArray<ExprNode> items = elements.ToImmutable();
        int depth = 0;
        foreach (ExprNode element in items)
        {
            depth = Math.Max(depth, DepthOf(element));
        }

        return Record(_nodes.List(items, optionalIndices?.ToImmutable() ?? [], bracket), depth, counts: false, bracket);
    }

    // "{" [MapInits] [","] "}", where MapInits is ["?"] Expr ":" Expr {"," ["?"] Expr ":" Expr}.
    private StructNode ParseMap()
    {
        int brace = _token.Offset;
        Open(TokenKind.RightBrace, items: true, brace);
        Advance();
        ImmutableArray<StructEntry>.Builder entries = ImmutableArray.CreateBuilder<StructEntry>();
        int depth = 0;
        if (!TryEndItems(TokenKind.RightBrace, "'}'"))
        {
            do
            {
                bool optional = TryOptional("'?'");
                ExprNode key = ParseExpression();
                int colon = _token.Offset;
                Expect(TokenKind.Colon, "':'");
                ExprNode value = ParseExpression();
                entries.Add(_nodes.Entry(key, value, optional, colon));
                depth = Math.Max(depth, Math.Max(DepthOf(key), DepthOf(value)));
            }
            while (NextItem(TokenKind.RightBrace, "'}'", trailingComma: true));
        }

        Close();
        return Record(_nodes.Struct(string.Empty, entries.ToImmutable(), brace), depth, counts: false, brace);
    }

    // "{" [FieldInits] [","] "}" of a message, where FieldInits is ["?"] field ":" Expr {"," ["?"] field ":" Expr} and
    // a field is an IDENT, reserved words included, or an escaped identifier.
    private StructNode ParseMessage(string name)
    {
        int brace = _token.Offset;
        Open(TokenKind.RightBrace, items: true, brace);
        Advance();
        ImmutableArray<StructEntry>.Builder entries = ImmutableArray.CreateBuilder<StructEntry>();
        int depth = 0;
        if (!TryEndItems(TokenKind.RightBrace, "'}'"))
        {
            do
            {
                bool optional = TryOptional("'?'");
                Token field = _token;
                if (field.Kind is not (TokenKind.Identifier or TokenKind.EscapedIdentifier))
                {
                    Fail("a field name");
                    continue;
                }

                Advance();
                int colon = _token.Offset;
                Expect(TokenKind.Colon, "':'");
                ExprNode value = ParseExpression();
                entries.Add(_nodes.Entry(FieldName(field), value, optional, colon));
                depth = Math.Max(depth, DepthOf(value));
            }
            while (NextItem(TokenKind.RightBrace, "'}'", trailingComma: true));
        }

        Close();
        return Record(_nodes.Struct(name, entries.ToImmutable(), brace), depth, counts: false, brace);
    }

    // Before the first item of a list, a map or a message: true when the construct ends here, at its closing token or
    // at a single comma before it, which the grammar allows even without items.
    private bool TryEndItems(TokenKind close, string closeName)
    {
        if (TryAdvance(close))
        {
            return true;
        }

        if (!TryAdvance(TokenKind.Comma))
        {
            return false;
        }

        if (TryAdvance(close))
        {
            return true;
        }

        Fail(closeName);
        return !AfterItem(close, trailingComma: true);
    }

    // After an item: true when another item follows. Anything but a comma or the closing token is a syntax error.
    private bool NextItem(TokenKind close, string closeName, bool trailingComma)
    {
        if (_token.Kind != TokenKind.Comma && _token.Kind != close)
        {
            Fail(closeName);
        }

        return AfterItem(close, trailingComma);
    }

    // A comma goes on to the next item, or, when allowed, ends the items before the closing token; the closing token
    // ends them. Any other token — after a syntax error, the closing token of an outer construct or the end of the
    // text — ends them without being read.
    private bool AfterItem(TokenKind close, bool trailingComma)
    {
        if (TryAdvance(TokenKind.Comma))
        {
            return !(trailingComma && TryAdvance(close));
        }

        TryAdvance(close);
        return false;
    }

    // Reads the "?" of an optional construct; without the optional syntax it is SyntaxNotEnabled at the "?", and
    // parsing goes on as if it were enabled.
    private bool TryOptional(string syntax)
    {
        if (_token.Kind != TokenKind.Question)
        {
            return false;
        }

        if (!_syntax.OptionalSyntax)
        {
            Report(CelErrorCode.SyntaxNotEnabled, syntax + OptionalSyntaxHint, _token.Offset);
        }

        Advance();
        return true;
    }

    // An IDENT, the name of an identifier or a global function, must not be a reserved word of the language definition.
    private void CheckNotReserved(Token identifier)
    {
        if (ReservedWords.Contains(_text.AsSpan(identifier.Offset, identifier.Length)))
        {
            Report(CelErrorCode.ReservedIdentifier, "reserved identifier '" + Text(identifier) + "'", identifier.Offset);
        }
    }

    private string Text(Token token) => _text.Substring(token.Offset, token.Length);

    // An identifier or the name of a global function, with its leading dot if any.
    private string Name(bool leadingDot, Token identifier) =>
        leadingDot ? string.Concat(".", _text.AsSpan(identifier.Offset, identifier.Length)) : Text(identifier);

    // The name of a field token: an IDENT as it is, an escaped identifier without its backticks.
    private string FieldName(Token token) =>
        token.Kind == TokenKind.EscapedIdentifier ? _text.Substring(token.Offset + 1, token.Length - 2) : Text(token);

    // A node that only lets parsing unwind after an error: the result has an error and no tree.
    private ConstantNode Placeholder() => _nodes.Constant(CelValue.Null, _token.Offset);

    private CallNode Call(string function, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset, bool counts) =>
        Record(_nodes.Call(function, target, arguments, offset), ChildDepth(target, arguments), counts, offset);

    // The largest depth of the syntax tree under the target and the arguments of a call.
    private int ChildDepth(ExprNode? target, ImmutableArray<ExprNode> arguments)
    {
        int depth = target is null ? 0 : DepthOf(target);
        foreach (ExprNode argument in arguments)
        {
            depth = Math.Max(depth, DepthOf(argument));
        }

        return depth;
    }

    private SelectNode Select(ExprNode operand, string field, int offset) =>
        Record(_nodes.Select(operand, field, isPresenceTest: false, offset), DepthOf(operand), counts: true, offset);

    private int DepthOf(ExprNode node) => node.Id <= _depths.Count ? _depths[node.Id - 1] : 0;

    // Records the depth of the syntax tree under a node just created: the largest depth of its children, plus 1 for a
    // node that counts. The first node deeper than the limit gives the place of the error.
    private T Record<T>(T node, int childDepth, bool counts, int offset)
        where T : ExprNode
    {
        int depth = counts ? childDepth + 1 : childDepth;
        if (depth == 0)
        {
            return node;
        }

        while (_depths.Count < node.Id - 1)
        {
            _depths.Add(0);
        }

        _depths.Add(depth);
        if (depth > MaxDepth && _deepNodeOffset < 0)
        {
            _deepNodeOffset = offset;
        }

        return node;
    }

    // Opens a bracket construct at the offset of its bracket, which adds to the nesting.
    private void Open(TokenKind close, bool items, int offset)
    {
        _open.Add((close, items));
        CheckNesting(offset);
    }

    private void Close() => _open.RemoveAt(_open.Count - 1);

    // The nesting at the current point of the text, as cel-go counts it: 1, the enclosing brackets, and the largest
    // count of the enclosing operands of one kind. Over the limit it stops parsing at the token that opened the operand.
    private void CheckNesting(int offset)
    {
        int operands = Math.Max(_thirdOperands, Math.Max(_relationOperands, _arithmeticOperands));
        if (1 + _open.Count + operands > MaxDepth)
        {
            Stop(CelErrorCode.NestingTooDeep, "nesting depth exceeds the limit of 250", offset);
        }
    }

    private void Advance()
    {
        if (_stopped)
        {
            return;
        }

        _token = _lexer!.Next();
        _recovering = false;
    }

    private bool TryAdvance(TokenKind kind)
    {
        if (_token.Kind != kind)
        {
            return false;
        }

        Advance();
        return true;
    }

    // Reads a token of the kind; otherwise a syntax error, after which the token is read if recovery stopped at it.
    private void Expect(TokenKind kind, string name)
    {
        if (!TryAdvance(kind))
        {
            Fail(name);
            TryAdvance(kind);
        }
    }

    private void Fail(string expected) => Fail(expected, _token);

    // Reports the syntax error at a token and skips to where parsing goes on. Nothing is reported while the parser
    // waits at the token where it stopped skipping, or after parsing stopped.
    private void Fail(string expected, Token at)
    {
        if (_recovering || _stopped)
        {
            return;
        }

        // A token of the kind Error is always the last token read, and the lexer holds its error.
        if (at.Kind == TokenKind.Error && _lexer!.Error is { } lexical)
        {
            Report(lexical);
        }
        else
        {
            Report(CelErrorCode.SyntaxError, "syntax error: expected " + expected + ", found " + Describe(at), at.Offset);
        }

        Recover();
    }

    // Skips tokens to the next comma of the innermost open construct with items, or to a closing token of an open
    // construct; brackets opened while skipping are stepped over. With nothing open, the rest of the text is skipped.
    private void Recover()
    {
        int skippedBrackets = 0;
        while (!_stopped)
        {
            switch (_token.Kind)
            {
                case TokenKind.End:
                    _recovering = true;
                    return;
                case TokenKind.LeftParenthesis or TokenKind.LeftBracket or TokenKind.LeftBrace:
                    skippedBrackets++;
                    break;
                case TokenKind.RightParenthesis or TokenKind.RightBracket or TokenKind.RightBrace:
                    if (skippedBrackets > 0)
                    {
                        skippedBrackets--;
                    }
                    else if (IsOpen(_token.Kind))
                    {
                        _recovering = true;
                        return;
                    }

                    break;
                case TokenKind.Comma:
                    if (skippedBrackets == 0 && _open.Count > 0 && _open[^1].Items)
                    {
                        _recovering = true;
                        return;
                    }

                    break;
            }

            Advance();
        }
    }

    private bool IsOpen(TokenKind close)
    {
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            if (_open[i].Close == close)
            {
                return true;
            }
        }

        return false;
    }

    private void Report(CelErrorCode code, string message, int offset) => Report(Error(code, message, offset));

    // Adds an error in the order of offsets, unless an error at its offset was found before. The error after the
    // hundredth becomes TooManyErrors at its place and stops parsing.
    private void Report(CelError error)
    {
        if (_stopped)
        {
            return;
        }

        int offset = error.Location.Offset;
        int index = _errors.Count;
        while (index > 0 && _errors[index - 1].Location.Offset > offset)
        {
            index--;
        }

        if (index > 0 && _errors[index - 1].Location.Offset == offset)
        {
            return;
        }

        if (_errors.Count == MaxErrors)
        {
            Stop(CelErrorCode.TooManyErrors, "more than 100 syntax errors; parsing stopped", offset);
            return;
        }

        _errors.Insert(index, error);
    }

    // Stops parsing with an error: the current token becomes the end of the text, so every construct around it ends
    // without reading further.
    private void Stop(CelErrorCode code, string message, int offset)
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        _token = new Token(TokenKind.End, _token.Offset, 0);
        if (!HasErrorAt(offset))
        {
            _stopError = Error(code, message, offset);
        }
    }

    private bool HasErrorAt(int offset)
    {
        foreach (CelError error in _errors)
        {
            if (error.Location.Offset == offset)
            {
                return true;
            }
        }

        return false;
    }

    private CelError Error(CelErrorCode code, string message, int offset) =>
        new(code, CelErrorStage.Parse, message, _lines!.GetLocation(offset), sourceLocation: null, exception: null);

    // The errors found, then the error that stopped parsing, then the given one.
    private ImmutableArray<CelError> Errors(CelError? last)
    {
        ImmutableArray<CelError>.Builder errors = ImmutableArray.CreateBuilder<CelError>(_errors.Count + 2);
        errors.AddRange(_errors);
        if (_stopError is not null)
        {
            errors.Add(_stopError);
        }

        if (last is not null)
        {
            errors.Add(last);
        }

        return errors.ToImmutable();
    }

    private string Describe(Token token) =>
        token.Kind == TokenKind.End ? "the end of the text" : string.Concat("'", _text.AsSpan(token.Offset, token.Length), "'");

    // The operators of one binary level and their functions.
    private sealed class BinaryLevel(bool balanced, BinaryOperator[] operators)
    {
        internal bool Balanced { get; } = balanced;

        internal string? FunctionOf(TokenKind kind)
        {
            foreach (BinaryOperator op in operators)
            {
                if (op.Token == kind)
                {
                    return op.Function;
                }
            }

            return null;
        }

        internal string? TextOf(string function)
        {
            foreach (BinaryOperator op in operators)
            {
                if (string.Equals(op.Function, function, StringComparison.Ordinal))
                {
                    return op.Text;
                }
            }

            return null;
        }
    }

    private readonly record struct BinaryOperator(TokenKind Token, string Function, string Text);
}
