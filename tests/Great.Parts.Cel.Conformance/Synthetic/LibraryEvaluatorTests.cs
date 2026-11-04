using System.Collections.Generic;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The environment of a case (docs/design/conformance.md, "Environment of the cases"). The flag disable_macros: the paths through
// the internal entry points parse without macros, as the cel-go runner clears them; the checked path has no entry point with syntax
// options. Functions of type_env: the case fails with the reason, whatever the mode.
public sealed class LibraryEvaluatorTests
{
    // With the macros, a call of all whose first argument is not a name is a parse error; without them it is a call.
    private const string InvalidMacroCall = "[1].all(1, true)";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CaseWithDisableMacros_IsParsedWithoutMacros(bool checkOnly)
    {
        CheckMode check = checkOnly ? CheckMode.CheckOnly : CheckMode.WithoutCheck;
        CaseOutcome expanded = Evaluate(check, disableMacros: false);
        CaseOutcome call = Evaluate(check, disableMacros: true);

        Assert.Equal(CaseStage.Parse, expanded.Stage);
        Assert.Equal(["invalid call of macro 'all': argument must be a simple name"], expanded.Errors);
        Assert.Null(call.Exception);
        Assert.Equal(checkOnly ? CaseStage.Check : CaseStage.Evaluate, call.Stage);
        Assert.NotEmpty(call.Errors);
    }

    [Fact]
    public void CheckedCase_WithDisableMacros_FailsWithTheReason()
    {
        CaseOutcome outcome = Evaluate(CheckMode.Check, disableMacros: true);

        Assert.Equal(CaseStage.Parse, outcome.Stage);
        Assert.Equal([LibraryEvaluator.DisableMacrosOnCheckedPath], outcome.Errors);
    }

    [Fact]
    public void CaseWithFunctionDeclarations_FailsWithTheReasonInEveryMode()
    {
        Decl function = new()
        {
            Name = "f",
            Function = new Decl.Types.FunctionDecl
            {
                Overloads = { new Decl.Types.FunctionDecl.Types.Overload { OverloadId = "f_int", ResultType = new Type { Dyn = new() } } },
            },
        };

        foreach (CheckMode check in (CheckMode[])[CheckMode.Check, CheckMode.WithoutCheck, CheckMode.CheckOnly])
        {
            CaseOutcome outcome = Evaluate(check, disableMacros: false, "true", [function]);

            Assert.Equal(CaseStage.Environment, outcome.Stage);
            Assert.Equal([LibraryEvaluator.FunctionDeclarationsInALaterPart], outcome.Errors);
            Assert.Null(outcome.Exception);
        }
    }

    private static CaseOutcome Evaluate(CheckMode check, bool disableMacros) => Evaluate(check, disableMacros, InvalidMacroCall, []);

    private static CaseOutcome Evaluate(CheckMode check, bool disableMacros, string expression, IReadOnlyList<Decl> declarations) =>
        new LibraryEvaluator().Evaluate(new ConformanceCase(
            "f/s/t",
            "f.textproto",
            1,
            1,
            expression,
            new CaseEnvironment("", declarations, disableMacros),
            new Dictionary<string, ExprValue>(),
            check,
            new Expectation(ExpectationKind.Value, "none", new Value { BoolValue = true }, null)));
}
