using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Great.Parts.Cel.Eng.Findings;

/// <summary>
/// A finding of one of the repository's own checks. <see cref="ToString"/> is the only place that formats a line:
/// <c>path(line,column): error|warning CODE: message</c>.
/// </summary>
/// <param name="Path">The path relative to the repository root with <c>/</c>, or <c>commit/&lt;sha&gt;</c> or <c>tag/&lt;name&gt;</c>.</param>
/// <param name="Line">The line, starting with 1.</param>
/// <param name="Column">The column in UTF-16 code units, starting with 1.</param>
/// <param name="Severity">The severity.</param>
/// <param name="Code">
/// The code, <c>ARCH</c> or <c>ENG</c> and three digits; for an error of the build of an example of <c>examples</c>, the code of the
/// compiler, an analyzer or the SDK, such as <c>CS0103</c>.
/// </param>
/// <param name="Message">The message: English, one line.</param>
public readonly record struct Finding(string Path, int Line, int Column, FindingSeverity Severity, string Code, string Message)
{
    private const string ErrorWord = "error";
    private const string WarningWord = "warning";

    /// <summary>The line of the finding, starting with 1.</summary>
    public int Line { get; } = Line >= 1
        ? Line
        : throw new ArgumentOutOfRangeException(nameof(Line), Line, "The line starts with 1.");

    /// <summary>The column of the finding, starting with 1.</summary>
    public int Column { get; } = Column >= 1
        ? Column
        : throw new ArgumentOutOfRangeException(nameof(Column), Column, "The column starts with 1.");

    /// <summary>
/// The form of every finding line with a code of the repository: <c>path(line,column): error|warning CODE: message</c>. A line of
/// an error of the build of an example has the same form, but its code is the code of the tool, which the pattern does not match.
/// </summary>
    public static Regex Pattern { get; } = new(
        @"^(?<path>[^(]+)\((?<line>\d+),(?<col>\d+)\): (error|warning) (ARCH|ENG)\d{3}: .+$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    /// <summary>A finding about a commit of the history, at the place <c>(1,1)</c> of the path <c>commit/&lt;sha&gt;</c>.</summary>
    /// <param name="sha">The commit hash.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="code">The code.</param>
    /// <param name="message">The message.</param>
    public static Finding ForCommit(string sha, FindingSeverity severity, string code, string message) =>
        new("commit/" + sha, 1, 1, severity, code, message);

    /// <summary>A finding about a tag, at the place <c>(1,1)</c> of the path <c>tag/&lt;name&gt;</c>.</summary>
    /// <param name="name">The tag name.</param>
    /// <param name="severity">The severity.</param>
    /// <param name="code">The code.</param>
    /// <param name="message">The message.</param>
    public static Finding ForTag(string name, FindingSeverity severity, string code, string message) =>
        new("tag/" + name, 1, 1, severity, code, message);

    /// <summary>Parses a line of the form of <see cref="Pattern"/>.</summary>
    /// <param name="line">One line of output without the line break.</param>
    /// <param name="finding">The parsed finding, or the default value when the line has another form.</param>
    /// <returns><see langword="true"/> when the line matches <see cref="Pattern"/> and its line and column are valid.</returns>
    public static bool TryParse(string line, out Finding finding)
    {
        finding = default;
        ArgumentNullException.ThrowIfNull(line);

        // "$" of the pattern also matches before a final line break, which is not one line.
        if (line.Contains('\n', StringComparison.Ordinal))
        {
            return false;
        }

        Match match = Pattern.Match(line);
        if (!match.Success
            || !int.TryParse(match.Groups["line"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int lineNumber)
            || !int.TryParse(match.Groups["col"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int columnNumber)
            || lineNumber < 1
            || columnNumber < 1)
        {
            return false;
        }

        // After "path(line,col): " come the severity word, a space, the code up to the first colon, ": " and the message.
        ReadOnlySpan<char> rest = line.AsSpan(match.Groups["col"].Index + match.Groups["col"].Length + "): ".Length);
        FindingSeverity severity = rest.StartsWith(ErrorWord, StringComparison.Ordinal) ? FindingSeverity.Error : FindingSeverity.Warning;
        rest = rest[(severity == FindingSeverity.Error ? ErrorWord.Length : WarningWord.Length)..][1..];
        int colon = rest.IndexOf(':');
        finding = new Finding(
            match.Groups["path"].Value,
            lineNumber,
            columnNumber,
            severity,
            rest[..colon].ToString(),
            rest[(colon + 2)..].ToString());
        return true;
    }

    /// <summary>The finding as one line of output: <c>path(line,column): error|warning CODE: message</c>.</summary>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Path}({Line},{Column}): {(Severity == FindingSeverity.Error ? ErrorWord : WarningWord)} {Code}: {Message}");
}
