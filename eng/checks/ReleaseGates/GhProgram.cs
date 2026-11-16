using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The result of one call of <c>gh</c>.</summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="Output">Standard output and standard error, standard error after standard output.</param>
internal readonly record struct GhResult(int ExitCode, string Output);

/// <summary>The GitHub CLI that checks the attestation of a package. A test gives its own, so no test needs <c>gh</c>.</summary>
internal interface IGh
{
    /// <summary>Runs <c>gh</c> and waits for it to end.</summary>
    /// <param name="arguments">The arguments, one string each.</param>
    /// <exception cref="CheckInputException"><c>gh</c> could not be started.</exception>
    GhResult Run(IReadOnlyList<string> arguments);
}

/// <summary>The <c>gh</c> program of the machine; the token of the job is in its environment.</summary>
internal sealed class GhProgram : IGh
{
    /// <inheritdoc/>
    public GhResult Run(IReadOnlyList<string> arguments)
    {
        ProcessStartInfo start = new("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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
            using Process process = Process.Start(start) ?? throw new CheckInputException("gh was not started");
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return new GhResult(process.ExitCode, output + "\n" + error.GetAwaiter().GetResult());
        }
        catch (Win32Exception exception)
        {
            throw new CheckInputException($"gh cannot be started: {exception.Message}", exception);
        }
    }
}
