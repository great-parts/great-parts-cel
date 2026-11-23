using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

/// <summary>A generated expression and the variables that are unknown, chosen by the bits of a mask.</summary>
/// <param name="Case">The expression with its bindings.</param>
/// <param name="Mask">The bit <c>i</c> makes the variable <c>i</c> of <see cref="TypedExpressions.Variables"/> unknown.</param>
internal sealed record SplitCase(TypedCase Case, uint Mask)
{
    /// <summary>The names of the unknown variables.</summary>
    internal IEnumerable<string> Unknown => TypedExpressions.Variables.Where((_, index) => (Mask >> index & 1) == 1).Select(variable => variable.Name);

    public override string ToString() =>
        $"{Case} with unknown {{{string.Join(", ", Unknown)}}}";
}

// The residual expression of a partial evaluation (docs/design/partial-evaluation.md, "The residual expression"): on the full data it
// gives the result of the original expression, and its text is parsed back into an expression that prints the same. The exception is
// the error of a pruned branch: where the original is an error and the residual a value, because the error was in an operand that the
// residual dropped, it is not a violation. An expression whose result with the unknown data is an error has no residual and is not
// checked.
public sealed class ResidualProperty
{
    private const ulong Seed = 20261125;
    private const int Cases = 700;

    private static readonly CelCompileOptions Partial = new() { PartialEvaluation = true };

    private static readonly Gen<SplitCase> Split = Gen.Select(TypedExpressions.WellTyped, Gen.UInt, (testCase, mask) => new SplitCase(testCase, mask));

    [Fact]
    [Trait("Requirement", "F-22")]
    public void ResidualOnTheFullData_GivesTheResultOfTheOriginal() =>
        PropertyLoop.Check(nameof(ResidualOnTheFullData_GivesTheResultOfTheOriginal), Split, Seed, Cases, Violation, split => split.ToString());

    private static string? Violation(SplitCase split)
    {
        TypedCase testCase = split.Case;
        CelEnvironment environment = TypedExpressions.Environment;
        CelCompileResult<CelValue> partialProgram = environment.Compile<CelValue>(testCase.Text, Partial);
        CelCompileResult<CelValue> plainProgram = environment.Compile<CelValue>(testCase.Text);
        if (!partialProgram.IsSuccess || !plainProgram.IsSuccess)
        {
            return $"compile error {(partialProgram.IsSuccess ? plainProgram : partialProgram).Errors[0].Code}";
        }

        List<CelAttributePattern> patterns = [.. split.Unknown.Select(name => new CelAttributePattern(name))];
        CelPartialEvalResult<CelValue> partial = partialProgram.Program.EvaluatePartial(testCase.Bindings, patterns, null, CancellationToken.None);
        if (!partial.IsSuccess && !partial.IsUnknown)
        {
            return null;
        }

        CelParsedExpr residual = partial.GetResidual();
        return Printed(environment, residual) ?? OnFullData(environment, residual, plainProgram.Program.Evaluate(testCase.Bindings), testCase.Bindings);
    }

    // The residual compiled again and evaluated on the full data against the result of the original.
    private static string? OnFullData(CelEnvironment environment, CelParsedExpr residual, CelEvalResult<CelValue> original, CelBindings bindings)
    {
        CelCompileResult<CelValue> compiled = environment.CompileParsed<CelValue>(residual);
        if (!compiled.IsSuccess)
        {
            // A value written as a literal has a more exact type than the variable it replaced, which an overload that the original
            // resolved when it ran may not accept; the original is an error then.
            return original.IsSuccess ? $"the residual {CelSyntax.Format(residual)} does not compile: {compiled.Errors[0].Code} {compiled.Errors[0].Message}" : null;
        }

        CelEvalResult<CelValue> result = compiled.Program.Evaluate(bindings);
        if (original.IsSuccess && result.IsSuccess)
        {
            return StructuralEquality.Equal(original.Value, result.Value) ? null : $"the residual {CelSyntax.Format(residual)} gives another value";
        }

        if (original.IsSuccess)
        {
            return $"the residual {CelSyntax.Format(residual)} is the error {result.Errors[0].Code}, the original a value";
        }

        // The error of a pruned branch: the original fails where the residual does not.
        return result.IsSuccess || result.Errors[0].Code == original.Errors[0].Code
            ? null
            : $"the residual {CelSyntax.Format(residual)} is the error {result.Errors[0].Code}, the original {original.Errors[0].Code}";
    }

    // The text of the residual is parsed into an expression that prints as the same text.
    private static string? Printed(CelEnvironment environment, CelParsedExpr residual)
    {
        string text = CelSyntax.Format(residual);
        CelParseResult parsed = environment.Parse(text);
        if (!parsed.IsSuccess)
        {
            return string.Create(CultureInfo.InvariantCulture, $"the residual {text} does not parse: {parsed.Errors[0].Message}");
        }

        string again = CelSyntax.Format(parsed.GetParsedExprOrThrow());
        return again == text ? null : $"the residual {text} prints as {again} after parsing";
    }
}
