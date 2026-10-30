using System;
using System.Collections.Generic;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;
using Xunit;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The synthetic suite (docs/design/conformance.md, "Synthetic suite"): synthetic.textproto, run with a scripted evaluator in a
// fixed cell. The case 'synthetic/failing/wrong_expectation' expects 1 + 1 to be 3; the two cases of 'passing' pass. In the
// skip lists the first entry is on line 5.
public sealed class SyntheticSuiteTests
{
    private const string SuitePath = "tests/Great.Parts.Cel.Conformance/Synthetic/synthetic.textproto";
    private const string ListPath = "tests/Great.Parts.Cel.Conformance/Synthetic/skips.md";
    private const string Commit = "8bef98f1caadc2036fe63d5516cf1df65dc4b741";
    private const string Failing = "| synthetic/failing/wrong_expectation | * * * | Expects 1 + 1 to be 3. | | |";

    private const string ListHead = """
        # Skip list

        | Case | Scope | Reason | Reference | Issue |
        |---|---|---|---|---|
        """;

    private static readonly RunCell JitLinux = new("jit", "linux-x64", "net11.0");

    [Fact]
    public void WrongExpectation_IsEng801AtTheTestTokenOfTheCase()
    {
        SuiteResult result = Run(JitLinux);

        FindingAssert.Single(
            result.Findings,
            EngCodes.Eng801,
            SuitePath,
            12,
            3,
            "case 'synthetic/failing/wrong_expectation' failed: expected int 3, got int 2 (jit, linux-x64, net11.0)");
    }

    [Fact]
    public void FailingCaseOnTheList_HasNoFindingAndCountsAsNotPassed()
    {
        SuiteResult result = Run(JitLinux, Failing);

        Assert.Empty(result.Findings);
        Assert.Equal("synthetic: 2/3 passed, 2026-11-20 (jit, linux-x64, net11.0)", result.Summary("synthetic", new DateOnly(2026, 11, 20)));
    }

    [Fact]
    public void EntryForAPassingCase_IsEng802AtTheEntryLine()
    {
        SuiteResult result = Run(JitLinux, Failing, "| synthetic/passing/int | * * * | Not implemented yet. | | |");

        FindingAssert.Single(
            result.Findings,
            EngCodes.Eng802,
            ListPath,
            6,
            1,
            "case 'synthetic/passing/int' now passes; remove it from the skip list");
    }

    [Fact]
    public void EntryForAMissingCase_IsEng804AtTheEntryLine()
    {
        SuiteResult result = Run(JitLinux, Failing, "| synthetic/passing/missing | jit * * | Not implemented yet. | | |");

        FindingAssert.Single(
            result.Findings,
            EngCodes.Eng804,
            ListPath,
            6,
            1,
            "case 'synthetic/passing/missing' is not in the suite; remove it from the skip list");
    }

    [Fact]
    public void InvalidEntry_IsEng800AtTheEntryLine()
    {
        SuiteResult result = Run(JitLinux, Failing, "| synthetic/passing/int | jit linux * | Not implemented yet. | | |");

        FindingAssert.Single(
            result.Findings,
            EngCodes.Eng800,
            ListPath,
            6,
            1,
            "entry 'synthetic/passing/int': the scope has the unknown value 'linux'; a part is linux-x64, macos, a list of them or *");
    }

    [Fact]
    public void FailureOutsideTheScopeOfItsEntry_IsEng801()
    {
        SuiteResult result = Run(JitLinux, "| synthetic/failing/wrong_expectation | native-aot * * | Fails under Native AOT. | | |");

        FindingAssert.Single(
            result.Findings,
            EngCodes.Eng801,
            SuitePath,
            12,
            3,
            "case 'synthetic/failing/wrong_expectation' failed: expected int 3, got int 2 (jit, linux-x64, net11.0)");
    }

    [Fact]
    public void PassOutsideTheScopeOfItsEntry_HasNoFinding()
    {
        SuiteResult result = Run(JitLinux, Failing, "| synthetic/passing/no_matcher | native-aot linux-x64 * | Fails under Native AOT. | | |");

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.Passed);
    }

    [Fact]
    public void PassInsideTheScopeOfItsEntry_IsEng802()
    {
        RunCell nativeAotLinux = new("native-aot", "linux-x64", "net10.0");

        SuiteResult result = Run(nativeAotLinux, Failing, "| synthetic/passing/no_matcher | native-aot linux-x64 * | Fails under Native AOT. | | |");

        FindingAssert.Single(result.Findings, EngCodes.Eng802, ListPath, 6, 1, "case 'synthetic/passing/no_matcher' now passes; remove it from the skip list");
    }

    [Fact]
    public void Summary_IsPassedOutOfAllCasesWithTheSourceTheDateAndTheCell()
    {
        SuiteResult result = Run(new RunCell("native-aot", "linux-x64", "net10.0"), Failing);

        Assert.Equal((2, 3), (result.Passed, result.Total));
        Assert.Equal("cel-spec 8bef98f: 2/3 passed, 2027-01-05 (native-aot, linux-x64, net10.0)", result.Summary("cel-spec 8bef98f", new DateOnly(2027, 1, 5)));
    }

    private static SuiteResult Run(RunCell cell, params string[] entries)
    {
        SkipList list = SkipList.ReadCelSpec(ListPath, ListHead + "\n" + string.Join("\n", entries) + "\n", Commit);
        return SuiteRun.Run(CaseReader.ReadEmbedded(SuitePath), list, new ScriptedEvaluator(), cell);
    }

    // Gives each expression of the synthetic suite its value, as a correct implementation would.
    private sealed class ScriptedEvaluator : ICaseEvaluator
    {
        private static readonly Dictionary<string, Value> Values = new(StringComparer.Ordinal)
        {
            ["1 + 1"] = new Value { Int64Value = 2 },
            ["{\"test\": 1}[\"test\"] == 1"] = new Value { BoolValue = true },
        };

        public CaseOutcome Evaluate(ConformanceCase testCase) =>
            CaseOutcome.Evaluated(new Type { Dyn = new() }, Values[testCase.Expression]);
    }
}
