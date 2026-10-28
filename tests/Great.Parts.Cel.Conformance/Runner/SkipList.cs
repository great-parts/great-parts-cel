using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>A valid entry of a skip list: the case may fail in the cells of its scope.</summary>
/// <param name="Line">The line of the entry in the list, starting with 1.</param>
/// <param name="Case">The case, <c>&lt;file&gt;/&lt;section&gt;/&lt;name&gt;</c>.</param>
/// <param name="Scope">The cells of the matrix where the case may fail.</param>
internal sealed record SkipEntry(int Line, string Case, IReadOnlySet<RunCell> Scope);

/// <summary>
/// The skip list of a suite, read from its Markdown table, and the <c>ENG800</c> findings of its invalid entries. Both suites
/// share the columns <c>Case</c>, <c>Scope</c>, <c>Reason</c> and <c>Issue</c>; the cel-spec suite adds <c>Reference</c>,
/// the regression corpus adds <c>Signature</c>, <c>Verdict</c> and <c>Severity</c>.
/// </summary>
internal sealed class SkipList
{
    private const string IssuePrefix = "https://github.com/great-parts/great-parts-cel/issues/";

    private static readonly string[] CelSpecColumns = ["Case", "Scope", "Reason", "Reference", "Issue"];
    private static readonly string[] CorpusColumns = ["Case", "Scope", "Signature", "Verdict", "Severity", "Reason", "Issue"];
    private static readonly string[] Verdicts = ["pending", "our-bug", "cel-go-bug", "by-design"];
    private static readonly string[] RegistryVerdicts = ["cel-go-bug", "by-design"];
    private static readonly string[] Severities = ["S1", "S2"];

    private SkipList(string path, IReadOnlyList<SkipEntry> entries, IReadOnlyList<Finding> findings)
    {
        Path = path;
        Entries = entries;
        Findings = findings;
    }

    /// <summary>The path of the list relative to the repository root.</summary>
    internal string Path { get; }

    /// <summary>
    /// The entries with a valid case and a valid scope, in the order of the list. An entry whose scope overlaps the scope of an
    /// earlier entry of the same case is left out.
    /// </summary>
    internal IReadOnlyList<SkipEntry> Entries { get; }

    /// <summary>The <c>ENG800</c> findings of the list, by line.</summary>
    internal IReadOnlyList<Finding> Findings { get; }

    /// <summary>Reads the skip list of the cel-spec suite.</summary>
    /// <param name="path">The path of the list relative to the repository root.</param>
    /// <param name="text">The text of the list.</param>
    /// <param name="commit">The full SHA of the pinned cel-spec commit: a link in <c>Reference</c> goes to its language definition.</param>
    internal static SkipList ReadCelSpec(string path, string text, string commit) =>
        Read(path, text, CelSpecColumns, cells => [ReferenceProblem(cells[3], commit)]);

    /// <summary>Reads the skip list of the regression corpus.</summary>
    /// <param name="path">The path of the list relative to the repository root.</param>
    /// <param name="text">The text of the list.</param>
    /// <param name="registrySignatures">The signatures of the registry of known differences.</param>
    internal static SkipList ReadCorpus(string path, string text, IReadOnlySet<string> registrySignatures) =>
        Read(path, text, CorpusColumns, cells => CorpusProblems(cells[2], cells[3], cells[4], registrySignatures));

    /// <summary>An <c>ENG800</c> finding at column 1 of a line of a list.</summary>
    /// <param name="path">The path of the list relative to the repository root.</param>
    /// <param name="line">The line, starting with 1.</param>
    /// <param name="message">What is wrong.</param>
    internal static Finding Invalid(string path, int line, string message) =>
        new(path, line, 1, FindingSeverity.Error, EngCodes.Eng800, message);

