using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>The result of a child process.</summary>
/// <param name="ExitCode">The exit code, or <see langword="null"/> when the process did not start.</param>
/// <param name="Reason">Why the step failed, or <see langword="null"/> when the exit code is 0.</param>
/// <param name="Output">The standard output and the standard error together when the output was captured, otherwise empty.</param>
internal sealed record ProcessResult(int? ExitCode, string? Reason, string Output)
{
    /// <summary>Whether the process started and ended with the exit code 0.</summary>
    internal bool Succeeded => ExitCode == 0;
}

/// <summary>Starts child processes for the steps and for the questions to git.</summary>
internal static class Processes
{
    /// <summary>Runs a program and waits for its end, however long it takes.</summary>
    /// <param name="fileName">The program.</param>
    /// <param name="arguments">The arguments of the program.</param>
    /// <param name="workingDirectory">The directory of the process, relative to the current directory; the current directory when <see langword="null"/>.</param>
    /// <param name="sink">Where the lines of the output go as they come; <see langword="null"/> lets the process write to the output of this process.</param>
    /// <param name="capture">Whether the output is also returned in the result; implied by a <paramref name="sink"/>.</param>
    internal static ProcessResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        TextWriter? sink = null,
        bool capture = false)
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
        using Process process = new() { StartInfo = start };
        if (redirect)
        {
            process.OutputDataReceived += (_, line) => Append(text, sink, line.Data);
            process.ErrorDataReceived += (_, line) => Append(text, sink, line.Data);
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

        process.WaitForExit();
        string reason = process.ExitCode == 0 ? string.Empty : $"exit code {process.ExitCode}";
        return new ProcessResult(process.ExitCode, reason.Length == 0 ? null : reason, text.ToString());
    }

    private static void Append(StringBuilder text, TextWriter? sink, string? line)
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
    }
}
