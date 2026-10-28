using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>Packs the solution with <c>dotnet pack</c> into <c>artifacts/packages</c>.</summary>
internal static class DotnetPack
{
    /// <summary>
    /// Runs <c>dotnet pack Great.Parts.Cel.slnx -c Release -o artifacts/packages</c> in the repository root and waits for it to end. The
    /// output of the build is not passed on: a failed pack is reported by one line that names the first error of the output.
    /// </summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="noBuild">Whether to pack the Release build that already exists instead of building first.</param>
    /// <exception cref="CheckInputException">The pack did not start or failed.</exception>
    internal static void Run(string root, bool noBuild)
    {
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "pack", RepositoryRoot.SolutionFile, "-c", "Release", "-o", PackagesCommand.PackagesDirectory })
        {
            start.ArgumentList.Add(argument);
        }

        if (noBuild)
        {
            start.ArgumentList.Add("--no-build");
        }

        string command = string.Join(' ', start.ArgumentList.Prepend(start.FileName));
        try
        {
            using Process process = Process.Start(start) ?? throw new CheckInputException($"'{command}' did not start");
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                string first = (output + "\n" + error.GetAwaiter().GetResult())
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault(line => line.Contains(": error ", StringComparison.Ordinal)) ?? "no error line in the output";
                throw new CheckInputException($"'{command}' failed with the exit code {process.ExitCode}: {first}");
            }
        }
        catch (Win32Exception exception)
        {
            throw new CheckInputException($"'{command}' did not start: {exception.Message}", exception);
        }
    }
}
