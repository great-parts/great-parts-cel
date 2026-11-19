using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The traces of the project plans in the texts of the repository (<c>docs/design/eng-checks-source.md</c>, "traces"): codes of
/// decision records, requirements, fitness functions, tasks and themes, names of the documents of the specification, and Cyrillic
/// letters. The repository is public and in English, and it names none of these.
/// </summary>
internal static class ConceptTraces
{
    /// <summary>
    /// The files in which Cyrillic letters are the subject of a test: a name that a test feeds to the library, a letter whose class a
    /// table of cases shows. Only the letters are allowed there; the other traces are found in these files too. Adding a file is a
    /// change of <c>docs/design/eng-checks-source.md</c> and of this list in one change set.
    /// </summary>
    internal static readonly IReadOnlyList<string> CyrillicTestData =
    [
        "tests/Great.Parts.Cel.Tests/Declarations/BuildChecksTests.cs",
        "tests/Great.Parts.Cel.Tests/Examples/Library/MatchesClassTests.cs",
        "tests/Great.Parts.Cel.Tests/Regex/DfaEquivalenceTests.cs",
    ];

    // The directories and the files that are read: the sources, the tests, the documents and the files that go into the packages.
    private static readonly string[] Directories = ["src/", "tests/", "docs/", "eng/packages/"];
    private static readonly string[] RootFiles = ["README.md", "CHANGELOG.md"];

    // The files that hold text; the data of the tests that are not text (archives, binary messages, pictures) are left out.
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".md", ".txt", ".json", ".csproj", ".props", ".targets", ".proto", ".textproto", ".xml", ".html", ".css", ".js", ".mjs",
        ".razor", ".yml", ".yaml", ".sh", ".slnx", ".toml",
    };

    // The marker of a test that belongs to a requirement: the one place where a requirement code is allowed.
    private static readonly Regex RequirementTrait = new(
        @"Trait\(\s*""Requirement""\s*,\s*""[^""]*""\s*\)",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    // The codes are found as whole words: a letter, a digit, an underscore, a plus or a hyphen before the code makes it part of
    // another word (UTF-8, x-F-1), and a letter or a digit after it too.
    private static readonly (Regex Pattern, string What)[] Codes =
    [
        (new(@"(?<![A-Za-z0-9_-])ADR-\d+(?![A-Za-z0-9])", RegexOptions.CultureInvariant), "a code of a decision record"),
        (new(@"(?<![A-Za-z0-9_-])FF-\d+(?![A-Za-z0-9])", RegexOptions.CultureInvariant), "a code of a fitness function"),
        (new(@"(?<![A-Za-z0-9_+-])(?:\+R-\d+|[FSRUPQ]-\d+)(?![A-Za-z0-9])", RegexOptions.CultureInvariant), "a code of a requirement"),
        (new(@"(?<![A-Za-z0-9_-])(?:TASK|THEME)-\d+(?![A-Za-z0-9])", RegexOptions.CultureInvariant), "a code of a task or a theme"),
        (new(@"(?<![A-Za-z0-9_-])c[1-7]-[a-z][a-z0-9-]*(?:\.md)?(?![A-Za-z0-9-])", RegexOptions.CultureInvariant), "the name of a document of the specification"),
    ];

    private static readonly Regex Cyrillic = new(@"[Ѐ-ӿ]", RegexOptions.CultureInvariant);

    /// <summary>Whether the file is read by the check: a text file of the sources, the tests, the documents or the packages.</summary>
    /// <param name="path">The path relative to the root, with <c>/</c>.</param>
    internal static bool IsScanned(string path)
    {
        bool inScope = RootFiles.Contains(path, StringComparer.Ordinal) || Directories.Any(directory => path.StartsWith(directory, StringComparison.Ordinal));
        if (!inScope || GeneratedCode.IsInAllowedDirectory(path))
        {
            return false;
        }

        int dot = path.LastIndexOf('.');
        return dot >= 0 && Extensions.Contains(path[dot..]);
    }

    /// <summary>Finds the traces in the text of one file.</summary>
    /// <param name="path">The path relative to the root, with <c>/</c>.</param>
    /// <param name="text">The text of the file.</param>
    internal static IEnumerable<Finding> Scan(string path, string text)
    {
        bool cyrillicAllowed = CyrillicTestData.Contains(path, StringComparer.Ordinal);
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            // The marker of a requirement is blanked, so the columns of the rest of the line stay as they are.
            string line = RequirementTrait.Replace(lines[index], match => new string(' ', match.Length));
            int number = index + 1;
            foreach ((Regex pattern, string what) in Codes)
            {
                foreach (Match match in pattern.Matches(line))
                {
                    yield return new Finding(path, number, match.Index + 1, FindingSeverity.Error, EngCodes.Eng103, $"'{match.Value}' is {what} of the plans of the project; the texts of the repository name none");
                }
            }

            if (!cyrillicAllowed && Cyrillic.Match(line) is { Success: true } letter)
            {
                yield return new Finding(
                    path,
                    number,
                    letter.Index + 1,
                    FindingSeverity.Error,
                    EngCodes.Eng103,
                    string.Create(CultureInfo.InvariantCulture, $"the Cyrillic letter U+{(int)letter.Value[0]:X4} is in a text of the repository, which is in English"));
            }
        }
    }
}
