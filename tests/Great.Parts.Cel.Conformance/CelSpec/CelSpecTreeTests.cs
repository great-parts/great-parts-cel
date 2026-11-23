using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.CelSpec;

// The text and the tree on the cel-spec suite (docs/design/tests.md, "Text and tree"): every case that is type checked runs through
// the text path and three tree paths of the same setup, and each tree path gives the outcome of the text path: the stage, the code
// and the place of each error, and the type and the value of a result. The cases of disable_check and of disable_macros are outside
// the comparison: they have no type-checked path in the suite (a disable_macros case fails the checked path of the runner), and the
// cases that only check have no result to compare. The skip list does not apply: a case that the library fails fails the same way
// on every path. A case that a path cannot match is named in NotComparable with the reason; the test fails when such a case matches.
public sealed class CelSpecTreeTests
{
    private const int FailuresShown = 40;

    // The cases that a path gives another outcome for, by the name of the path and of the case. The bytes of a checked expression hold
    // the names after type checking, and compiling them again resolves a name through the container once more: `.y` in the
    // container com.example is the global y, which the checking writes as y, and the container resolves it to com.example.y.
    private static readonly Dictionary<string, string> NotComparable = new(StringComparer.Ordinal)
    {
        ["checked-expr/namespace/namespace_shadowing/disambiguation"] = "`.y` is written without the dot, as cel-go writes it unless a loop variable shadows y, and the check again resolves y to com.example.y",
    };

    [Fact]
    [Trait("Requirement", "F-19")]
    public void ParsedPath_GivesTheOutcomeOfTheTextPath() =>
        AssertSameOutcome("parsed", new ParsedEvaluator(), onlyValid: false);

    [Fact]
    [Trait("Requirement", "F-20")]
    public void ProtobufPath_GivesTheOutcomeOfTheTextPath() =>
        AssertSameOutcome("protobuf", new ProtobufEvaluator(), onlyValid: false);

    [Fact]
    [Trait("Requirement", "F-20")]
    public void CheckedExprPath_GivesTheOutcomeOfTheTextPath_WhereTheTextValidates() =>
        AssertSameOutcome("checked-expr", new CheckedExprEvaluator(), onlyValid: true);

    private static void AssertSameOutcome(string path, ICaseEvaluator evaluator, bool onlyValid)
    {
        TextEvaluator text = new();
        List<string> failures = [];
        int compared = 0;
        foreach (ConformanceCase testCase in CelSpecSuite.ReadCases().Where(IsTypeChecked))
        {
            CaseOutcome expected = text.Evaluate(testCase);
            if (onlyValid && (expected.Exception is not null || expected.Stage != CaseStage.Evaluate))
            {
                continue;
            }

            compared++;
            string? difference = Difference(expected, evaluator.Evaluate(testCase));
            if (NotComparable.TryGetValue(path + "/" + testCase.Name, out string? reason))
            {
                difference = difference is null ? $"matches now, remove it from NotComparable ({reason})" : null;
            }

            if (difference is not null)
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{testCase.Path}({testCase.Line},{testCase.Column}): {testCase.Name}: {difference}"));
            }
        }

        string shown = string.Join('\n', failures.Take(FailuresShown));
        Assert.True(
            failures.Count == 0,
            string.Create(CultureInfo.InvariantCulture, $"{failures.Count} of {compared} cases differ on the {path} path:\n{shown}"));
        Assert.True(compared > 1000, string.Create(CultureInfo.InvariantCulture, $"only {compared} cases were compared"));
    }

    private static bool IsTypeChecked(ConformanceCase testCase) =>
        testCase.Check == CheckMode.Check && !testCase.Environment.DisableMacros;

    private static string? Difference(CaseOutcome expected, CaseOutcome actual)
    {
        if (expected.Exception?.GetType() != actual.Exception?.GetType())
        {
            return $"the text path threw {Name(expected.Exception)}, the path {Name(actual.Exception)}: {actual.Exception?.Message}";
        }

        if (expected.Stage != actual.Stage)
        {
            return $"the text path stopped at {expected.Stage}, the path at {actual.Stage}: {string.Join("; ", actual.Errors)}";
        }

        if (!expected.Errors.SequenceEqual(actual.Errors, StringComparer.Ordinal))
        {
            return $"errors [{string.Join("; ", expected.Errors)}] against [{string.Join("; ", actual.Errors)}]";
        }

        if (!Equals(expected.ResultType, actual.ResultType))
        {
            return $"result type {expected.ResultType} against {actual.ResultType}";
        }

        return Equals(expected.Value, actual.Value) ? null : $"value {expected.Value} against {actual.Value}";
    }

    private static string Name(Exception? exception) => exception?.GetType().Name ?? "nothing";
}
