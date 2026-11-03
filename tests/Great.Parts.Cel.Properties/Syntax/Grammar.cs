using System.Linq;
using CsCheck;

namespace Great.Parts.Cel.Properties.Syntax;

/// <summary>
/// Generates grammatical texts within the limits of parsing, from the whole grammar (docs/design/syntax.md, "Grammar"): every level
/// of operators, unary runs, signed literals, selections, indexes, calls, lists, maps, messages, escaped fields, the optional syntax,
/// the standard macros, comments and line breaks. Every text parses with the optional syntax and the standard macros.
/// </summary>
internal static class Grammar
{
    // The levels of the grammar table that the generator places operands by.
    private const int ConditionalLevel = 1;
    private const int ConditionLevel = 2;
    private const int UnaryLevel = 7;
    private const int MemberLevel = 8;
    private const int PrimaryLevel = 9;

    // Compounds nest at most this deep: a text stays far within the limits of the length and of the nesting.
    private const int Depth = 5;

    private static readonly Piece[] Literals =
    [
        .. new[]
        {
            "0", "1", "42", "0x1F", "0xaB", "9223372036854775807", "-1", "-0x10", "-9223372036854775808",
            "0.0", "1.5", ".5", "1e3", "1.5e-3", "6.02E23", "-2.5", "-0.0",
        }.Select(text => new Piece(text, PrimaryLevel, IsNumber: true)),
        .. new[]
        {
            "0u", "1U", "0x1Fu", "18446744073709551615u", "true", "false", "null",
            "\"\"", "'a'", "\"q\\\"b\"", "'\\n\\t\\x41\\u00e9\\U0001F600\\101'", "\"\"\"two\nlines\"\"\"", "'''x'''", "r'\\d+'",
            "R\"raw\"", "\"é😀\"", "b''", "b'ab'", "B\"\\xff\\x00\"", "br'\\x'", "b'\\377'",
        }.Select(text => new Piece(text, PrimaryLevel)),
    ];

    private static readonly Piece[] Identifiers =
        [.. new[] { "x", "y", "a_b", "_z", "int", "dyn", "v1", "has", ".x", ".y" }.Select(text => new Piece(text, PrimaryLevel))];

    // The first operands of the comprehension macros: simple names.
    private static readonly string[] Variables = ["v", "w", "it", "x"];

    // Names after "." or ".?": selectors, reserved words among them, and escaped names.
    private static readonly string[] Fields = ["f", "g_h", "if", "as", "package", "`a-b`", "`x.y z/w`", "`in`", "`true`"];

    // Global functions: no macro name, no reserved word; a leading dot is allowed.
    private static readonly string[] GlobalFunctions = ["f", "size", "g_1", "int", "matches", ".f"];

    // Receiver functions: no macro name; reserved words are allowed.
    private static readonly string[] ReceiverFunctions = ["f", "size", "contains", "package", "if"];

    private static readonly string[] MessageNames = ["A", "a.B", ".a.b.C", "google.protobuf.Int64Value"];

    private static readonly string[] MessageFields = ["f", "if", "single_int64", "`g-h`"];

    private static readonly (string Operator, int Level)[] BinaryOperators =
    [
        ("||", 2), ("&&", 3), ("<", 4), ("<=", 4), (">=", 4), (">", 4), ("==", 4), ("!=", 4), ("in", 4),
        ("+", 5), ("-", 5), ("*", 6), ("/", 6), ("%", 6),
    ];

    private static readonly string[] UnaryRuns = ["!", "!!", "!!!", "-", "--", "---"];

    // Whitespace and comments between tokens; mostly a space.
    private static readonly Gen<string> Space = Gen.FrequencyConst((8, " "), (1, "\n"), (1, "\t"), (1, " // note\n"), (1, "\r\n  "));

    /// <summary>A grammatical text.</summary>
    internal static Gen<string> Expression { get; } = Build().Select(piece => piece.Text);

    private static Gen<Piece> Build()
    {
        Gen<Piece> atom = Gen.Frequency((3, Gen.OneOfConst(Literals)), (2, Gen.OneOfConst(Identifiers)));
        Gen<Piece> current = atom;
        for (int depth = 0; depth < Depth; depth++)
        {
            current = Gen.Frequency((1, atom), (3, Gen.OneOf(Compounds(current))));
        }

        return current;
    }

