using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Properties.Syntax;

// Properties of the parser (docs/design/tests.md, "Properties of the parser"): any text parses into a tree or into errors,
// and a parsed tree prints into a text that parses into the same tree.
public sealed class ParserProperties
{
    private const ulong NeverThrowsSeed = 20261103;
    private const int NeverThrowsCases = 1000;
    private const ulong RoundTripSeed = 20261104;
    private const int RoundTripCases = 1000;

    // The limits of parsing (docs/limits.md).
    private const int LengthLimit = 65536;
    private const int NestingLimit = 250;
    private const int ErrorLimit = 100;

    private const string TooLongMessage = "expression is longer than 65536 UTF-8 bytes";
    private const string NestingMessage = "nesting depth exceeds the limit of 250";
    private const string SyntaxTreeMessage = "syntax tree depth exceeds the limit of 250";
    private const string TooManyErrorsMessage = "more than 100 syntax errors; parsing stopped";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    private static readonly SyntaxOptions WithOptionalSyntax = SyntaxOptions.Standard with { OptionalSyntax = true };

    // Characters of the lexis, characters outside it, any UTF-16 code unit (lone surrogates included) and pairs of surrogates.
    private static readonly Gen<string> Character = Gen.Frequency(
        (4, Gen.Char[' ', '~'].Select(character => character.ToString())),
        (3, Gen.Char["()[]{}.,?:!-+*/%<>=&|'\"`\\ \n\r\t#@$"].Select(character => character.ToString())),
        (1, Gen.Char.Select(character => character.ToString())),
        (1, Gen.Char['\u0080', '￿'].Select(character => character.ToString())),
        (1, Gen.Int[0x10000, 0x10FFFF].Select(char.ConvertFromUtf32)));

    // Tokens, broken tokens and pieces of tokens of the lexis.
    private static readonly Gen<string> Token = Gen.OneOfConst(
        " ", "\n", "\r\n", "// c\n", "x", "y", "f", "_a1", ".x", "true", "false", "null", "in", "if", "package", "as",
        "0", "42", "0x1F", "1u", "1.5", ".5", "1e3", "1e", "0x", "99999999999999999999", "1.5e999", "12.34m",
        "'a'", "\"b\"", "'''c'''", "r'\\d'", "b'\\xff'", "'\\z'", "'\\uD800'", "\"open", "'''open", "`a-b`", "`", "`a",
        "(", ")", "[", "]", "{", "}", ".", ",", ":", "?", ".?", "[?", "{?", "!", "-", "+", "*", "/", "%",
        "<", "<=", ">", ">=", "==", "!=", "=", "&&", "||", "&", "|", "#", "@", "$", "\uD800", "😀", "é",
        "has(", "all(", "map(", "exists_one(", "__result__", "@result");

    // Openers that each add one to the nesting: a bracket of any kind or the third operand of the conditional operator. The
    // anchor is the offset of the bracket or the "?" in the opener.
    private static readonly (string Text, int Anchor)[] Openers =
    [
        ("(", 0), ("[", 0), ("{", 0), ("{1: ", 0), ("[1, ", 0), ("f(", 1), ("x[", 1), ("a.f(", 3), ("x.all(v, ", 5),
        ("A{f: ", 1), ("!(", 1), ("-(", 1), ("c ? a : ", 2), ("c ? a :\n", 2),
    ];

    // Units with one error each in an argument list or a list, and the offset of the error in the unit.
    private static readonly (string Text, int Anchor)[] ErrorUnits =
        [("a b", 2), ("1 2", 2), ("if", 0), ("x.y z", 4), ("'s' t", 4), ("f(1 2)", 4)];

    // Units of chains in which every unit adds one to the depth of the syntax tree, and the offset of its node in the unit.
    private static readonly (string Text, int Anchor)[][] DepthUnits =
    [
        [(".f", 0), ("[0]", 0), (".g()", 2), (".if", 0)],
        [(" + 1", 1)], [(" * x", 1)], [(" == y", 1)], [(" - 2", 1)], [(" in z", 1)],
    ];

