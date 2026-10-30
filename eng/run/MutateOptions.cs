using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// The arguments of <c>mutate</c> (<c>docs/design/eng-mutate.md</c>, "Steps"): the configuration file of the run, and the commit from
/// which the changes are counted.
/// </summary>
/// <param name="Configuration">The configuration file of Stryker.NET.</param>
/// <param name="Since">The commit as it was given, or <see langword="null"/> when the configuration file names the target.</param>
internal sealed record MutateOptions(string Configuration, string? Since)
{
    /// <summary>The option that gives the commit from which the changes are counted.</summary>
    internal const string SinceOption = "--since";

    /// <summary>The configuration of <c>mutate</c> when the command gets none.</summary>
    internal const string DefaultConfiguration = "stryker-config.json";

    /// <summary>Reads the arguments after the command name: at most one configuration file and at most one <c>--since &lt;commit&gt;</c>, in any order.</summary>
    /// <param name="arguments">The arguments after the command name.</param>
    /// <exception cref="InvalidOperationException">An argument is not one of them, or an option has no value.</exception>
    internal static MutateOptions Parse(IReadOnlyList<string> arguments)
    {
        string? configuration = null;
        string? since = null;
        for (int i = 0; i < arguments.Count; i++)
        {
            string argument = arguments[i];
            if (argument == SinceOption)
            {
                if (since is not null)
                {
                    throw new InvalidOperationException($"{SinceOption} is given twice");
                }

                since = i + 1 < arguments.Count && !arguments[i + 1].StartsWith('-') ? arguments[++i] : throw new InvalidOperationException($"{SinceOption} needs a commit");
            }
            else if (argument.StartsWith('-'))
            {
                throw new InvalidOperationException($"unknown option '{argument}'; the arguments are a configuration file and {SinceOption} <commit>");
            }
            else if (configuration is not null)
            {
                throw new InvalidOperationException("at most one configuration file");
            }
            else
            {
                configuration = argument;
            }
        }

        return new MutateOptions(configuration ?? DefaultConfiguration, since);
    }

    /// <summary>
    /// The full SHA of the commit that was given, or <see langword="null"/> when none was. Stryker.NET takes a full SHA or a reference
    /// but not a short SHA, and <c>eng/checks</c> must count the changes from the same commit, so both get the full SHA.
    /// </summary>
    /// <param name="workingDirectory">The directory of the repository, relative to the current directory; the current directory when <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">The commit is not a commit of the repository.</exception>
    internal string? ResolveSince(string? workingDirectory = null)
    {
        if (Since is null)
        {
            return null;
        }

        ProcessResult result = Processes.Run("git", ["rev-parse", "--verify", "--quiet", Since + "^{commit}"], workingDirectory, capture: true);
        return result.Succeeded && result.Output.Trim() is { Length: > 0 } sha
            ? sha
            : throw new InvalidOperationException($"{SinceOption} '{Since}' is not a commit of this repository");
    }
}
