using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Google.Protobuf.Collections;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>Whether a case passed, and why not.</summary>
/// <param name="Passed">Whether the case passed.</param>
/// <param name="Reason">Why the case failed: one line of English; empty when it passed.</param>
internal readonly record struct Judgement(bool Passed, string Reason)
{
    /// <summary>The case passed.</summary>
    internal static Judgement Pass { get; } = new(true, string.Empty);

    /// <summary>The case failed.</summary>
    /// <param name="reason">Why.</param>
    internal static Judgement Fail(string reason) => new(false, reason);
}

/// <summary>
/// The rule of the cel-go conformance runner, cel-go v0.32.0, <c>conformance/conformance_test.go</c>: the function
/// <c>conformanceTest</c> and the default matcher of <c>TestConformance</c>. The steps follow the order of that function.
/// </summary>
internal static class Rule
{
    /// <summary>Judges the outcome of a case.</summary>
    /// <param name="testCase">The case.</param>
    /// <param name="outcome">What the case gave.</param>
    internal static Judgement Judge(ConformanceCase testCase, CaseOutcome outcome)
    {
        // parse, environment, check: an error or an exception of a stage fails the case, even when an evaluation error is expected.
        if (outcome.Exception is not null)
        {
            return Judgement.Fail($"{StageName(outcome.Stage)} threw {outcome.Exception.GetType().Name}: {ExprText.Quote(outcome.Exception.Message)}");
        }

        if (outcome.Stage != CaseStage.Evaluate && outcome.Errors.Count > 0)
        {
            return Judgement.Fail($"{StageName(outcome.Stage)} failed: {Errors(outcome.Errors)}");
        }

        // check_only: only the deduced type of a typed_result is compared.
        if (testCase.Check == CheckMode.CheckOnly)
        {
            return testCase.Expected.Kind == ExpectationKind.TypedResult
                ? Types(testCase.Expected.DeducedType, outcome.ResultType)
                : Judgement.Fail($"a check_only case needs the matcher typed_result, not {testCase.Expected.Matcher}");
        }

        // bindings: the runner binds values only.
        KeyValuePair<string, ExprValue> binding = testCase.Bindings.FirstOrDefault(pair => pair.Value.KindCase != ExprValue.KindOneofCase.Value);
        if (binding.Value is not null)
        {
            return Judgement.Fail($"the binding '{binding.Key}' is not a value but {BindingKind(binding.Value)}; the runner binds values only");
        }

        return testCase.Expected.Kind switch
        {
            ExpectationKind.Value => Values(testCase.Expected.Value, outcome),
            ExpectationKind.TypedResult => Values(testCase.Expected.Value, outcome) is { Passed: false } failed
                ? failed
                : Types(testCase.Expected.DeducedType, outcome.ResultType),
            ExpectationKind.EvaluationError => outcome.Stage == CaseStage.Evaluate && outcome.Errors.Count > 0
                ? Judgement.Pass
                : Judgement.Fail($"expected an evaluation error, got {Got(outcome)}"),
            _ => Judgement.Fail($"the matcher {testCase.Expected.Matcher} is not supported"),
        };
    }

    /// <summary>
    /// Whether two values are equal as <c>cel.expr.Value</c> messages, as <c>diffValue</c> of the cel-go runner compares them with
    /// <c>protocmp</c>: field by field, a <c>double</c> with Go <c>==</c> (NaN is not equal to itself, <c>-0</c> equals
    /// <c>0</c>), the entries of a map in any order.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    internal static bool AreEqual(Value? expected, Value? actual)
    {
        if (expected is null || actual is null || expected.KindCase != actual.KindCase)
        {
            return expected is null && actual is null;
        }

        return expected.KindCase switch
        {
            Value.KindOneofCase.DoubleValue => expected.DoubleValue == actual.DoubleValue,
            Value.KindOneofCase.ListValue => expected.ListValue.Values.Count == actual.ListValue.Values.Count
                && expected.ListValue.Values.Zip(actual.ListValue.Values).All(pair => AreEqual(pair.First, pair.Second)),
            Value.KindOneofCase.MapValue => SameEntries(expected.MapValue.Entries, actual.MapValue.Entries),
            _ => expected.Equals(actual),
        };
    }

    private static Judgement Values(Value? expected, CaseOutcome outcome) =>
        outcome.Value is not null && AreEqual(expected, outcome.Value)
            ? Judgement.Pass
            : Judgement.Fail($"expected {ExprText.Of(expected)}, got {Got(outcome)}");

    // Types are compared as cel.expr.Type messages, as diffType of the cel-go runner does; a type has no double field.
    private static Judgement Types(Type? expected, Type? actual) =>
        Equals(expected, actual)
            ? Judgement.Pass
            : Judgement.Fail($"expected type {ExprText.Of(expected)}, got type {ExprText.Of(actual)}");

    private static string Got(CaseOutcome outcome) =>
        outcome.Value is not null ? ExprText.Of(outcome.Value)
        : outcome.Errors.Count > 0 ? "error " + Errors(outcome.Errors)
        : $"no value after the stage {StageName(outcome.Stage)}";

    // Each actual entry matches at most one expected entry.
    private static bool SameEntries(RepeatedField<MapValue.Types.Entry> expected, RepeatedField<MapValue.Types.Entry> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        List<MapValue.Types.Entry> unmatched = [.. actual];
        foreach (MapValue.Types.Entry entry in expected)
        {
            int index = unmatched.FindIndex(candidate => AreEqual(entry.Key, candidate.Key) && AreEqual(entry.Value, candidate.Value));
            if (index < 0)
            {
                return false;
            }

            unmatched.RemoveAt(index);
        }

        return true;
    }

    private static string Errors(IReadOnlyList<string> errors) => string.Join(", ", errors.Select(ExprText.Quote));

    private static string BindingKind(ExprValue binding) =>
        binding.KindCase == ExprValue.KindOneofCase.Error ? "an error" : binding.KindCase == ExprValue.KindOneofCase.Unknown ? "an unknown set" : "empty";

    private static string StageName(CaseStage stage) => stage switch
    {
        CaseStage.Parse => "parse",
        CaseStage.Environment => "environment",
        CaseStage.Check => "check",
        _ => "evaluation",
    };
}