    private static readonly Gen<ParserCase> NeverThrowsCase = Gen.Frequency(
        (3, Gen.Select(Character.Array[0, 200], Gen.Bool, (parts, optional) => Case(string.Concat(parts), optional))),
        (1, Gen.Select(Character.Array[60000, 70000], Gen.Bool, (parts, optional) => Case(string.Concat(parts), optional))),
        (3, Gen.Select(Token.Array[0, 3000], Gen.Bool, (parts, optional) => Case(string.Concat(parts), optional))),
        (2, Gen.Select(Grammar.Expression, Gen.Bool, Case)),
        (2, Gen.Select(Grammar.Expression, Gen.Int[0, 1000], Gen.Int[0, 5], Token, Gen.Bool, Damaged)),
        (1, Gen.Select(Grammar.Expression, Gen.Int[0, 70000], Gen.Bool, Repeated)),
        (1, Gen.Select(Gen.OneOfConst(Openers).Array[NestingLimit, 400], Token.Array[0, 50], Gen.Bool, TooDeep)),
        (1, Gen.Select(Gen.OneOfConst(DepthUnits), Gen.Int[NestingLimit + 1, 400], Gen.Int[0, 3], Gen.Bool, SyntaxTreeTooDeep)),
        (1, Gen.Select(Gen.OneOfConst(ErrorUnits).Array[ErrorLimit + 1, 400], Gen.OneOfConst(("f(", ")"), ("[", "]")), Gen.Bool, TooManyErrors)));

    // Any text — random characters, random tokens, grammatical texts, damaged and repeated ones, and texts over each limit —
    // parses into a tree or into errors: no exception escapes, no error is InternalError, every error lies within the text, and
    // a text over a limit has the error of that limit at its place.
    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-9")]
    public void NeverThrows() =>
        PropertyLoop.Check(nameof(NeverThrows), NeverThrowsCase, NeverThrowsSeed, NeverThrowsCases, Violation, Show);

    // A grammatical text parses; printing its tree and parsing the text again gives a tree without a difference.
    [Fact]
    [Trait("Requirement", "F-1")]
    public void RoundTrip() =>
        PropertyLoop.Check(nameof(RoundTrip), Grammar.Expression, RoundTripSeed, RoundTripCases, RoundTripViolation, Quoted);

    private static ParserCase Case(string text, bool optional) => new(text, optional, Expected: default);

    // A grammatical text with a part replaced by a token.
    private static ParserCase Damaged(string text, int start, int length, string token, bool optional)
    {
        int from = Math.Min(start, text.Length);
        return Case(text[..from] + token + text[Math.Min(from + length, text.Length)..], optional);
    }

    // A grammatical text repeated with "+" up to a length, over the limit of the length or not.
    private static ParserCase Repeated(string text, int length, bool optional)
    {
        var builder = new StringBuilder(text);
        while (builder.Length < length)
        {
            builder.Append(" + ").Append(text);
        }

        return Case(builder.ToString(), optional);
    }

    // At least 250 openers: the 250th opens nesting 251 and stops parsing, so the tokens after it are not read.
    private static ParserCase TooDeep((string Text, int Anchor)[] openers, string[] tail, bool optional)
    {
        var builder = new StringBuilder();
        int anchor = 0;
        for (int i = 0; i < openers.Length; i++)
        {
            if (i == NestingLimit - 1)
            {
                anchor = builder.Length + openers[i].Anchor;
            }

            builder.Append(openers[i].Text);
        }

        builder.Append(string.Concat(tail));
        return new ParserCase(builder.ToString(), optional, [Error(CelErrorCode.NestingTooDeep, anchor, NestingMessage)]);
    }

