using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// Runs a command of <c>eng/</c> in the repository root: the steps of its stages run as child processes or, for <c>min-sdk</c>, as C#
/// in this process; the lanes of a stage run at the same time. The output of the steps passes through unchanged. The last line of the
/// command is the summary of the run: the result, the commit and whether the working tree is clean, the duration, the exit code and
/// the counters of the command. The exit code is 0 when every step returned 0, otherwise 1; a step that passes its exit code on, as
/// the run of <c>eng/difftest</c>, gives the exit code of the command when it ran. A step that is still running when its time limit is
/// over is killed with its process tree and counts as failed (<see cref="Timeouts"/>).
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("eng/run", $"no command given; known commands: {Commands.Known}");
        }

        string command = args[0];
        if (!Commands.IsKnown(command))
        {
            return Fail(command, $"unknown command; known commands: {Commands.Known}");
        }

        if (!File.Exists(Commands.Solution))
        {
            return Fail(command, $"{Commands.Solution} is not in the working directory; run in the repository root");
        }

        string? problem = CheckArguments(command, args);
        if (problem is not null)
        {
            return Fail(command, problem);
        }

        IReadOnlyList<Stage>? stages;
        try
        {
            stages = Commands.Find(command, args[1..]);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or XmlException)
        {
            return Fail(command, exception.Message);
        }

        return stages is null ? Fail(command, $"unknown command; known commands: {Commands.Known}") : Run(command, stages, Console.Out);
    }

    private static string? CheckArguments(string command, string[] args)
    {
        if (command != "mutate")
        {
            return null;
        }

        MutateOptions options;
        try
        {
            options = MutateOptions.Parse(args[1..]);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }

        return LinkedWorktreeProblem(options, File.Exists(".git"));
    }

    /// <summary>
    /// Why <c>mutate</c> cannot run in this working copy, or <see langword="null"/> when it can. In a linked worktree <c>.git</c> is a file
    /// with a host path, so Stryker.NET could not match the changed files of <c>since</c> (<c>docs/design/eng-mutate.md</c>, "Linked
    /// worktrees"); a run without <c>since</c>, such as the control set, reads no git through the tool and runs there.
    /// </summary>
    /// <param name="options">The arguments of the command.</param>
    /// <param name="linkedWorktree">Whether <c>.git</c> of the working copy is a file.</param>
    /// <param name="workingDirectory">The directory that the path of the configuration is relative to; the current directory when <see langword="null"/>.</param>
    internal static string? LinkedWorktreeProblem(MutateOptions options, bool linkedWorktree, string? workingDirectory = null) =>
        linkedWorktree && options.UsesSince(workingDirectory) ? "linked worktree: a run with since needs the main worktree" : null;

    // Runs the stages and writes the summary line to `summary`; the output of the steps goes to the console as they write it.
    internal static int Run(string command, IReadOnlyList<Stage> stages, TextWriter summary)
    {
        Stopwatch clock = Stopwatch.StartNew();
        RepositoryState start = RepositoryState.Read();

        // An older package or report would be a package or report that this run did not make.
        foreach (string directory in Commands.Written(command))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        List<string> failed = RunStages(stages, out int? passedOn);
        int exitCode = passedOn ?? (failed.Count == 0 ? 0 : 1);
        string result = failed.Count == 0 ? "passed" : $"failed: {string.Join("; ", failed)}";
        string seconds = clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        List<string> parts =
        [
            $"{command}: {result}",
            RepositoryState.Describe(start, RepositoryState.Read()),
            $"{seconds} s",
            $"exit {exitCode.ToString(CultureInfo.InvariantCulture)}",
        ];
        if (Commands.Counters(command) is { } counters)
        {
            parts.Add(counters);
        }

        summary.WriteLine(string.Join("; ", parts));
        return exitCode;
    }

    // Runs the stages up to the first stage that stops the command; returns the failed steps with their reasons, and the exit code of
    // the step that passes its exit code on, when it ran.
    private static List<string> RunStages(IReadOnlyList<Stage> stages, out int? passedOn)
    {
        List<string> failed = [];
        passedOn = null;
        foreach (Stage stage in stages)
        {
            StageResult result = StageRunner.Run(stage);
            failed.AddRange(result.Failed);
            passedOn = result.PassedOn ?? passedOn;
            if (stage.StopsOnFailure && result.Failed.Count > 0)
            {
                break;
            }
        }

        return failed;
    }

    private static int Fail(string command, string reason)
    {
        Console.Out.WriteLine($"{command}: failed: {reason}");
        return 1;
    }
}
