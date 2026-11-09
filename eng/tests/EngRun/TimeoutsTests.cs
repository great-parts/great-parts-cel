using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Run;
using Xunit;
using DifftestOptions = Great.Parts.Cel.Eng.Difftest.RunOptions;
using DifftestProgram = Great.Parts.Cel.Eng.Difftest.Program;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The limits of the steps of the commands of eng/run, and the limits that depend on the arguments or on the output of a step
// (docs/design/eng-test.md, "Time limits").
public sealed class TimeoutsTests
{
    private static readonly TestProject Published = new("Lib.Tests", "tests/Lib.Tests", ["net10.0", "net11.0"], PublishAot: true);
    private static readonly TestProject JitOnly = new("Lib.Eng", "eng/tests", ["net10.0"], PublishAot: false);

    [Fact]
    public void TestSteps_EveryStepOfEveryLaneHasTheLimitOfItsKind()
    {
        IReadOnlyList<Stage> stages = Commands.TestStages([Published, JitOnly]);

        string[] limits = [.. stages.SelectMany(stage => stage.Lanes).SelectMany(lane => lane).Select(step => $"{Kind(step)}={step.Timeout?.TotalSeconds}")];

        // Stage 1: the build. Stage 2: the lane of the JIT run — three modules and the checks — and two lanes that delete the
        // unfinished outputs of an earlier publish (in this process, no limit), publish and run, one for each target of the published
        // project. Stage 3: the test count of each published target.
        string[] expected =
        [
            "build=139",
            .. Enumerable.Repeat("jit=120", 4),
            "clean=",
            "publish=303",
            "aot run=120",
            "clean=",
            "publish=303",
            "aot run=120",
            .. Enumerable.Repeat("test-count=12", 2),
        ];
        Assert.Equal(expected, limits);
    }

