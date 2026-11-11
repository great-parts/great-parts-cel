using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// The wait of <c>eng/run</c> for a quiet host (<c>docs/design/eng-test.md</c>, "Waiting for the host"). The time limits of the steps
/// are those of a run on a quiet machine: when the build of another working copy runs in a container of the same host, the steps
/// get a part of the processors and fail by timeout without a fault of the change. Before the first stage and before each later
/// one the command waits until the machine has been quiet for a time, and writes how long it waited.
/// </summary>
/// <remarks>
/// A container does not see the other containers without the Docker socket, which the container of <c>eng/</c> does not get. It sees
/// the processor time of the host kernel (<see cref="CpuTimes"/>), so a build in another container, or on the host, shows as processor
/// time that this process does not use. At the points of the wait nothing of this command runs, because the steps of the last stage have ended.
/// The time of the wait is not part of the limit of any step.
/// </remarks>
internal sealed class QuietWait
{
    /// <summary>How long the host must be quiet before the first stage of a command.</summary>
    internal static readonly TimeSpan StartQuiet = TimeSpan.FromSeconds(60);

    /// <summary>How long the host must be quiet before each later stage; the build of this command has just ended.</summary>
    internal static readonly TimeSpan BetweenQuiet = TimeSpan.FromSeconds(10);

    /// <summary>The wait ends after this time even when the host is not quiet: the command then runs, and says so.</summary>
    internal static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(60);

    /// <summary>The load of other processes below which a reading counts as quiet: processors that work, over one reading.</summary>
    internal const double QuietCores = 0.5;

    /// <summary>The time between two readings of the host.</summary>
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>How often the wait writes that it still waits.</summary>
    internal static readonly TimeSpan ReportEvery = TimeSpan.FromSeconds(30);

    private readonly Func<CpuTimes?> _read;
    private readonly Func<TimeSpan> _clock;
    private readonly Action<TimeSpan> _sleep;
    private readonly TextWriter _output;
    private readonly bool _skip;

    /// <summary>Makes a wait with its sources, so a test gives its own host and time.</summary>
    /// <param name="read">Reads the processor time of the host; <see langword="null"/> when it cannot.</param>
    /// <param name="clock">The time that has passed since some start.</param>
    /// <param name="sleep">Waits for the given time.</param>
    /// <param name="output">Where the lines about the wait go.</param>
    /// <param name="skip">When true the wait does not wait: the line says why, and the run goes on at once.</param>
    internal QuietWait(Func<CpuTimes?> read, Func<TimeSpan> clock, Action<TimeSpan> sleep, TextWriter output, bool skip = false)
    {
        _skip = skip;
        _read = read;
        _clock = clock;
        _sleep = sleep;
        _output = output;
    }

    /// <summary>The time that all waits of this object took.</summary>
    internal TimeSpan Waited { get; private set; }

    /// <summary>
    /// The wait of the host: its own clock and <c>/proc/stat</c>. On a GitHub Actions runner (<see cref="InCi"/>) it does not wait: the
    /// runner is a machine of its own with no other build of ours, and the load that <c>/proc/stat</c> shows there is that of the
    /// services of the runner, which never ends and held a job of CI until its limit.
    /// </summary>
    /// <param name="output">Where the lines about the wait go.</param>
    internal static QuietWait ForHost(TextWriter output) => ForHost(output, Environment.GetEnvironmentVariable);

    /// <summary>The wait of the host with a given source of environment variables, so a test gives its own.</summary>
    /// <param name="output">Where the lines about the wait go.</param>
    /// <param name="environment">Reads an environment variable; <see langword="null"/> when it is not set.</param>
    internal static QuietWait ForHost(TextWriter output, Func<string, string?> environment)
    {
        Stopwatch clock = Stopwatch.StartNew();
        return new QuietWait(() => CpuTimes.Read(), () => clock.Elapsed, Thread.Sleep, output, InCi(environment));
    }

    /// <summary>True when <c>GITHUB_ACTIONS</c> is <c>true</c>: the command runs in a job of GitHub Actions.</summary>
    /// <param name="environment">Reads an environment variable; <see langword="null"/> when it is not set.</param>
    internal static bool InCi(Func<string, string?> environment) =>
        string.Equals(environment("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Waits until every reading of the host, one per <see cref="Interval"/>, has been quiet for <paramref name="quiet"/> without a break,
    /// or until <see cref="MaxWait"/> is over, and writes how long it waited and how busy the host was.
    /// </summary>
    /// <param name="quiet">How long the host must be quiet.</param>
    internal void Until(TimeSpan quiet)
    {
        if (_skip)
        {
            _output.WriteLine("== wait: not waiting in CI (GITHUB_ACTIONS is true); the runner is a machine of its own");
            return;
        }

        _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"== wait: the host quiet for {Seconds(quiet)} s"));
        TimeSpan start = _clock();
        TimeSpan quietSince = start;
        TimeSpan reported = start;
        CpuTimes? previous = _read();
        double peak = 0;
        bool giveUp = false;
        while (previous is not null)
        {
            _sleep(Interval);
            CpuTimes? current = _read();
            TimeSpan now = _clock();
            if (current is null)
            {
                previous = null;
                break;
            }

            double cores = CpuTimes.Cores(previous.Value, current.Value);
            previous = current;
            if (cores > QuietCores)
            {
                peak = Math.Max(peak, cores);
                quietSince = now;
                if (now - reported >= ReportEvery)
                {
                    reported = now;
                    _output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wait: other processes use {cores:0.0} processors; waited {Seconds(now - start)} s"));
                }
            }
            else if (now - quietSince >= quiet)
            {
                break;
            }

            if (now - start >= MaxWait)
            {
                giveUp = true;
                break;
            }
        }

        TimeSpan took = _clock() - start;
        Waited += took;
        _output.WriteLine(Describe(previous is null, giveUp, took, peak));
    }

    private static string Describe(bool unreadable, bool giveUp, TimeSpan took, double peak)
    {
        if (unreadable)
        {
            return "wait: the load of the host cannot be read; not waiting";
        }

        string seen = peak > 0 ? string.Create(CultureInfo.InvariantCulture, $"other processes used up to {peak:0.0} processors") : "no load of other processes seen";
        string ending = giveUp ? "gave up; the run goes on and may be slowed by the load" : "the host is quiet";
        return string.Create(CultureInfo.InvariantCulture, $"wait: {ending}; waited {Seconds(took)} s; {seen}");
    }

    private static long Seconds(TimeSpan time) => (long)Math.Round(time.TotalSeconds);
}
