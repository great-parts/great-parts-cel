using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Difftest.Numbers;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// Prints a generated expression as CEL source. Literals take the form their node carries; parentheses are added where
/// precedence needs them and where the node asks for them.
/// </summary>
internal static class ExprPrinter
{
    // The precedence of CEL, from the conditional, the lowest, to members and primaries.
    private const int ConditionalPrecedence = 0;
    private const int OrPrecedence = 1;
    private const int AndPrecedence = 2;
    private const int RelationPrecedence = 3;
    private const int AddPrecedence = 4;
    private const int MultiplyPrecedence = 5;
    private const int UnaryPrecedence = 6;
    private const int PrimaryPrecedence = 7;

    /// <summary>Prints an expression.</summary>
    /// <param name="expr">The expression.</param>
    /// <exception cref="ArgumentException">A literal carries a form that its value cannot take.</exception>
    internal static string Print(Expr expr)
    {
        StringBuilder text = new();
        Write(text, expr, needsParentheses: false);
        return text.ToString();
    }

    /// <summary>
    /// Prints the literal of a <c>double</c> value in a notation; the digits are the shortest that read back to the magnitude,
    /// and a negative value, <c>-0.0</c> included, is <c>-</c> and its magnitude.
    /// </summary>
    /// <param name="value">A finite value.</param>
    /// <param name="form">The notation.</param>
    /// <exception cref="ArgumentException">The value is not finite.</exception>
    internal static string DoubleText(double value, DoubleForm form)
    {
        if (!double.IsFinite(value))
        {
            throw Invalid("a double literal is finite, not " + value.ToString(CultureInfo.InvariantCulture));
        }

        return (double.IsNegative(value) ? "-" : string.Empty) + MagnitudeText(Math.Abs(value), form);
    }

    /// <summary>
    /// The shortest decimal digits that read back to a value, without leading and trailing zeros (<c>0</c> for zero), and the
    /// exponent of the first digit: the value is <c>d.ddd × 10^exponent</c>.
    /// </summary>
    /// <param name="value">A finite value that is not negative.</param>
    internal static (string Digits, int Exponent) ShortestDecimal(double value)
    {
        // The shortest text that reads back to the same bits: "1.5E-05", "123.456", "1E+20", "0".
        string text = DoubleRoundTrip.Format(value);
        int exponentIndex = text.IndexOf('E', StringComparison.Ordinal);
        int exponent = exponentIndex < 0 ? 0 : int.Parse(text.AsSpan(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        string mantissa = exponentIndex < 0 ? text : text[..exponentIndex];
        int dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        string integer = dot < 0 ? mantissa : mantissa[..dot];
        string digits = integer + (dot < 0 ? string.Empty : mantissa[(dot + 1)..]);
        int point = integer.Length + exponent;
        string significant = digits.TrimStart('0');
        point -= digits.Length - significant.Length;
        significant = significant.TrimEnd('0');
        return significant.Length == 0 ? ("0", 0) : (significant, point - 1);
    }

    // The magnitude of a double literal, a finite value that is not negative, in a notation.
    private static string MagnitudeText(double value, DoubleForm form)
    {
        (string digits, int exponent) = ShortestDecimal(value);
        return form switch
        {
            DoubleForm.Plain => PlainDouble(digits, exponent),
            DoubleForm.Exponent => digits[..1] + (digits.Length > 1 ? "." + digits[1..] : string.Empty) + "e"
                + exponent.ToString(CultureInfo.InvariantCulture),
            DoubleForm.LeadingDot => exponent < 0 || digits == "0"
                ? "." + new string('0', Math.Max(0, -exponent - 1)) + digits
                : "." + digits + "E+" + (exponent + 1).ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, "Not a notation of a double literal."),
        };
    }

    private static string PlainDouble(string digits, int exponent)
    {
        if (exponent < 0)
        {
            return "0." + new string('0', -exponent - 1) + digits;
        }

        string integer = digits.Length > exponent + 1 ? digits[..(exponent + 1)] : digits.PadRight(exponent + 1, '0');
        string fraction = digits.Length > exponent + 1 ? digits[(exponent + 1)..] : "0";
        return integer + "." + fraction;
    }

    private static void Write(StringBuilder text, Expr expr, bool needsParentheses)
    {
        bool parentheses = needsParentheses || expr.Parenthesized;
        if (parentheses)
        {
            text.Append('(');
        }

        switch (expr)
        {
            case Call call:
                WriteCall(text, call);
                break;
            case ListLiteral list:
                WriteList(text, list);
                break;
            case MapLiteral map:
                WriteMap(text, map);
                break;
            case Ident ident:
                text.Append(ident.Name);
                break;
            case Select select:
                WriteMemberOperand(text, select.Operand);
                text.Append('.').Append(select.Field);
                break;
            case PresenceTest test:
                text.Append("has(");
                WriteMemberOperand(text, test.Operand);
                text.Append('.').Append(test.Field).Append(')');
                break;
            case Comprehension comprehension:
                WriteMemberOperand(text, comprehension.Range);
                text.Append('.').Append(comprehension.Macro).Append('(').Append(comprehension.Variable);
                foreach (Expr argument in comprehension.Arguments)
                {
                    text.Append(", ");
                    Write(text, argument, needsParentheses: false);
                }

                text.Append(')');
                break;
            default:
                text.Append(LiteralText(expr));
                break;
        }

        if (parentheses)
        {
            text.Append(')');
        }
    }

    private static string LiteralText(Expr literal) => literal switch
    {
        IntLiteral i => (i.Value < 0 ? "-" : string.Empty) + Digits(i.Value < 0 ? (ulong)(-(i.Value + 1)) + 1 : (ulong)i.Value, i.Form),
        UintLiteral u => Digits(u.Value, u.Form) + (u.Suffix is 'u' or 'U' ? u.Suffix.ToString() : throw Invalid("the suffix of a uint literal is u or U")),
        DoubleLiteral d => DoubleText(d.Value, d.Form),
        BoolLiteral b => b.Value ? "true" : "false",
        NullLiteral => "null",
        StringLiteral s => StringText(s),
        BytesLiteral b => BytesText(b),
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal, "Not a node of the profile."),
    };

