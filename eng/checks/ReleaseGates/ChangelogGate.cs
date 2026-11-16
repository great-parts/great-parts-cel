using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG901</c>: <c>CHANGELOG.md</c> has the entry of the version of the tag, the heading <c>## [&lt;version&gt;] - &lt;yyyy-MM-dd&gt;</c>
/// and a text that is not empty up to the next <c>## </c> heading.
/// </summary>
internal static class ChangelogGate
{
    private const string ChangelogPath = "CHANGELOG.md";

    /// <summary>Checks the file of the checked commit.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="tag">The release tag.</param>
    internal static IEnumerable<Finding> Check(string root, ReleaseTag tag)
    {
        string path = Path.Combine(root, ChangelogPath);
        if (!File.Exists(path))
        {
            throw new CheckInputException($"{ChangelogPath} does not exist");
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int heading = Array.FindIndex(lines, line => IsEntryOf(line, tag.Version));
        if (heading < 0)
        {
            return [new Finding(ChangelogPath, 1, 1, FindingSeverity.Error, EngCodes.Eng901, $"no entry for version {tag.Version}")];
        }

        for (int index = heading + 1; index < lines.Length && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
        {
            if (lines[index].Trim().Length > 0)
            {
                return [];
            }
        }

        return [new Finding(ChangelogPath, 1, 1, FindingSeverity.Error, EngCodes.Eng901, $"the entry for version {tag.Version} has no text")];
    }

    private static bool IsEntryOf(string line, string version)
    {
        string start = "## [" + version + "] - ";
        return line.StartsWith(start, StringComparison.Ordinal)
            && DateOnly.TryParseExact(line[start.Length..].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
