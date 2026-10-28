using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks ratchet` (docs/design/eng-checks-source.md, "ratchet"). Every test writes the files of its example into a temporary
// repository and checks the code, the path, the line, the column and the message of each finding; FindingAssert also checks that
// every line has the common form. The temporary repositories are not git repositories unless the test makes one, so the
// messages name every place of a key, as they do without history.
public sealed class RatchetTests
{
    private const string Path = "eng/ratchet.json";
    private const string Reason = " // The planner is a table of cases";

    // The baseline of the examples: the key `suppressions` is on line 4, the first entry of it on line 5, column 9.
    private static readonly string Empty = Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 0,", "    \"suppressions\": {}", "}");

    // ---- ENG602: more suppressions than the baseline ----

    [Fact]
    public void SuppressionsAboveTheBaseline_AreEng602AtTheKeyAndNameEveryPlaceWithoutHistory()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason, "class A { }"));
        root.Write("src/B.cs", Text("class B { }", "#pragma warning disable CA1502" + Reason));
        root.Write(Path, Baseline("CA1502", 1));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng602, Path, 5, 9, "suppressions of CA1502: 2, baseline 1; new: src/A.cs(1,1), src/B.cs(2,1)"));
    }

    [Fact]
    public void SuppressionsOfAKeyThatTheBaselineLacks_AreEng602AtTheKeySuppressions()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("class A { }", "#nullable disable" + Reason));
        root.Write(Path, Empty);

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng602, Path, 4, 5, "suppressions of nullable: 1, baseline 0; new: src/A.cs(2,1)"));
    }

    [Fact]
    public void SuppressionsOfSeveralKeys_AreOneFindingForEachKeyThatGrew()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502, IDE0060" + Reason, "#pragma warning disable" + Reason));
        root.Write(Path, Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 0,", "    \"suppressions\": {", "        \"CA1502\": 1,", "        \"IDE0060\": 1", "    }", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng602, Path, 4, 5, "suppressions of pragma: 1, baseline 0; new: src/A.cs(2,1)"));
    }

    [Fact]
    public void SuppressionsWithoutAReason_AreNotCounted()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502"));
        root.Write(Path, Empty);

        FindingAssert.None(Run(root));
    }

    // ---- New places by git ----

    [Fact]
    public void SuppressionsAboveTheBaseline_WithHistory_NameOnlyThePlacesOnLinesAddedAgainstTheMergeBase()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason, "class A { }"));
        root.Write(Path, Baseline("CA1502", 1));
        Commit(root, "main", "base");
        Git.Run(root.Path, "switch", "-c", "feature");
        root.Write("src/A.cs", Text("class Z { }", "#pragma warning disable CA1502" + Reason, "class A { }"));
        root.Write("src/B.cs", Text("class B { }", "#pragma warning disable CA1502" + Reason));
        Commit(root, "feature", "grow");

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng602, Path, 5, 9, "suppressions of CA1502: 2, baseline 1; new: src/B.cs(2,1)"));
    }

    [Fact]
    public void SuppressionsAboveTheBaseline_WithHistory_NameThePlacesOfAFileThatIsNotYetAddedToGit()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason));
        root.Write(Path, Baseline("CA1502", 1));
        Commit(root, "main", "base");
        root.Write("src/B.cs", Text("#pragma warning disable CA1502" + Reason));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng602, Path, 5, 9, "suppressions of CA1502: 2, baseline 1; new: src/B.cs(1,1)"));
    }

    [Fact]
    public void SuppressionsAboveTheBaseline_WithHistoryAndNoPlaceAdded_NameEveryPlace()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason, "#pragma warning disable CA1502" + Reason));
        root.Write(Path, Baseline("CA1502", 1));
        Commit(root, "main", "base");

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng602, Path, 5, 9, "suppressions of CA1502: 2, baseline 1; new: src/A.cs(1,1), src/A.cs(2,1)"));
    }

    [Fact]
    public void AddedLines_AreTheNewSideOfTheHunksOfTheDiff()
    {
        const string Diff =
            "diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs\n@@ -3,0 +4,2 @@ class A\n+x\n+y\n@@ -9 +11 @@\n-a\n+b\n@@ -20,2 +22,0 @@\n"
            + "diff --git a/src/Gone.cs b/src/Gone.cs\n--- a/src/Gone.cs\n+++ /dev/null\n@@ -1,2 +0,0 @@\n"
            + "diff --git a/docs/My file.md b/docs/My file.md\n--- a/docs/My file.md\n+++ b/docs/My file.md\t\n@@ -1 +1,3 @@\n";

        Dictionary<string, List<(int First, int Last)>> ranges = AddedLines.ParseDiff(Diff);

        Assert.Equal([(4, 5), (11, 11)], ranges["src/A.cs"]);
        Assert.Equal([(1, 3)], ranges["docs/My file.md"]);
        Assert.False(ranges.ContainsKey("src/Gone.cs"));
    }

    [Fact]
    public void WithoutRepository_ThereIsNoHistory()
    {
        using TempDirectory root = new();

        Assert.False(AddedLines.Read(root.Path).HasHistory);
    }

    // ---- ENG603: more duplicates than the baseline ----

    [Fact]
    public void DuplicatesAboveTheBaseline_AreEng603AtTheKeyAndNameTheirPlaces()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", "x"));
        root.Write("src/B.cs", ClassFile("B", "y"));
        root.Write(Path, Empty);

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng603, Path, 3, 5, "duplicate pairs: 1, baseline 0; new: src/A.cs(1,1) with src/B.cs(1,1)"));
    }

    [Fact]
    public void DuplicatesAboveTheBaseline_WithHistory_NameOnlyThePairsWithAPlaceOnAnAddedLine()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", "x"));
        root.Write("src/B.cs", ClassFile("B", "y"));
        root.Write(Path, Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 1,", "    \"suppressions\": {}", "}"));
        Commit(root, "main", "base");
        Git.Run(root.Path, "switch", "-c", "feature");
        root.Write("src/C.cs", ClassFile("C", "z"));
        Commit(root, "feature", "grow");

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng603, Path, 3, 5, "duplicate pairs: 3, baseline 1; new: src/A.cs(1,1) with src/C.cs(1,1), src/B.cs(1,1) with src/C.cs(1,1)"));
    }

    [Fact]
    public void DuplicatesWithinTheBaseline_AreNotAFinding()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", "x"));
        root.Write("src/B.cs", ClassFile("B", "y"));
        root.Write(Path, Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 1,", "    \"suppressions\": {}", "}"));

        FindingAssert.None(Run(root));
    }

    // ---- ENG604: thresholds that differ ----

    [Fact]
    public void ThresholdThatDiffersFromTheFile_IsEng604AtTheKeyCodeMetrics()
    {
        using TempDirectory root = new();
        root.Write("CodeMetricsConfig.txt", "CA1502: 15\n");
        root.Write(Path, Text("{", "    \"codeMetrics\": {", "        \"CA1502\": 20", "    },", "    \"duplicates\": 0,", "    \"suppressions\": {}", "}"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng604, Path, 2, 5, "code metrics threshold differs: CodeMetricsConfig.txt sets CA1502 to 15, baseline 20"));
    }

    [Fact]
    public void ThresholdsThatOneSideLacks_AreEng604ForEachKey()
    {
        using TempDirectory root = new();
        root.Write("CodeMetricsConfig.txt", "CA1502: 15\nCA1506(Type): 90\n");
        root.Write(Path, Text("{", "    \"codeMetrics\": {", "        \"CA1502\": 15,", "        \"CA1505\": 10", "    },", "    \"duplicates\": 0,", "    \"suppressions\": {}", "}"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng604, Path, 2, 5, "code metrics threshold differs: baseline sets CA1505 to 10, CodeMetricsConfig.txt has no CA1505"),
            Error(EngCodes.Eng604, Path, 2, 5, "code metrics threshold differs: CodeMetricsConfig.txt sets CA1506(Type) to 90, the baseline has no CA1506(Type)"));
    }

    [Fact]
    public void ThresholdsInTheFileAndTheBaseline_AreNotAFinding()
    {
        using TempDirectory root = new();
        root.Write("CodeMetricsConfig.txt", "CA1502: 15\r\nCA1506(Type): 90\r\n\r\n");
        root.Write(Path, Text("{", "    \"codeMetrics\": {", "        \"CA1502\": 15,", "        \"CA1506(Type)\": 90", "    },", "    \"duplicates\": 0,", "    \"suppressions\": {}", "}"));

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void BaselineWithThresholdsAndNoFile_IsEng604()
    {
        using TempDirectory root = new();
        root.Write(Path, Text("{", "    \"codeMetrics\": {", "        \"CA1502\": 15", "    },", "    \"duplicates\": 0,", "    \"suppressions\": {}", "}"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng604, Path, 2, 5, "code metrics threshold differs: baseline sets CA1502 to 15, CodeMetricsConfig.txt has no CA1502"));
    }

    // ---- ENG605: numbers below the baseline ----

    [Fact]
    public void SuppressionsBelowTheBaseline_AreEng605AtTheKeyAndAskToLowerIt()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason));
        root.Write(Path, Baseline("CA1502", 3));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng605, Path, 5, 9, "suppressions of CA1502: 1, baseline 3; lower the baseline to 1"));
    }

    [Fact]
    public void SuppressionsThatAreGone_AreEng605AndAskToRemoveTheKey()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("class A { }"));
        root.Write(Path, Baseline("CA1502", 2));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng605, Path, 5, 9, "suppressions of CA1502: 0, baseline 2; remove the key"));
    }

    [Fact]
    public void DuplicatesBelowTheBaseline_AreEng605AtTheKeyDuplicates()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", "x"));
        root.Write(Path, Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 4,", "    \"suppressions\": {}", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng605, Path, 3, 5, "duplicate pairs: 0, baseline 4; lower the baseline to 0"));
    }

    // ---- A missing input is exit code 2, not a finding ----

    [Fact]
    public void RatchetWithoutTheBaseline_ExitsWithTwoAndNamesTheFileOnStandardError()
    {
        using TempDirectory root = new();
        root.Write("CodeMetricsConfig.txt", "CA1502: 15\n");

        Result result = Invoke(root, "ratchet");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal("eng/checks: eng/ratchet.json does not exist\n", result.Error);
    }

    [Fact]
    public void DuplicatesWithoutTheBaseline_ExitsWithTwoAndNamesTheFileOnStandardError()
    {
        using TempDirectory root = new();
        root.Write("src/B.cs", ClassFile("B", "x"));
        root.Write("src/C.cs", ClassFile("C", "y"));

        Result result = Invoke(root, "duplicates");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal("eng/checks: eng/ratchet.json does not exist\n", result.Error);
    }

    [Fact]
    public void RatchetWithoutTheCodeMetricsFile_ExitsWithTwoAndNamesTheFileOnStandardError()
    {
        using TempDirectory root = new();
        root.Write(Path, Empty);

        Result result = Invoke(root, "ratchet");

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal("eng/checks: CodeMetricsConfig.txt does not exist\n", result.Error);
    }

    // ---- ENG600: the file is not valid ----

    [Fact]
    public void UnknownKey_IsEng600AtTheKey()
    {
        using TempDirectory root = new();
        root.Write(Path, Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 0,", "    \"extra\": 1,", "    \"suppressions\": {}", "}"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng600, Path, 4, 5, "unknown key 'extra'; the keys are codeMetrics, duplicates and suppressions"));
    }

    [Theory]
    [InlineData("", 1, 1, "the file is not valid JSON")]
    [InlineData("not JSON", 1, 2, "the file is not valid JSON")]
    [InlineData("{\n    \"duplicates\": 0,\n}\n", 3, 1, "the file is not valid JSON")]
    [InlineData("{\n    \"duplicates\": \n}\n", 3, 1, "the file is not valid JSON")]
    [InlineData("{\n    // note\n    \"duplicates\": 0\n}\n", 2, 5, "the file is not valid JSON")]
    [InlineData("{\n    \"duplicates\": 0\n}\n{}\n", 4, 1, "the file is not valid JSON")]
    [InlineData("{\n    \"duplicates\": 0\n", 3, 1, "the file is not valid JSON")]
    [InlineData("[]\n", 1, 1, "the file must hold one JSON object")]
    [InlineData("{\n    \"duplicates\": 0,\n    \"duplicates\": 1\n}\n", 3, 5, "repeated key 'duplicates'")]
    [InlineData("{\n    \"codeMetrics\": {},\n    \"duplicates\": 0,\n    \"suppressions\": {},\n    \"suppressions\": {}\n}\n", 5, 5, "repeated key 'suppressions'")]
    [InlineData("{\n    \"codeMetrics\": {},\n    \"duplicates\": 0,\n    \"suppressions\": {\n        \"CA1502\": 1,\n        \"CA1502\": 2\n    }\n}\n", 6, 9, "repeated key 'CA1502'")]
    [InlineData("{\n    \"duplicates\": 0,\n    \"codeMetrics\": {}\n}\n", 3, 5, "key 'codeMetrics' is out of order: it must come before 'duplicates'")]
    [InlineData("{\n    \"suppressions\": {\n        \"IDE0060\": 1,\n        \"CA1502\": 1\n    }\n}\n", 4, 9, "key 'CA1502' is out of order: it must come before 'IDE0060'")]
    [InlineData("{\n    \"codeMetrics\": {\n        \"CA1502\": 15,\n        \"CA1502(Type)\": 15,\n        \"CA1501\": 3\n    }\n}\n", 5, 9, "key 'CA1501' is out of order: it must come before 'CA1502(Type)'")]
    public static void InvalidFile_IsEng600AtThePlaceOfTheProblem(string json, int line, int column, string message)
    {
        using TempDirectory root = new();
        root.Write(Path, json);

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng600, Path, line, column, message));
    }

    [Theory]
    [InlineData("\"duplicates\": -1", 2, "duplicates")]
    [InlineData("\"duplicates\": 1.5", 2, "duplicates")]
    [InlineData("\"duplicates\": \"3\"", 2, "duplicates")]
    [InlineData("\"duplicates\": null", 2, "duplicates")]
    [InlineData("\"duplicates\": true", 2, "duplicates")]
    [InlineData("\"duplicates\": [1]", 2, "duplicates")]
    [InlineData("\"duplicates\": {\"a\": 1}", 2, "duplicates")]
    [InlineData("\"duplicates\": 3000000000", 2, "duplicates")]
    [InlineData("\"duplicates\": 1e2", 2, "duplicates")]
    public static void ValueThatIsNotANonNegativeInteger_IsEng600AtTheKey(string entry, int line, string key)
    {
        using TempDirectory root = new();
        root.Write(Path, Text("{", "    " + entry, "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng600, Path, line, 5, $"the value of '{key}' must be an integer that is not negative"));
    }

    [Theory]
    [InlineData("suppressions", "CA1502", "-2")]
    [InlineData("suppressions", "CA1502", "\"2\"")]
    [InlineData("suppressions", "CA1502", "2.5")]
    [InlineData("codeMetrics", "CA1502", "-15")]
    [InlineData("codeMetrics", "CA1502", "null")]
    public static void EntryValueThatIsNotANonNegativeInteger_IsEng600AtTheKeyOfTheEntry(string section, string key, string value)
    {
        using TempDirectory root = new();
        root.Write(Path, Text("{", $"    \"{section}\": {{", $"        \"{key}\": {value}", "    }", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng600, Path, 3, 9, $"the value of '{key}' must be an integer that is not negative"));
    }

    [Fact]
    public void SuppressionsEntryWithZero_IsEng600ButAThresholdOfZeroIsNot()
    {
        using TempDirectory root = new();
        root.Write("CodeMetricsConfig.txt", "CA1502: 0\n");
        root.Write(Path, Text("{", "    \"codeMetrics\": {", "        \"CA1502\": 0", "    },", "    \"suppressions\": {", "        \"CA1062\": 0", "    }", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng600, Path, 6, 9, "'CA1062' is 0 in suppressions; a rule without a place is not written"));
    }

    [Theory]
    [InlineData("suppressions")]
    [InlineData("codeMetrics")]
    public static void SectionThatIsNotAnObject_IsEng600AtTheKeyOfTheSection(string section)
    {
        using TempDirectory root = new();
        root.Write(Path, Text("{", $"    \"{section}\": 3", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng600, Path, 2, 5, $"the value of '{section}' must be an object of numbers"));
    }

    [Fact]
    public void SeveralProblems_AreOneFindingForEach_AndNoNumberIsCompared()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason));
        root.Write(Path, Text("{", "    \"duplicates\": 1.5,", "    \"extra\": 0,", "    \"suppressions\": {}", "}"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng600, Path, 2, 5, "the value of 'duplicates' must be an integer that is not negative"),
            Error(EngCodes.Eng600, Path, 3, 5, "unknown key 'extra'; the keys are codeMetrics, duplicates and suppressions"));
    }

    [Fact]
    public void PositionsCountUtf16UnitsAndLinesEndWithAnyOfTheThreeBreaks()
    {
        using TempDirectory root = new();
        root.Write(Path, "{\"codeMetrics\": {\"é\": 1}, \"duplicates\": \"x\"}");
        using TempDirectory crlf = new();
        crlf.Write(Path, "{\r\n    \"codeMetrics\": {},\r\n    \"duplicates\": 0,\r\n    \"suppressions\": {\r\n        \"CA1062\": 0\r\n    }\r\n}\r\n");

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng600, Path, 1, 27, "the value of 'duplicates' must be an integer that is not negative"));
        FindingAssert.Exactly(Run(crlf), Error(EngCodes.Eng600, Path, 5, 9, "'CA1062' is 0 in suppressions; a rule without a place is not written"));
    }

    [Fact]
    public void FileWithAByteOrderMark_IsRead()
    {
        RatchetFile file = RatchetFile.Parse([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Baseline("CA1502", 2))]);

        Assert.Empty(file.Problems);
        Assert.Equal([new RatchetEntry("CA1502", 2, new Position(5, 9))], file.Suppressions);
    }

    // ---- The file ----

    [Fact]
    public void ValidFile_GivesTheNumbersWithThePlaceOfEveryKey()
    {
        RatchetFile file = RatchetFile.Parse(Encoding.UTF8.GetBytes(Text(
            "{", "    \"codeMetrics\": {", "        \"CA1502\": 15", "    },", "    \"duplicates\": 16,", "    \"suppressions\": {", "        \"CA1065\": 1,", "        \"pragma\": 2", "    }", "}")));

        Assert.Empty(file.Problems);
        Assert.Equal(16, file.Duplicates);
        Assert.Equal([new RatchetEntry("CA1502", 15, new Position(3, 9))], file.CodeMetrics);
        Assert.Equal([new RatchetEntry("CA1065", 1, new Position(7, 9)), new RatchetEntry("pragma", 2, new Position(8, 9))], file.Suppressions);
        Assert.Equal(new Position(2, 5), file.CodeMetricsAt);
        Assert.Equal(new Position(5, 5), file.DuplicatesAt);
        Assert.Equal(new Position(6, 5), file.SuppressionsAt);
    }

    [Theory]
    [InlineData("{\"codeMetrics\": {\"CA1502\": 15}, \"duplicates\": 7, \"suppressions\": {}}", 7)]
    [InlineData("{\"duplicates\": 0}", 0)]
    [InlineData("{\"suppressions\": {\"duplicates\": 9}}", 0)]
    [InlineData("{\"duplicates\": -1}", 0)]
    [InlineData("{\"duplicates\": 1.5}", 0)]
    [InlineData("{\"duplicates\": \"3\"}", 0)]
    [InlineData("{\"duplicates\": ", 0)]
    [InlineData("{\"duplicates\": 2, \"duplicates\": 5}", 2)]
    [InlineData("{}", 0)]
    [InlineData("", 0)]
    public static void DuplicatesBaseline_IsTheTopLevelNumberOrZero(string json, int expected) =>
        Assert.Equal(expected, RatchetFile.Parse(Encoding.UTF8.GetBytes(json)).Duplicates);

    // ---- The command ----

    [Fact]
    public void ValidBaselineThatMatchesTheRepository_HasNoFindings()
    {
        using TempDirectory root = new();
        root.Write("CodeMetricsConfig.txt", "CA1502: 15\n");
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason, "#nullable disable" + Reason, "class A { }"));
        root.Write("src/B.cs", ClassFile("B", "x"));
        root.Write("src/C.cs", ClassFile("C", "y"));
        root.Write(Path, Text("{", "    \"codeMetrics\": {", "        \"CA1502\": 15", "    },", "    \"duplicates\": 1,", "    \"suppressions\": {", "        \"CA1502\": 1,", "        \"nullable\": 1", "    }", "}"));

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void ThreeCommandsOfOneRun_ShareTheScansAndTheBaseline()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason));
        root.Write("CodeMetricsConfig.txt", string.Empty);
        root.Write(Path, Baseline("CA1502", 1));
        CheckContext context = Context(root);

        _ = new SuppressionsCommand().Run(context).ToList();
        _ = new DuplicatesCommand().Run(context).ToList();
        File.Delete(FullPath(root, "src/A.cs"));
        File.Delete(FullPath(root, Path));
        List<Finding> findings = [.. new RatchetCommand().Run(context)];

        FindingAssert.None(findings);
        Assert.Same(SuppressionScan.Of(context), SuppressionScan.Of(context));
        Assert.Same(RatchetFile.Of(context), RatchetFile.Of(context));
    }

    [Fact]
    public void Command_IsRegisteredAndWritesTheLineOfTheFinding()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502" + Reason));
        root.Write("src/B.cs", ClassFile("B", "x"));
        root.Write("src/C.cs", ClassFile("C", "y"));
        root.Write("CodeMetricsConfig.txt", string.Empty);
        root.Write(Path, Baseline("CA1502", 0).Replace("        \"CA1502\": 0\n", string.Empty, StringComparison.Ordinal));
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["suppressions", "duplicates", "ratchet", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal(
            "eng/ratchet.json(3,5): error ENG603: duplicate pairs: 1, baseline 0; new: src/B.cs(1,1) with src/C.cs(1,1)\n"
            + "eng/ratchet.json(4,5): error ENG602: suppressions of CA1502: 1, baseline 0; new: src/A.cs(1,1)\n"
            + "src/B.cs(1,1): error ENG601: " + ClassTokens + " tokens duplicated at src/C.cs(1,1)\n",
            Sorted(output.ToString()));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Command_WithABaselineThatMatches_ExitsWithZeroAndKeepsTheWarningsOfDuplicates()
    {
        using TempDirectory root = new();
        root.Write("src/B.cs", ClassFile("B", "x"));
        root.Write("src/C.cs", ClassFile("C", "y"));
        root.Write("CodeMetricsConfig.txt", string.Empty);
        root.Write(Path, Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 1,", "    \"suppressions\": {}", "}"));
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["suppressions", "duplicates", "ratchet", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal("src/B.cs(1,1): warning ENG601: " + ClassTokens + " tokens duplicated at src/C.cs(1,1)\n", output.ToString().ReplaceLineEndings("\n"));
    }

    // ---- Helpers ----

    private const int Statements = 8;
    private const int ClassTokens = (Statements * (Statements + 5)) + 12;

    private static Finding Error(string code, string path, int line, int column, string message) =>
        FindingAssert.Error(code, path, line, column, message);

    private static CheckContext Context(TempDirectory root) => new(root.Path, new Dictionary<string, string>());

    // The command needs CodeMetricsConfig.txt as it needs the baseline; an example that does not write one gets an empty file.
    private static List<Finding> Run(TempDirectory root)
    {
        if (!File.Exists(FullPath(root, "CodeMetricsConfig.txt")))
        {
            root.Write("CodeMetricsConfig.txt", string.Empty);
        }

        return [.. new RatchetCommand().Run(Context(root))];
    }

    private static Result Invoke(TempDirectory root, string command)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = Program.Run([command, "--root", root.Path], CommandRegistry.All, root.Path, output, error);
        return new Result(exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    private readonly record struct Result(int ExitCode, string Output, string Error);

    // `eng/ratchet.json` with one entry of suppressions: the key on line 5, column 9.
    private static string Baseline(string key, int count) =>
        Text("{", "    \"codeMetrics\": {},", "    \"duplicates\": 0,", "    \"suppressions\": {", $"        \"{key}\": {count}", "    }", "}");

    // A class that repeats after the variable is renamed: 8 statements of a method are 116 tokens with the class around them.
    private static string ClassFile(string name, string variable) =>
        $"class {name}\n{{\nvoid F(int {variable})\n{{\n"
        + string.Concat(Enumerable.Range(0, Statements).Select(index => $"    {variable} = {variable}{string.Concat(Enumerable.Repeat(" + 1", index + 1))};\n"))
        + "}\n}\n";

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    private static string FullPath(TempDirectory root, string relative) => System.IO.Path.Combine(root.Path, relative);

    private static string Sorted(string output) =>
        string.Concat(output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).Select(line => line + "\n"));

    // A repository with the files of the temporary directory committed on the branch; the first commit creates `main`.
    private static void Commit(TempDirectory root, string branch, string message)
    {
        if (branch == "main")
        {
            Assert.True(Git.Run(root.Path, "init", "-q", "-b", "main").Succeeded);
        }

        Assert.True(Git.Run(root.Path, "add", "--all").Succeeded);
        GitResult result = Git.Run(root.Path, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", message);
        Assert.True(result.Succeeded, result.Error);
    }
}
