using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// Runs a command of <c>eng/</c>: the steps run as child processes in the repository root and their output
/// passes through unchanged. The exit code is 0 when every step returned 0, otherwise 1.
/// </summary>
internal static class Program
{
    internal static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return Fail("eng/run", "no command given; known commands: test");
        }

        string command = args[0];
        IReadOnlyList<Step>? steps = Commands.Find(command);
        if (steps is null)
        {
            return Fail(command, "unknown command; known commands: test");
        }

        if (!File.Exists(Commands.Solution))
        {
            return Fail(command, $"{Commands.Solution} is not in the working directory; run in the repository root");
        }

        List<string> failed = [];
        foreach (Step step in steps)
        {
            Console.Out.WriteLine($"== {step.CommandLine}");
            if (!TryRun(step, out string? reason))
            {
                failed.Add(reason is null ? step.CommandLine : $"{step.CommandLine} ({reason})");
            }
        }

        return failed.Count == 0 ? Pass(command) : Fail(command, string.Join("; ", failed));
    }

    private static bool TryRun(Step step, out string? reason)
    {
        ProcessStartInfo start = new(step.FileName) { UseShellExecute = false };
        foreach (string argument in step.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process? process = Process.Start(start);
            if (process is null)
            {
                reason = "the process did not start";
                return false;
            }

            process.WaitForExit();
            reason = null;
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