    [Fact]
    public void DocsSteps_EveryStepHasTheLimitOfItsKindAndOnlyLychee_IgnoresItsExitCode()
    {
        List<Stage> stages = Commands.DocsStages();

        Step[] steps = [.. stages.SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        // The build of eng/checks and of the library: 139 s; the examples: 46 s; the error codes page: 12 s; the directory of the
        // report: in this process, no limit; lychee: 2 s; the links of the report: 12 s. Literals, not the constants of Timeouts,
        // so a changed constant fails the test.
        Assert.Equal(
            [
                "dotnet build eng/checks -c Release=139",
                "dotnet build src/Great.Parts.Cel -c Release=139",
                "dotnet run --project eng/checks -c Release --no-build -- examples=46",
                "dotnet run --project eng/checks -c Release --no-build -- error-codes --page docs/error-codes.md=12",
                "docs: create artifacts/docs=",
                "lychee --offline --include-fragments --format json --output artifacts/docs/lychee.json --config lychee.toml README.md docs src/**/*.cs=3",
                "dotnet run --project eng/checks -c Release --no-build -- links --report artifacts/docs/lychee.json=12",
            ],
            steps.Select(step => $"{step.CommandLine}={step.Timeout?.TotalSeconds}"));
        Assert.Equal(["lychee"], steps.Where(step => step.IgnoresExitCode).Select(step => step.FileName));
    }

    [Fact]
    public void DocsSteps_FailedBuildOfTheLibrary_StopsItsLaneButNotTheCommand()
    {
        List<Stage> stages = Commands.DocsStages();

        // The build of eng/checks stops the command; the lane of the examples is a stage that does not, and its first step stops the lane.
        Assert.True(stages[0].StopsOnFailure);
        Assert.False(stages[1].StopsOnFailure);
        Assert.True(stages[1].Lanes[0][0].StopsOnFailure);
        Assert.False(stages[2].StopsOnFailure);
    }

    [Fact]
    public void MutateSteps_EveryStepHasTheLimitOfItsKindAndOnlyTheRunOfTheToolExtendsIt()
    {
        List<Stage> stages = Commands.MutateStages(new MutateOptions("stryker-config.json", null), null);

        Step[] steps = [.. stages.SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        // The restore of the tool and the preparation of the tool: 241 s; the build of eng/checks: 139 s; the reading of the
        // report by eng/checks mutation: 12 s. Literals, not the constants of Timeouts, so a changed constant fails the test.
        Assert.Equal(
            ["tool=241", "build=139", "stryker=241", "run=12"],
            steps.Select(step => $"{step.Arguments[0]}={step.Timeout?.TotalSeconds}"));
        Assert.Equal(["stryker"], steps.Where(step => step.Extend is not null).Select(step => step.Arguments[0]));
    }

    [Fact]
    public void DifftestSteps_TheRunHasTheLimitOfItsBudgetAndPassesItsExitCodeOn()
    {
        IReadOnlyList<Stage>? stages = Commands.Find("difftest", ["--budget", "10m"]);

        Step[] steps = [.. stages!.SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        Assert.Equal(Timeouts.Build, steps[0].Timeout);
        Assert.Equal(TimeSpan.FromSeconds(756), steps[1].Timeout);
        Assert.True(steps[1].PassesExitCode);
    }

    [Theory]
    [InlineData("30s")]
    [InlineData("5m")]
    [InlineData("2h")]
    public static void Difftest_BudgetOfTheOptions_IsTheBudgetThatEngDifftestReads(string budget)
    {
        bool valid = DifftestProgram.TryParse(["--budget", budget], DateTimeOffset.UnixEpoch, out DifftestOptions? options, out _);

        Assert.True(valid);
        Assert.Equal(Timeouts.Difftest(options!.Budget), Timeouts.Difftest(["--seed", "7", "--budget", budget]));
    }

    [Theory]
    [InlineData("0m")]
    [InlineData("5")]
    [InlineData("m")]
    [InlineData("-5m")]
    [InlineData("1.5h")]
    [InlineData("5d")]
    public static void Difftest_BudgetThatEngDifftestRefuses_GivesTheDefaultBudget(string budget)
    {
        Assert.False(DifftestProgram.TryParse(["--budget", budget], DateTimeOffset.UnixEpoch, out _, out _));

        Assert.Equal(Timeouts.Difftest(DifftestProgram.DefaultBudget), Timeouts.Difftest(["--budget", budget]));
    }

    [Fact]
    public void Difftest_WithoutBudgetAndWithTheOptionLast_IsTheDefaultBudgetTimesOnePointTwoAndThirtySixSeconds()
    {
        Assert.Equal(DifftestProgram.DefaultBudget, Timeouts.DifftestDefaultBudget);
        Assert.Equal(TimeSpan.FromSeconds(396), Timeouts.Difftest([]));
        Assert.Equal(TimeSpan.FromSeconds(396), Timeouts.Difftest(["--numbers-only", "--budget"]));
        Assert.Equal(TimeSpan.FromSeconds(8676), Timeouts.Difftest(["--budget", "2h"]));
    }

    [Fact]
    public void MutateChecking_LineWithTheMutantCount_IsTheTimeSoFarPlusThreeTenthsOfASecondPerMutantPlusThirtySeconds()
    {
        TimeSpan? limit = Timeouts.MutateChecking("[21:05:10 INF] 812   total mutants will be tested", TimeSpan.FromSeconds(118));

        Assert.Equal(TimeSpan.FromSeconds(118) + TimeSpan.FromMilliseconds(243_600) + TimeSpan.FromSeconds(30), limit);
    }

    [Theory]
    [InlineData("812 mutants found")]
    [InlineData("[19:18:44 INF] 30    mutants will be tested because: Mutant changed compared to target commit")]
    [InlineData("total mutants will be tested")]
    [InlineData("x total mutants will be tested")]
    [InlineData("")]
    public static void MutateChecking_OtherLine_GivesNoLimit(string line)
    {
        Assert.Null(Timeouts.MutateChecking(line, TimeSpan.FromSeconds(10)));
    }

    // The kind of a step for the table of limits.
    private static string Kind(Step step) => step.Action is not null ? "clean" : step.Arguments switch
    {
        ["build", ..] => "build",
        ["publish", ..] => "publish",
        [.., "test-count", _, _, _, _, _, _] => "test-count",
        ["run", "--project", "eng/checks", ..] => "jit",
        _ => step.FileName.EndsWith("Tests", StringComparison.Ordinal) ? "aot run" : "jit",
    };
}
