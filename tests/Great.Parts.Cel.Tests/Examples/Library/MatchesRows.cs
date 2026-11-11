using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The table forms of the matches examples. Three forms of row, each checked by its own method.
//
// Expression rows, "bindings | expression | outcome" (Check):
//   bindings   "-" for none, else name=value pairs separated by ";"; the value is a string, taken as written
//   outcome    the text that Outcome gives:
//                bool true, bool false                 the value
//                error EvalInvalidRegex 7: message     the only error of evaluation: code, offset of its place
//                                                      in the expression (the line is 1) and message
//                compile InvalidRegex 12: message      the errors of compilation, in the order of the text; the
//                                                      pairs are joined with " ; "
//              An outcome that ends with "*" is a prefix: the text before the star is the start of the real
//              one. It is used where the standard fixes the code and the place of an error but not its text.
//
// Value rows, ""subject" | pattern | true" (CheckValues): the subject is in double quotes with the escapes
// \\ \" \n \r \t \f \v \0 \uXXXX \UXXXXXXXX decoded; the pattern is the text of the pattern as written, with
// no escapes of this table; the last cell is the value, true or false. Each row is checked in four
// expressions that must all give it: matches(S, P) and S.matches(P) with the subject and the pattern as
// literals (a constant pattern, prepared at planning), and matches(s, p) and s.matches(p) with the subject
// and the pattern in the variables s and p (a pattern that is not a constant, prepared at evaluation).
//
// Rejection rows, "pattern | offset | reason" (CheckRejections): the pattern is not valid; the offset is
// the offset in the pattern, in UTF-16 code units, where the fragment that the error is about begins (for an
// error about the whole pattern, 0). The reason is the text after "offset N: ", or a prefix of it, ending
// with "*", where the standard fixes the place and the code but not the text. Each row is checked in four
// expressions: as a constant pattern, in the global and the receiver form, the error is InvalidRegex
// at the literal of the pattern; in the variable p it is EvalInvalidRegex at the call, which is the "(" after
// the name. The message is the same.
//
// The environment of the tests that use these methods declares the string variables s and p.
internal static class MatchesRows
{
    public static List<string> Check(CelEnvironment environment, string rows, bool checkTypes = true)
    {
        var failures = new List<string>();
        foreach (string row in rows.Split('\n'))
        {
            string text = row.Trim();
            int first = text.IndexOf(" | ", StringComparison.Ordinal);
            int second = text.IndexOf(" | ", first + 3, StringComparison.Ordinal);
            string bindingsText = text[..first];
            string expression = text[(first + 3)..second];
            string expected = text[(second + 3)..];
            string actual = Outcome(environment, expression, Bindings(bindingsText), checkTypes);
            if (!Same(expected, actual))
            {
                failures.Add(text + " (actual: " + actual + ")");
            }
        }

        return failures;
    }

    public static List<string> CheckValues(CelEnvironment environment, string rows)
    {
        var failures = new List<string>();
        foreach (string row in rows.Split('\n'))
        {
            string text = row.Trim();
            int end = 1;
            while (text[end] != '"')
            {
                end += text[end] == '\\' ? 2 : 1;
            }

            string subject = Decode(text[1..end]);
            string rest = text[(end + 1 + 3)..];
            int last = rest.LastIndexOf(" | ", StringComparison.Ordinal);
            string pattern = rest[..last];
            string expected = "bool " + rest[(last + 3)..];
            CelBindings bindings = new CelBindings().Add("s", subject).Add("p", pattern);
            string subjectLiteral = StringLiteral(subject);
            string patternLiteral = PatternLiteral(pattern);
            foreach (string expression in new[]
            {
                "matches(" + subjectLiteral + ", " + patternLiteral + ")",
                subjectLiteral + ".matches(" + patternLiteral + ")",
                "matches(s, p)",
                "s.matches(p)",
            })
            {
                string actual = Outcome(environment, expression, bindings);
                if (actual != expected)
                {
                    failures.Add(text + " in " + expression + " (actual: " + actual + ")");
                }
            }
        }

        return failures;
    }

    public static List<string> CheckRejections(CelEnvironment environment, string rows)
    {
        var failures = new List<string>();
        foreach (string row in rows.Split('\n'))
        {
            string text = row.Trim();
            int last = text.LastIndexOf(" | ", StringComparison.Ordinal);
            int middle = text.LastIndexOf(" | ", last - 1, StringComparison.Ordinal);
            string pattern = text[..middle];
            string offset = text[(middle + 3)..last];
            string reason = text[(last + 3)..];
            string message = "invalid regular expression at offset " + offset + ": " + reason;
            string literal = PatternLiteral(pattern);
            CelBindings bindings = new CelBindings().Add("s", string.Empty).Add("p", pattern);
            foreach ((string expression, string outcome) in new[]
            {
                ("matches('', " + literal + ")", "compile InvalidRegex 12: " + message),
                ("''.matches(" + literal + ")", "compile InvalidRegex 11: " + message),
                ("matches('', p)", "error EvalInvalidRegex 7: " + message),
                ("''.matches(p)", "error EvalInvalidRegex 10: " + message),
            })
            {
                string actual = Outcome(environment, expression, bindings);
                if (!Same(outcome, actual))
                {
                    failures.Add(text + " in " + expression + " (actual: " + actual + ")");
                }
            }
        }

        return failures;
    }

