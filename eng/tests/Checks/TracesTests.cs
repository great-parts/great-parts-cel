using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using TestRoot = Great.Parts.Cel.Eng.Tests.Support.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks traces` (docs/design/eng-checks-source.md, "traces"): the codes of decision records, requirements, fitness functions,
// tasks and themes, the names of the documents of the specification and Cyrillic letters in the texts of the repository. Every test
// writes the files of its example into a temporary repository and checks the path, the line, the column and the message.
public sealed class TracesTests
{
    private const string Plans = "of the plans of the project; the texts of the repository name none";

    [Fact]
    public void TracesCommand_IsInTheRegistryAndTakesNoArgument()
    {
        Assert.Contains(CommandRegistry.All, command => command is TracesCommand && command.Name == "traces" && command.ArgumentName is null);
    }

    [Theory]
    [InlineData("// See ADR-4 for the choice.", "ADR-4", "a code of a decision record")]
    [InlineData("// The fitness function FF-45 holds.", "FF-45", "a code of a fitness function")]
    [InlineData("// The radar item +R-8.", "+R-8", "a code of a requirement")]
    [InlineData("// Done in TASK-842.", "TASK-842", "a code of a task or a theme")]
    [InlineData("// Decided in THEME-780.", "THEME-780", "a code of a task or a theme")]
    [InlineData("// See c2-cost.md for the rules.", "c2-cost.md", "the name of a document of the specification")]
    [InlineData("// See c4-fitness-functions.", "c4-fitness-functions", "the name of a document of the specification")]
    public static void Trace_InAComment_IsEng103AtItsFirstCharacter(string line, string code, string what)
    {
        using TempDirectory root = new();
        root.Write("src/Great.Parts.Cel/A.cs", Text("class A", "{", line, "}"));

        FindingAssert.Exactly(Run(root), Trace("src/Great.Parts.Cel/A.cs", 3, line.IndexOf(code, StringComparison.Ordinal) + 1, code, what));
    }

    // Every family of requirement codes: the letter, a hyphen and a number.
    [Fact]
    public void RequirementCode_OfEveryFamily_IsEng103()
    {
        foreach (string family in new[] { "F", "S", "R", "U", "P", "Q" })
        {
            using TempDirectory root = new();
            root.Write("docs/a.md", Text($"Requirement {family}-15."));

            FindingAssert.Exactly(Run(root), Trace("docs/a.md", 1, 13, $"{family}-15", "a code of a requirement"));
        }
    }

    [Fact]
    public void Trace_SeveralInOneLineAndInSeveralFiles_AreEachAFinding()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", Text("# A", "", "See F-1 and ADR-2."));
        root.Write("README.md", Text("TASK-3"));

        FindingAssert.Exactly(
            Run(root),
            Trace("docs/a.md", 3, 5, "F-1", "a code of a requirement"),
            Trace("docs/a.md", 3, 13, "ADR-2", "a code of a decision record"),
            Trace("README.md", 1, 1, "TASK-3", "a code of a task or a theme"));
    }

    [Theory]
    [InlineData("src/Great.Parts.Cel/A.cs")]
    [InlineData("tests/Great.Parts.Cel.Tests/ATests.cs")]
    [InlineData("docs/a.md")]
    [InlineData("README.md")]
    [InlineData("CHANGELOG.md")]
    [InlineData("eng/packages/Great.Parts.Cel.AGENTS.md")]
    [InlineData("eng/packages/Great.Parts.Cel.txt")]
    [InlineData("src/Great.Parts.Cel/B.csproj")]
    public static void Trace_InTheFilesThatAreRead_IsAFinding(string path)
    {
        using TempDirectory root = new();
        root.Write(path, "ADR-1\n");

        FindingAssert.Exactly(Run(root), Trace(path, 1, 1, "ADR-1", "a code of a decision record"));
    }

    [Theory]
    [InlineData("eng/checks/A.cs")]
    [InlineData("eng/tests/ATests.cs")]
    [InlineData("AGENTS.md")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData("src/Great.Parts.Cel/Library/Unicode/UnicodeTables.Names.g.cs")]
    [InlineData("tests/Great.Parts.Cel.Generator.Tests/Snapshots/A.cs")]
    [InlineData("src/Great.Parts.Cel/data.bin")]
    [InlineData("tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip")]
    public static void Trace_InTheFilesThatAreNotRead_IsNoFinding(string path)
    {
        using TempDirectory root = new();
        root.Write(path, "ADR-1 TASK-1 F-1\n");

        FindingAssert.None(Run(root));
    }

    // The marker of a test that belongs to a requirement is the one place of a requirement code; any other code on the line is found.
    [Fact]
    public void RequirementTrait_IsAllowedAndKeepsTheColumnsOfTheRestOfTheLine()
    {
        using TempDirectory root = new();
        root.Write(
            "tests/Great.Parts.Cel.Tests/ATests.cs",
            Text(
                "[Trait(\"Requirement\", \"F-15\")]",
                "[Trait( \"Requirement\" , \"S-1\" )] // ADR-9",
                "[Trait(\"Category\", \"F-15\")]"));

        FindingAssert.Exactly(
            Run(root),
            Error("tests/Great.Parts.Cel.Tests/ATests.cs", 2, 37, $"'ADR-9' is a code of a decision record {Plans}"),
            Error("tests/Great.Parts.Cel.Tests/ATests.cs", 3, 21, $"'F-15' is a code of a requirement {Plans}"));
    }

    // Names that contain a letter and a number but are not codes: other words, numbers of a format, an exponent.
    [Theory]
    [InlineData("UTF-8 and SHA-1 and ISO-8601")]
    [InlineData("mantissa + \"E-6143\"")]
    [InlineData("x-F-1 and c-S-2 and _R-3")]
    [InlineData("F-1a and ADR-1x and TASK-1b")]
    [InlineData("ADR and ADR-N and F-N and TASK-N and THEME-N")]
    [InlineData("IL2xxx and CS0103 and RFC 9636 and X-1")]
    [InlineData("a c8-text and c0-text and c2- and c2-*.md")]
    public static void TextThatIsNotACode_IsNoFinding(string line)
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", Text(line));

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void CyrillicLetter_InAFile_IsOneFindingAtTheFirstLetterOfEachLine()
    {
        using TempDirectory root = new();
        root.Write("src/Great.Parts.Cel/A.cs", Text("class A", "{", "    // пример и ещё", "    string s = \"я\";", "}"));

        FindingAssert.Exactly(
            Run(root),
            Error("src/Great.Parts.Cel/A.cs", 3, 8, "the Cyrillic letter U+043F is in a text of the repository, which is in English"),
            Error("src/Great.Parts.Cel/A.cs", 4, 17, "the Cyrillic letter U+044F is in a text of the repository, which is in English"));
    }

    // In a file of the test data the letters are allowed, as they are the subject of the test, and the other traces are not.
    [Fact]
    public void CyrillicLetter_InAFileOfTheTestData_IsAllowedAndOtherTracesAreNot()
    {
        string path = ConceptTraces.CyrillicTestData[0];
        using TempDirectory root = new();
        root.Write(path, Text("[InlineData(\"счёт\")]", "// F-1"));
        root.Write("tests/Great.Parts.Cel.Tests/Other.cs", Text("[InlineData(\"счёт\")]"));

        FindingAssert.Exactly(
            Run(root),
            Error(path, 2, 4, $"'F-1' is a code of a requirement {Plans}"),
            Error("tests/Great.Parts.Cel.Tests/Other.cs", 1, 14, "the Cyrillic letter U+0441 is in a text of the repository, which is in English"));
    }

    [Fact]
    public void CyrillicTestData_AreFilesOfTheRepositoryThatHaveCyrillicLetters()
    {
        string root = TestRoot.Find();

        foreach (string path in ConceptTraces.CyrillicTestData)
        {
            string text = File.ReadAllText(Path.Combine(root, path));
            Assert.True(text.Any(letter => letter is >= 'Ѐ' and <= 'ӿ'), path);
        }
    }

    [Fact]
    public void CarriageReturns_DoNotMoveTheColumns()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", "# A\r\n\r\nSee TASK-5\r\n");

        FindingAssert.Exactly(Run(root), Error("docs/a.md", 3, 5, $"'TASK-5' is a code of a task or a theme {Plans}"));
    }

    [Fact]
    public void Findings_ExitCodeIsOneAndTheLinesHaveTheCommonForm()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", "ADR-3\n");
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new();

        int exitCode = Program.Run(["traces", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal($"docs/a.md(1,1): error {EngCodes.Eng103}: 'ADR-3' is a code of a decision record {Plans}\n", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void RepositoryWithoutTraces_ExitCodeIsZero()
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", "# A\n\nText.\n");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["traces", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, output.ToString());
    }

    private static IReadOnlyList<Finding> Run(TempDirectory root) =>
        [.. new TracesCommand().Run(new CheckContext(root.Path, new Dictionary<string, string>()))];

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    private static Finding Trace(string path, int line, int column, string code, string what) =>
        Error(path, line, column, $"'{code}' is {what} {Plans}");

    private static Finding Error(string path, int line, int column, string message) =>
        FindingAssert.Error(EngCodes.Eng103, path, line, column, message);
}
