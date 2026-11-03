using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The result of the run of one example.</summary>
/// <param name="ExitCode">The exit code, or <see langword="null"/> when the program was stopped because it did not end in time.</param>
/// <param name="Output">What the program wrote to standard output.</param>
/// <param name="Error">What the program wrote to standard error.</param>
internal sealed record ExampleRunResult(int? ExitCode, string Output, string Error)
{
    /// <summary>Whether the program ended with the exit code 0.</summary>
    internal bool Succeeded => ExitCode == 0;
}

/// <summary>Runs a built example with <c>dotnet</c> within a time limit (<c>docs/design/eng-docs.md</c>, "examples").</summary>
internal static class ExampleRun
{
    /// <summary>Runs the assembly. The program has no input: its standard input is closed. A program that does not end within the limit is stopped with its processes.</summary>
    /// <param name="assembly">The full path of the built assembly.</param>
    /// <param name="workingDirectory">The directory of the process.</param>
    /// <param name="limit">The time the program may run.</param>
    /// <exception cref="CheckInputException">The process did not start.</exception>
    internal static ExampleRunResult Run(string assembly, string workingDirectory, TimeSpan limit)
    {
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(assembly);
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        try
        {
            using Process process = Process.Start(start) ?? throw new CheckInputException($"'dotnet {assembly}' did not start");
            process.StandardInput.Close();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            bool ended = process.WaitForExit(limit);
            if (!ended)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            return new ExampleRunResult(ended ? process.ExitCode : null, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
        catch (Win32Exception exception)
        {
            throw new CheckInputException($"'dotnet {assembly}' did not start: {exception.Message}", exception);
        }
    }
}
