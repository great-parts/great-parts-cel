using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// The data of the suites, read from the embedded resources of the assembly. A resource is named by its path from the root of the
/// repository, with forward slashes; the runner reads no files, so it works the same when published under Native AOT.
/// </summary>
internal static class EmbeddedData
{
    /// <summary>The directory of the cel-spec test files, relative to the repository root.</summary>
    internal const string CelSpecTestData = "tests/cel-spec/tests/simple/testdata/";

    /// <summary>The extension of a text file of a suite.</summary>
    private const string TextExtension = ".textproto";

    /// <summary>The extension of the binary form of a text file, written by the build with protoc.</summary>
    private const string BinaryExtension = ".binpb";

    private static readonly Assembly Source = typeof(EmbeddedData).Assembly;

    /// <summary>Returns the paths of the text files of a suite that lie in a directory, in ordinal order.</summary>
    /// <param name="directory">The directory relative to the repository root, with a trailing slash.</param>
    internal static IReadOnlyList<string> TextFiles(string directory)
    {
        List<string> paths = [];
        foreach (string name in Source.GetManifestResourceNames())
        {
            if (name.StartsWith(directory, StringComparison.Ordinal)
                && !name.AsSpan(directory.Length).Contains('/')
                && name.EndsWith(TextExtension, StringComparison.Ordinal))
            {
                paths.Add(name);
            }
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    /// <summary>Returns the text of an embedded file.</summary>
    /// <param name="path">The path of the file relative to the repository root.</param>
    internal static string ReadText(string path)
    {
        using StreamReader reader = new(Open(path));
        return reader.ReadToEnd();
    }

    /// <summary>Returns the binary form of an embedded text file of a suite.</summary>
    /// <param name="textPath">The path of the text file relative to the repository root.</param>
    internal static byte[] ReadBinary(string textPath)
    {
        string name = string.Concat(textPath.AsSpan(0, textPath.Length - TextExtension.Length), BinaryExtension);
        using Stream stream = Open(name);
        using MemoryStream copy = new();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static Stream Open(string name) =>
        Source.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"The embedded resource '{name}' does not exist.");
}
