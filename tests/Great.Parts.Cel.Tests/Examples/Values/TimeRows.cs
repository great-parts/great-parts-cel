using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The table form of the time examples. A table has a row per line, "expression | outcome"; a table of
// identifiers has "identifier | expression | outcome" (labelColumns is 1: the label is not part of the
// check, it names the row of the standard library table that the row exercises). The outcome is the text
// that Outcome gives:
//   int 5, uint 5, bool true, double 1.5, str text    the value, as its kind and string() write it
//   ts 1234567890 0                                   a timestamp: Unix seconds and nanoseconds
//   du 3600000000000                                  a duration: nanoseconds
//   error Overflow 4: timestamp overflow              the only error: code, offset of its place in the
//                                                     expression (the line is 1) and message
// An outcome that ends with " *" is a prefix: the text before the star is the start of the real one. It
// is used where the standard fixes the code and the place of an error but its detail text is the library's
// own (the detail of InvalidArgument for a string of time that does not parse).
// Evaluation errors have the stage Evaluate; compile errors give "compile <code>".
internal static class TimeRows
{
    public static List<string> Check(CelEnvironment environment, string rows, CelBindings? bindings = null, int labelColumns = 0, bool checkTypes = true)
    {
        var failures = new List<string>();
        foreach (string row in rows.Split('\n'))
        {
            string text = row.Trim();
            for (int skipped = 0; skipped < labelColumns; skipped++)
            {
                text = text[(text.IndexOf(" | ", System.StringComparison.Ordinal) + 3)..];
            }

            int bar = text.IndexOf(" | ", System.StringComparison.Ordinal);
            string expression = text[..bar];
            string expected = text[(bar + 3)..];
            string actual = Outcome(environment, expression, bindings, checkTypes);
            bool same = expected.EndsWith(" *", System.StringComparison.Ordinal)
                ? actual.StartsWith(expected[..^1], System.StringComparison.Ordinal)
                : actual == expected;
            if (!same)
            {
                failures.Add(text + " (actual: " + actual + ")");
            }
        }

        return failures;
    }

    // The labels (the first column) of a table with labelColumns = 1, without repeats.
    public static SortedSet<string> Labels(string rows)
    {
        var labels = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (string row in rows.Split('\n'))
        {
            string text = row.Trim();
            labels.Add(text[..text.IndexOf(" | ", System.StringComparison.Ordinal)]);
        }

        return labels;
    }

    // checkTypes false is the internal entry of evaluation without type checking: the expression is parsed
    // and evaluated, and the outcomes are those of evaluation ("parse <code>" if it does not parse).
    public static string Outcome(CelEnvironment environment, string expression, CelBindings? bindings = null, bool checkTypes = true)
    {
        CelEvalResult<CelValue> result;
        if (checkTypes)
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
            if (!compiled.IsSuccess)
            {
                return "compile " + compiled.Errors[0].Code;
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
            return Describe(result.GetValueOrThrow());
        }

        CelError error = result.Errors[0];
        string place = error.Location.Offset.ToString(CultureInfo.InvariantCulture);
        string stage = error.Stage == CelErrorStage.Evaluate && result.Errors.Length == 1 && error.Location.Line == 1
            ? string.Empty
            : " (unexpected stage, count or line)";
        return "error " + error.Code + " " + place + ": " + error.Message + stage;
    }

    public static string Describe(CelValue value)
    {
        if (value.TryGetInt64(out long integer))
        {
            return "int " + integer.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetUInt64(out ulong unsigned))
        {
            return "uint " + unsigned.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetBoolean(out bool boolean))
        {
            return boolean ? "bool true" : "bool false";
        }

        if (value.TryGetDouble(out double real))
        {
            return "double " + real.ToString("R", CultureInfo.InvariantCulture);
        }

        if (value.TryGetString(out string? text))
        {
            return "str " + text;
        }

        if (value.TryGetTimestamp(out CelTimestamp timestamp))
        {
            return "ts " + timestamp.Seconds.ToString(CultureInfo.InvariantCulture) + " " + timestamp.Nanoseconds.ToString(CultureInfo.InvariantCulture);
        }

        if (value.TryGetDuration(out CelDuration duration))
        {
            return "du " + duration.TotalNanoseconds.ToString(CultureInfo.InvariantCulture);
        }

        return "kind " + value.Kind;
    }
}
