using System;
using System.Collections.Generic;
using System.IO;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// Deletes what an unfinished earlier run may have left, so a run never takes it for a result (<c>docs/design/eng-test.md</c>,
/// "Unfinished Native AOT outputs").
/// </summary>
internal static class StaleOutputs
{
    /// <summary>
    /// Deletes the directories with their contents. A directory that is not there is skipped.
    /// </summary>
    /// <param name="directories">The directories, relative to the current directory or absolute.</param>
    /// <returns><see langword="null"/> on success, otherwise why a directory could not be deleted.</returns>
    internal static string? Delete(IEnumerable<string> directories)
    {
        foreach (string directory in directories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return $"{directory}: {exception.Message}";
            }
        }

        return null;
    }
}
