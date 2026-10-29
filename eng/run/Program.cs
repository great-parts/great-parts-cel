using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// Runs a command of <c>eng/</c>: the steps run as child processes in the repository root and their output
/// passes through unchanged. The exit code is 0 when every step returned 0, otherwise 1; a step that passes its exit code on, as
/// the run of <c>eng/difftest</c>, gives the exit code of the command when it ran.
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
        IReadOnlyList<Step>? steps = Commands.Find(command, args[1..]);
        if (steps is null)
        {
            return Fail(command, $"unknown command; known commands: {Commands.Known}");
        }

        if (!File.Exists(Commands.Solution))
        {
            return Fail(command, $"{Commands.Solution} is not in the working directory; run in the repository root");
        }

        if (command == "mutate")
        {
            if (args.Length > 2)
            {
                return Fail(command, "at most one argument, the configuration file");
            }

            // In a linked worktree .git is a file with a host path: Stryker.NET could not match the changed files (docs/design/eng-mutate.md).
            if (File.Exists(".git"))
            {
                return Fail(command, "linked worktree");
            }
        }

        // An older package in artifacts/packages would be a package without a list for the check of the packages.
        foreach (string directory in Commands.Written(command))
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        List<string> failed = RunSteps(steps, out int? passedOn);
        int result = failed.Count == 0 ? Pass(command) : Fail(command, string.Join("; ", failed));
        return passedOn ?? result;
    }

    // Runs the steps up to the first failed step that stops the command; returns the failed steps with their reasons, and the
    // exit code of the step that passes its exit code on, when it ran.
    private static List<string> RunSteps(IReadOnlyList<Step> steps, out int? passedOn)
    {
        List<string> failed = [];
        passedOn = null;
        foreach (Step step in steps)
        {
            Console.Out.WriteLine($"== {step.CommandLine}");
            bool succeeded = TryRun(step, out int? exitCode, out string? reason);
            if (step.PassesExitCode && exitCode is not null)
            {
                passedOn = exitCode;
            }

            if (!succeeded)
            {
                failed.Add(reason is null ? step.CommandLine : $"{step.CommandLine} ({reason})");
                if (step.StopsOnFailure)
                {
                    break;
                }
            }
        }

        return failed;
    }

    private static bool TryRun(Step step, out int? exitCode, out string? reason)
    {
        ProcessStartInfo start = new(step.FileName) { UseShellExecute = false };
        foreach (string argument in step.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        exitCode = null;
        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
            {
                reason = "the process did not start";
                return false;
            }

            process.WaitForExit();
            exitCode = process.ExitCode;
            reason = process.ExitCode == 0 ? null : $"exit code {process.ExitCode}";
            return process.ExitCode == 0;
        }
        catch (Win32Exception exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    private static int Pass(string command)
    {
        Console.Out.WriteLine($"{command}: passed");
        return 0;
    }

    private static int Fail(string command, string reason)
    {
        Console.Out.WriteLine($"{command}: failed: {reason}");
        return 1;
    }
}
