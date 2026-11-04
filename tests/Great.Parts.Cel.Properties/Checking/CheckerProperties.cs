using System;
using System.Globalization;
using CsCheck;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Properties.Syntax;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Properties.Checking;

// Properties of the checker (docs/design/tests.md, "Properties of the parser and the checker"), in the environment of the
// standard library with the variables of the generator: any parsed text checks into a type or into errors, and a well-typed
// expression of the generator checks without errors into a type assignable to its target.
public sealed class CheckerProperties
{
    private const ulong NeverThrowsSeed = 20261106;
    private const int NeverThrowsCases = 1000;
    private const ulong WellTypedSeed = 20261107;
    private const int WellTypedCases = 1000;

    // Grammatical texts of the parser properties and texts of the generator with one node of another type.
    private static readonly Gen<string> NeverThrowsCase = Gen.Frequency(
        (1, Grammar.Expression),
        (2, TypedExpressions.Damaged.Select(testCase => testCase.Text)));

    [Fact]
    [Trait("Requirement", "R-2")]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-9")]
    public void NeverThrows() =>
        PropertyLoop.Check(nameof(NeverThrows), NeverThrowsCase, NeverThrowsSeed, NeverThrowsCases, NeverThrowsViolation, text => text);

    [Fact]
    [Trait("Requirement", "F-1")]
    public void WellTyped() =>
        PropertyLoop.Check(nameof(WellTyped), TypedExpressions.WellTyped, WellTypedSeed, WellTypedCases, WellTypedViolation, Shown);

    // Check returns for a text that parses; no exception escapes, no error is InternalError, every error lies within the text.
    private static string? NeverThrowsViolation(string text)
    {
        ParseResult parsed = TypedExpressions.Environment.Parse(text);
        if (!parsed.IsSuccess)
        {
            return null;
        }

        CheckResult result;
        try
        {
            result = TypedExpressions.Environment.Check(parsed.Expression);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{exception.GetType().Name} escaped: {exception.Message}";
        }

        foreach (CelError error in result.Errors)
        {
            if (error.Code == CelErrorCode.InternalError)
            {
                return $"InternalError at {error.Location.Offset}: {error.Exception}";
            }

            if (error.Location.Offset < 0 || error.Location.Offset > text.Length)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{error.Code} at {error.Location.Offset} outside the text of {text.Length}");
            }
        }

        return null;
    }

    // A text of the generator parses and checks without errors, and its type is assignable to its target type.
    private static string? WellTypedViolation(TypedCase testCase)
    {
        ParseResult parsed = TypedExpressions.Environment.Parse(testCase.Text);
        if (!parsed.IsSuccess)
        {
            return $"parse error: {parsed.Errors[0].Message}";
        }

        CheckResult result = TypedExpressions.Environment.Check(parsed.Expression);
        if (!result.IsSuccess)
        {
            return $"check error {result.Errors[0].Code} at {result.Errors[0].Location.Offset}: {result.Errors[0].Message}";
        }

        CelType type = result.Expression.ResultType;
        return AssignabilityRules.IsAssignable(type, testCase.Target, new TypeSubstitution(), [])
            ? null
            : $"type {type} is not assignable to the target {testCase.Target}";
    }

    private static string Shown(TypedCase testCase) => $"{testCase.Text} : {testCase.Target}";
}