    private static Gen<Piece>[] Compounds(Gen<Piece> operand)
    {
        Gen<Piece[]> arguments = operand.Array[0, 3];
        Gen<string> field = Gen.OneOfConst(Fields);
        Gen<string> element = Gen.Select(operand, Gen.Bool, (piece, optional) => (optional ? "?" : "") + piece.Text);
        Gen<string> entry = Gen.Select(operand, operand, Gen.Bool, (key, value, optional) => (optional ? "?" : "") + key.Text + ": " + value.Text);
        Gen<string> messageField = Gen.Select(
            Gen.OneOfConst(MessageFields), operand, Gen.Bool, (name, value, optional) => (optional ? "?" : "") + name + ": " + value.Text);
        return
        [
            Gen.Select(operand, Gen.OneOfConst(BinaryOperators), operand, Space, Space, Binary),
            Gen.Select(
                operand,
                operand,
                operand,
                Space,
                (condition, then, otherwise, space) => new Piece(
                    $"{AtLeast(condition, ConditionLevel)}{space}? {AtLeast(then, ConditionLevel)} : {AtLeast(otherwise, ConditionalLevel)}",
                    ConditionalLevel)),
            Gen.Select(operand, Gen.OneOfConst(UnaryRuns), (piece, run) => new Piece(run + UnaryOperand(piece), UnaryLevel)),
            Gen.Select(operand, field, Gen.Bool, (target, name, optional) => Member(target, (optional ? ".?" : ".") + name)),
            Gen.Select(operand, operand, Gen.Bool, (target, index, optional) => Member(target, (optional ? "[?" : "[") + index.Text + "]")),
            Gen.Select(
                operand, Gen.OneOfConst(ReceiverFunctions), arguments, (target, name, list) => Member(target, $".{name}({Join(list)})")),
            Gen.Select(Gen.OneOfConst(GlobalFunctions), arguments, (name, list) => new Piece($"{name}({Join(list)})", PrimaryLevel)),
            Gen.Select(element.Array[0, 3], Gen.Bool, (items, trailing) => new Piece($"[{Join(items, trailing)}]", PrimaryLevel)),
            Gen.Select(entry.Array[0, 3], Gen.Bool, (items, trailing) => new Piece($"{{{Join(items, trailing)}}}", PrimaryLevel)),
            Gen.Select(
                Gen.OneOfConst(MessageNames),
                messageField.Array[0, 3],
                Gen.Bool,
                (name, items, trailing) => new Piece($"{name}{{{Join(items, trailing)}}}", PrimaryLevel)),
            operand.Select(piece => new Piece($"({piece.Text})", PrimaryLevel)),
            Gen.Select(operand, field, (target, name) => new Piece($"has({MemberTarget(target)}.{name})", PrimaryLevel)),
            Gen.Select(
                operand,
                Gen.OneOfConst("all", "exists", "exists_one", "map", "filter"),
                Gen.OneOfConst(Variables),
                operand,
                (target, macro, variable, body) => Member(target, $".{macro}({variable}, {body.Text})")),
            Gen.Select(
                operand,
                Gen.OneOfConst(Variables),
                operand,
                operand,
                (target, variable, condition, result) => Member(target, $".map({variable}, {condition.Text}, {result.Text})")),
        ];
    }

    // The left operand of a binary operator is of its level or higher, the right one of a higher level.
    private static Piece Binary(Piece left, (string Operator, int Level) binary, Piece right, string before, string after) =>
        new($"{AtLeast(left, binary.Level)}{before}{binary.Operator}{after}{AtLeast(right, binary.Level + 1)}", binary.Level);

    private static Piece Member(Piece target, string suffix) => new(MemberTarget(target) + suffix, MemberLevel);

    private static string AtLeast(Piece piece, int level) => piece.Level >= level ? piece.Text : $"({piece.Text})";

    // A numeric literal before "." could join it, so it is put in parentheses, as an operand of a lower level.
    private static string MemberTarget(Piece piece) => piece.Level >= MemberLevel && !piece.IsNumber ? piece.Text : $"({piece.Text})";

    // A unary run applies to a member; a member that starts with a sign would join the run or be a signed literal.
    private static string UnaryOperand(Piece piece) =>
        piece.Level >= MemberLevel && piece.Text[0] != '-' ? piece.Text : $"({piece.Text})";

    private static string Join(Piece[] pieces) => string.Join(", ", pieces.Select(piece => piece.Text));

    // Lists, maps and messages allow a trailing comma after the last item.
    private static string Join(string[] items, bool trailingComma) =>
        string.Join(", ", items) + (trailingComma && items.Length > 0 ? "," : "");

    /// <summary>A generated text and the level of the grammar table of its outermost construct.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="Level">The level: 1 for the conditional operator up to 9 for a primary.</param>
    /// <param name="IsNumber">Whether the text is a numeric literal.</param>
    private sealed record Piece(string Text, int Level, bool IsNumber = false);
}
