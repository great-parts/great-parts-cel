using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Eng.Difftest;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// The time that a step may take, in one place so a limit is easy to raise (<c>docs/design/eng-test.md</c>, "Time limits"). A limit is
/// the time of a cold run with a margin added, rounded up to a second. The build, the module of the tests of <c>eng/</c>, the Native AOT
/// publish and the examples of <c>docs</c> are those of one cold run on 2026-11-09 with 30 % added; the other limits are the time of a
/// cold run measured on 2026-10-30, the lychee of <c>docs</c> and the preparation of <c>mutate</c> on 2026-11-03 and 2026-11-04, with 20 %
/// added. A step that is still running when its limit is over
/// is killed with its whole process tree and fails with <c>timeout after N s in step &lt;step&gt;</c>.
/// </summary>
internal static class Timeouts
{
    /// <summary>
    /// The build of the solution; also the build of one project that a command builds before its run. A cold build of the solution on
    /// 2026-11-09 took 106.6 s, next to the code of the fourth increment.
    /// </summary>
    internal static readonly TimeSpan Build = TimeSpan.FromSeconds(139);

    /// <summary>
    /// One test module under JIT, and the source and package checks of <c>eng/checks</c>: the module of the tests of <c>eng/</c> took 92.1 s in
    /// the cold run of 2026-11-09, next to the four publishes.
    /// </summary>
    internal static readonly TimeSpan Jit = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The Native AOT publish of one project and target: the longest of the four publishes of the cold run of 2026-11-09 took 233.0 s,
    /// that of the tests of the library for <c>net10.0</c>.
    /// </summary>
    internal static readonly TimeSpan Publish = TimeSpan.FromSeconds(303);

    /// <summary>The run of one published executable.</summary>
    internal static readonly TimeSpan AotRun = TimeSpan.FromSeconds(120);

    /// <summary>A command of <c>eng/checks</c> that reads reports or files and builds nothing: <c>test-count</c>, <c>mutation</c>.</summary>
    internal static readonly TimeSpan Check = TimeSpan.FromSeconds(12);

    /// <summary>
    /// The build and the run of all examples of the documentation in <c>eng/checks examples</c>: one <c>dotnet build</c> of all example
    /// projects, then every program. A cold run of <c>docs</c> on 2026-11-09 took 34.7 s for the step.
    /// </summary>
    internal static readonly TimeSpan Examples = TimeSpan.FromSeconds(46);

    /// <summary>
    /// lychee, offline, over <c>README.md</c>, <c>docs/</c> and the sources: it reads files and checks them against files, with no
    /// request. A run of <c>docs</c> on 2026-11-03 took 0.9 s for the step, the longest run of that day 1.4 s; on 2026-11-04 three runs took 1.6 s, 2.1 s and 2.1 s; on 2026-11-09, with the pages of time, six runs took 2.5 s to 3.9 s.
    /// </summary>
    internal static readonly TimeSpan Lychee = TimeSpan.FromSeconds(5);

    /// <summary>Each child process of <c>min-sdk</c>: the version of the SDK and the build of the sample application.</summary>
    internal static readonly TimeSpan MinSdk = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The preparation of <c>mutate</c>: the restore of the tool, and the tool until it says how many mutants it will test.
    /// The runs of the control set on 2026-11-03, after the tests of the documentation checks were added, took 179.2 s cold, 200.1 s with
    /// the Debug build done and more than 180 s once; the limit is the longest of them with 20 % added.
    /// </summary>
    internal static readonly TimeSpan MutatePreparation = TimeSpan.FromSeconds(241);

    /// <summary>The time of <c>mutate</c> for one mutant, counted from the line in which the tool says how many it will test.</summary>
    internal static readonly TimeSpan MutatePerMutant = TimeSpan.FromSeconds(0.3);

    /// <summary>The time added to that of the mutants, for the end of the tool: its report is written after the last mutant.</summary>
    internal static readonly TimeSpan MutateEnd = TimeSpan.FromSeconds(30);

    /// <summary>The factor of the budget of <c>difftest</c>.</summary>
    internal const double DifftestFactor = 1.2;

    /// <summary>The time added to the budget of <c>difftest</c>, for its start and its end.</summary>
    internal static readonly TimeSpan DifftestExtra = TimeSpan.FromSeconds(36);

    /// <summary>The budget of <c>eng/difftest</c> without <c>--budget</c>; it must equal the default of that program.</summary>
    internal static readonly TimeSpan DifftestDefaultBudget = TimeSpan.FromMinutes(5);

    /// <summary>The reason of a step that was killed because it ran out of time.</summary>
    /// <param name="seconds">The limit of the step, in seconds.</param>
    internal static string Reason(int seconds) => string.Create(CultureInfo.InvariantCulture, $"{ReasonPrefix}{seconds} s");

    /// <summary>Whether a reason of failure is that of a step that was killed because it ran out of time.</summary>
    /// <param name="reason">The reason of a failed step.</param>
    internal static bool IsReason(string? reason) => reason?.StartsWith(ReasonPrefix, StringComparison.Ordinal) == true;

    /// <summary>The limit of the run of <c>eng/difftest</c> that has the given budget.</summary>
    /// <param name="budget">The budget of the run, minimization included.</param>
    internal static TimeSpan Difftest(TimeSpan budget) => budget * DifftestFactor + DifftestExtra;

    /// <summary>
    /// The limit of the run of <c>eng/difftest</c> for its arguments. The value of <c>--budget</c> is read as that program reads it:
    /// a whole number of seconds, minutes or hours, such as <c>30s</c>, <c>5m</c> or <c>2h</c>. An absent or invalid value gives the default
    /// budget: the program then ends at once with its own message.
    /// </summary>
    /// <param name="arguments">The arguments of the command after its name.</param>
    internal static TimeSpan Difftest(IReadOnlyList<string> arguments)
    {
        TimeSpan budget = DifftestDefaultBudget;
        for (int i = 0; i + 1 < arguments.Count; i++)
        {
            if (arguments[i] == "--budget" && BudgetText.TryParse(arguments[i + 1], out TimeSpan given))
            {
                budget = given;
            }
        }

        return Difftest(budget);
    }

    /// <summary>
    /// The new limit of <c>stryker</c> when a line of its output says how many mutants it will test: the time so far plus the time for
    /// the mutants. Another line gives <see langword="null"/>.
    /// </summary>
    /// <param name="line">A line of the output of the tool.</param>
    /// <param name="elapsed">The time since the start of the step.</param>
    internal static TimeSpan? MutateChecking(string line, TimeSpan elapsed)
    {
        // The tool pads the number with spaces: "[19:18:44 INF] 30    total mutants will be tested".
        int marker = line.IndexOf(MutantCountMarker, StringComparison.Ordinal);
        int end = marker;
        while (end > 0 && line[end - 1] == ' ')
        {
            end--;
        }

        int start = end;
        while (start > 0 && char.IsAsciiDigit(line[start - 1]))
        {
            start--;
        }

        if (marker <= 0 || start == end || !int.TryParse(line.AsSpan(start, end - start), NumberStyles.None, CultureInfo.InvariantCulture, out int mutants))
        {
            return null;
        }

        return elapsed + (MutatePerMutant * mutants) + MutateEnd;
    }

    private const string ReasonPrefix = "timeout after ";

    // The line of Stryker.NET before the check of the mutants: "812   total mutants will be tested".
    private const string MutantCountMarker = "total mutants will be tested";
}
