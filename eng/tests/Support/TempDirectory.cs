using System;
using System.IO;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>A new temporary directory that is deleted with the object. A test writes the files of its violating example into it.</summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>Creates the directory.</summary>
    internal TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gpc-eng-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>The full path of the directory.</summary>
    internal string Path { get; }

    /// <summary>Writes a file under the directory, creating its parent directories.</summary>
    /// <param name="relativePath">The path relative to the directory, with <c>/</c>.</param>
    /// <param name="text">The content of the file.</param>
    internal void Write(string relativePath, string text)
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    /// <summary>Deletes the directory with its content.</summary>
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