    public static string Outcome(CelEnvironment environment, string expression, CelBindings? bindings = null, bool checkTypes = true)
    {
        CelEvalResult<CelValue> result;
        if (checkTypes)
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
            if (!compiled.IsSuccess)
            {
                var parts = new List<string>();
                foreach (CelError error in compiled.Errors)
                {
                    string stage = error.Stage is CelErrorStage.Parse or CelErrorStage.Check && error.Location.Line == 1
                        ? string.Empty
                        : " (unexpected stage or line)";
                    parts.Add(error.Code + " " + error.Location.Offset.ToString(CultureInfo.InvariantCulture) + ": " + error.Message + stage);
                }

                return "compile " + string.Join(" ; ", parts);
            }

            result = compiled.GetProgramOrThrow().Evaluate(bindings ?? new CelBindings(), cancellationToken: CancellationToken.None);
        }
        else
        {
            ParseResult parsed = environment.Parse(expression);
            if (!parsed.IsSuccess)
            {
                return "parse " + parsed.Errors[0].Code;
            }

            result = environment.EvaluateWithoutTypeCheck(parsed.Expression, bindings ?? new CelBindings());
        }

        if (result.IsSuccess)
        {
            CelValue value = result.GetValueOrThrow();
            return value.TryGetBoolean(out bool boolean) ? (boolean ? "bool true" : "bool false") : "kind " + value.Kind;
        }

        CelError evaluationError = result.Errors[0];
        string place = evaluationError.Location.Offset.ToString(CultureInfo.InvariantCulture);
        string evaluationStage = evaluationError.Stage == CelErrorStage.Evaluate && result.Errors.Length == 1 && evaluationError.Location.Line == 1
            ? string.Empty
            : " (unexpected stage, count or line)";
        return "error " + evaluationError.Code + " " + place + ": " + evaluationError.Message + evaluationStage;
    }

    // The literal of CEL that holds the value: single quotes, ASCII letters as they are, every other
    // character as \u or \U. The value has no lone surrogate.
    public static string StringLiteral(string value)
    {
        var builder = new StringBuilder("'");
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is '\\' or '\'')
            {
                builder.Append('\\').Append(character);
            }
            else if (character is >= ' ' and < '\u007f')
            {
                builder.Append(character);
            }
            else if (char.IsHighSurrogate(character))
            {
                builder.Append("\\U").Append(char.ConvertToUtf32(character, value[index + 1]).ToString("x8", CultureInfo.InvariantCulture));
                index++;
            }
            else
            {
                builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
            }
        }

        return builder.Append('\'').ToString();
    }

    // The raw string literal that holds the pattern: no escape of CEL is read in it. A pattern with a
    // single quote is written with double quotes; the patterns of the examples have no double quote then.
    public static string PatternLiteral(string pattern) =>
        pattern.Contains('\'', StringComparison.Ordinal) ? "r\"" + pattern + "\"" : "r'" + pattern + "'";

    private static bool Same(string expected, string actual) =>
        expected.Length > 0 && expected[^1] == '*'
            ? actual.StartsWith(expected[..^1], StringComparison.Ordinal)
            : actual == expected;

    // The bindings of a row: "-" for none, else name=value pairs separated by ";".
    public static CelBindings Bindings(string text)
    {
        var bindings = new CelBindings();
        if (text == "-")
        {
            return bindings;
        }

        foreach (string pair in text.Split(';'))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            bindings = bindings.Add(pair[..equals], pair[(equals + 1)..]);
        }

        return bindings;
    }

    private static string Decode(string cell)
    {
        var builder = new StringBuilder();
        for (int index = 0; index < cell.Length; index++)
        {
            if (cell[index] != '\\')
            {
                builder.Append(cell[index]);
                continue;
            }

            index++;
            switch (cell[index])
            {
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'f': builder.Append('\f'); break;
                case 'v': builder.Append('\v'); break;
                case '0': builder.Append('\0'); break;
                case 'u':
                    builder.Append((char)int.Parse(cell.AsSpan(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    index += 4;
                    break;
                case 'U':
                    builder.Append(char.ConvertFromUtf32(int.Parse(cell.AsSpan(index + 1, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                    index += 8;
                    break;
                default: builder.Append(cell[index]); break;
            }
        }

        return builder.ToString();
    }
}
