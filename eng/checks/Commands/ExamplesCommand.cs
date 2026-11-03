using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks that every C# example of the documentation compiles and prints what it shows (<c>docs/design/eng-docs.md</c>, "examples"):
/// the fenced <c>csharp</c> blocks of <c>README.md</c> and <c>docs/**/*.md</c> and the <c>&lt;code&gt;</c> elements of the XML documentation in
/// <c>src/**/*.cs</c>. Every example that is not skipped becomes a project in a temporary directory outside the repository; all projects
/// are built with one <c>dotnet build</c>, and every built program runs within the time limit. A compiler error keeps its code and
/// is reported at its line of the file that holds the example; the output that differs from the expected one is <c>ENG301</c>, an
/// exception, an exit code other than 0 and the time limit are <c>ENG302</c>.
/// </summary>
internal sealed class ExamplesCommand : ICheckCommand
{
    /// <summary>The time that an example may run: 60 seconds.</summary>
    internal static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _timeLimit;

    /// <summary>Creates the command with the time limit of 60 seconds.</summary>
    public ExamplesCommand()
        : this(DefaultTimeLimit)
    {
    }

    /// <summary>Creates the command with another time limit; the tests give a short one.</summary>
    /// <param name="timeLimit">The time that an example may run.</param>
    internal ExamplesCommand(TimeSpan timeLimit)
    {
        _timeLimit = timeLimit;
    }

    /// <inheritdoc/>
    public string Name => "examples";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        List<Example> examples = [.. ExampleSources.Of(context).Where(example => example.Skip is null)];
        if (examples.Count == 0)
        {
            return [];
        }

        foreach (string project in new[] { ExampleProjects.LibraryProject, ExampleProjects.ProtobufProject })
        {
            if (!File.Exists(Path.Combine(context.Root, project)))
            {
                throw new CheckInputException($"the project {project} does not exist; the examples refer to it");
            }
        }

        string work = Path.Combine(Path.GetTempPath(), "gpc-examples-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(work);
            WriteProjects(context.Root, work, examples);
            return BuildAndRun(context.Root, work, examples);
        }
        finally
        {
            Cleanup(work);
        }
    }

    private static void WriteProjects(string root, string work, List<Example> examples)
    {
        for (int index = 0; index < examples.Count; index++)
        {
            string directory = Path.Combine(work, ExampleProjects.Name(index));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, ExampleProjects.Name(index) + ".csproj"), ExampleProjects.ProjectText(root, examples[index]));
            File.WriteAllText(Path.Combine(directory, "Program.cs"), ExampleProjects.ProgramText(examples[index]));
        }

        File.WriteAllText(Path.Combine(work, ExampleProjects.SolutionFile), ExampleProjects.SolutionText(examples.Count));

        // The restore of the projects outside the repository takes the package source of the repository too.
        string nuget = Path.Combine(root, "NuGet.config");
        if (File.Exists(nuget))
        {
            File.Copy(nuget, Path.Combine(work, "NuGet.config"));
        }
    }

    private List<Finding> BuildAndRun(string root, string work, List<Example> examples)
    {
        BuildResult build = ExampleBuild.Run(root, Path.Combine(work, ExampleProjects.SolutionFile));
        List<Finding> findings = [];
        HashSet<int> failed = [];
        List<BuildError> others = [];
        Dictionary<string, int> byName = Enumerable.Range(0, examples.Count).ToDictionary(ExampleProjects.Name, StringComparer.Ordinal);
        foreach (BuildError error in build.Errors)
        {
            string? project = ExampleBuild.ProjectName(error);
            if (project is null || !byName.TryGetValue(project, out int index))
            {
                others.Add(error);
                continue;
            }

            failed.Add(index);
            findings.Add(CompilerError(examples[index], error));
        }

        if (others.Count > 0 || (build.ExitCode != 0 && failed.Count == 0))
        {
            string first = others.Count > 0 ? $"{others[0].Code}: {others[0].Text}" : "no error line in the output";
            throw new CheckInputException($"the build of the examples failed with the exit code {build.ExitCode.ToString(CultureInfo.InvariantCulture)}: {first}");
        }

        for (int index = 0; index < examples.Count; index++)
        {
            if (!failed.Contains(index) && Execute(work, index, examples[index]) is { } finding)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    // The error at the line and column of the compiler when it names the file of the example, otherwise at the start of the example.
    private static Finding CompilerError(Example example, BuildError error)
    {
        Position at = error.Position is { } position && error.Path is { } printed && Names(printed, example.Path) ? position : example.Start;
        return new Finding(example.Path, at.Line, at.Column, FindingSeverity.Error, error.Code, error.Text);
    }

    // The compiler prints the path of a '#line' directive as written, or resolved against the directory of the source file.
    private static bool Names(string printed, string path)
    {
        string normalized = printed.Replace('\\', '/');
        return normalized == path || normalized.EndsWith("/" + path, StringComparison.Ordinal);
    }

    private Finding? Execute(string work, int index, Example example)
    {
        string assembly = Path.Combine(work, ExampleProjects.Assembly(index, example.Target));
        if (!File.Exists(assembly))
        {
            throw new CheckInputException($"the build of the example at {example.Path}({example.FirstLine.ToString(CultureInfo.InvariantCulture)}) wrote no assembly");
        }

        ExampleRunResult result = ExampleRun.Run(assembly, Path.GetDirectoryName(assembly) ?? work, _timeLimit);
        if (result.Succeeded)
        {
            return ExpectedOutput.Compare(example, result.Output);
        }

        Position start = example.Start;
        return new Finding(example.Path, start.Line, start.Column, FindingSeverity.Error, EngCodes.Eng302, Failure(result));
    }

    private string Failure(ExampleRunResult result)
    {
        if (result.ExitCode is not { } exitCode)
        {
            return $"example did not end within {_timeLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s";
        }

        string first = result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "no message on standard error";
        return $"example ended with the exit code {exitCode.ToString(CultureInfo.InvariantCulture)}: {first}";
    }

    private static void Cleanup(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The directory is in the temporary location of the machine; a file that is still locked is left there.
        }
    }
}
