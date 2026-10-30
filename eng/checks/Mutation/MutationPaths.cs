using System;
using System.Collections.Generic;
using System.IO;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Turns the absolute paths of a mutation report, the paths of the machine that ran the tool, into paths relative to the repository
/// root. The tool runs in a container where the root is <c>/repo</c>, the checks may run elsewhere, and a path that git lists is
/// relative already.
/// </summary>
internal sealed class MutationPaths
{
    private readonly List<string> _prefixes = [];

    /// <summary>Creates the converter of one run.</summary>
    /// <param name="root">The full path of the repository root on the machine of the check.</param>
    /// <param name="projectRoot">The directory of the project under test as the report gives it, or <see langword="null"/>.</param>
    /// <param name="projectDirectory">The directory of the project under test relative to the repository root, or <see langword="null"/>.</param>
    internal MutationPaths(string root, string? projectRoot, string? projectDirectory)
    {
        // The root of the report: its project directory without the part that the configuration names relative to the root.
        if (projectRoot is not null && projectDirectory is not null)
        {
            string directory = projectRoot.Replace('\\', '/').TrimEnd('/');
            string suffix = projectDirectory.Length == 0 ? string.Empty : "/" + projectDirectory;
            if (directory.EndsWith(suffix, StringComparison.Ordinal) && directory.Length > suffix.Length)
            {
                _prefixes.Add(directory[..^suffix.Length] + "/");
            }
        }

        _prefixes.Add(root.Replace('\\', '/').TrimEnd('/') + "/");
    }

    /// <summary>Returns the path relative to the repository root, or the path itself with <c>/</c> when it is under none of the roots.</summary>
    /// <param name="path">A path from the report.</param>
    internal string Relative(string path)
    {
        string normalized = path.Replace('\\', '/');
        foreach (string prefix in _prefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                return normalized[prefix.Length..];
            }
        }

        return normalized;
    }

    /// <summary>Returns the directory of a project file relative to the repository root, with <c>/</c>; empty for the root itself.</summary>
    /// <param name="projectFile">The path of the project file as the configuration gives it.</param>
    internal static string DirectoryOf(string projectFile) =>
        (Path.GetDirectoryName(projectFile.Replace('\\', '/')) ?? string.Empty).Replace('\\', '/').Trim('/');
}
