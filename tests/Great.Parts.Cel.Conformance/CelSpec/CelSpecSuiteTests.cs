using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.CelSpec;

// The cel-spec suite on the library (docs/design/conformance.md, "Core and suites", "Calls into the library").
public sealed class CelSpecSuiteTests
{
    [Fact]
    public void AllCases_PassOrAreSkipped() =>
        SuiteAssert.PassOrAreSkipped(CelSpecSuite.Run(new LibraryEvaluator(), RunCell.Current), CelSpecSuite.Source);

    [Fact]
    public void Run_RunsThe2592CasesOfThe32TestFiles()
    {
        // unknowns.textproto has no cases.
        Assert.Equal(32, EmbeddedData.TextFiles(EmbeddedData.CelSpecTestData).Count);
        Assert.Equal(2592, CelSpecSuite.Run(new LibraryEvaluator(), RunCell.Current).Total);
    }

    // A disable_check case is evaluated through the internal entry point without type checking, with the result type dyn: the
    // value of a list whose elements have different types, and the error of a name without a value.
    [Fact]
    public void CasesWithoutCheck_AreEvaluatedWithoutTypeCheckWithTheResultTypeDyn()
    {
        IReadOnlyList<ConformanceCase> cases = CelSpecSuite.ReadCases();
        LibraryEvaluator evaluator = new();

        CaseOutcome list = evaluator.Evaluate(Case(cases, "plumbing/check_inputs/skip_check"));
        CaseOutcome unbound = evaluator.Evaluate(Case(cases, "basic/variables/self_eval_unbound_lookup"));

        Assert.Equal(141, cases.Count(testCase => testCase.Check == CheckMode.WithoutCheck));
        Assert.Equal((CaseStage.Evaluate, TypeConversion.Dyn), (list.Stage, list.ResultType));
        Assert.Equal(
            new Value { ListValue = new ListValue { Values = { new Value { Int64Value = 17 }, new Value { StringValue = "pancakes" } } } },
            list.Value);
        Assert.Equal((CaseStage.Evaluate, TypeConversion.Dyn), (unbound.Stage, unbound.ResultType));
        Assert.Equal(["no value is bound to variable 'x'"], unbound.Errors);
    }

    // A check_only case is parsed and checked through the internal entry points, and nothing is evaluated.
    [Fact]
    public void CasesOnlyChecked_AreCheckedAndNotEvaluated()
    {
        ConformanceCase testCase = Case(CelSpecSuite.ReadCases(), "type_deductions/flexible_type_parameter_assignment/list_parameter");

        CaseOutcome outcome = new LibraryEvaluator().Evaluate(testCase);

        Assert.Equal(CheckMode.CheckOnly, testCase.Check);
        Assert.Null(outcome.Exception);
        Assert.Equal(CaseStage.Check, outcome.Stage);
        Assert.Empty(outcome.Errors);
        Assert.Equal(Type.TypeKindOneofCase.ListType, outcome.ResultType?.TypeKindCase);
        Assert.Null(outcome.Value);
    }

    [Fact]
    public void Source_IsTheShortShaOfThePinnedCommit()
    {
        Assert.Equal("8bef98f1caadc2036fe63d5516cf1df65dc4b741", CelSpecSuite.Commit);
        Assert.Equal("cel-spec 8bef98f", CelSpecSuite.Source);
    }

    private static ConformanceCase Case(IReadOnlyList<ConformanceCase> cases, string name) =>
        cases.Single(testCase => testCase.Name == name);
}
