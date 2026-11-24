using System;
using System.Collections.Generic;
using System.IO;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using TestRoot = Great.Parts.Cel.Eng.Tests.Support.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks corpus` (docs/design/filtering-corpus.md, "Publication"): the README of the filtering package and the page of conformance
// have the line "AIP-160: own test corpus, N cases, run on yyyy-MM-dd" with N the number of the cases of the corpus and no percentage.
// Every test writes the files of its example into a temporary repository and checks the path, the line, the column and the message.
public sealed class CorpusTests
{
    private const string Readme = "eng/packages/Great.Parts.Cel.Filtering.README.md";
    private const string Page = "docs/conformance.md";
    private const string Corpus = "tests/filtering/testdata/syntax.textproto";

    // Two cases in one section, a case with a nested resource, a comment with the word and a case written with a colon.
    private const string TwoCases = "# test { in a comment\nname: \"syntax\"\nsection {\n  name: \"s\"\n  test {\n    name: \"a\"\n    resource { value {} match: true }\n  }\n  test: {\n    name: \"b\"\n  }\n}\n";

    [Fact]
    public void CorpusCommand_IsInTheRegistryAndTakesNoArgument()
    {
        Assert.Contains(CommandRegistry.All, command => command is CorpusCommand && command.Name == "corpus" && command.ArgumentName is null);
    }

    [Fact]
    public void PagesWithTheCountOfTheCorpus_HaveNoFinding()
    {
        using TempDirectory root = Repository("AIP-160: own test corpus, 2 cases, run on 2026-11-24, see the corpus.");

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void Cases_AreCountedInEveryFileOfTheCorpusAndNowhereElse()
    {
        using TempDirectory root = Repository("AIP-160: own test corpus, 4 cases, run on 2026-11-24.");
        root.Write("tests/filtering/testdata/values.textproto", TwoCases);
        root.Write("tests/filtering/proto/other.textproto", TwoCases);
        root.Write("tests/filtering/testdata/notes.md", "test {\n");

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void Count_ThatDiffersFromTheCorpus_IsEng307AtTheLineOfTheCount()
    {
        using TempDirectory root = Repository("AIP-160: own test corpus, 3 cases, run on 2026-11-24.");

        FindingAssert.Exactly(
            Run(root),
            Error(Readme, 3, 1, "corpus count 3, corpus has 2"),
            Error(Page, 3, 1, "corpus count 3, corpus has 2"));
    }

    [Fact]
    public void PageWithoutTheLine_IsEng307AtTheStartOfThePage()
    {
        using TempDirectory root = Repository("AIP-160: own test corpus, 2 cases, run on 2026-11-24.");
        root.Write(Page, "# Conformance\n\nNothing about the filters.\n");

        FindingAssert.Exactly(Run(root), Error(Page, 1, 1, "the page has no line 'AIP-160: own test corpus, N cases, run on yyyy-MM-dd'"));
    }

    [Theory]
    [InlineData("The own test corpus has 2 cases.")]
    [InlineData("AIP-160: own test corpus, 2 cases.")]
    [InlineData("AIP-160: own test corpus, two cases, run on 2026-11-24.")]
    [InlineData("AIP-160: own test corpus, 2 cases, run on yesterday.")]
    public static void Line_ThatIsNotOfTheForm_IsEng307AtTheWordsOfTheCorpus(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        using TempDirectory root = Repository(line);

        FindingAssert.Exactly(
            Run(root),
            Error(Readme, 3, line.IndexOf("own test corpus", StringComparison.Ordinal) + 1, "the line of the corpus is not 'AIP-160: own test corpus, N cases, run on yyyy-MM-dd'"),
            Error(Page, 3, line.IndexOf("own test corpus", StringComparison.Ordinal) + 1, "the line of the corpus is not 'AIP-160: own test corpus, N cases, run on yyyy-MM-dd'"));
    }

    [Fact]
    public void DateThatIsNotADate_IsEng307AtTheDate()
    {
        using TempDirectory root = Repository("AIP-160: own test corpus, 2 cases, run on 2026-13-45.");

        FindingAssert.Exactly(
            Run(root),
            Error(Readme, 3, 43, "'2026-13-45' is not a date"),
            Error(Page, 3, 43, "'2026-13-45' is not a date"));
    }

    [Fact]
    public void Percentage_IsEng307AtThePercentSign()
    {
        string line = "AIP-160: own test corpus, 2 cases, run on 2026-11-24, 100% pass.";
        using TempDirectory root = Repository(line);
        int column = line.IndexOf('%', StringComparison.Ordinal) + 1;

        FindingAssert.Exactly(
            Run(root),
            Error(Readme, 3, column, "the line of the corpus has a percentage; the corpus has a count of cases, not a share"),
            Error(Page, 3, column, "the line of the corpus has a percentage; the corpus has a count of cases, not a share"));
    }

    [Fact]
    public void PageThatDoesNotExist_IsAnInputErrorWithExitCodeTwo()
    {
        using TempDirectory root = new();
        root.Write(Corpus, TwoCases);
        root.Write(Readme, "AIP-160: own test corpus, 2 cases, run on 2026-11-24.\n");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["corpus", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(2, exitCode);
        Assert.Equal($"eng/checks: the page {Page} does not exist\n", error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void CorpusWithoutCases_IsAnInputErrorWithExitCodeTwo()
    {
        using TempDirectory root = new();
        root.Write(Readme, "AIP-160: own test corpus, 0 cases, run on 2026-11-24.\n");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["corpus", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(2, exitCode);
        Assert.Equal("eng/checks: the directory tests/filtering/testdata/ has no case of the corpus\n", error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Findings_ExitCodeIsOneAndTheLinesHaveTheCommonForm()
    {
        using TempDirectory root = Repository("AIP-160: own test corpus, 7 cases, run on 2026-11-24.");
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new();

        int exitCode = Program.Run(["corpus", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            $"{Page}(3,1): error {EngCodes.Eng307}: corpus count 7, corpus has 2\n{Readme}(3,1): error {EngCodes.Eng307}: corpus count 7, corpus has 2\n",
            output.ToString());
    }

    // The repository itself publishes the number of the cases that its corpus has: this is the check that the build makes.
    [Fact]
    public void TheRepository_PublishesTheCountOfItsCorpus()
    {
        FindingAssert.None([.. new CorpusCommand().Run(new CheckContext(TestRoot.Find(), new Dictionary<string, string>()))]);
    }

    // A temporary repository with a corpus of two cases and the same line on the README and on the page, as the third line of each.
    private static TempDirectory Repository(string line)
    {
        TempDirectory root = new();
        root.Write(Corpus, TwoCases);
        root.Write(Readme, $"# Package\n\n{line}\n");
        root.Write(Page, $"# Conformance\n\n{line}\n");
        return root;
    }

    private static IReadOnlyList<Finding> Run(TempDirectory root) =>
        [.. new CorpusCommand().Run(new CheckContext(root.Path, new Dictionary<string, string>()))];

    private static Finding Error(string path, int line, int column, string message) =>
        FindingAssert.Error(EngCodes.Eng307, path, line, column, message);
}
