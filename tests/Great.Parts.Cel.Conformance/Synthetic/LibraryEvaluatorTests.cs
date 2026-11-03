using System.Collections.Generic;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The flag disable_macros of a case (docs/design/conformance.md, "Environment of the cases"): the paths through the internal
// entry points parse without macros, as the cel-go runner clears them; the checked path has no entry point with syntax options.
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

    private static CaseOutcome Evaluate(CheckMode check, bool disableMacros) =>
        new LibraryEvaluator().Evaluate(new ConformanceCase(
            "f/s/t",
            "f.textproto",
            1,
            1,
            InvalidMacroCall,
            new CaseEnvironment("", [], disableMacros),
            new Dictionary<string, ExprValue>(),
            check,
            new Expectation(ExpectationKind.Value, "none", new Value { BoolValue = true }, null)));
}
