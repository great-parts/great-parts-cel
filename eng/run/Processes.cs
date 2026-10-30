using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>The result of a child process.</summary>
/// <param name="ExitCode">The exit code, or <see langword="null"/> when the process did not start or was killed because it ran out of time.</param>
/// <param name="Reason">Why the step failed, or <see langword="null"/> when the exit code is 0.</param>
/// <param name="Output">The standard output and the standard error together when the output was captured, otherwise empty.</param>
/// <param name="TimeoutSeconds">The limit in seconds, when the process ran out of time and was killed with its process tree; otherwise <see langword="null"/>.</param>
internal sealed record ProcessResult(int? ExitCode, string? Reason, string Output, int? TimeoutSeconds = null)
{
    /// <summary>Whether the process started and ended with the exit code 0.</summary>
    internal bool Succeeded => ExitCode == 0;
}

/// <summary>Starts child processes for the steps and for the questions to git.</summary>
internal static class Processes
{
    // How often a running process is checked against its limit, and how long the end of a killed process is awaited.
    private const int PollInterval = 100;
    private const int KillWait = 10_000;

    /// <summary>Runs a program and waits for its end.</summary>
    /// <param name="fileName">The program.</param>
    /// <param name="arguments">The arguments of the program.</param>
    /// <param name="workingDirectory">The directory of the process, relative to the current directory; the current directory when <see langword="null"/>.</param>
    /// <param name="sink">Where the lines of the output go as they come; <see langword="null"/> lets the process write to the output of this process.</param>
    /// <param name="capture">Whether the output is also returned in the result; implied by a <paramref name="sink"/>.</param>
    /// <param name="timeout">How long the process may run. When it is still running after that, it is killed with its whole process tree,
    /// and the result has no exit code and has <see cref="ProcessResult.TimeoutSeconds"/>. No limit when <see langword="null"/>.</param>
    /// <param name="extend">Gives a new limit, counted from the start of the process, when a line of the output calls for it; needs a
    /// <paramref name="sink"/> or <paramref name="capture"/>. Only the first new limit counts.</param>
    internal static ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        TextWriter? sink = null,
        bool capture = false,
        TimeSpan? timeout = null,
        Func<string, TimeSpan, TimeSpan?>? extend = null)
    {
        bool redirect = capture || sink is not null;
        ProcessStartInfo start = new(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
        };
        if (workingDirectory is not null)
        {
            start.WorkingDirectory = Path.GetFullPath(workingDirectory);
        }

        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        StringBuilder text = new();
        Limit limit = new(timeout, extend);
        using Process process = new() { StartInfo = start };
        if (redirect)
        {
            process.OutputDataReceived += (_, line) => Append(text, sink, line.Data, limit);
            process.ErrorDataReceived += (_, line) => Append(text, sink, line.Data, limit);
        }

        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            return new ProcessResult(null, exception.Message, string.Empty);
        }

        if (redirect)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        while (!process.WaitForExit(PollInterval))
        {
            if (limit.SecondsWhenOver() is { } seconds)
            {
                return TimedOut(process, text, seconds);
            }
        }

        process.WaitForExit();
        string reason = process.ExitCode == 0 ? string.Empty : $"exit code {process.ExitCode}";
        return new ProcessResult(process.ExitCode, reason.Length == 0 ? null : reason, text.ToString());
    }

    // Kills the process and everything it started: a step that ran out of time may have children that would go on running.
    private static ProcessResult TimedOut(Process process, StringBuilder text, int seconds)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process ended between the last wait and the kill; it still counts as out of time.
        }

        process.WaitForExit(KillWait);
        lock (text)
        {
            return new ProcessResult(null, Timeouts.Reason(seconds), text.ToString(), seconds);
        }
    }

    private static void Append(StringBuilder text, TextWriter? sink, string? line, Limit limit)
    {
        if (line is null)
        {
            return;
        }

        lock (text)
        {
            text.AppendLine(line);
            sink?.WriteLine(line);
        }

        limit.Observe(line);
    }

    // The time a process may run: the first limit, and the one that a line of its output may replace once.
    private sealed class Limit(TimeSpan? first, Func<string, TimeSpan, TimeSpan?>? extend)
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly object gate = new();
        private TimeSpan? current = first;
        private bool extended;

        // The limit in whole seconds, rounded up, when the time is over; otherwise null.
        internal int? SecondsWhenOver()
        {
            lock (gate)
            {
                return current is { } limit && clock.Elapsed > limit ? (int)Math.Ceiling(limit.TotalSeconds) : null;
            }
        }

        internal void Observe(string line)
        {
            if (extend is null)
            {
                return;
            }

            lock (gate)
            {
                if (!extended && extend(line, clock.Elapsed) is { } next)
                {
                    current = next;
                    extended = true;
                }
            }
        }
    }
}