    private static string Digits(ulong magnitude, IntegerForm form) => form switch
    {
        IntegerForm.DecimalDigits => magnitude.ToString(CultureInfo.InvariantCulture),
        IntegerForm.HexLower => "0x" + magnitude.ToString("x", CultureInfo.InvariantCulture),
        IntegerForm.HexUpper => "0x" + magnitude.ToString("X", CultureInfo.InvariantCulture),
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "Not a form of an integer literal."),
    };

    private static void WriteCall(StringBuilder text, Call call)
    {
        IReadOnlyList<Expr> args = call.Args;
        if (call.Receiver)
        {
            WriteMemberOperand(text, args[0]);
            text.Append('.').Append(call.Function).Append('(');
            WriteJoined(text, [.. args.Skip(1)], (builder, arg) => Write(builder, arg, needsParentheses: false));
            text.Append(')');
            return;
        }

        switch (call.Function)
        {
            case ProfileTables.Negate or ProfileTables.LogicalNot:
                text.Append(call.Function == ProfileTables.Negate ? '-' : '!');
                Write(text, args[0], Precedence(args[0]) < UnaryPrecedence);
                return;
            case ProfileTables.Conditional:
                // The condition and the first branch are below the conditional in the grammar; the second branch may be one.
                Write(text, args[0], Precedence(args[0]) <= ConditionalPrecedence);
                text.Append(" ? ");
                Write(text, args[1], Precedence(args[1]) <= ConditionalPrecedence);
                text.Append(" : ");
                Write(text, args[2], needsParentheses: false);
                return;
            case ProfileTables.Index:
                WriteMemberOperand(text, args[0]);
                text.Append('[');
                Write(text, args[1], needsParentheses: false);
                text.Append(']');
                return;
        }

        if (Operator(call.Function) is { } symbol)
        {
            // Binary operators associate to the left: a right operand of the same precedence keeps its parentheses.
            int precedence = Precedence(call);
            Write(text, args[0], Precedence(args[0]) < precedence);
            text.Append(' ').Append(symbol).Append(' ');
            Write(text, args[1], Precedence(args[1]) <= precedence);
            return;
        }

        text.Append(Named(call.Function)).Append('(');
        WriteJoined(text, args, (builder, arg) => Write(builder, arg, needsParentheses: false));
        text.Append(')');
    }

    // The operand of a selection, a presence test, an index, a receiver call or a macro: a member or a primary. A number
    // literal keeps parentheses too, since its sign or its dot would join the selection.
    private static void WriteMemberOperand(StringBuilder text, Expr operand) =>
        Write(text, operand, Precedence(operand) < PrimaryPrecedence || operand is IntLiteral or UintLiteral or DoubleLiteral);

    private static string? Operator(string function) => function switch
    {
        ProfileTables.LogicalOr => "||",
        ProfileTables.LogicalAnd => "&&",
        ProfileTables.Equal => "==",
        ProfileTables.NotEqual => "!=",
        ProfileTables.Less => "<",
        ProfileTables.LessEqual => "<=",
        ProfileTables.Greater => ">",
        ProfileTables.GreaterEqual => ">=",
        ProfileTables.In => "in",
        ProfileTables.Add => "+",
        ProfileTables.Subtract => "-",
        ProfileTables.Multiply => "*",
        ProfileTables.Divide => "/",
        ProfileTables.Modulo => "%",
        _ => null,
    };

    // The name of a function called globally.
    private static string Named(string function) => function switch
    {
        ProfileTables.Size or ProfileTables.TypeOf or ProfileTables.Dyn or ProfileTables.IntConversion or ProfileTables.UintConversion
            or ProfileTables.DoubleConversion or ProfileTables.StringConversion or ProfileTables.BytesConversion
            or ProfileTables.BoolConversion => function,
        _ => throw Invalid("the function " + function + " is not in the profile"),
    };

    private static int Precedence(Expr expr) => expr is Call { Receiver: false } call
        ? call.Function switch
        {
            ProfileTables.Conditional => ConditionalPrecedence,
            ProfileTables.LogicalOr => OrPrecedence,
            ProfileTables.LogicalAnd => AndPrecedence,
            ProfileTables.Equal or ProfileTables.NotEqual or ProfileTables.Less or ProfileTables.LessEqual or ProfileTables.Greater
                or ProfileTables.GreaterEqual or ProfileTables.In => RelationPrecedence,
            ProfileTables.Add or ProfileTables.Subtract => AddPrecedence,
            ProfileTables.Multiply or ProfileTables.Divide or ProfileTables.Modulo => MultiplyPrecedence,
            ProfileTables.Negate or ProfileTables.LogicalNot => UnaryPrecedence,
            _ => PrimaryPrecedence,
        }
        : PrimaryPrecedence;

    private static void WriteList(StringBuilder text, ListLiteral list)
    {
        text.Append('[');
        WriteJoined(text, list.Elements, (builder, element) => Write(builder, element, needsParentheses: false));
        if (list.TrailingComma)
        {
            text.Append(',');
        }

        text.Append(']');
    }

    private static void WriteMap(StringBuilder text, MapLiteral map)
    {
        text.Append('{');
        WriteJoined(text, map.Entries, (builder, entry) =>
        {
            Write(builder, entry.Key, needsParentheses: false);
            builder.Append(": ");
            Write(builder, entry.Value, needsParentheses: false);
        });
        if (map.TrailingComma)
        {
            text.Append(',');
        }

        text.Append('}');
    }

    private static void WriteJoined<T>(StringBuilder text, IReadOnlyList<T> items, Action<StringBuilder, T> write)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                text.Append(", ");
            }

            write(text, items[i]);
        }
    }

    private static string StringText(StringLiteral literal)
    {
        TextForm form = literal.Form;
        StringBuilder text = new();
        string delimiter = OpenQuotes(text, form);
        int unit = 0;
        foreach (Rune rune in literal.Value.EnumerateRunes())
        {
            Escape escape = UnitForm(form, unit++);
            if (!TextUnits.StringForms(rune.Value, form.Quote, form.Raw is not null).Contains(escape))
            {
                throw Invalid($"U+{rune.Value:X4} cannot be printed as {escape} here");
            }

            AppendUnit(text, rune.Value, escape, rune.ToString());
        }

        return CloseQuotes(text, form, unit, delimiter);
    }

    private static string BytesText(BytesLiteral literal)
    {
        TextForm form = literal.Form;
        StringBuilder text = new();
        text.Append(literal.Prefix is 'b' or 'B' ? literal.Prefix : throw Invalid("the prefix of a bytes literal is b or B"));
        string delimiter = OpenQuotes(text, form);
        int unit = 0;
        for (int index = 0; index < literal.Value.Count; unit++)
        {
            Escape escape = UnitForm(form, unit);
            if (!TextUnits.BytesForms(literal.Value, index, form.Quote, form.Raw is not null).Contains(escape))
            {
                throw Invalid($"the byte 0x{literal.Value[index]:X2} at {index} cannot be printed as {escape} here");
            }

            if (escape == Escape.Literal)
            {
                TextUnits.LiteralCodePoint(literal.Value, index, out int codePoint, out int length);
                text.Append(char.ConvertFromUtf32(codePoint));
                index += length;
            }
            else
            {
                AppendUnit(text, literal.Value[index], escape, string.Empty);
                index++;
            }
        }

        return CloseQuotes(text, form, unit, delimiter);
    }

    private static string OpenQuotes(StringBuilder text, TextForm form)
    {
        if (form.Raw is { } raw)
        {
            text.Append(raw is 'r' or 'R' ? raw : throw Invalid("the raw prefix is r or R"));
        }

        string delimiter = TextUnits.Delimiter(form.Quote);
        text.Append(delimiter);
        return delimiter;
    }

    private static string CloseQuotes(StringBuilder text, TextForm form, int units, string delimiter)
    {
        if (units != form.Units.Count)
        {
            throw Invalid($"the literal has {units} units and {form.Units.Count} forms");
        }

        return text.Append(delimiter).ToString();
    }

    private static Escape UnitForm(TextForm form, int unit) =>
        unit < form.Units.Count ? form.Units[unit] : throw Invalid($"the literal has more units than its {form.Units.Count} forms");

    private static void AppendUnit(StringBuilder text, int codePoint, Escape escape, string literal)
    {
        string unit = escape switch
        {
            Escape.Literal => literal,
            Escape.Character => "\\" + TextUnits.CharacterEscape(codePoint),
            Escape.Hex => "\\x" + codePoint.ToString("x2", CultureInfo.InvariantCulture),
            Escape.Octal => "\\" + Convert.ToString(codePoint, 8).PadLeft(3, '0'),
            Escape.UnicodeShort => "\\u" + codePoint.ToString("x4", CultureInfo.InvariantCulture),
            Escape.UnicodeLong => "\\U" + codePoint.ToString("x8", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(escape), escape, "Not a form of a unit."),
        };
        text.Append(unit);
    }

    private static ArgumentException Invalid(string problem) => new("The expression cannot be printed: " + problem + ".");
}
