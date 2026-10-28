using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the exclusions of <c>lychee.toml</c> in the repository root (<c>docs/design/eng-checks-source.md</c>,
/// "suppressions"): every string of the arrays <c>exclude</c> and <c>exclude_path</c> and every flag
/// <c>exclude_all_private</c>, <c>exclude_private</c>, <c>exclude_link_local</c> and <c>exclude_loopback</c> set to
/// <c>true</c> is one exclusion, found at its string or at its key. The reason is a comment in the same line.
/// </summary>
internal sealed class LycheeSuppressionScanner : ISuppressionScanner
{
    private const string FileName = "lychee.toml";
    private const string ReasonPlace = "comment in the same line";
    private const string Key = "lychee";
    private static readonly string[] PatternKeys = ["exclude", "exclude_path"];
    private static readonly string[] FlagKeys = ["exclude_all_private", "exclude_private", "exclude_link_local", "exclude_loopback"];

    /// <inheritdoc/>
    public IEnumerable<SuppressionForm> Scan(CheckContext context) => context.Files
        .Where(file => file.Path == FileName)
        .SelectMany(file => ScanFile(file.Path, file.ReadText()));

    private static IEnumerable<SuppressionForm> ScanFile(string path, string text)
    {
        SourceLines lines = new(text);
        List<TomlToken> tokens = TomlTokens.Read(text);
        Dictionary<int, List<string>> comments = [];
        foreach (TomlToken comment in tokens.Where(token => token.Kind == TomlTokenKind.Comment))
        {
            int line = lines.Locate(comment.Offset).Line;
            if (!comments.TryGetValue(line, out List<string>? list))
            {
                comments[line] = list = [];
            }

            list.Add(comment.Text);
        }

        foreach ((TomlToken token, string description) in Exclusions(tokens.Where(token => token.Kind != TomlTokenKind.Comment).ToList(), text))
        {
            Position position = lines.Locate(token.Offset);
            string reason = comments.TryGetValue(position.Line, out List<string>? found) ? string.Join(' ', found) : string.Empty;
            yield return new SuppressionForm(path, position, $"'{description}'", ReasonPlace, [Key], [], reason);
        }
    }

    // Walks the statements: a table header is skipped, 'key = value' is read by the name of the key.
    private static List<(TomlToken Token, string Description)> Exclusions(List<TomlToken> tokens, string text)
    {
        List<(TomlToken, string)> found = [];
        int index = 0;
        while (index < tokens.Count)
        {
            if (IsPunctuation(tokens[index], "["))
            {
                index = SkipHeader(tokens, index);
                continue;
            }

            int equals = tokens.FindIndex(index, token => IsPunctuation(token, "="));
            if (equals <= index)
            {
                index++;
                continue;
            }

            TomlToken keyToken = tokens[equals - 1];
            string name = keyToken.Kind == TomlTokenKind.Bare ? keyToken.Text[(keyToken.Text.LastIndexOf('.') + 1)..] : keyToken.Text;
            index = ReadValue(tokens, equals + 1, name, keyToken, text, found);
        }

        return found;
    }

    // A table header, [name] or [[name]]: the tokens up to the closing bracket.
    private static int SkipHeader(List<TomlToken> tokens, int start)
    {
        int index = start;
        while (index < tokens.Count && !IsPunctuation(tokens[index], "]"))
        {
            index++;
        }

        while (index < tokens.Count && IsPunctuation(tokens[index], "]"))
        {
            index++;
        }

        return index;
    }

    // Reads the value that starts at the index and returns the index after it.
    private static int ReadValue(List<TomlToken> tokens, int start, string name, TomlToken keyToken, string text, List<(TomlToken, string)> found)
    {
        if (start >= tokens.Count)
        {
            return start;
        }

        TomlToken first = tokens[start];
        if (first.Kind == TomlTokenKind.Bare && FlagKeys.Contains(name) && first.Text == "true")
        {
            found.Add((keyToken, $"{name} = true"));
        }

        if (first.Kind == TomlTokenKind.String && PatternKeys.Contains(name))
        {
            found.Add((first, $"{name} {text.Substring(first.Offset, first.Length)}"));
        }

        if (!IsPunctuation(first, "[") && !IsPunctuation(first, "{"))
        {
            return start + 1;
        }

        return ReadContainer(tokens, start, name, text, found);
    }

    // An array or an inline table: a string directly inside an array of a pattern key is an exclusion; a nested container is
    // skipped.
    private static int ReadContainer(List<TomlToken> tokens, int start, string name, string text, List<(TomlToken, string)> found)
    {
        bool isPatternArray = IsPunctuation(tokens[start], "[") && PatternKeys.Contains(name);
        int depth = 0;
        int index = start;
        for (; index < tokens.Count; index++)
        {
            TomlToken token = tokens[index];
            if (IsPunctuation(token, "[") || IsPunctuation(token, "{"))
            {
                depth++;
            }
            else if (IsPunctuation(token, "]") || IsPunctuation(token, "}"))
            {
                depth--;
            }
            else if (isPatternArray && depth == 1 && token.Kind == TomlTokenKind.String)
            {
                found.Add((token, $"{name} {text.Substring(token.Offset, token.Length)}"));
            }

            if (depth == 0)
            {
                return index + 1;
            }
        }

        return index;
    }

    private static bool IsPunctuation(TomlToken token, string text) => token.Kind == TomlTokenKind.Punctuation && token.Text == text;
}
