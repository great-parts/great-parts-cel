using System;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The place of a test for <c>test-count</c>: the source file and the line that the report gives, else the method found in the PDB
/// of the JIT build, else the <c>&lt;test&gt;</c> element of the report. The path is relative to the repository root.
/// </summary>
internal sealed class TestPlaces
{
    private readonly string _root;
    private readonly TestSymbols _symbols;

    /// <summary>Creates the places of one run.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="jit">The report of the JIT run; its assemblies and their PDBs are read when a test has no source in its report.</param>
    internal TestPlaces(string root, TestReport jit)
    {
        _root = root;
        _symbols = new TestSymbols(jit.Assemblies);
    }

    /// <summary>Builds a finding at the place of a test.</summary>
    /// <param name="test">The test.</param>
    /// <param name="report">The report that has the test.</param>
    /// <param name="code">The code of the finding.</param>
    /// <param name="message">The message of the finding.</param>
    /// <exception cref="CheckInputException">A PDB that the place needs cannot be read.</exception>
    internal Finding FindingAt(ReportedTest test, TestReport report, string code, string message)
    {
        string path;
        int line;
        if (test.SourceFile is { Length: > 0 } sourceFile && test.SourceLine >= 1)
        {
            path = sourceFile;
            line = test.SourceLine;
        }
        else if (_symbols.TryFind(test.Type, test.Method, out TestSymbol symbol))
        {
            path = symbol.File;
            line = symbol.Line;
        }
        else
        {
            // Nothing says where the method is: the report itself is the place.
            return new Finding(RelativePath(report.Path), test.ElementLine, 1, FindingSeverity.Error, code, message);
        }

        return new Finding(RelativePath(path), line, FirstColumn(path, line), FindingSeverity.Error, code, message);
    }

    // The root of the run as written in the path, "/_/" of a deterministic build, or the path as it is.
    private string RelativePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/_/", StringComparison.Ordinal))
        {
            return normalized[3..];
        }

        string root = _root.Replace('\\', '/').TrimEnd('/') + "/";
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return normalized.StartsWith(root, comparison) ? normalized[root.Length..] : normalized;
    }

    // The first character of the line that is not white space; 1 when the file or the line cannot be read, or the line is empty.
    private int FirstColumn(string path, int line)
    {
        string full = Path.GetFullPath(RelativePath(path), _root);
        try
        {
            string? text = File.ReadLines(full).Skip(line - 1).FirstOrDefault();
            if (text is not null)
            {
                int index = text.AsSpan().IndexOfAnyExcept(" \t");
                return index < 0 ? 1 : index + 1;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The source file is not an input of the command: without it the column is 1.
        }

        return 1;
    }
}