    private static SkipList Read(string path, string text, string[] columns, Func<IReadOnlyList<string>, IEnumerable<string?>> suiteProblems)
    {
        MarkdownTable table = MarkdownTable.Read(text, columns);
        int reason = Array.IndexOf(columns, "Reason");
        int issue = Array.IndexOf(columns, "Issue");
        List<Finding> findings = [.. table.Problems.Select(problem => Invalid(path, problem.Line, problem.Message))];
        List<SkipEntry> entries = [];
        Dictionary<string, List<SkipEntry>> entriesOfCase = new(StringComparer.Ordinal);
        string? previous = null;
        foreach (MarkdownRow row in table.Rows)
        {
            string caseName = row.Cells[0];
            bool isCase = IsCase(caseName);
            bool isScope = RunCell.TryParseScope(row.Cells[1], out IReadOnlySet<RunCell> scope, out string? scopeProblem);
            IEnumerable<string?> problems =
            [
                isCase ? null : "the case is not of the form <file>/<section>/<name>",
                scopeProblem,
                row.Cells[reason].Length == 0 ? "the reason is empty" : null,
                IssueProblem(row.Cells[issue]),
                .. suiteProblems(row.Cells),
                previous is not null && string.CompareOrdinal(caseName, previous) < 0 ? $"the entry is out of order: it follows '{previous}'" : null,
            ];
            previous = caseName;

            if (isCase && isScope)
            {
                if (!entriesOfCase.TryGetValue(caseName, out List<SkipEntry>? same))
                {
                    same = [];
                    entriesOfCase.Add(caseName, same);
                }

                SkipEntry? earlier = same.Find(entry => entry.Scope.Overlaps(scope));
                if (earlier is null)
                {
                    SkipEntry entry = new(row.Line, caseName, scope);
                    same.Add(entry);
                    entries.Add(entry);
                }
                else
                {
                    problems = [.. problems, OverlapProblem(earlier, scope)];
                }
            }

            findings.AddRange(problems.OfType<string>().Select(problem => Invalid(path, row.Line, $"entry '{caseName}': {problem}")));
        }

        return new SkipList(path, entries, [.. findings.OrderBy(finding => finding.Line)]);
    }

    private static string OverlapProblem(SkipEntry earlier, IReadOnlySet<RunCell> scope)
    {
        string line = earlier.Line.ToString(CultureInfo.InvariantCulture);
        return earlier.Scope.SetEquals(scope) ? $"the entry repeats the entry at line {line}" : $"the scope overlaps the scope of the entry at line {line}";
    }

    // The name of a case may contain '/', the names of its file and its section do not: the first two '/' separate the three parts.
    private static bool IsCase(string text)
    {
        int file = text.IndexOf('/', StringComparison.Ordinal);
        int section = file < 0 ? -1 : text.AsSpan(file + 1).IndexOf('/');
        return file > 0 && section > 0 && file + section + 2 < text.Length;
    }

    private static string? IssueProblem(string text) =>
        text.Length == 0
        || (text.StartsWith(IssuePrefix, StringComparison.Ordinal) && IsNumber(text.AsSpan(IssuePrefix.Length)))
            ? null
            : $"the issue '{text}' is not a link {IssuePrefix}<number>";

    private static bool IsNumber(ReadOnlySpan<char> text) =>
        !text.IsEmpty && text[0] != '0' && !text.ContainsAnyExceptInRange('0', '9');

    // A link is a word with "://"; every link of the reference goes to a section of the language definition at the pinned commit.
    private static string? ReferenceProblem(string text, string commit)
    {
        string langdef = $"https://github.com/google/cel-spec/blob/{commit}/doc/langdef.md#";
        string? link = text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(word => word.Contains("://", StringComparison.Ordinal)
                && !(word.StartsWith(langdef, StringComparison.Ordinal) && IsAnchor(word.AsSpan(langdef.Length))));
        return link is null ? null : $"the reference has the link '{link}', which is not {langdef}<section>";
    }

    private static bool IsAnchor(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
        {
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character is not '-' and not '_')
            {
                return false;
            }
        }

        return !text.IsEmpty;
    }

    private static IEnumerable<string?> CorpusProblems(string signature, string verdict, string severity, IReadOnlySet<string> registrySignatures) =>
    [
        signature.Length == 0 ? "the signature is empty" : null,
        Verdicts.Contains(verdict) ? null : $"the verdict '{verdict}' is not one of {string.Join(", ", Verdicts)}",
        Severities.Contains(severity) ? null : $"the severity '{severity}' is not one of {string.Join(", ", Severities)}",
        RegistryVerdicts.Contains(verdict) && !registrySignatures.Contains(signature)
            ? $"the verdict '{verdict}' needs a row of the registry of known differences with the signature '{signature}'"
            : null,
    ];
}
