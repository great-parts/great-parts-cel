using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Prints a syntax tree as the text of an expression that parses, with the same syntax options, into an equal tree,
/// as <c>parser/unparser.go</c> of cel-go. It serves the property "parse, print, parse gives the same tree".
/// </summary>
/// <remarks>
/// Binary operators and the conditional operator have single spaces around them; parentheses are added only where
/// the tree needs them; a macro expansion is printed as its recorded call. The printer recurses without checking the
/// stack: it serves trees within the limits of parsing.
/// </remarks>
internal sealed class Printer
{
    // The levels of the grammar table below and above the binary levels of the parser.
    private const int ConditionalLevel = 1;
    private const int UnaryLevel = 7;
    private const int MemberLevel = 8;
    private const int PrimaryLevel = 9;

    // The level of the operands of the conditional operator before its ":".
    private const int ConditionOperandLevel = 2;

    private readonly ParsedExpression _expression;
    private readonly StringBuilder _text = new();

    private Printer(ParsedExpression expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Prints a parsed expression.
    /// </summary>
    /// <param name="expression">The parsed expression.</param>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">
    /// The tree has a comprehension or a presence test without a recorded macro call.
    /// </exception>
    internal static string Print(ParsedExpression expression)
    {
        var printer = new Printer(expression);
        printer.Node(expression.Root);
        return printer._text.ToString();
    }

    /// <summary>
    /// Appends a constant as a literal: <c>int</c> in decimal, <c>uint</c> with <c>u</c>, <c>double</c> in the
    /// shortest round-trip form with a point or an exponent, a string or bytes in double quotes with escapes.
    /// </summary>
    /// <param name="text">The text to append to.</param>
    /// <param name="value">The value of a constant node.</param>
    internal static void AppendConstant(StringBuilder text, CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long integer);
                text.Append(integer.ToString(CultureInfo.InvariantCulture));
                break;
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                text.Append(unsigned.ToString(CultureInfo.InvariantCulture)).Append('u');
                break;
            case CelValueKind.Double:
                _ = value.TryGetDouble(out double number);
                AppendDouble(text, number);
                break;
            case CelValueKind.String:
                _ = value.TryGetString(out string? s);
                AppendString(text, s!);
                break;
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                AppendBytes(text, bytes.Span);
                break;
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool flag);
                text.Append(flag ? "true" : "false");
                break;
            default:
                text.Append("null");
                break;
        }
    }

    private static void AppendDouble(StringBuilder text, double number)
    {
        string digits = number.ToString("R", CultureInfo.InvariantCulture);

        // The text of a power of two can read back to the double below it, such as 2^-25: the gap below is half the gap
        // above, and .NET takes the wider one on both sides.
        if (double.IsFinite(number) && double.Parse(digits, NumberStyles.Float, CultureInfo.InvariantCulture) != number)
        {
            AppendShortestExponentForm(text, number);
            return;
        }

        text.Append(digits);
        if (digits.AsSpan().IndexOfAny('.', 'E') < 0)
        {
            text.Append(".0");
        }
    }

    // The shortest digits that read back, as d.dddE±XX with at least two digits of the exponent, as .NET writes it.
    private static void AppendShortestExponentForm(StringBuilder text, double number)
    {
        Span<char> digits = stackalloc char[DoubleTextRules.BufferLength];
        int count = DoubleTextRules.ShortestDigits(number, digits, out int point);
        if (double.IsNegative(number))
        {
            text.Append('-');
        }

        text.Append(digits[0]);
        if (count > 1)
        {
            text.Append('.').Append(digits[1..count]);
        }

        int exponent = point - 1;
        text.Append('E').Append(exponent < 0 ? '-' : '+').Append(Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture));
    }

    // A string in double quotes with escapes for the backslash, the quote and the control characters.
    private static void AppendString(StringBuilder text, string value)
    {
        text.Append('"');
        foreach (char c in value)
        {
            _ = c switch
            {
                '\\' => text.Append(@"\\"),
                '"' => text.Append("\\\""),
                '\n' => text.Append(@"\n"),
                '\r' => text.Append(@"\r"),
                '\t' => text.Append(@"\t"),
                < ' ' or '\u007F' => text.Append(@"\u00").Append(((int)c).ToString("X2", CultureInfo.InvariantCulture)),
                _ => text.Append(c),
            };
        }

        text.Append('"');
    }

    // Bytes as b"…": printable ASCII other than the quote and the backslash as it is, other bytes as \xHH.
    private static void AppendBytes(StringBuilder text, ReadOnlySpan<byte> value)
    {
        text.Append("b\"");
        foreach (byte b in value)
        {
            if (b is >= 0x20 and < 0x7F and not (byte)'"' and not (byte)'\\')
            {
                text.Append((char)b);
            }
            else
            {
                text.Append(@"\x").Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        text.Append('"');
    }

    // A field that is not a SELECTOR — an identifier that is not a keyword — is written in backticks.
    private static bool IsSelector(string field)
    {
        if (field.Length == 0 || char.IsAsciiDigit(field[0]) || field is "true" or "false" or "null" or "in")
        {
            return false;
        }

        foreach (char c in field)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNumber(ExprNode node) =>
        node is ConstantNode { Value.Kind: CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double };

    private void Node(ExprNode node)
    {
        switch (node)
        {
            case ConstantNode constant:
                AppendConstant(_text, constant.Value);
                break;
            case IdentNode ident:
                _text.Append(ident.Name);
                break;
            case SelectNode { IsPresenceTest: false } select:
                Target(select.Operand);
                _text.Append('.');
                Field(select.Field);
                break;
            case CallNode call:
                Call(call);
                break;
            case ListNode list:
                List(list);
                break;
            case StructNode structure:
                Struct(structure);
                break;
            default:
                MacroCall(node);
                break;
        }
    }

    private void Call(CallNode call)
    {
        if (call.Target is not null)
        {
            Target(call.Target);
            _text.Append('.').Append(call.Function);
            Arguments(call.Arguments);
            return;
        }

        ImmutableArray<ExprNode> arguments = call.Arguments;
        if (Parser.TryGetBinaryOperator(call.Function, out int level, out string text) && arguments.Length == 2)
        {
            Binary(call.Function, level, text, arguments[0], arguments[1]);
            return;
        }

        switch (call.Function)
        {
            case "_?_:_" when arguments.Length == 3:
                Operand(arguments[0], ConditionOperandLevel);
                _text.Append(" ? ");
                Operand(arguments[1], ConditionOperandLevel);
                _text.Append(" : ");
                Node(arguments[2]);
                break;
            case "!_" or "-_" when arguments.Length == 1:
                _text.Append(call.Function[0]);
                Unary(arguments[0]);
                break;
            case "_[_]" or "_[?_]" when arguments.Length == 2:
                Target(arguments[0]);
                _text.Append(call.Function == "_[_]" ? "[" : "[?");
                Node(arguments[1]);
                _text.Append(']');
                break;
            case "_?._" when arguments is [_, ConstantNode field] && field.Value.TryGetString(out string? name):
                Target(arguments[0]);
                _text.Append(".?");
                Field(name);
                break;
            default:
                _text.Append(call.Function);
                Arguments(arguments);
                break;
        }
    }

    // An operand of a lower level needs parentheses, and so does one of the same level: on the right of a
    // left-associative level, and on either side of a balanced level, whose only operator it then is, so that the shape
    // of the chain stays.
    private void Binary(string function, int level, string text, ExprNode left, ExprNode right)
    {
        bool balanced = function is "_||_" or "_&&_";
        int leftLevel = LevelOf(left);
        Parenthesized(left, leftLevel < level || (balanced && leftLevel == level));
        _text.Append(' ').Append(text).Append(' ');
        Parenthesized(right, LevelOf(right) <= level);
    }

    // The operand of !_ or -_: a numeric constant, a run of either operator or a lower level needs parentheses.
    private void Unary(ExprNode operand) => Parenthesized(operand, IsNumber(operand) || LevelOf(operand) <= UnaryLevel);

    // The operand of a selection, an index or a receiver call: a numeric constant or a lower level needs parentheses.
    private void Target(ExprNode target) => Parenthesized(target, IsNumber(target) || LevelOf(target) < MemberLevel);

    private void Operand(ExprNode operand, int level) => Parenthesized(operand, LevelOf(operand) < level);

    private void Parenthesized(ExprNode node, bool parentheses)
    {
        if (parentheses)
        {
            _text.Append('(');
            Node(node);
            _text.Append(')');
        }
        else
        {
            Node(node);
        }
    }

    private void Arguments(ImmutableArray<ExprNode> arguments)
    {
        _text.Append('(');
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                _text.Append(", ");
            }

            Node(arguments[i]);
        }

        _text.Append(')');
    }

    private void List(ListNode list)
    {
        _text.Append('[');
        for (int i = 0; i < list.Elements.Length; i++)
        {
            if (i > 0)
            {
                _text.Append(", ");
            }

            if (list.OptionalIndices.Contains(i))
            {
                _text.Append('?');
            }

            Node(list.Elements[i]);
        }

        _text.Append(']');
    }

    private void Struct(StructNode structure)
    {
        _text.Append(structure.MessageName).Append('{');
        for (int i = 0; i < structure.Entries.Length; i++)
        {
            StructEntry entry = structure.Entries[i];
            if (i > 0)
            {
                _text.Append(", ");
            }

            if (entry.IsOptional)
            {
                _text.Append('?');
            }

            if (entry.FieldName is { } field)
            {
                Field(field);
            }
            else
            {
                Node(entry.Key);
            }

            _text.Append(": ");
            Node(entry.Value);
        }

        _text.Append('}');
    }

    private void Field(string field)
    {
        if (IsSelector(field))
        {
            _text.Append(field);
        }
        else
        {
            _text.Append('`').Append(field).Append('`');
        }
    }

    // A comprehension or a presence test, printed as the macro call it is the expansion of.
    private void MacroCall(ExprNode node)
    {
        MacroCall call = RecordedCall(node);
        if (call.Target is not null)
        {
            Target(call.Target);
            _text.Append('.');
        }

        _text.Append(call.Function);
        Arguments(call.Arguments);
    }

    private MacroCall RecordedCall(ExprNode node) =>
        _expression.GetMacroCall(node.Id)
            ?? throw new InvalidOperationException("The node " + node.Id.ToString(CultureInfo.InvariantCulture) + " has no recorded macro call to print.");

    // The level of the grammar table at which a node is written.
    private int LevelOf(ExprNode node) => node switch
    {
        CallNode { Target: not null } => MemberLevel,
        CallNode call => CallLevel(call),
        SelectNode { IsPresenceTest: false } => MemberLevel,
        ComprehensionNode or SelectNode => RecordedCall(node).Target is null ? PrimaryLevel : MemberLevel,
        _ => PrimaryLevel,
    };

    private static int CallLevel(CallNode call)
    {
        int count = call.Arguments.Length;
        if (Parser.TryGetBinaryOperator(call.Function, out int level, out _) && count == 2)
        {
            return level;
        }

        return call.Function switch
        {
            "_?_:_" when count == 3 => ConditionalLevel,
            "!_" or "-_" when count == 1 => UnaryLevel,
            "_[_]" or "_[?_]" or "_?._" when count == 2 => MemberLevel,
            _ => PrimaryLevel,
        };
    }
}
