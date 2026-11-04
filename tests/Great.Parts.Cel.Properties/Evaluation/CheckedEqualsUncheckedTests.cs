using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

// Evaluation with type checking equals evaluation without it (docs/design/tests.md, "Properties of evaluation"): on the
// expressions and bindings of the generator both give a value, equal by the structural comparison, or both an error.
public sealed class CheckedEqualsUncheckedTests
{
    private const ulong Seed = 20261108;
    private const int Cases = 1500;

    [Fact]
    [Trait("Requirement", "+R-6")]
    [Trait("Requirement", "F-1")]
    public void SameResult() =>
        PropertyLoop.Check(nameof(SameResult), TypedExpressions.WellTyped, Seed, Cases, Violation, testCase => testCase.ToString());

    private static string? Violation(TypedCase testCase)
    {
        CelEnvironment environment = TypedExpressions.Environment;
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(testCase.Text);
        if (!compiled.IsSuccess)
        {
            return $"compile error {Code(compiled.Errors[0])}";
        }

        CelEvalResult<CelValue> checkedResult = compiled.Program.Evaluate(testCase.Bindings);
        ParseResult parsed = environment.Parse(testCase.Text);
        CelEvalResult<CelValue> uncheckedResult = environment.EvaluateWithoutTypeCheck(parsed.Expression!, testCase.Bindings);

        if (checkedResult.IsSuccess && uncheckedResult.IsSuccess)
        {
            return StructuralEquality.Equal(checkedResult.Value, uncheckedResult.Value)
                ? null
                : $"checked {Shown(checkedResult.Value)}, unchecked {Shown(uncheckedResult.Value)}";
        }

        if (!checkedResult.IsSuccess && !uncheckedResult.IsSuccess)
        {
            return null;
        }

        return checkedResult.IsSuccess
            ? $"checked {Shown(checkedResult.Value)}, unchecked error {Code(uncheckedResult.Errors[0])}"
            : $"checked error {Code(checkedResult.Errors[0])}, unchecked {Shown(uncheckedResult.Value)}";
    }

    private static string Code(CelError error) => $"{error.Code}: {error.Message}";

    private static string Shown(CelValue value)
    {
        var text = new StringBuilder();
        TypedExpressions.Print(text, value);
        return text.ToString();
    }
}
