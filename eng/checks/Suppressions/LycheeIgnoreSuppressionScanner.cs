using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads <c>.lycheeignore</c> in the repository root, which lychee reads from its working directory without being told
/// (<c>docs/design/eng-checks-source.md</c>, "suppressions"). Every line that is neither empty nor a comment is a regular expression of
/// the addresses to skip, and one place under the key <c>lychee</c>. A comment line starts with <c>#</c> after optional white space;
/// a <c>#</c> inside a pattern line is part of the pattern. The reason is the comment line directly above: one comment line covers one
/// pattern, and an empty line between them leaves the pattern without a reason. A reason that only repeats the pattern is
/// <c>ENG102</c>, so the pattern is the one "rule ID" of the form.
/// </summary>
internal sealed class LycheeIgnoreSuppressionScanner : ISuppressionScanner
{
    private const string FileName = ".lycheeignore";
    private const string ReasonPlace = "comment line above";
    private const string Key = "lychee";

    /// <inheritdoc/>
    public IEnumerable<SuppressionForm> Scan(CheckContext context) => context.Files
        .Where(file => file.Path == FileName)
        .SelectMany(file => ScanFile(file.Path, file.ReadText()));

    private static IEnumerable<SuppressionForm> ScanFile(string path, string text)
    {
        using StringReader reader = new(text);
        List<string> comments = [];
        int lineNumber = 0;
        for (string? line = reader.ReadLine(); line is not null; line = reader.ReadLine())
        {
            lineNumber++;
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                comments.Add(trimmed);
                continue;
            }

            if (trimmed.Length > 0)
            {
                string pattern = trimmed.TrimEnd();
                yield return new SuppressionForm(
                    path,
                    new Position(lineNumber, line.Length - trimmed.Length + 1),
                    $"'{pattern}'",
                    ReasonPlace,
                    [Key],
                    [pattern],
                    string.Join(' ', comments));
            }

            comments.Clear();
        }
    }
}