    // "x" and a chain of more than 250 units: the 251st unit creates the first node of depth 251.
    private static ParserCase SyntaxTreeTooDeep((string Text, int Anchor)[] units, int count, int first, bool optional)
    {
        var builder = new StringBuilder("x");
        int anchor = 0;
        for (int i = 0; i < count; i++)
        {
            (string text, int unitAnchor) = units[(first + i) % units.Length];
            if (i == NestingLimit)
            {
                anchor = builder.Length + unitAnchor;
            }

            builder.Append(text);
        }

        return new ParserCase(builder.ToString(), optional, [Error(CelErrorCode.NestingTooDeep, anchor, SyntaxTreeMessage)]);
    }

    // More than 100 units with one error each: the first 100 errors are reported, the 101st becomes TooManyErrors.
    private static ParserCase TooManyErrors((string Text, int Anchor)[] units, (string Open, string Close) brackets, bool optional)
    {
        var builder = new StringBuilder(brackets.Open);
        var expected = ImmutableArray.CreateBuilder<string>(ErrorLimit + 1);
        for (int i = 0; i < units.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            int anchor = builder.Length + units[i].Anchor;
            if (i < ErrorLimit)
            {
                CelErrorCode code = units[i].Text == "if" ? CelErrorCode.ReservedIdentifier : CelErrorCode.SyntaxError;
                expected.Add(Error(code, anchor, message: null));
            }
            else if (i == ErrorLimit)
            {
                expected.Add(Error(CelErrorCode.TooManyErrors, anchor, TooManyErrorsMessage));
            }

            builder.Append(units[i].Text);
        }

        builder.Append(brackets.Close);
        return new ParserCase(builder.ToString(), optional, expected.MoveToImmutable());
    }

    private static string? Violation(ParserCase testCase)
    {
        ParseResult result;
        try
        {
            result = Environment.Parse(testCase.Text, testCase.OptionalSyntax ? WithOptionalSyntax : SyntaxOptions.Standard);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{exception.GetType().Name} escaped: {exception.Message}";
        }

        string[] actual = [.. result.Errors.Select(Format)];
        return Invalid(result, testCase.Text.Length) ?? Unexpected(actual, Expected(testCase));
    }

    // The rules for every result: a tree or errors; no InternalError; parse errors within the text; at most 100 errors and
    // TooManyErrors as the 101st; the errors by offset, except the last one, which may have stopped parsing.
    private static string? Invalid(ParseResult result, int length)
    {
        ImmutableArray<CelError> errors = result.Errors;
        if (result.IsSuccess != errors.IsEmpty)
        {
            return "the result has both a tree and errors, or neither";
        }

        int tooMany = -1;
        for (int i = 0; i < errors.Length; i++)
        {
            CelError error = errors[i];
            if (error.Code == CelErrorCode.InternalError)
            {
                return $"InternalError: {error.Message}";
            }

            if (error.Stage != CelErrorStage.Parse || error.Location.Offset < 0 || error.Location.Offset > length)
            {
                return $"{Format(error)} of the stage {error.Stage} is not a parse error within the text of {length} characters";
            }

            if (error.Code == CelErrorCode.TooManyErrors && tooMany < 0)
            {
                tooMany = i;
            }
        }

        if (errors.Length > ErrorLimit + 1 || (errors.Length == ErrorLimit + 1) != (tooMany == ErrorLimit))
        {
            return $"{errors.Length} errors, TooManyErrors at the index {tooMany}: only the 101st error is TooManyErrors";
        }

        for (int i = 1; i < errors.Length - 1; i++)
        {
            if (errors[i].Location.Offset < errors[i - 1].Location.Offset)
            {
                return $"the errors are not in the order of their offsets: {Format(errors[i - 1])} before {Format(errors[i])}";
            }
        }

        return null;
    }

    // The errors of a text over the limit of the length, the errors that the generator knows for a text over another limit,
    // or default when the generator does not know them.
    private static ImmutableArray<string> Expected(ParserCase testCase) =>
        OverTheLength(testCase.Text) is { } offset ? [Error(CelErrorCode.ExpressionTooLong, offset, TooLongMessage)] : testCase.Expected;

