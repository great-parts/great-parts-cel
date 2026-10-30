using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

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

    // The key under which the settings of a configuration file stand; without it they are at the top of the file.
    private const string Section = "stryker-config";

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
    /// Whether the run counts the changes from a commit: <c>--since</c> is given, or the configuration file enables <c>since</c> — an
    /// object <c>since</c> under the key <c>stryker-config</c> or at the top of the file, unless its <c>enabled</c> is <see langword="false"/>,
    /// as Stryker.NET reads it. Only such a run reads git through Stryker.NET. A file that cannot be read, is not JSON or has settings
    /// of another form counts as one that enables <c>since</c>: the run is not known to do without it.
    /// </summary>
    /// <param name="workingDirectory">The directory that the path of the configuration is relative to; the current directory when <see langword="null"/>.</param>
    internal bool UsesSince(string? workingDirectory = null)
    {
        if (Since is not null)
        {
            return true;
        }

        try
        {
            string text = File.ReadAllText(Path.Combine(workingDirectory ?? string.Empty, Configuration));
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            JsonElement root = document.RootElement;
            JsonElement settings = root.ValueKind == JsonValueKind.Object && root.TryGetProperty(Section, out JsonElement section) ? section : root;
            if (settings.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            if (!settings.TryGetProperty("since", out JsonElement since))
            {
                return false;
            }

            return since.ValueKind != JsonValueKind.Object || !since.TryGetProperty("enabled", out JsonElement enabled) || enabled.ValueKind != JsonValueKind.False;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return true;
        }
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
