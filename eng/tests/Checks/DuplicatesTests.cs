using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks duplicates` (docs/design/eng-checks-source.md, "duplicates"). Every test writes the files of its example into a
// temporary repository. Statement number i of a method is `v = v + 1 + ... ;` with i + 1 terms, so no two statements are equal and
// the code is not periodic (a periodic body repeats at many shifts). A method of N statements is N * (N + 5) + 8 tokens and a
// class around it adds 4, so the examples know the length of a repeat exactly: 8 statements in a class are 116 tokens.
public sealed class DuplicatesTests
{
    private const int Statements = 8;
    private const int MethodTokens = (Statements * (Statements + 5)) + 8;
    private const int ClassTokens = MethodTokens + 4;

    // ---- What is found ----

    [Fact]
    public void RepeatAfterRenaming_IsOnePairAtTheFirstPlace()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2")));

        DuplicateScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"));
        Assert.Equal([new DuplicatePair("src/A.cs", new Position(1, 1), "src/B.cs", new Position(1, 1), ClassTokens)], scan.Pairs);
    }

    [Fact]
    public void Literals_OfOneKindAreEqualAndOfAnotherKindAreNot()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "a", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("F", "a", "22")));
        root.Write("src/C.cs", ClassFile("C", Method("F", "a", "\"text\"")));
        root.Write("src/D.cs", ClassFile("D", Method("F", "a", "'c'")));
        root.Write("src/E.cs", ClassFile("E", Method("F", "a", "1.5")));

        FindingAssert.Exactly(
            Run(root, 100),
            Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"),
            Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/E.cs(1,1)"),
            Error("src/B.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/E.cs(1,1)"));
    }

    [Theory]
    [InlineData("long")]
    [InlineData("string")]
    public static void ChangedKeyword_BreaksTheRepeat(string keyword)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "total", "1").Replace("(int", $"({keyword}", StringComparison.Ordinal)));

        FindingAssert.None(Run(root, ClassTokens));
    }

    [Fact]
    public void ChangedOperator_BreaksTheRepeat()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "total", "1").Replace("+", "-", StringComparison.Ordinal)));

        FindingAssert.None(Run(root, ClassTokens));
    }

    [Fact]
    public void RepeatOfExactlyTheWindow_IsFoundAndOneTokenLess_IsNot()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2")));

        FindingAssert.Exactly(Run(root, ClassTokens), Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"));
        FindingAssert.None(Run(root, ClassTokens + 1));
    }

    [Theory]
    [InlineData(7, 0)]
    [InlineData(8, 116)]
    public static void TheWindowOfTheCommand_IsOneHundredTokens(int statements, int expectedTokens)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1", statements)));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2", statements)));

        List<Finding> findings = [.. new DuplicatesCommand().Run(Context(root))];

        Assert.Equal(100, DuplicatesCommand.WindowTokens);
        if (expectedTokens == 0)
        {
            FindingAssert.None(findings);
        }
        else
        {
            FindingAssert.Exactly(findings, Error("src/A.cs", 1, 1, $"{expectedTokens} tokens duplicated at src/B.cs(1,1)"));
        }
    }

    [Fact]
    public void RepeatLongerThanTheWindow_IsOneRunAndOnePair()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1", 12)));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2", 12)));

        FindingAssert.Exactly(Run(root, 100), Error("src/A.cs", 1, 1, $"{(12 * 17) + 12} tokens duplicated at src/B.cs(1,1)"));
    }

    [Fact]
    public void ThreeCopies_AreThreePairs()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "a", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("F", "b", "1")));
        root.Write("src/C.cs", ClassFile("C", Method("F", "c", "1")));

        FindingAssert.Exactly(
            Run(root, 100),
            Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"),
            Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/C.cs(1,1)"),
            Error("src/B.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/C.cs(1,1)"));
    }

    // ---- What is not a token ----

    [Fact]
    public void CommentsWhiteSpaceDirectivesAndInactiveBranches_AreNotTokens()
    {
        using TempDirectory plain = new();
        plain.Write("src/A.cs", "class A { int x; }\n");
        using TempDirectory decorated = new();
        decorated.Write(
            "src/A.cs",
            Text(
                "// a comment",
                "#region Region name",
                "/* another comment */ class   A",
                "{",
                "#if SYMBOL_THAT_NO_SET_DEFINES",
                "    int hidden = 1;",
                "#else",
                "    int x; // tail comment",
                "#endif",
                "#pragma warning disable CS0169 // The field is for the example",
                "}",
                "#endregion"));

        TokenStream clean = TokenStream.Build(Context(plain).Sources.Files, 0);
        TokenStream other = TokenStream.Build(Context(decorated).Sources.Files, 0);

        Assert.Equal(7, clean.Count);
        Assert.Equal(7, other.Count);
        Assert.Equal(Kinds(clean), Kinds(other));
    }

    [Fact]
    public void RepeatWithComments_IsFoundAtTheFirstTokenNotAtTheComment()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write(
            "src/B.cs",
            "// A long comment that is not code and has many words in it, enough to be longer than any window of a search\n"
            + "#region Region\n"
            + "\n"
            + "    class B /* c */ { " + Method("G", "sum", "2").Replace("{", "{ // note", StringComparison.Ordinal) + "}\n"
            + "#endregion\n");

        FindingAssert.Exactly(Run(root, 100), Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(4,5)"));
    }

    [Fact]
    public void IdentifiersAndLiterals_ArePlacedByTheirKind()
    {
        using TempDirectory first = new();
        first.Write("src/A.cs", "class A { int x = 1; string s = \"a\"; char c = 'c'; double d = 1.5; }\n");
        using TempDirectory second = new();
        second.Write("src/A.cs", "class Other { int y = 22; string t = \"b c\"; char d = 'z'; double e = 2.5; }\n");

        Assert.Equal(Kinds(TokenStream.Build(Context(first).Sources.Files, 0)), Kinds(TokenStream.Build(Context(second).Sources.Files, 0)));
    }

    [Fact]
    public void GeneratedFile_IsNotCompared()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.g.cs", ClassFile("B", Method("G", "sum", "2")));
        root.Write("src/C.cs", "// <auto-generated/>\n" + ClassFile("C", Method("H", "x", "3")));

        FindingAssert.None(Run(root, 100));
    }

    // ---- Where a repeat can be ----

    [Fact]
    public void RepeatInOneFile_IsOnePairWithoutOverlap()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", "class A\n{\n" + Method("F", "total", "1") + Method("G", "sum", "2") + "}\n");

        // The first method is lines 3-13 and the second one starts at line 14.
        FindingAssert.Exactly(Run(root, 100), Error("src/A.cs", 3, 1, $"{MethodTokens} tokens duplicated at src/A.cs(14,1)"));
    }

    [Fact]
    public void RunsInOneFile_NeverOverlapWhenTheCodeIsPeriodic()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", "void F()\n{\n" + Repeat("    total = total + 1;\n", 60) + "}\n"));
        TokenStream stream = TokenStream.Build(Context(root).Sources.Files, 0);

        IReadOnlyList<RawDuplicate> pairs = DuplicateSearch.Find(stream, 100);

        Assert.NotEmpty(pairs);
        Assert.All(pairs, pair => Assert.False(pair.First.Overlaps(pair.Second)));
        Assert.All(pairs, pair => Assert.True(pair.Tokens >= 100));
    }

    [Fact]
    public void Window_NeverCrossesTheBoundaryOfAFile()
    {
        // The end of X and the beginning of Y read like the end of P and the beginning of Q, and each file is shorter than the
        // window. A search over the joined sequence would find 100 tokens that no file holds.
        string tail = Repeat("a = 1;", 13);
        string head = Repeat("return a;", 17);
        using TempDirectory root = new();
        root.Write("src/P.cs", Repeat("continue;", 4) + tail);
        root.Write("src/Q.cs", head + "break;");
        root.Write("src/X.cs", Repeat("break;", 4) + tail);
        root.Write("src/Y.cs", head + "continue;");

        FindingAssert.None(Run(root, 100));
    }

    [Fact]
    public void PathOrder_IsOrdinal()
    {
        using TempDirectory root = new();
        root.Write("src/a.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/Z.cs", ClassFile("Z", Method("G", "sum", "2")));

        FindingAssert.Exactly(Run(root, 100), Error("src/Z.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/a.cs(1,1)"));
    }

    [Fact]
    public void RepeatThatStartsAfterOtherCode_IsPlacedAtItsFirstToken()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", "class A1 { }\n\n\nclass A2\n{\n" + Method("F", "total", "1") + "}\n");
        root.Write("src/B.cs", "class B\n{\n" + Method("G", "sum", "2") + "}\n");

        FindingAssert.Exactly(Run(root, 100), Error("src/A.cs", 4, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"));
    }

    // ---- Symbol sets ----

    [Fact]
    public void RepeatUnderNet11_FoundByAllSetsOfTheTarget_IsOnePair()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write("src/A.cs", Text("class A", "{", "#if NET11_0_OR_GREATER") + Method("F", "total", "1") + Text("#endif", "}"));
        root.Write("src/B.cs", Text("class B", "{", "#if NET11_0_OR_GREATER") + Method("G", "sum", "2") + Text("#endif", "}"));

        DuplicateScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"));
        Assert.Single(scan.Pairs);
    }

    [Fact]
    public void RepeatUnderADebugOnlyBranch_IsOnePair()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write("src/A.cs", Text("class A", "{", "#if DEBUG") + Method("F", "total", "1") + Text("#endif", "}"));
        root.Write("src/B.cs", Text("class B", "{", "#if DEBUG") + Method("G", "sum", "2") + Text("#endif", "}"));

        FindingAssert.Exactly(Run(root, 100), Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"));
    }

    [Fact]
    public void BranchAtTheStartOfARepeat_MovesTheRunAndDoesNotAddAPair()
    {
        // Under net11.0 the field is in both files and the repeat starts at the class; under net10.0 it is only in B and the
        // repeat starts at the method. The two runs overlap at both places and are one pair, with the places of the longer one.
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write(
            "src/A.cs",
            Text("class A", "{", "#if NET11_0_OR_GREATER", "    int extra;", "#endif") + Method("F", "total", "1") + Text("}"));
        root.Write("src/B.cs", Text("class B", "{", "    int extra;") + Method("G", "sum", "2") + Text("}"));

        DuplicateScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error("src/A.cs", 1, 1, $"{ClassTokens + 3} tokens duplicated at src/B.cs(1,1)"));
        Assert.Single(scan.Pairs);
    }

    [Fact]
    public void RepeatThatOnlyOneTargetSees_IsAPair()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write("src/A.cs", Text("class A", "{", "#if !NET11_0_OR_GREATER") + Method("F", "total", "1") + Text("#endif", "}"));
        root.Write("src/B.cs", Text("class B", "{", "#if !NET11_0_OR_GREATER") + Method("G", "sum", "2") + Text("#endif", "}"));

        FindingAssert.Exactly(Run(root, 100), Error("src/A.cs", 1, 1, $"{ClassTokens} tokens duplicated at src/B.cs(1,1)"));
    }

    [Fact]
    public void Join_PairsOfDifferentSetsThatOverlapAtBothPlaces_AreOnePairWithTheLongestRun()
    {
        RawDuplicate shorter = Raw((0, 100, 200), (1, 100, 200), 50);
        RawDuplicate longer = Raw((0, 150, 300), (1, 130, 260), 80);

        IReadOnlyList<RawDuplicate> joined = DuplicateMerge.Join([[shorter], [longer]]);

        Assert.Equal([longer], joined);
    }

    [Fact]
    public void Join_ChainOfPairs_IsOnePair()
    {
        RawDuplicate first = Raw((0, 100, 200), (1, 100, 200), 50);
        RawDuplicate middle = Raw((0, 180, 300), (1, 150, 260), 60);
        RawDuplicate last = Raw((0, 280, 400), (1, 250, 360), 70);

        IReadOnlyList<RawDuplicate> joined = DuplicateMerge.Join([[first], [middle], [last]]);

        Assert.Equal([last], joined);
    }

    [Fact]
    public void Join_OverlapAtOnePlaceOnly_ArePairsOfTheirOwn()
    {
        RawDuplicate left = Raw((0, 100, 200), (1, 100, 200), 50);
        RawDuplicate right = Raw((0, 150, 250), (2, 100, 200), 50);
        RawDuplicate elsewhere = Raw((0, 150, 250), (1, 500, 600), 50);

        IReadOnlyList<RawDuplicate> joined = DuplicateMerge.Join([[left], [right, elsewhere]]);

        Assert.Equal([left, elsewhere, right], joined);
    }

    [Fact]
    public void Join_PairsOfOneSetThatOverlap_AreNotJoined()
    {
        RawDuplicate left = Raw((0, 100, 200), (1, 100, 200), 50);
        RawDuplicate right = Raw((0, 150, 250), (1, 150, 250), 50);

        Assert.Equal([left, right], DuplicateMerge.Join([[left, right]]));
    }

    // ---- Level ----

    [Theory]
    [InlineData("{}", "error")]
    [InlineData("{\"duplicates\": 0}", "error")]
    [InlineData("{\"duplicates\": 1}", "warning")]
    [InlineData("{\"duplicates\": 5}", "warning")]
    [InlineData("not JSON", "error")]
    public static void OnePair_IsAWarningWithinTheBaselineAndAnErrorAbove(string ratchet, string severity)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2")));
        root.Write("eng/ratchet.json", ratchet);

        Finding finding = Assert.Single(Run(root, 100));

        Assert.Equal(Level(severity), finding.Severity);
        Assert.Equal($"src/A.cs(1,1): {severity} ENG601: {ClassTokens} tokens duplicated at src/B.cs(1,1)", finding.ToString());
    }

    [Theory]
    [InlineData(2, "error")]
    [InlineData(3, "warning")]
    [InlineData(4, "warning")]
    public static void EveryPair_HasTheLevelOfTheWholeNumber(int baseline, string severity)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "a", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("F", "b", "1")));
        root.Write("src/C.cs", ClassFile("C", Method("F", "c", "1")));
        root.Write("eng/ratchet.json", $"{{\n    \"duplicates\": {baseline}\n}}\n");

        List<Finding> findings = Run(root, 100);

        Assert.Equal(3, findings.Count);
        Assert.All(findings, finding => Assert.Equal(Level(severity), finding.Severity));
    }

    // ---- The command ----

    [Fact]
    public void Command_IsRegisteredAndWritesTheLineOfTheFinding()
    {
        (int exitCode, string output, string error) = RunCommand("{}\n");

        Assert.Equal(1, exitCode);
        Assert.Equal($"src/A.cs(1,1): error ENG601: {ClassTokens} tokens duplicated at src/B.cs(1,1)\n", output);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Command_WithinTheBaseline_ExitCodeIsZero()
    {
        (int exitCode, string output, _) = RunCommand("{\n    \"duplicates\": 1\n}\n");

        Assert.Equal(0, exitCode);
        Assert.Contains("warning ENG601", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_IsMadeOnceAndSharedThroughTheContext()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2")));
        CheckContext context = Context(root);

        Assert.Same(DuplicateScan.Of(context), DuplicateScan.Of(context));
        Assert.Single(DuplicateScan.Of(context).Pairs);
    }

    [Fact]
    public void RepositoryWithoutRepeats_HasNoFindings()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("F", "total", "1", 5)));

        FindingAssert.None(Run(root, 100));
    }

    // ---- Helpers ----

    private static Finding Error(string path, int line, int column, string message) =>
        FindingAssert.Error(EngCodes.Eng601, path, line, column, message);

    private static FindingSeverity Level(string severity) => severity == "error" ? FindingSeverity.Error : FindingSeverity.Warning;

    // The baseline is an input of the command; an example that does not write one gets a baseline without the key `duplicates`.
    private static CheckContext Context(TempDirectory root)
    {
        if (!File.Exists(Path.Combine(root.Path, "eng", "ratchet.json")))
        {
            root.Write("eng/ratchet.json", "{}\n");
        }

        return new CheckContext(root.Path, new Dictionary<string, string>());
    }

    // Two files with one repeat and the given baseline, run as the command line does.
    private static (int ExitCode, string Output, string Error) RunCommand(string ratchet)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", ClassFile("A", Method("F", "total", "1")));
        root.Write("src/B.cs", ClassFile("B", Method("G", "sum", "2")));
        root.Write("eng/ratchet.json", ratchet);
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["duplicates", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        return (exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString());
    }

    private static DuplicateScan Scan(TempDirectory root, int window = 100) => DuplicateScan.Run(Context(root), window);

    private static List<Finding> Run(TempDirectory root, int window) => [.. Scan(root, window).Findings];

    // Statement i is `v = v + l + l ...;` with i + 1 terms l, so a method of N statements is N * (N + 5) + 8 tokens.
    private static string Method(string name, string variable, string literal, int statements = Statements) =>
        $"void {name}(int {variable})\n{{\n"
        + string.Concat(Enumerable.Range(0, statements).Select(index => $"    {variable} = {variable}{Repeat($" + {literal}", index + 1)};\n"))
        + "}\n";

    private static string ClassFile(string name, string members) => $"class {name}\n{{\n{members}}}\n";

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    private static ushort[] Kinds(TokenStream stream) => [.. Enumerable.Range(0, stream.Count).Select(stream.KindAt)];

    private static RawDuplicate Raw((int File, int Start, int End) first, (int File, int Start, int End) second, int tokens) =>
        new(new DuplicateSite(first.File, first.Start, first.End), new DuplicateSite(second.File, second.Start, second.End), tokens);

    private static void WriteTargets(TempDirectory root, string targets) =>
        root.Write("Directory.Build.props", $"<Project><PropertyGroup><TargetFrameworks>{targets}</TargetFrameworks></PropertyGroup></Project>");
}
