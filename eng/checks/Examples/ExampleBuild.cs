using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One error line of the build.</summary>
/// <param name="Path">The file of the error as the compiler printed it; <see langword="null"/> when the line names none.</param>
/// <param name="Position">The line and column of the error; <see langword="null"/> when the line has no place.</param>
/// <param name="Code">The code of the error, such as <c>CS0103</c>.</param>
/// <param name="Text">The message of the error.</param>
/// <param name="Project">The project file that the line names at its end; <see langword="null"/> when it names none.</param>
internal sealed record BuildError(string? Path, Position? Position, string Code, string Text, string? Project);

/// <summary>The result of the build of the generated solution.</summary>
/// <param name="ExitCode">The exit code of <c>dotnet build</c>.</param>
/// <param name="Errors">The error lines of the output, each once.</param>
internal sealed record BuildResult(int ExitCode, IReadOnlyList<BuildError> Errors);

/// <summary>Builds the generated solution of the examples with one <c>dotnet build</c> (<c>docs/design/eng-docs.md</c>, "examples").</summary>
internal static class ExampleBuild
{
    // 'path(line,column): error CS0103: text [project]' or 'tool : error CS5001: text [project]'; the project and the place are
    // optional. A range of the place, (line,column,line,column), is read by its start; the project of a build for one target of
    // several has '::TargetFramework=net10.0' after its path, which is not part of the project.
    private static readonly Regex ErrorLine = new(
        @"^(?:(?<path>.+?)\((?<line>\d+),(?<column>\d+)(?:,\d+,\d+)?\): |(?<tool>.+? : ))error (?<code>[A-Za-z]+\d+): (?<text>.*?)(?: \[(?<project>[^\]]+?\.csproj)(?:::[^\]]*)?\])?\s*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    /// <summary>
    /// Runs <c>dotnet build -c Release</c> on the solution in the root of the repository, so that <c>global.json</c> of the repository
    /// picks the SDK. The messages are in English, whatever the language of the machine.
    /// </summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="solution">The full path of the generated solution.</param>
    /// <exception cref="CheckInputException">The build did not start.</exception>
    internal static BuildResult Run(string root, string solution)
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
        foreach (string argument in new[] { "build", solution, "-c", "Release", "-nologo", "-v:q", "-clp:NoSummary", "-nodeReuse:false" })
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        start.Environment["VSLANG"] = "1033";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        string command = string.Join(' ', start.ArgumentList.Prepend(start.FileName));
        try
        {
            using Process process = Process.Start(start) ?? throw new CheckInputException($"'{command}' did not start");
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return new BuildResult(process.ExitCode, ErrorsOf(output + "\n" + error.GetAwaiter().GetResult()));
        }
        catch (Win32Exception exception)
        {
            throw new CheckInputException($"'{command}' did not start: {exception.Message}", exception);
        }
    }

    /// <summary>Reads the error lines of the output of the build; a line that the build prints twice is returned once.</summary>
    /// <param name="output">The text of the output.</param>
    internal static IReadOnlyList<BuildError> ErrorsOf(string output)
    {
        List<BuildError> errors = [];
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match match = ErrorLine.Match(line);
            if (!match.Success)
            {
                continue;
            }

            Position? position = match.Groups["line"].Success
                ? new Position(Number(match.Groups["line"].Value), Number(match.Groups["column"].Value))
                : null;
            BuildError error = new(
                match.Groups["path"].Success ? match.Groups["path"].Value : null,
                position,
                match.Groups["code"].Value,
                match.Groups["text"].Value,
                match.Groups["project"].Success ? match.Groups["project"].Value : null);
            if (!errors.Contains(error))
            {
                errors.Add(error);
            }
        }

        return errors;
    }

    // A place of the compiler starts with 1; the guard keeps a line that has 0 from breaking the finding.
    private static int Number(string digits) => Math.Max(1, int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture));

    /// <summary>The name of the project of an error line without the directory and the extension, or <see langword="null"/>.</summary>
    /// <param name="error">The error line.</param>
    internal static string? ProjectName(BuildError error) =>
        error.Project is null ? null : Path.GetFileNameWithoutExtension(error.Project.Replace('\\', '/').Split('/')[^1]);
}
