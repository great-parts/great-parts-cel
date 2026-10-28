using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

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

    // Until the library has the internal entry points, the cases disable_check and check_only fail without a call into the
    // library: a call would end in an exception of the library, not in this reason.
    [Fact]
    public void CasesWithoutCheckOrOnlyChecked_FailWithTheReasonThatTheInternalEntryPointIsNotAvailable()
    {
        IReadOnlyList<ConformanceCase> cases = CelSpecSuite.ReadCases();
        ConformanceCase[] internalCases = [.. cases.Where(testCase => testCase.Check != CheckMode.Check)];
        LibraryEvaluator evaluator = new();

        Assert.Equal(141, cases.Count(testCase => testCase.Check == CheckMode.WithoutCheck));
        Assert.Contains(internalCases, testCase => testCase.Check == CheckMode.CheckOnly);
        Assert.All(internalCases, testCase =>
        {
            CaseOutcome outcome = evaluator.Evaluate(testCase);
            Assert.Null(outcome.Exception);
            Assert.Equal(["internal entry point is not available yet"], outcome.Errors);
            Assert.Equal(Judgement.Fail("parse failed: 'internal entry point is not available yet'"), Rule.Judge(testCase, outcome));
        });
    }

    [Fact]
    public void Source_IsTheShortShaOfThePinnedCommit()
    {
        Assert.Equal("8bef98f1caadc2036fe63d5516cf1df65dc4b741", CelSpecSuite.Commit);
        Assert.Equal("cel-spec 8bef98f", CelSpecSuite.Source);
    }
}
