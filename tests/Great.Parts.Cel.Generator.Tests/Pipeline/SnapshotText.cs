using System;
using System.IO;
using System.Text;

namespace Great.Parts.Cel.Generator.Tests.Pipeline;

// The comparison of the created text with a snapshot, which is the code of this project: no library for it. Both texts get "\r\n"
// replaced by "\n"; the version in the [GeneratedCode] attribute is "<version>" in the snapshot and is replaced in the created text
// before the comparison; then the texts are compared line by line and the first different line is the difference.
internal static class SnapshotText
{
    internal const string VersionMarker = "<version>";

    private const string GeneratedCodePrefix = "GeneratedCode(\"Great.Parts.Cel.Generator\", \"";

    // The variable that turns the snapshot tests into the writer of the snapshots.
    internal const string DirectoryVariable = "GPC_SNAPSHOT_DIR";

    internal static string Normalize(string text) => ReplaceVersion(text.Replace("\r\n", "\n", StringComparison.Ordinal));

    // The first difference as the message of a failure, or null when the texts are equal.
    internal static string? FirstDifference(string? expected, string actual, string name)
    {
        string where = "tests/Great.Parts.Cel.Generator.Tests/Snapshots/" + name + ".g.cs";
        if (expected is null)
        {
            return "first difference at " + where + " line 1: the snapshot file does not exist";
        }

        string[] expectedLines = Lines(expected);
        string[] actualLines = Lines(actual);
        int shorter = Math.Min(expectedLines.Length, actualLines.Length);
        for (int i = 0; i < shorter; i++)
        {
            if (!string.Equals(expectedLines[i], actualLines[i], StringComparison.Ordinal))
            {
                return Difference(where, i + 1, expectedLines[i], actualLines[i]);
            }
        }

        if (expectedLines.Length == actualLines.Length)
        {
            return null;
        }

        string expectedLine = expectedLines.Length > shorter ? expectedLines[shorter] : "<end of the text>";
        string actualLine = actualLines.Length > shorter ? actualLines[shorter] : "<end of the text>";
        return Difference(where, shorter + 1, expectedLine, actualLine);
    }

    internal static string? Read(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Snapshots", name + ".g.cs");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    // The text that is written as a snapshot: normalized, with the version of the generator as the marker. A relative directory is
    // relative to the working copy of the repository (the directory that holds the solution), whatever the directory of the process.
    internal static void Write(string directory, string name, string actual)
    {
        string full = Path.IsPathRooted(directory) ? directory : Path.Combine(RepositoryRoot(), directory);
        Directory.CreateDirectory(full);
        File.WriteAllText(Path.Combine(full, name + ".g.cs"), Normalize(actual));
    }

    // The lines of a normalized text; the line end that closes the last line does not start another one.
    private static string[] Lines(string text)
    {
        string[] lines = Normalize(text).Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Great.Parts.Cel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("The directory of the solution is not above the test output.");
    }

    private static string Difference(string where, int line, string expected, string actual) =>
        "first difference at " + where + " line " + line + ": expected \"" + expected + "\", actual \"" + actual + "\"";

    // Every [GeneratedCode] attribute of the text: the version is the marker.
    private static string ReplaceVersion(string text)
    {
        var result = new StringBuilder();
        int position = 0;
        while (true)
        {
            int start = text.IndexOf(GeneratedCodePrefix, position, StringComparison.Ordinal);
            int from = start + GeneratedCodePrefix.Length;
            int end = start < 0 ? -1 : text.IndexOf('"', from);
            if (end < 0)
            {
                return result.Append(text, position, text.Length - position).ToString();
            }

            result.Append(text, position, from - position).Append(VersionMarker);
            position = end;
        }
    }
}
