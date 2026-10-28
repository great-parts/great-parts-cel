using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The result of one call of <c>git</c>.</summary>
/// <param name="ExitCode">The exit code of the process, or <see cref="Git.NotStarted"/> when <c>git</c> could not be started.</param>
/// <param name="Output">Standard output, decoded as UTF-8.</param>
/// <param name="Error">Standard error, decoded as UTF-8; the reason when <c>git</c> could not be started.</param>
internal readonly record struct GitResult(int ExitCode, string Output, string Error)
{
    /// <summary>Whether the process ran and returned 0.</summary>
    internal bool Succeeded => ExitCode == 0;
}

/// <summary>Runs the <c>git</c> program as a process and reads its output. No git library is used.</summary>
internal static class Git
{
    /// <summary>The exit code in the result when the <c>git</c> program could not be started.</summary>
    internal const int NotStarted = -1;

    /// <summary>Runs <c>git</c> with the arguments in the directory and waits for it to end.</summary>
    /// <param name="workingDirectory">The directory where <c>git</c> runs, usually the repository root.</param>
    /// <param name="arguments">The arguments, one string each; no quoting is needed.</param>
    internal static GitResult Run(string workingDirectory, params string[] arguments) => RunWithInput(workingDirectory, null, arguments);

    /// <summary>Runs <c>git</c> with the arguments in the directory, gives it the text on standard input and waits for it to end.</summary>
    /// <param name="workingDirectory">The directory where <c>git</c> runs, usually the repository root.</param>
    /// <param name="input">The text for standard input, encoded as UTF-8 without a byte order mark; <see langword="null"/> for none.</param>
    /// <param name="arguments">The arguments, one string each; no quoting is needed.</param>
    internal static GitResult RunWithInput(string workingDirectory, string? input, params string[] arguments)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("The process of git was not started.");
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (input is not null)
            {
                process.StandardInput.BaseStream.Write(new UTF8Encoding(false).GetBytes(input));
                process.StandardInput.Close();
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return new GitResult(process.ExitCode, output, error.GetAwaiter().GetResult());
        }
        catch (Win32Exception exception)
        {
            return new GitResult(NotStarted, string.Empty, exception.Message);
        }
    }
}
