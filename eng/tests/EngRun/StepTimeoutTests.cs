using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using Great.Parts.Cel.Eng.Run;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The time limits of the steps of eng/run (docs/design/eng-test.md, "Time limits"). The steps are `sh` and `sleep`, which the image of
// eng/ has. A limit of one second is enough for a test: the step that is killed would otherwise run for a minute.
public sealed class StepTimeoutTests
{
    private const string LongSleep = "sleep 60";
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public void Step_ThatRunsLongerThanItsLimit_IsKilledAndNamedWithTheLimit()
    {
        Stopwatch clock = Stopwatch.StartNew();

        StageResult result = StageRunner.Run(Stage.Of(Shell(LongSleep, OneSecond)));

        Assert.Equal("timeout after 1 s in step sh -c sleep 60", Assert.Single(result.Failed));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), "the step was killed when its limit was over, not when it ended");
    }

    [Fact]
    public void Step_ThatRunsLongerThanItsLimit_KillsTheProcessesItStarted()
    {
        using TempDirectory directory = new();
        string pidFile = Path.Combine(directory.Path, "pid");

        StageResult result = StageRunner.Run(Stage.Of(Shell($"{LongSleep} & echo $! > '{pidFile}'; wait", TimeSpan.FromSeconds(2))));

        Assert.Single(result.Failed);
        int child = int.Parse(File.ReadAllText(pidFile).Trim(), CultureInfo.InvariantCulture);
        Assert.True(SpinWait.SpinUntil(() => !IsRunning(child), TimeSpan.FromSeconds(10)), "the child of the step is still running");
    }

    [Fact]
    public void Step_ThatEndsInTime_IsNotAFailure()
    {
        StageResult result = StageRunner.Run(Stage.Of(Shell("exit 0", TimeSpan.FromSeconds(30))));

        Assert.Empty(result.Failed);
    }

    [Fact]
    public void Step_ThatFailsInTime_IsNamedWithItsExitCode()
    {
        StageResult result = StageRunner.Run(Stage.Of(Shell("exit 3", TimeSpan.FromSeconds(30))));

        Assert.Equal("sh -c exit 3 (exit code 3)", Assert.Single(result.Failed));
    }

    [Fact]
    public void StepInThisProcess_ThatFailsWithTheTimeoutOfItsChild_IsNamedWithTheLimit()
    {
        StageResult result = StageRunner.Run(Stage.Of(Step.InProcess("min-sdk: build", () => Timeouts.Reason(20))));

        Assert.Equal("timeout after 20 s in step min-sdk: build", Assert.Single(result.Failed));
    }

    [Fact]
    public void Extend_LineWithTheMutantCount_ReplacesTheLimit()
    {
        Step step = new("sh", ["-c", $"echo '5     total mutants will be tested'; {LongSleep}"], TimeSpan.FromSeconds(50), Extend: ShortAfterTheCount);

        StageResult result = StageRunner.Run(Stage.Of(step));

        // The new limit is the time until the line plus 1.5 s: far below the first limit of 50 s.
        Assert.Matches(@"^timeout after [2-9] s in step sh -c ", Assert.Single(result.Failed));
    }

    [Fact]
    public void Extend_WithoutTheLine_KeepsTheFirstLimit()
    {
        Step step = new("sh", ["-c", $"echo hello; {LongSleep}"], OneSecond, Extend: Timeouts.MutateChecking);

        StageResult result = StageRunner.Run(Stage.Of(step));

        Assert.StartsWith("timeout after 1 s in step ", Assert.Single(result.Failed), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_StepThatPassesItsExitCodeOnAndRunsOutOfTime_IsExitCodeOneWithTheLimitInTheSummary()
    {
        using StringWriter summary = new();
        Step step = Shell(LongSleep, OneSecond) with { PassesExitCode = true };

        int exitCode = Program.Run("timeout-test", [Stage.Of(step)], summary);

        Assert.Equal(1, exitCode);
        Assert.Matches(@"^timeout-test: failed: timeout after 1 s in step sh -c sleep 60; head \S+; tree \w+; [\d.]+ s; exit 1\r?\n$", summary.ToString());
    }

    // The limit of Timeouts.MutateChecking with a small time for the end of the tool, so the test does not wait for half a minute.
    private static TimeSpan? ShortAfterTheCount(string line, TimeSpan elapsed) =>
        Timeouts.MutateChecking(line, elapsed) is { } limit ? limit - Timeouts.MutateEnd : null;

    private static Step Shell(string script, TimeSpan timeout) => new("sh", ["-c", script], timeout);

    // A process that is gone, or that ended and waits to be collected by its parent, is not running.
    private static bool IsRunning(int processId)
    {
        try
        {
            string stat = File.ReadAllText($"/proc/{processId.ToString(CultureInfo.InvariantCulture)}/stat");
            return stat[(stat.LastIndexOf(')') + 2)..].StartsWith('Z') is false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
