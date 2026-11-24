using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The publication line of the corpus of the filtering package (<c>docs/design/filtering-corpus.md</c>, "Publication") and the count of its
/// cases. The corpus is a set of text files of test cases, <c>test { … }</c> inside <c>section { … }</c>; the line is
/// <c>AIP-160: own test corpus, N cases, run on yyyy-MM-dd</c>, with the link of the corpus around it, and it never has a percentage: the
/// corpus has a count of cases, not a share of conformance.
/// </summary>
internal static class CorpusPublication
{
    /// <summary>The directory of the files of the corpus, relative to the repository root.</summary>
    internal const string DataDirectory = "tests/filtering/testdata/";

    /// <summary>The pages that carry the publication line, relative to the repository root.</summary>
    internal static readonly IReadOnlyList<string> Pages = ["eng/packages/Great.Parts.Cel.Filtering.README.md", "docs/conformance.md"];

    private const string Marker = "own test corpus";

    // A case begins a line with the field test; a comment begins with a number sign and a string never begins a line.
    private static readonly Regex CaseStart = new(@"^[ \t]*test[ \t]*:?[ \t]*\{", RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex Line = new(
        @"AIP-160: own test corpus, (?<count>\d+) cases, run on (?<date>\d{4}-\d{2}-\d{2})",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    /// <summary>Counts the cases of the files of the corpus in the file set.</summary>
    /// <param name="files">The files of the repository.</param>
    internal static int CountCases(IEnumerable<RepositoryFile> files) =>
        files
            .Where(file => file.Path.StartsWith(DataDirectory, StringComparison.Ordinal) && file.Path.EndsWith(".textproto", StringComparison.Ordinal))
            .Sum(file => CaseStart.Count(file.ReadText()));

    /// <summary>Checks the lines of a page that name the corpus against the count of the cases.</summary>
    /// <param name="path">The path of the page relative to the repository root.</param>
    /// <param name="text">The text of the page.</param>
    /// <param name="cases">The number of the cases of the corpus.</param>
    internal static IEnumerable<Finding> Check(string path, string text, int cases)
    {
        string[] lines = text.Split('\n');
        bool found = false;
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            int at = line.IndexOf(Marker, StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            found = true;
            int number = index + 1;
            Match match = Line.Match(line);
            if (!match.Success)
            {
                yield return Error(path, number, at + 1, "the line of the corpus is not 'AIP-160: own test corpus, N cases, run on yyyy-MM-dd'");
                continue;
            }

            int count = int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
            if (count != cases)
            {
                yield return Error(path, number, match.Index + 1, string.Create(CultureInfo.InvariantCulture, $"corpus count {count}, corpus has {cases}"));
            }

            if (!DateOnly.TryParseExact(match.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                yield return Error(path, number, match.Groups["date"].Index + 1, $"'{match.Groups["date"].Value}' is not a date");
            }

            if (line.Contains('%', StringComparison.Ordinal))
            {
                yield return Error(path, number, line.IndexOf('%', StringComparison.Ordinal) + 1, "the line of the corpus has a percentage; the corpus has a count of cases, not a share");
            }
        }

        if (!found)
        {
            yield return Error(path, 1, 1, "the page has no line 'AIP-160: own test corpus, N cases, run on yyyy-MM-dd'");
        }
    }

    private static Finding Error(string path, int line, int column, string message) =>
        new(path, line, column, FindingSeverity.Error, EngCodes.Eng307, message);
}
