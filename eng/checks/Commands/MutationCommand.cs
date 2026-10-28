using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the JSON report of Stryker.NET and the configuration file of the run (<c>docs/design/eng-mutate.md</c>, "mutation"). A survived
/// mutant is <c>ENG501</c>, a mutant without coverage is <c>ENG502</c>, a score below <c>break</c> is <c>ENG503</c>, and a changed file
/// that the run did not mutate is <c>ENG504</c>. The changed files are read from git, and only when the configuration enables
/// <c>since</c>.
/// </summary>
internal sealed class MutationCommand : ICheckCommand
{
    /// <summary>The option of the command line: the JSON report of Stryker.NET.</summary>
    internal const string ReportOption = "--report";

    /// <summary>The option of the command line: the Stryker.NET configuration file of the run.</summary>
    internal const string ConfigOption = "--config";

    private const string MainBranch = "main";
    private const string SinceFilter = "Removed by since filter";
    private const string MutateFilter = "Removed by mutate filter";

    /// <inheritdoc/>
    public string Name => "mutation";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        string reportPath = Required(context, ReportOption);
        string configPath = Required(context, ConfigOption);
        MutationConfig config = MutationConfig.Read(configPath);
        MutationReport report = ReadReport(reportPath, config);
        MutationPaths paths = new(context.Root, report.ProjectRoot, config.Project is { } project ? MutationPaths.DirectoryOf(project) : null);

        List<ReportedFile> files = [.. report.Files];
        int detected = files.Sum(file => file.Mutants.Count(mutant => IsDetected(mutant.Status)));
        int notDetected = files.Sum(file => file.Mutants.Count(mutant => IsNotDetected(mutant.Status)));
        int counted = detected + notDetected;
        bool belowThreshold = config.Break is { } threshold && counted > 0 && detected * 100.0 < threshold * counted;
        FindingSeverity level = belowThreshold ? FindingSeverity.Error : FindingSeverity.Warning;

        List<Finding> findings = [];
        foreach (ReportedFile file in files)
        {
            string path = paths.Relative(file.Path);
            string[] lines = file.Source.ReplaceLineEndings("\n").Split('\n');
            foreach (ReportedMutant mutant in file.Mutants)
            {
                if (mutant.Status is "Survived" or "NoCoverage")
                {
                    string kind = mutant.Status == "Survived" ? "survived mutant" : "mutant without coverage";
                    string code = mutant.Status == "Survived" ? EngCodes.Eng501 : EngCodes.Eng502;
                    findings.Add(new Finding(path, mutant.Start.Line, mutant.Start.Column, level, code, $"{kind} ({mutant.Mutator}): '{Original(lines, mutant)}' -> '{OneLine(mutant.Replacement)}'"));
                }
            }
        }

        if (belowThreshold)
        {
            decimal score = detected * 100m / counted;
            string where = paths.Relative(configPath);
            findings.Add(new Finding(
                where,
                config.BreakPlace.Line,
                config.BreakPlace.Column,
                FindingSeverity.Error,
                EngCodes.Eng503,
                string.Create(CultureInfo.InvariantCulture, $"mutation score {Math.Floor(score * 100m) / 100m:0.##} % is below the threshold {config.Break:0.##} % ({detected} of {counted} mutants detected)")));
        }

        if (config.SinceEnabled)
        {
            findings.AddRange(NotMutated(context.Root, config, files, paths));
        }

        return findings;
    }

    private static string Required(CheckContext context, string option) =>
        context.Options.TryGetValue(option, out string? value)
            ? value
            : throw new CheckInputException($"the command mutation needs the option {option}");

    // A run with since can end without a report when the tool fails: the report then has no mutants, and every changed file is a finding.
    private static MutationReport ReadReport(string path, MutationConfig config) =>
        config.SinceEnabled && !File.Exists(path) ? MutationReport.Empty : MutationReport.Read(path);

    private static bool IsDetected(string status) => status is "Killed" or "Timeout";

    private static bool IsNotDetected(string status) => status is "Survived" or "NoCoverage";

    // The changed C# files of the project under test that the run did not mutate.
    private static IEnumerable<Finding> NotMutated(string root, MutationConfig config, List<ReportedFile> files, MutationPaths paths)
    {
        if (config.Project is not { } project)
        {
            throw new CheckInputException("the configuration enables since but names no project, so the changed files of the project are not known");
        }

        string directory = MutationPaths.DirectoryOf(project);
        Dictionary<string, ReportedFile> byPath = files.ToDictionary(file => paths.Relative(file.Path), StringComparer.Ordinal);
        foreach (string changed in ChangedFiles(root))
        {
            bool inProject = directory.Length == 0 || changed.StartsWith(directory + "/", StringComparison.Ordinal);
            if (!inProject || !changed.EndsWith(".cs", StringComparison.Ordinal) || !File.Exists(Path.Combine(root, changed)))
            {
                continue;
            }

            string? problem = byPath.TryGetValue(changed, out ReportedFile? file) ? Problem(file) : "it is not in the mutation report";
            if (problem is not null)
            {
                yield return new Finding(changed, 1, 1, FindingSeverity.Error, EngCodes.Eng504, $"changed file was not mutated: {problem}");
            }
        }
    }

    // A file without mutants is no finding. A file with mutants but none counted is one when a filter of the tool left them out.
    private static string? Problem(ReportedFile file)
    {
        if (file.Mutants.Any(mutant => IsDetected(mutant.Status) || IsNotDetected(mutant.Status)))
        {
            return null;
        }

        string[] filters = [.. file.Mutants.Select(mutant => mutant.StatusReason).OfType<string>().Where(reason => reason is SinceFilter or MutateFilter).Distinct().Order(StringComparer.Ordinal)];
        return filters.Length == 0 ? null : $"none of its mutants was counted ({string.Join(", ", filters)})";
    }

    private static string[] ChangedFiles(string root)
    {
        GitResult diff = Git.Run(root, "-c", "core.quotepath=off", "diff", "--name-only", "--no-renames", MainBranch, "--");
        if (!diff.Succeeded)
        {
            string reason = diff.Error.Trim().Split('\n')[0].TrimEnd();
            throw new CheckInputException($"the files changed against {MainBranch} cannot be listed by git diff: {reason}");
        }

        return diff.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    // The text of the source file at the place of the mutant.
    private static string Original(string[] lines, ReportedMutant mutant)
    {
        int first = Math.Clamp(mutant.Start.Line, 1, lines.Length) - 1;
        int last = Math.Clamp(mutant.End.Line, first + 1, lines.Length) - 1;
        List<string> parts = [];
        for (int index = first; index <= last; index++)
        {
            string line = lines[index];
            int from = index == first ? Math.Clamp(mutant.Start.Column - 1, 0, line.Length) : 0;
            int to = index == last ? Math.Clamp(mutant.End.Column - 1, from, line.Length) : line.Length;
            parts.Add(line[from..to]);
        }

        return OneLine(string.Join('\n', parts));
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ");
}
