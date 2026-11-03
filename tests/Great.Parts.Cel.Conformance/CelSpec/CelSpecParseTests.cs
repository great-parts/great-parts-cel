using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Conformance.CelSpec;

// The parse of every expression of the cel-spec suite (docs/design/conformance.md, "Parse test of the cel-spec suite"): with the
// syntax options of the cel-go runner, every expression parses, and printing its tree and parsing the text again gives a tree
// without a difference; the cases of BlockTestMacroCases fail as described there. The skip list does not apply.
public sealed class CelSpecParseTests
{
    private const int CaseCount = 2592;

    private const string MacroMessageStart = "invalid call of macro '";
    private const string MacroMessageEnd = "': argument must be a simple name";

    // The cel-go runner gives all files one setup with optional types and clears the macros for disable_macros.
    private static readonly SyntaxOptions Syntax = SyntaxOptions.Standard with { OptionalSyntax = true };
    private static readonly SyntaxOptions WithoutMacros = Syntax with { Macros = MacroTable.Empty };

    // Test macros of the internal set block stay calls until the set joins the options (docs/design/conformance.md, "Parse test of the cel-spec suite").
    private static readonly string[] BlockTestMacroCases =
    [
        "block_ext/basic/multiple_macros_1",
        "block_ext/basic/multiple_macros_2",
        "block_ext/basic/multiple_macros_3",
        "block_ext/basic/nested_macros_1",
        "block_ext/basic/nested_macros_2",
        "block_ext/basic/adjacent_macros",
        "block_ext/basic/macro_shadowed_variable_1",
        "block_ext/basic/macro_shadowed_variable_2",
    ];

    [Fact]
    [Trait("Requirement", "F-1")]
    public void EveryExpression_ParsesAndPrintsBack()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        IReadOnlyList<ConformanceCase> cases = CelSpecSuite.ReadCases();
        HashSet<string> names = [.. cases.Select(testCase => testCase.Name)];

        string[] failures =
        [
            .. cases.Select(testCase => Failure(environment, testCase)).OfType<string>(),
            .. BlockTestMacroCases.Where(name => !names.Contains(name)).Select(name => $"{name}: no such case: remove it from the list"),
        ];

        Assert.True(failures.Length == 0, string.Join('\n', failures));
    }

    // A lost case shows up here; the number changes when the pinned commit moves.
    [Fact]
    public void CaseCount_IsThatOfThePinnedCommit()
    {
        Assert.Equal(CaseCount, CelSpecSuite.ReadCases().Count);
    }

    // The failure of a case as "<path>(<line>,<column>): <name>: <first error or difference>", or null.
    private static string? Failure(CelEnvironment environment, ConformanceCase testCase)
    {
        SyntaxOptions syntax = testCase.Environment.DisableMacros ? WithoutMacros : Syntax;
        string? reason = BlockTestMacroCases.Contains(testCase.Name, StringComparer.Ordinal)
            ? BlockTestMacroReason(environment, testCase.Expression, syntax)
            : Reason(environment, testCase.Expression, syntax);
        return reason is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{testCase.Path}({testCase.Line},{testCase.Column}): {testCase.Name}: {reason}");
    }

    private static string? Reason(CelEnvironment environment, string expression, SyntaxOptions syntax)
    {
        try
        {
            ParseResult parsed = environment.Parse(expression, syntax);
            if (!parsed.IsSuccess)
            {
                return "parse error " + Format(parsed.Errors[0]);
            }

            string printed = Printer.Print(parsed.Expression);
            ParseResult again = environment.Parse(printed, syntax);
            if (!again.IsSuccess)
            {
                return $"the printed text {ExprText.Quote(printed)} has the parse error {Format(again.Errors[0])}";
            }

            return SyntaxTreeComparer.Difference(parsed.Expression.Root, again.Expression.Root) is { } difference
                ? $"the printed text {ExprText.Quote(printed)} parses into another tree: {difference}"
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    // A case whose iteration variables are calls of the test macros of the set block: every error is InvalidMacroCall at such a call.
    private static string? BlockTestMacroReason(CelEnvironment environment, string expression, SyntaxOptions syntax)
    {
        try
        {
            ParseResult parsed = environment.Parse(expression, syntax);
            if (parsed.IsSuccess)
            {
                return "parses now: remove it from the list";
            }

            if (parsed.Errors.IsEmpty)
            {
                return "expected InvalidMacroCall at a test macro, got no error";
            }

            foreach (CelError error in parsed.Errors)
            {
                if (!IsAtTestMacro(expression, error))
                {
                    return "expected InvalidMacroCall at a test macro, got " + Format(error);
                }
            }

            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }
    }

    private static bool IsAtTestMacro(string expression, CelError error)
    {
        if (error.Code != CelErrorCode.InvalidMacroCall
            || !error.Message.StartsWith(MacroMessageStart, StringComparison.Ordinal)
            || !error.Message.EndsWith(MacroMessageEnd, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> rest = expression.AsSpan(error.Location.Offset);
        return rest.StartsWith("(cel.iterVar(", StringComparison.Ordinal) || rest.StartsWith("(cel.accuVar(", StringComparison.Ordinal);
    }

    private static string Format(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{error.Code} at {error.Location.Offset}: {ExprText.Quote(error.Message)}");
}
