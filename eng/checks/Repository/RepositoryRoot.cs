using System.IO;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>Finds the root of the repository: the nearest parent directory with the solution file.</summary>
internal static class RepositoryRoot
{
    /// <summary>The solution of the repository; its directory is the root.</summary>
    internal const string SolutionFile = "Great.Parts.Cel.slnx";

    /// <summary>Looks for the solution file in the directory and then in each parent directory.</summary>
    /// <param name="startDirectory">The directory where the search starts.</param>
    /// <returns>The full path of the root, or <see langword="null"/> when no parent directory has the solution file.</returns>
    internal static string? Find(string startDirectory)
    {
        for (DirectoryInfo? directory = new(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
