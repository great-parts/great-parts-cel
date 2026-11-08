using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Run;
using Xunit;
using RunProgram = Great.Parts.Cel.Eng.Run.Program;

namespace Great.Parts.Cel.Eng.Tests.EngRun;

// The wait of eng/run for a quiet host (docs/design/eng-test.md, "Waiting for the host"). The host and the time are fakes: one reading
// per second of the fake clock, and the wait advances the clock when it sleeps, so no test waits for real.
public sealed class QuietWaitTests
{
    private const int Processors = 12;
    private static readonly TimeSpan Minute = TimeSpan.FromSeconds(60);

    [Fact]
    public void QuietHost_IsWaitedForTheWholeQuietTime_AndTheLineSaysHowLong()
    {
        using FakeHost host = new(_ => 0.1);

        host.Wait.Until(Minute);

        Assert.Equal(Minute, host.Wait.Waited);
        Assert.Contains("wait: the host is quiet; waited 60 s; no load of other processes seen", host.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyHost_IsWaitedForUntilItHasBeenQuietForTheWholeQuietTime()
    {
        // 45 s of a foreign build with 2 processors, then quiet.
        using FakeHost host = new(second => second <= 45 ? 2.0 : 0.0);

        host.Wait.Until(Minute);

        // The last busy reading is at 45 s; the host is quiet from 46 s, so the wait ends at 105 s.
        Assert.Equal(TimeSpan.FromSeconds(105), host.Wait.Waited);
        Assert.Contains("waited 105 s; other processes used up to 2.0 processors", host.Text, StringComparison.Ordinal);
        Assert.Contains("wait: other processes use 2.0 processors; waited 30 s", host.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortBusyBreak_RestartsTheQuietTime()
    {
        // Quiet for 50 s, one busy second, then quiet again: 51 s + the full quiet time of 60 s.
        using FakeHost host = new(second => second == 51 ? 3.0 : 0.0);

        host.Wait.Until(Minute);

        Assert.Equal(TimeSpan.FromSeconds(111), host.Wait.Waited);
    }

    [Fact]
    public void LoadBelowTheThreshold_IsQuiet()
    {
        using FakeHost host = new(_ => QuietWait.QuietCores - 0.05);

        host.Wait.Until(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(10), host.Wait.Waited);
    }

    [Fact]
    public void HostThatIsNeverQuiet_IsWaitedForUntilTheMaximum_ThenTheRunGoesOnAndSaysSo()
    {
        using FakeHost host = new(_ => 4.0);

        host.Wait.Until(Minute);

        Assert.Equal(QuietWait.MaxWait, host.Wait.Waited);
        Assert.Contains("wait: gave up; the run goes on and may be slowed by the load", host.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void HostThatCannotBeRead_IsNotWaitedFor()
    {
        using StringWriter output = new();
        TimeSpan now = TimeSpan.Zero;
        QuietWait wait = new(() => null, () => now, slept => now += slept, output);

        wait.Until(Minute);

        Assert.Equal(TimeSpan.Zero, wait.Waited);
        Assert.Contains("wait: the load of the host cannot be read; not waiting", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Waits_AreAddedUp()
    {
        using FakeHost host = new(_ => 0.0);

        host.Wait.Until(Minute);
        host.Wait.Until(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(70), host.Wait.Waited);
    }

    [Fact]
    public void Command_WaitsBeforeEveryStage_AndTheSummaryNamesTheWaitWithoutCountingItInTheDuration()
    {
        using FakeHost host = new(_ => 0.0);
        Stage first = Stage.Of(new("sh", ["-c", "exit 0"], TimeSpan.FromSeconds(30)));
        Stage second = Stage.Of(new("sh", ["-c", "exit 0"], TimeSpan.FromSeconds(30)));
        using StringWriter summary = new();

        // The command name is none of the commands, so it deletes no directory of the working copy.
        int exitCode = RunProgram.Run("probe", [first, second], summary, host.Wait);

        Assert.Equal(0, exitCode);
        Assert.Equal(["== wait: the host quiet for 60 s", "== wait: the host quiet for 10 s"], host.Lines("== wait"));
        Assert.Contains("; waited 70 s; exit 0", summary.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Command_WithoutAWait_HasNoWaitInItsSummary()
    {
        using StringWriter summary = new();

        int exitCode = RunProgram.Run("probe", [Stage.Of(new("sh", ["-c", "exit 0"], TimeSpan.FromSeconds(30)))], summary);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("waited", summary.ToString(), StringComparison.Ordinal);
    }

    // A host whose foreign load is a function of the second of the fake clock, and a wait that reads it. The fake clock moves only when
    // the wait sleeps.
    private sealed class FakeHost : IDisposable
    {
        private readonly StringWriter _output = new();
        private TimeSpan _now = TimeSpan.Zero;
        private long _busy;
        private long _total;

        internal FakeHost(Func<int, double> cores)
        {
            Wait = new QuietWait(
                () => new CpuTimes(_busy, _total, Processors),
                () => _now,
                slept =>
                {
                    _now += slept;
                    // One second of the machine is 100 ticks of each processor.
                    _total += 100L * Processors;
                    _busy += (long)Math.Round(cores((int)_now.TotalSeconds) * 100);
                },
                _output);
        }

        internal QuietWait Wait { get; }

        public void Dispose() => _output.Dispose();

        internal string Text => _output.ToString();

        internal List<string> Lines(string prefix) =>
        [
            .. Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith(prefix, StringComparison.Ordinal)),
        ];
    }
}
