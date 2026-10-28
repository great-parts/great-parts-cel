using System;
using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks that every commit of a revision range has a <c>Signed-off-by</c> trailer (<c>docs/design/eng-checks-artifacts.md</c>,
/// "signoff"). The commits are those of <c>git rev-list</c>; merges and commits of bots count too, there are no exceptions. The
/// trailers are read with <c>git interpret-trailers --parse</c> from the full message of the commit.
/// </summary>
internal sealed class SignoffCommand : ICheckCommand
{
    private const string TrailerKey = "Signed-off-by";
    private const int ShortHashLength = 8;

    /// <inheritdoc/>
    public string Name => "signoff";

    /// <inheritdoc/>
    public string? ArgumentName => "<range>";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        if (!context.Arguments.TryGetValue(Name, out string? range))
        {
            throw new CheckInputException($"the command {Name} needs {ArgumentName}");
        }

        GitResult list = Git.Run(context.Root, "rev-list", "--end-of-options", range);
        if (!list.Succeeded)
        {
            throw new CheckInputException($"the range '{range}' cannot be listed by git rev-list: {Reason(list)}");
        }

        List<Finding> findings = [];
        foreach (string hash in list.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string message = ReadMessage(context.Root, hash);
            if (!HasSignOff(context.Root, hash, message))
            {
                string shortHash = hash.Length > ShortHashLength ? hash[..ShortHashLength] : hash;
                findings.Add(Finding.ForCommit(
                    shortHash,
                    FindingSeverity.Error,
                    EngCodes.Eng701,
                    $"commit {shortHash} '{Subject(message)}' has no {TrailerKey} trailer"));
            }
        }

        return findings;
    }

    // The raw commit object: the headers, an empty line, then the message exactly as it was written.
    private static string ReadMessage(string root, string hash)
    {
        GitResult commit = Git.Run(root, "cat-file", "commit", hash);
        if (!commit.Succeeded)
        {
            throw new CheckInputException($"the commit {hash} cannot be read by git cat-file: {Reason(commit)}");
        }

        int separator = commit.Output.IndexOf("\n\n", StringComparison.Ordinal);
        return separator < 0 ? string.Empty : commit.Output[(separator + 2)..];
    }

    // --no-divider: a line of three dashes in the body of a message is text, not the start of a patch.
    private static bool HasSignOff(string root, string hash, string message)
    {
        GitResult trailers = Git.RunWithInput(root, message, "interpret-trailers", "--parse", "--no-divider");
        if (!trailers.Succeeded)
        {
            throw new CheckInputException($"the trailers of the commit {hash} cannot be read by git interpret-trailers: {Reason(trailers)}");
        }

        foreach (string line in trailers.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && line.AsSpan(0, colon).Trim().Equals(TrailerKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Subject(string message)
    {
        int end = message.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? message : message[..end]).Trim();
    }

    private static string Reason(GitResult result)
    {
        string text = result.Error.Trim();
        int end = text.IndexOf('\n', StringComparison.Ordinal);
        return end < 0 ? text : text[..end].TrimEnd();
    }
}
