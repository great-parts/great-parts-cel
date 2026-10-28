using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>A diagnostic in the common form of the findings of the repository's own checks.</summary>
/// <param name="Path">The path of the file as the compilation names it.</param>
/// <param name="Line">The line, starting with one.</param>
/// <param name="Column">The column, starting with one.</param>
/// <param name="Level"><c>error</c> or <c>warning</c>.</param>
/// <param name="Code">The diagnostic ID.</param>
/// <param name="Message">The message of the diagnostic.</param>
internal sealed record AnalyzerFinding(string Path, int Line, int Column, string Level, string Code, string Message)
{
    /// <summary>The form of every finding line: <c>path(line,column): error|warning CODE: text</c>.</summary>
    internal static readonly Regex Pattern = new(
        @"^(?<path>[^(]+)\((?<line>\d+),(?<col>\d+)\): (error|warning) (ARCH|ENG)\d{3}: .+$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    /// <summary>Turns a diagnostic into a finding: the path of its location, the line and column of its start plus one.</summary>
    /// <param name="diagnostic">A diagnostic of the compiler or of an analyzer.</param>
    internal static AnalyzerFinding From(Diagnostic diagnostic)
    {
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
        return new AnalyzerFinding(
            span.Path,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            diagnostic.Severity == DiagnosticSeverity.Error ? "error" : "warning",
            diagnostic.Id,
            diagnostic.GetMessage(CultureInfo.InvariantCulture));
    }

    /// <summary>The finding as one line of output.</summary>
    internal string Format() => string.Create(CultureInfo.InvariantCulture, $"{Path}({Line},{Column}): {Level} {Code}: {Message}");
}
