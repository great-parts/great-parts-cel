using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The table forms of the examples of the extension sets, each checked by its own method. Every method returns the
// rows that fail, so a test asserts that the list is empty and the failure shows the rows.
//
// Value rows, "expression => expected" (CheckValues): both sides are evaluated in the environment with the bindings, and
// the value of the left side must have the kind of the right side and be equal to it by CEL equality; NaN equals
// NaN. The value of every row is that of cel-go v0.32.0 (a probe of the oracle with the environment flags of the
// conformance runner). A row whose value is a net.IP, a net.CIDR or a message is written as a comparison with ==: the
// comparison here is CelValue.CelEquals outside an evaluation, which has no type index, and a bare instance has no description there.
//
// Error rows, "stage | expression | codes | messages" (CheckErrors). The stage is Parse, Check or Evaluate. In the
// expression every "^" marks the place of one expected error and is removed from the text; the places are
// UTF-16 offsets on line 1, in the order of the marks, which is the order of the text. The codes are CelErrorCode
// members, one per mark, separated by " ". The messages are the texts of the errors, separated by " ;; ", or "-"
// for no check of the text; a message that ends with "*" is a prefix. An Evaluate row compiles without errors and
// has exactly one error; a Parse or Check row has exactly the marked errors and no other.
internal static class ExtensionRows
{
    public static List<string> CheckValues(CelEnvironment environment, string rows, CelBindings? bindings = null)
    {
        var failures = new List<string>();
        foreach (string row in Lines(rows))
        {
            string[] sides = row.Split(" => ");
            string? failure = CheckValue(environment, sides[0], sides[1], bindings ?? new CelBindings());
            if (failure is not null)
            {
                failures.Add(row + ": " + failure);
            }
        }

        return failures;
    }

    public static List<string> CheckErrors(CelEnvironment environment, string rows, CelBindings? bindings = null)
    {
        var failures = new List<string>();
        foreach (string row in Lines(rows))
        {
            string[] cells = row.Split(" | ");
            (string expression, int[] offsets) = Unmark(cells[1]);
            string[] codes = cells[2].Split(' ');
            string[] messages = cells[3] == "-" ? [] : cells[3].Split(" ;; ");
            string? failure = CheckError(environment, Enum.Parse<CelErrorStage>(cells[0]), expression, offsets, codes, messages, bindings ?? new CelBindings());
            if (failure is not null)
            {
                failures.Add(row + ": " + failure);
            }
        }

        return failures;
    }

    public static string[] Lines(string rows) => rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    // The text without the marks and the offsets that the marks stood at.
    public static (string Text, int[] Offsets) Unmark(string marked)
    {
        var offsets = new List<int>();
        var text = new System.Text.StringBuilder();
        foreach (char character in marked)
        {
            if (character == '^')
            {
                offsets.Add(text.Length);
            }
            else
            {
                text.Append(character);
            }
        }

        return (text.ToString(), [.. offsets]);
    }

    private static string? CheckValue(CelEnvironment environment, string expression, string expected, CelBindings bindings)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
        }

        CelEvalResult<CelValue> actual = compiled.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
        if (!actual.IsSuccess)
        {
            return "error " + actual.Errors[0].Code + ": " + actual.Errors[0].Message;
        }

        CelValue value = actual.GetValueOrThrow();
        CelValue wanted = environment.Compile<CelValue>(expected).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
        if (value.Kind != wanted.Kind)
        {
            return "kind " + value.Kind;
        }

        if (value.TryGetDouble(out double number) && wanted.TryGetDouble(out double wantedNumber) && double.IsNaN(number) && double.IsNaN(wantedNumber))
        {
            return null;
        }

        return CelValue.CelEquals(value, wanted).TryGetBoolean(out bool equal) && equal ? null : "another value";
    }

    private static string? CheckError(
        CelEnvironment environment,
        CelErrorStage stage,
        string expression,
        int[] offsets,
        string[] codes,
        string[] messages,
        CelBindings bindings)
    {
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        ImmutableArray<CelError> errors;
        if (stage == CelErrorStage.Evaluate)
        {
            if (!compiled.IsSuccess)
            {
                return "compile error " + compiled.Errors[0].Code + ": " + compiled.Errors[0].Message;
            }

            CelEvalResult<CelValue> result = compiled.GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
            if (result.IsSuccess)
            {
                return "a value";
            }

            errors = result.Errors;
        }
        else
        {
            if (compiled.IsSuccess)
            {
                return "no error";
            }

            errors = compiled.Errors;
        }

        if (errors.Length != offsets.Length)
        {
            return "errors: " + Describe(errors);
        }

        for (int index = 0; index < errors.Length; index++)
        {
            CelError error = errors[index];
            if (error.Stage != stage || error.Code.ToString() != codes[index] || error.Location.Offset != offsets[index] || error.Location.Line != 1)
            {
                return "error " + index.ToString(CultureInfo.InvariantCulture) + ": " + Describe([error]);
            }

            if (messages.Length > 0 && !SameText(messages[index], error.Message))
            {
                return "message " + index.ToString(CultureInfo.InvariantCulture) + ": " + error.Message;
            }
        }

        return null;
    }

    private static bool SameText(string expected, string actual) =>
        expected.Length > 0 && expected[^1] == '*'
            ? actual.StartsWith(expected[..^1], StringComparison.Ordinal)
            : actual == expected;

    private static string Describe(ImmutableArray<CelError> errors)
    {
        var parts = new List<string>();
        foreach (CelError error in errors)
        {
            parts.Add(error.Stage + " " + error.Code + "@" + error.Location.Offset.ToString(CultureInfo.InvariantCulture) + " " + error.Message);
        }

        return string.Join(" ;; ", parts);
    }
}
