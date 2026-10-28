using System;
using System.Collections.Generic;
using System.IO;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The file set of the checks: all files under the root except the directories <c>.git</c>, <c>bin</c> and <c>obj</c> at any
/// depth and <c>artifacts</c> and <c>tests/cel-spec</c> under the root.
/// </summary>
internal static class RepositoryFiles
{
    private static readonly string[] ExcludedAnywhere = [".git", "bin", "obj"];
    private static readonly string[] ExcludedUnderRoot = ["artifacts", "tests/cel-spec"];

    /// <summary>Lists the files of the file set. Links to directories are not followed.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <returns>The files sorted by relative path, ordinal.</returns>
    internal static IReadOnlyList<RepositoryFile> Enumerate(string root)
    {
        List<RepositoryFile> files = [];
        Walk(root, string.Empty, files);
        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        return files;
    }

    private static void Walk(string directory, string relative, List<RepositoryFile> files)
    {
        foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            string path = relative.Length == 0 ? entry.Name : relative + "/" + entry.Name;
            if (IsExcluded(entry.Name, path))
            {
                continue;
            }

            if (entry is not DirectoryInfo)
            {
                files.Add(new RepositoryFile(path, entry.FullName));
            }
            else if (!entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                Walk(entry.FullName, path, files);
            }
        }
    }

    private static bool IsExcluded(string name, string path) =>
        Array.IndexOf(ExcludedAnywhere, name) >= 0 || Array.IndexOf(ExcludedUnderRoot, path) >= 0;
}