    private static string? Unexpected(string[] actual, ImmutableArray<string> expected)
    {
        if (!expected.IsDefault)
        {
            return expected.SequenceEqual(actual, StringComparer.Ordinal)
                ? null
                : $"expected the errors [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]";
        }

        return actual.FirstOrDefault(text => text.StartsWith(nameof(CelErrorCode.ExpressionTooLong), StringComparison.Ordinal)) is { } tooLong
            ? $"{tooLong} for a text of at most 65536 UTF-8 bytes"
            : null;
    }

    private static string? RoundTripViolation(string text)
    {
        try
        {
            ParseResult parsed = Environment.Parse(text, WithOptionalSyntax);
            if (!parsed.IsSuccess)
            {
                return "the text does not parse: " + string.Join(", ", parsed.Errors.Select(Format));
            }

            string printed = Printer.Print(parsed.Expression);
            ParseResult again = Environment.Parse(printed, WithOptionalSyntax);
            if (!again.IsSuccess)
            {
                return $"the printed text {Quoted(printed)} does not parse: " + string.Join(", ", again.Errors.Select(Format));
            }

            return SyntaxTreeComparer.Difference(parsed.Expression.Root, again.Expression.Root) is { } difference
                ? $"the printed text {Quoted(printed)} parses into another tree: {difference}"
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{exception.GetType().Name} escaped: {exception.Message}";
        }
    }

    // The offset of the first character with which the UTF-8 bytes of the text pass 65 536, or null when they do not. A pair
    // of surrogates is 4 bytes, a lone surrogate 3, the length of the U+FFFD that replaces it.
    private static int? OverTheLength(string text)
    {
        int bytes = 0;
        int i = 0;
        while (i < text.Length)
        {
            int start = i;
            char unit = text[i++];
            if (char.IsHighSurrogate(unit) && i < text.Length && char.IsLowSurrogate(text[i]))
            {
                i++;
                bytes += 4;
            }
            else
            {
                bytes += unit < '\u0080' ? 1 : unit < 'ࠀ' ? 2 : 3;
            }

            if (bytes > LengthLimit)
            {
                return start;
            }
        }

        return null;
    }

    private static string Error(CelErrorCode code, int offset, string? message) =>
        message is null
            ? string.Create(CultureInfo.InvariantCulture, $"{code}@{offset}")
            : string.Create(CultureInfo.InvariantCulture, $"{code}@{offset}: {message}");

    // An error as "<code>@<offset>", with ": <message>" for the codes of the limits.
    private static string Format(CelError error) =>
        Error(
            error.Code,
            error.Location.Offset,
            error.Code is CelErrorCode.ExpressionTooLong or CelErrorCode.NestingTooDeep or CelErrorCode.TooManyErrors ? error.Message : null);

    private static string Show(ParserCase testCase) => (testCase.OptionalSyntax ? "with the optional syntax " : "") + Quoted(testCase.Text);

    // The text in double quotes, with escapes for the quote, the backslash, control characters and surrogates.
    private static string Quoted(string text)
    {
        var builder = new StringBuilder("\"", text.Length + 2);
        foreach (char character in text)
        {
            if (character is '"' or '\\')
            {
                builder.Append('\\').Append(character);
            }
            else if (char.IsControl(character) || char.IsSurrogate(character))
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}");
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>A generated text, the syntax options and, for a text over a limit, the expected errors.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="OptionalSyntax">Whether the optional syntax is on.</param>
    /// <param name="Expected">
    /// The expected errors as <c>&lt;code&gt;@&lt;offset&gt;</c>, with <c>: &lt;message&gt;</c> for the codes of the limits; default
    /// when the generator does not know them.
    /// </param>
    private sealed record ParserCase(string Text, bool OptionalSyntax, ImmutableArray<string> Expected);
}
