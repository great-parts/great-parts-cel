using System;
using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The files of the project under test that a run mutates, as the patterns of <c>mutate</c> in its configuration file name them
/// (<c>docs/design/eng-mutate.md</c>, "mutation"). A pattern is relative to the directory of the project; <c>*</c> stands for any
/// characters of one name, <c>**</c> for any characters including separators, <c>**/</c> for any number of directories, <c>?</c> for
/// one character of a name; a leading <c>!</c> excludes the files that match. Without an include pattern every file is in, as in
/// Stryker.NET, and the order of the patterns is irrelevant. A span after the file (<c>File.cs{10..100}</c>) is left out: a file
/// is mutated or not.
/// </summary>
internal sealed class MutateScope
{
    private readonly List<string> _include = [];
    private readonly List<string> _exclude = [];
    private readonly string _directory;

    /// <summary>Creates the scope of one configuration file.</summary>
    /// <param name="patterns">The patterns of <c>mutate</c>; empty when the file sets none.</param>
    /// <param name="projectDirectory">The directory of the project relative to the repository root, with <c>/</c>; empty for the root itself.</param>
    internal MutateScope(IReadOnlyList<string> patterns, string projectDirectory)
    {
        _directory = projectDirectory;
        foreach (string pattern in patterns)
        {
            string text = WithoutSpan(pattern.Replace('\\', '/').Trim());
            if (text.StartsWith('!'))
            {
                _exclude.Add(text[1..]);
            }
            else
            {
                _include.Add(text);
            }
        }
    }

    /// <summary>Whether the run mutates the file.</summary>
    /// <param name="path">The path of the file relative to the repository root, with <c>/</c>.</param>
    internal bool Contains(string path)
    {
        string relative = _directory.Length > 0 && path.StartsWith(_directory + "/", StringComparison.Ordinal) ? path[(_directory.Length + 1)..] : path;
        bool included = _include.Count == 0 || _include.Any(pattern => Matches(pattern, relative));
        return included && !_exclude.Any(pattern => Matches(pattern, relative));
    }

    /// <summary>Whether a pattern without a leading <c>!</c> and without a span matches a path.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="path">The path relative to the directory of the project, with <c>/</c>.</param>
    internal static bool Matches(string pattern, string path) => Match(pattern, 0, path, 0);

    private static string WithoutSpan(string pattern)
    {
        int open = pattern.LastIndexOf('{');
        return pattern.EndsWith('}') && open >= 0 ? pattern[..open] : pattern;
    }

    private static bool Match(string pattern, int patternIndex, string text, int textIndex)
    {
        while (patternIndex < pattern.Length)
        {
            char c = pattern[patternIndex];
            if (c == '*')
            {
                return patternIndex + 1 < pattern.Length && pattern[patternIndex + 1] == '*'
                    ? MatchAnything(pattern, patternIndex + 2, text, textIndex)
                    : MatchName(pattern, patternIndex + 1, text, textIndex);
            }

            if (textIndex >= text.Length || (c == '?' ? text[textIndex] == '/' : c != text[textIndex]))
            {
                return false;
            }

            patternIndex++;
            textIndex++;
        }

        return textIndex == text.Length;
    }

    // "**": the rest of the pattern matches the text from any place; after "**/" only from the start of a name.
    private static bool MatchAnything(string pattern, int next, string text, int textIndex)
    {
        bool directories = next < pattern.Length && pattern[next] == '/';
        int rest = directories ? next + 1 : next;
        for (int index = textIndex; index <= text.Length; index++)
        {
            bool atNameStart = index == textIndex || text[index - 1] == '/';
            if ((!directories || atNameStart) && Match(pattern, rest, text, index))
            {
                return true;
            }
        }

        return false;
    }

    // "*": the rest of the pattern matches the text from any place of the same name.
    private static bool MatchName(string pattern, int next, string text, int textIndex)
    {
        int index = textIndex;
        while (true)
        {
            if (Match(pattern, next, text, index))
            {
                return true;
            }

            if (index >= text.Length || text[index] == '/')
            {
                return false;
            }

            index++;
        }
    }
}
