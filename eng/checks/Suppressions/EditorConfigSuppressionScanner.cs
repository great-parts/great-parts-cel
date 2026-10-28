using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the forms of <c>.editorconfig</c> and <c>.globalconfig</c> files: a rule set to <c>none</c>, <c>silent</c> or
/// <c>suggestion</c>, an analyzer level set to one of them for a category or for all rules, and <c>generated_code = true</c>
/// (<c>docs/design/eng-checks-source.md</c>, "suppressions"). The reason is the comment lines directly above the entry. A
/// comment line starts with <c>#</c> or <c>;</c>; a blank line, a section header or another entry ends the comment.
/// </summary>
internal sealed class EditorConfigSuppressionScanner : ISuppressionScanner
{
    private const string ReasonPlace = "comment line above";
    private const string DiagnosticPrefix = "dotnet_diagnostic.";
    private const string CategoryPrefix = "dotnet_analyzer_diagnostic.category-";
    private const string AllRulesKey = "dotnet_analyzer_diagnostic.severity";
    private const string SeveritySuffix = ".severity";
    private const string GeneratedCodeKey = "generated_code";
    private const string AnalysisModeKey = "AnalysisMode";
    private const string GeneratedKey = "generated";
    private static readonly string[] LoweredLevels = ["none", "silent", "suggestion"];

    /// <inheritdoc/>
    public IEnumerable<SuppressionForm> Scan(CheckContext context) => context.Files
        .Where(file => IsConfigFile(file.Path))
        .SelectMany(file => ScanFile(file.Path, file.ReadText()));

    private static bool IsConfigFile(string path)
    {
        string name = path[(path.LastIndexOf('/') + 1)..];
        return name == ".editorconfig" || name.EndsWith(".globalconfig", StringComparison.Ordinal);
    }

    private static IEnumerable<SuppressionForm> ScanFile(string path, string text)
    {
        using StringReader reader = new(text);
        List<string> comments = [];
        int lineNumber = 0;
        for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            lineNumber++;
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith('#') || trimmed.StartsWith(';'))
            {
                comments.Add(trimmed);
                continue;
            }

            Draft? form = TryParseEntry(trimmed, out string key, out string value) ? Classify(key, value) : null;
            if (form is not null)
            {
                yield return new SuppressionForm(
                    path,
                    new Position(lineNumber, line.Length - trimmed.Length + 1),
                    $"'{key} = {value}'",
                    ReasonPlace,
                    form.Keys,
                    form.RuleIds,
                    string.Join(' ', comments));
            }

            comments.Clear();
        }
    }

    // An entry is 'key = value' or 'key : value' with a key of letters, digits and '_', '.' and '-'; both sides are trimmed.
    private static bool TryParseEntry(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        int separator = line.AsSpan().IndexOfAny('=', ':');
        if (separator <= 0)
        {
            return false;
        }

        key = line[..separator].Trim();
        value = line[(separator + 1)..].Trim();
        return key.Length > 0 && key.All(character => char.IsLetterOrDigit(character) || character is '_' or '.' or '-');
    }

    private static Draft? Classify(string key, string value)
    {
        if (string.Equals(key, GeneratedCodeKey, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? new Draft([GeneratedKey], []) : null;
        }

        if (!LoweredLevels.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (RuleId(key) is { } id)
        {
            return new Draft([id], [id]);
        }

        return IsAnalyzerLevel(key) ? new Draft([AnalysisModeKey], []) : null;
    }

    // dotnet_diagnostic.<id>.severity
    private static string? RuleId(string key)
    {
        if (!key.StartsWith(DiagnosticPrefix, StringComparison.OrdinalIgnoreCase) || !key.EndsWith(SeveritySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string id = key[DiagnosticPrefix.Length..];
        return id.Length > SeveritySuffix.Length ? id[..^SeveritySuffix.Length] : null;
    }

    // dotnet_analyzer_diagnostic.severity, dotnet_analyzer_diagnostic.category-<name>.severity
    private static bool IsAnalyzerLevel(string key) =>
        string.Equals(key, AllRulesKey, StringComparison.OrdinalIgnoreCase)
        || (key.StartsWith(CategoryPrefix, StringComparison.OrdinalIgnoreCase)
            && key.EndsWith(SeveritySuffix, StringComparison.OrdinalIgnoreCase)
            && key.Length > CategoryPrefix.Length + SeveritySuffix.Length);

    private sealed record Draft(IReadOnlyList<string> Keys, IReadOnlyList<string> RuleIds);
}
