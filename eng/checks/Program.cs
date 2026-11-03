using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Runs the commands given on the command line in that order and writes their findings to standard output. Exit code:
/// 0 when no finding is an error, 1 when at least one is, 2 for an invalid invocation or an input that does not exist.
/// </summary>
internal static class Program
{
    private const string RootOption = "--root";
    private const string NoBuildOption = PackagesCommand.NoBuildOption;
    private const string WriteOption = ErrorCodesCommand.WriteOption;

    // The options that take the next token as their value, with what the value is, for the message of a missing one.
    private static readonly Dictionary<string, string> ValueOptions = new(StringComparer.Ordinal)
    {
        [RootOption] = "a directory",
        [TestCountCommand.JitOption] = "a report",
        [TestCountCommand.AotOption] = "a report",
        [TestCountCommand.WhereOption] = "a text",
        [MutationCommand.ReportOption] = "a report",
        [MutationCommand.ConfigOption] = "a configuration file",
        [MutationCommand.SinceOption] = "a commit",
        [ErrorCodesCommand.PageOption] = "a page",
    };

    internal static int Main(string[] args) =>
        Run(args, CommandRegistry.All, Directory.GetCurrentDirectory(), Console.Out, Console.Error);

    /// <summary>Runs one invocation of the application.</summary>
    /// <param name="args">The command line: command names and options.</param>
    /// <param name="commands">The commands that the application knows.</param>
    /// <param name="workingDirectory">The directory where the search for the repository root starts.</param>
    /// <param name="output">Standard output: the findings.</param>
    /// <param name="error">Standard error: the messages of the application itself.</param>
    /// <returns>The exit code.</returns>
    internal static int Run(
        IReadOnlyList<string> args,
        IReadOnlyList<ICheckCommand> commands,
        string workingDirectory,
        TextWriter output,
        TextWriter error)
    {
        if (!TryParse(args, commands, out List<ICheckCommand> selected, out Dictionary<string, string> options, out Dictionary<string, string> arguments, out string problem))
        {
            return Invalid(error, problem);
        }

        string? root = ResolveRoot(options, workingDirectory, out problem);
        if (root is null)
        {
            return Invalid(error, problem);
        }

        // A relative path of a report or a configuration file is taken from the working directory, not from the root of the repository.
        foreach (string reportOption in new[] { TestCountCommand.JitOption, TestCountCommand.AotOption, MutationCommand.ReportOption, MutationCommand.ConfigOption, ErrorCodesCommand.PageOption })
        {
            if (options.TryGetValue(reportOption, out string? report) && report.Length > 0)
            {
                options[reportOption] = Path.GetFullPath(report, workingDirectory);
            }
        }

        CheckContext context = new(root, options, arguments);
        bool failed = false;
        foreach (ICheckCommand command in selected)
        {
            List<Finding> findings;
            try
            {
                findings = [.. Sort(command.Run(context))];
            }
            catch (CheckInputException exception)
            {
                return Invalid(error, exception.Message);
            }

            foreach (Finding finding in findings)
            {
                output.WriteLine(finding.ToString());
                failed |= finding.Severity == FindingSeverity.Error;
            }
        }

        return failed ? 1 : 0;
    }

    private static bool TryParse(
        IReadOnlyList<string> args,
        IReadOnlyList<ICheckCommand> commands,
        out List<ICheckCommand> selected,
        out Dictionary<string, string> options,
        out Dictionary<string, string> arguments,
        out string problem)
    {
        selected = [];
        options = new Dictionary<string, string>(StringComparer.Ordinal);
        arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        problem = string.Empty;
        string known = string.Join(", ", commands.Select(command => command.Name));
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (ValueOptions.TryGetValue(arg, out string? needs))
            {
                if (index + 1 >= args.Count)
                {
                    problem = $"the option {arg} needs {needs}";
                    return false;
                }

                options[arg] = args[++index];
            }
            else if (arg is NoBuildOption or WriteOption)
            {
                // A flag: the value is empty.
                options[arg] = string.Empty;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                problem = $"unknown option {arg}";
                return false;
            }
            else
            {
                ICheckCommand? command = commands.FirstOrDefault(candidate => candidate.Name == arg);
                if (command is null)
                {
                    problem = $"unknown command {arg}; known commands: {known}";
                    return false;
                }

                selected.Add(command);
                if (command.ArgumentName is { } argumentName)
                {
                    // The token after a command that takes an argument is its argument, even when it looks like a command name.
                    if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        problem = $"the command {command.Name} needs {argumentName}";
                        return false;
                    }

                    arguments[command.Name] = args[++index];
                }
            }
        }

        if (selected.Count == 0)
        {
            problem = $"no command given; known commands: {known}";
            return false;
        }

        return true;
    }

    private static string? ResolveRoot(Dictionary<string, string> options, string workingDirectory, out string problem)
    {
        problem = string.Empty;
        if (options.TryGetValue(RootOption, out string? given))
        {
            string full = Path.GetFullPath(given, workingDirectory);
            if (Directory.Exists(full))
            {
                return full;
            }

            problem = $"the directory given by {RootOption} does not exist: {given}";
            return null;
        }

        string? found = RepositoryRoot.Find(workingDirectory);
        if (found is not null)
        {
            return found;
        }

        problem = $"{RepositoryRoot.SolutionFile} is not in {workingDirectory} or a parent directory; run in the repository or give {RootOption}";
        return null;
    }

    // The same input gives the same lines: path (ordinal), line, column, then code and message as tie-breakers.
    private static IEnumerable<Finding> Sort(IEnumerable<Finding> findings) => findings
        .OrderBy(finding => finding.Path, StringComparer.Ordinal)
        .ThenBy(finding => finding.Line)
        .ThenBy(finding => finding.Column)
        .ThenBy(finding => finding.Code, StringComparer.Ordinal)
        .ThenBy(finding => finding.Message, StringComparer.Ordinal);

    private static int Invalid(TextWriter error, string problem)
    {
        error.WriteLine($"eng/checks: {problem}");
        return 2;
    }
}
