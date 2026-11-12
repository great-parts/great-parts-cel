using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Run;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The steps of eng/run and the steps of its commands (docs/design/eng-test.md). A step has no time limit, so there is no test of one.
// The steps that start a program use `sh`, which the image of eng/ has.
public sealed class StepsTests
{
    private static readonly TestProject Published = new("Lib.Tests", "tests/Lib.Tests", ["net10.0", "net11.0"], PublishAot: true);
    private static readonly TestProject JitOnly = new("Lib.Eng", "eng/tests", ["net10.0"], PublishAot: false);

    [Fact]
    public void Step_ThatEndsWithTheExitCodeZero_IsNotAFailure()
    {
        StageResult result = StageRunner.Run(Stage.Of(Shell("exit 0")));

        Assert.Empty(result.Failed);
    }

    [Fact]
    public void Step_ThatFails_IsNamedWithItsExitCode()
    {
        StageResult result = StageRunner.Run(Stage.Of(Shell("exit 3")));

        Assert.Equal("sh -c exit 3 (exit code 3)", Assert.Single(result.Failed));
    }

    [Fact]
    public void Step_ThatIgnoresItsExitCodeAndFails_IsNotAFailure()
    {
        StageResult result = StageRunner.Run(Stage.Of(Shell("exit 2") with { IgnoresExitCode = true }));

        Assert.Empty(result.Failed);
    }

    [Fact]
    public void Step_ThatIgnoresItsExitCodeAndDoesNotStart_StillFails()
    {
        Step step = new("/this/program/does/not/exist", [], IgnoresExitCode: true);

        StageResult result = StageRunner.Run(Stage.Of(step));

        Assert.StartsWith("/this/program/does/not/exist (", Assert.Single(result.Failed), StringComparison.Ordinal);
    }

    [Fact]
    public void StepInThisProcess_ThatFails_IsNamedWithItsReason()
    {
        StageResult result = StageRunner.Run(Stage.Of(Step.InProcess("min-sdk: build", () => "build")));

        Assert.Equal("min-sdk: build (build)", Assert.Single(result.Failed));
    }

    [Fact]
    public void Run_StepThatPassesItsExitCodeOn_IsTheExitCodeOfTheCommand()
    {
        using StringWriter summary = new();

        int exitCode = Program.Run("steps-test", [Stage.Of(Shell("exit 2") with { PassesExitCode = true })], summary);

        Assert.Equal(2, exitCode);
        Assert.Matches(@"^steps-test: failed: sh -c exit 2 \(exit code 2\); head \S+; tree \w+; [\d.]+ s; exit 2\r?\n$", summary.ToString());
    }

    [Fact]
    public void Run_StepsThatPass_AreASummaryWithoutAWait()
    {
        using StringWriter summary = new();

        int exitCode = Program.Run("steps-test", [Stage.Of(Shell("exit 0"))], summary);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"^steps-test: passed; head \S+; tree \w+; [\d.]+ s; exit 0\r?\n$", summary.ToString());
    }

    [Fact]
    public void TestSteps_EveryLaneHasTheStepsOfItsKind()
    {
        IReadOnlyList<Stage> stages = Commands.TestStages([Published, JitOnly]);

        string[] kinds = [.. stages.SelectMany(stage => stage.Lanes).SelectMany(lane => lane).Select(Kind)];

        // Stage 1: the build. Stage 2: the lane of the JIT run — three modules and the checks — and two lanes that delete the
        // unfinished outputs of an earlier publish (in this process), publish and run, one for each target of the published
        // project. Stage 3: the test count of each published target.
        string[] expected =
        [
            "build",
            .. Enumerable.Repeat("jit", 4),
            "clean",
            "publish",
            "aot run",
            "clean",
            "publish",
            "aot run",
            .. Enumerable.Repeat("test-count", 2),
        ];
        Assert.Equal(expected, kinds);
    }

    [Fact]
    public void DocsSteps_OnlyLychee_IgnoresItsExitCode()
    {
        List<Stage> stages = Commands.DocsStages();

        Step[] steps = [.. stages.SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        Assert.Equal(
            [
                "dotnet build eng/checks -c Release",
                "dotnet build src/Great.Parts.Cel -c Release",
                "dotnet run --project eng/checks -c Release --no-build -- examples",
                "dotnet run --project eng/checks -c Release --no-build -- error-codes --page docs/error-codes.md",
                "docs: create artifacts/docs",
                "lychee --offline --include-fragments --format json --output artifacts/docs/lychee.json --config lychee.toml README.md docs src/**/*.cs",
                "dotnet run --project eng/checks -c Release --no-build -- links --report artifacts/docs/lychee.json",
            ],
            steps.Select(step => step.CommandLine));
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
    public void MutateSteps_AreTheRestoreTheBuildTheToolAndTheCheck()
    {
        List<Stage> stages = Commands.MutateStages(new MutateOptions("stryker-config.json", null), null);

        Step[] steps = [.. stages.SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        Assert.Equal(["tool", "build", "stryker", "run"], steps.Select(step => step.Arguments[0]));
        Assert.Equal([true, true, false, false], steps.Select(step => step.StopsOnFailure));
    }

    [Fact]
    public void DifftestSteps_TheRunGetsTheArgumentsAndPassesItsExitCodeOn()
    {
        IReadOnlyList<Stage>? stages = Commands.Find("difftest", ["--budget", "10m"]);

        Step[] steps = [.. stages!.SelectMany(stage => stage.Lanes).SelectMany(lane => lane)];

        Assert.True(steps[0].StopsOnFailure);
        Assert.False(steps[0].PassesExitCode);
        Assert.Equal(["--budget", "10m"], steps[1].Arguments.TakeLast(2));
        Assert.True(steps[1].PassesExitCode);
    }

    private static Step Shell(string script) => new("sh", ["-c", script]);

    // The kind of a step for the list of the steps of test.
    private static string Kind(Step step) => step.Action is not null ? "clean" : step.Arguments switch
    {
        ["build", ..] => "build",
        ["publish", ..] => "publish",
        [.., "test-count", _, _, _, _, _, _] => "test-count",
        ["run", "--project", "eng/checks", ..] => "jit",
        _ => step.FileName.EndsWith("Tests", StringComparison.Ordinal) ? "aot run" : "jit",
    };
}
