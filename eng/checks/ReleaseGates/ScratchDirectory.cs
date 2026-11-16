using System;
using System.IO;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A new temporary directory that is deleted with the object, files that git made read-only included.</summary>
internal sealed class ScratchDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    /// <param name="prefix">The start of the name of the directory.</param>
    internal ScratchDirectory(string prefix)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>The full path of the directory.</summary>
    internal string Path { get; }

    /// <summary>Deletes the directory with its content.</summary>
    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Path, recursive: true);
    }
}
