using System;
using System.IO;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>Finds the root of the repository that holds the running tests.</summary>
internal static class RepositoryRoot
{
    private const string SolutionFile = "Great.Parts.Cel.slnx";

    /// <summary>The nearest parent directory of the test assembly that contains <c>Great.Parts.Cel.slnx</c>.</summary>
    internal static string Find()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionFile)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"{SolutionFile} is not in a parent directory of {AppContext.BaseDirectory}.");
    }
}
