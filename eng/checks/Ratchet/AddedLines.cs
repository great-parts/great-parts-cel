using System;
using System.Collections.Generic;
using System.Globalization;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The lines of the working copy that <c>git diff</c> adds against the merge base with the local <c>main</c>, so the ratchet can name
/// the new places of a growth (<c>docs/design/eng-checks-source.md</c>, "ratchet"). Without that history — a shallow clone, a
/// linked worktree started without its second Compose file, a directory that is not a repository — <see cref="HasHistory"/>
/// is false and every place counts as new.
/// </summary>
internal sealed class AddedLines
{
    private const string ScanName = "added-lines";
    private const string MainBranch = "main";
    private const string HunkMarker = "@@ -";
    private const string AddedFileMarker = "+++ ";

    private readonly Dictionary<string, List<(int First, int Last)>> _ranges;
    private readonly HashSet<string> _untracked;

    private AddedLines(bool hasHistory, Dictionary<string, List<(int First, int Last)>> ranges, HashSet<string> untracked)
    {
        HasHistory = hasHistory;
        _ranges = ranges;
        _untracked = untracked;
    }

    /// <summary>Whether the merge base with <c>main</c> was found and the diff against it was read.</summary>
    internal bool HasHistory { get; }

    /// <summary>Returns the added lines of the run, reading them on the first call.</summary>
    /// <param name="context">The context of the run.</param>
    internal static AddedLines Of(CheckContext context) => context.Scan(ScanName, () => Read(context.Root));

    /// <summary>Reads the added lines of the repository at the root.</summary>
    /// <param name="root">The full path of the repository root.</param>
    internal static AddedLines Read(string root)
    {
        GitResult mergeBase = Git.Run(root, "merge-base", MainBranch, "HEAD");
        string commit = mergeBase.Output.Trim();
        if (!mergeBase.Succeeded || commit.Length == 0)
        {
            return None();
        }

        GitResult diff = Git.Run(root, "-c", "core.quotepath=off", "diff", "--unified=0", "--no-color", "--no-ext-diff", "--no-renames", "--src-prefix=a/", "--dst-prefix=b/", commit);
        GitResult untracked = Git.Run(root, "-c", "core.quotepath=off", "ls-files", "--others", "--exclude-standard");
        if (!diff.Succeeded || !untracked.Succeeded)
        {
            return None();
        }

        HashSet<string> files = new(StringComparer.Ordinal);
        foreach (string line in untracked.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            files.Add(line);
        }

        return new AddedLines(hasHistory: true, ParseDiff(diff.Output), files);
    }

    /// <summary>Returns true when the line of the file is added by the diff; a file that git does not track is added whole.</summary>
    /// <param name="path">The path of the file, relative to the root, with <c>/</c>.</param>
    /// <param name="line">The line, from 1.</param>
    internal bool IsAdded(string path, int line)
    {
        if (_untracked.Contains(path))
        {
            return true;
        }

        return _ranges.TryGetValue(path, out List<(int First, int Last)>? ranges) && ranges.Exists(range => range.First <= line && line <= range.Last);
    }

    /// <summary>Reads the added ranges of the new side of a diff made with <c>--unified=0</c>.</summary>
    /// <param name="diff">The output of <c>git diff</c>.</param>
    internal static Dictionary<string, List<(int First, int Last)>> ParseDiff(string diff)
    {
        Dictionary<string, List<(int First, int Last)>> ranges = new(StringComparer.Ordinal);
        List<(int First, int Last)>? current = null;
        foreach (string line in diff.Split('\n'))
        {
            if (line.StartsWith(AddedFileMarker, StringComparison.Ordinal))
            {
                current = FileRanges(ranges, line[AddedFileMarker.Length..]);
            }
            else if (current is not null && line.StartsWith(HunkMarker, StringComparison.Ordinal) && NewSide(line) is { } range)
            {
                current.Add(range);
            }
        }

        return ranges;
    }

    // The path after "+++ " is "b/<path>", sometimes followed by a tab; a deleted file is /dev/null and has no new side.
    private static List<(int First, int Last)>? FileRanges(Dictionary<string, List<(int First, int Last)>> ranges, string target)
    {
        string path = target.TrimEnd('\t', '\r');
        if (!path.StartsWith("b/", StringComparison.Ordinal))
        {
            return null;
        }

        string key = path[2..];
        if (!ranges.TryGetValue(key, out List<(int First, int Last)>? list))
        {
            list = [];
            ranges[key] = list;
        }

        return list;
    }

    // "@@ -a,b +c,d @@": the new side is c and d lines from it; d is 1 when it is not written and 0 when nothing is added.
    private static (int First, int Last)? NewSide(string hunk)
    {
        int plus = hunk.IndexOf(" +", StringComparison.Ordinal);
        int end = hunk.IndexOf(" @@", plus + 1, StringComparison.Ordinal);
        if (plus < 0 || end < 0)
        {
            return null;
        }

        string[] parts = hunk[(plus + 2)..end].Split(',');
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int first))
        {
            return null;
        }

        int count = 1;
        if (parts.Length > 1 && !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out count))
        {
            return null;
        }

        return count == 0 ? null : (first, first + count - 1);
    }

    private static AddedLines None() => new(hasHistory: false, [], []);
}
