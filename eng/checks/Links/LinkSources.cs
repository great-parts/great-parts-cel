using System;
using System.Collections.Generic;
using System.IO;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The input files of the lychee report, read on demand, for the place of a failed link (<c>docs/design/eng-docs.md</c>, "links"): the
/// place of the report, with its column converted to the columns of a finding, or — when the report gives none — the first place in
/// the file that holds the address.
/// </summary>
internal sealed class LinkSources
{
    private readonly string _root;
    private readonly Dictionary<string, string[]?> _lines = new(StringComparer.Ordinal);

    /// <summary>Creates the reader for the files under a root.</summary>
    /// <param name="root">The full path of the repository root.</param>
    internal LinkSources(string root) => _root = root;

    /// <summary>The path of an input file as the finding names it: relative to the root, with <c>/</c>.</summary>
    /// <param name="key">The key of the file in the report, relative to the working directory of lychee or absolute.</param>
    internal string RelativePath(string key)
    {
        string relative = Path.GetRelativePath(_root, Path.GetFullPath(key, _root)).Replace('\\', '/');
        return relative.StartsWith("../", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? key.Replace('\\', '/') : relative;
    }

    /// <summary>Finds the place of a failed link.</summary>
    /// <param name="path">The path of the input file, relative to the root.</param>
    /// <param name="span">The place of the report: the column counts code points. <see langword="null"/> when the report gives none.</param>
    /// <param name="needles">The texts to search for when there is no span, the most exact first; the first one found in the file gives the place.</param>
    /// <returns>The place of a finding, <c>(1,1)</c> when neither the report nor the search gives one.</returns>
    internal Position Locate(string path, Position? span, IReadOnlyList<string> needles)
    {
        string[]? lines = Lines(path);
        if (span is { } given)
        {
            return lines is not null && given.Line <= lines.Length
                ? new Position(given.Line, CodeUnitColumn(lines[given.Line - 1], given.Column))
                : given;
        }

        if (lines is not null)
        {
            foreach (string needle in needles)
            {
                for (int index = 0; needle.Length > 0 && index < lines.Length; index++)
                {
                    int found = lines[index].IndexOf(needle, StringComparison.Ordinal);
                    if (found >= 0)
                    {
                        return new Position(index + 1, found + 1);
                    }
                }
            }
        }

        return new Position(1, 1);
    }

    // The column of lychee counts Unicode code points from 1; a finding counts UTF-16 code units, so a code point outside the basic plane
    // takes two.
    private static int CodeUnitColumn(string line, int codePointColumn)
    {
        int index = 0;
        for (int count = 1; count < codePointColumn && index < line.Length; count++)
        {
            index += char.IsSurrogatePair(line, index) ? 2 : 1;
        }

        return index + 1;
    }

    private string[]? Lines(string path)
    {
        if (_lines.TryGetValue(path, out string[]? lines))
        {
            return lines;
        }

        string full = Path.Combine(_root, path);
        try
        {
            lines = File.Exists(full) ? File.ReadAllText(full).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n') : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            lines = null;
        }

        _lines[path] = lines;
        return lines;
    }
}
