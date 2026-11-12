using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The skip lists of both suites (docs/design/conformance.md, "Skip lists and findings"). An invalid entry is ENG800 at its line,
// column 1; a file without the table is ENG800 at (1,1). In the examples the entry under test is on line 8, after a valid entry
// on line 7.
public sealed class SkipListTests
{
    private const string CelSpecPath = "tests/Great.Parts.Cel.Conformance/CelSpec/skips.md";
    private const string CorpusPath = "tests/Great.Parts.Cel.Conformance/Regression/skips.md";
    private const string RegistryPath = "tests/Great.Parts.Cel.Conformance/Regression/known-differences.md";
    private const string Commit = "8bef98f1caadc2036fe63d5516cf1df65dc4b741";
    private const string Langdef = "https://github.com/google/cel-spec/blob/8bef98f1caadc2036fe63d5516cf1df65dc4b741/doc/langdef.md#";
    private const string Signature = "add_int64(int, int) we-error";

    private const string CelSpecTop = """
        # cel-spec conformance skip list

        Cases that may fail.

        | Case | Scope | Reason | Reference | Issue |
        |---|---|---|---|---|
        """;

    private const string CelSpecHead = CelSpecTop + "\n| basic/a/a | * * * | Not implemented yet. | | |";

    private const string CorpusHead = """
        # Regression corpus skip list

        Cases that may fail.

        | Case | Scope | Signature | Verdict | Severity | Reason | Issue |
        |---|---|---|---|---|---|---|
        | corpus/a/a | * * * | add_int64(int, int) we-error | cel-go-bug | S2 | A known difference. | |
        """;

    // ---- Valid lists ----

    [Fact]
    public void PinnedCelSpecList_HasNoFindingAndEveryEntryInEveryCell()
    {
        string commit = EmbeddedData.ReadText("tests/cel-spec/COMMIT").Trim();

        SkipList list = SkipList.ReadCelSpec(CelSpecPath, EmbeddedData.ReadText(CelSpecPath), commit);

        Assert.Empty(list.Findings);
        Assert.NotEmpty(list.Entries);
        Assert.All(list.Entries, entry => Assert.Equal(6, entry.Scope.Count));
    }

    // A signature of the registry that has a case in the corpus but no entry in the list. The expectation of a case is an evaluation
    // error, since a case of cel-spec cannot expect a compile error, so the library, which fails this case at evaluation, passes it,
    // and an entry for a passing case is ENG802.
    private static readonly string[] SignaturesWithoutEntry =
    [
        "matches(string, error) stage-mismatch",
        "string_to_cidr(string) stage-mismatch",
        "string_to_ip(string) stage-mismatch",
    ];

    // The 106 differences of the registry — map literals, a bytes value in a map, orderings of NaN with another kind, time, matches, the subtraction of the smallest duration and
    // the extension sets (the offset of indexOf and lastIndexOf, the constant of ip and cidr, the order of sort and sortBy, %s of bytes,
    // json.encode of a duration and of a timestamp) — and
    // the cases of the corpus that minimize them: one entry of the list for each signature but three, the entries in ordinal order.
    [Fact]
    public void PinnedCorpusListAndRegistry_HaveNoFindingAndAnEntryForEachSignatureOfTheRegistry()
    {
        KnownDifferences registry = KnownDifferences.Read(RegistryPath, EmbeddedData.ReadText(RegistryPath));
        string text = EmbeddedData.ReadText(CorpusPath);

        SkipList list = SkipList.ReadCorpus(CorpusPath, text, registry.Signatures);

        Assert.Empty(registry.Findings);
        Assert.Empty(list.Findings);
        Assert.Equal(106, registry.Signatures.Count);
        Assert.Equal(103, list.Entries.Count);
        Assert.Equal(
            registry.Signatures.Except(SignaturesWithoutEntry).Order(StringComparer.Ordinal),
            EntrySignatures(text).Order(StringComparer.Ordinal));
        Assert.Equal(list.Entries.Select(entry => entry.Case).Order(StringComparer.Ordinal), list.Entries.Select(entry => entry.Case));
    }

    // The signature column of the rows of a corpus list, after the head of the table.
    private static List<string> EntrySignatures(string text) =>
    [
        .. text.ReplaceLineEndings("\n").Split('\n')
            .SkipWhile(line => !line.StartsWith("|---", StringComparison.Ordinal))
            .Skip(1)
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => line.Split(" | ")[2]),
    ];

    [Fact]
    public void ValidCelSpecList_HasNoFindingAndItsEntriesWithTheirScopes()
    {
        string text = CelSpecTop + "\n" + string.Join(
            "\n",
            $"| basic/a/a | jit,native-aot macos net11.0 | Fails \\| twice. | {Langdef}macros | https://github.com/great-parts/great-parts-cel/issues/7 |",
            "| basic/a/a | native-aot linux-x64 * | Fails. | Minimal case: expression 1 + 1, no variables, the result 2. | https://github.com/great-parts/great-parts-cel/issues/12 |",
            "| string_ext/format/dyntype NaN/infinity support | * macos * | Fails on macOS. | | |",
            "");

        SkipList list = SkipList.ReadCelSpec(CelSpecPath, text, Commit);

        Assert.Empty(list.Findings);
        Assert.Equal(
            [
                "7 basic/a/a: jit macos net11.0",
                "8 basic/a/a: native-aot linux-x64 net10.0, native-aot linux-x64 net11.0",
                "9 string_ext/format/dyntype NaN/infinity support: jit macos net10.0, jit macos net11.0",
            ],
            Describe(list.Entries));
    }

    [Fact]
    public void ValidCorpusList_HasNoFindingAndItsEntries()
    {
        string text = CorpusHead + "\n" + string.Join(
            "\n",
            "| corpus/b/b | jit * * | size(string) we-value | pending | S1 | Not analysed yet. | |",
            "| corpus/c/c | * * net10.0 | size(bytes) we-error | our-bug | S2 | Our bug. | https://github.com/great-parts/great-parts-cel/issues/3 |",
            "| corpus/d/d | * * * | int64_to_string(int) value-mismatch | by-design | S1 | By design. | |",
            "");

        SkipList list = SkipList.ReadCorpus(CorpusPath, text, Registry(Signature, "int64_to_string(int) value-mismatch"));

        Assert.Empty(list.Findings);
        Assert.Equal([(7, "corpus/a/a"), (8, "corpus/b/b"), (9, "corpus/c/c"), (10, "corpus/d/d")], list.Entries.Select(entry => (entry.Line, entry.Case)));
    }

    [Fact]
    public void EmptyTable_HasNoFindingAndNoEntry()
    {
        SkipList list = SkipList.ReadCelSpec(CelSpecPath, "| Case | Scope | Reason | Reference | Issue |\n|:--|:-:|--:|---|-|\n", Commit);

        Assert.Empty(list.Findings);
        Assert.Empty(list.Entries);
    }

    // ---- ENG800: the table ----

    [Theory]
    [InlineData("")]
    [InlineData("# cel-spec conformance skip list\n\nNo table.\n")]
    [InlineData(" | Case | Scope | Reason | Reference | Issue |\n")]
    public static void FileWithoutTheTable_IsEng800AtLineOneColumnOne(string text)
    {
        FindingAssert.Single(
            SkipList.ReadCelSpec(CelSpecPath, text, Commit).Findings,
            EngCodes.Eng800,
            CelSpecPath,
            1,
            1,
            "the file has no table with the columns 'Case | Scope | Reason | Reference | Issue'");
    }

    [Theory]
    [InlineData("| Case | Reason | Scope | Reference | Issue |", "the columns of the table are 'Case | Reason | Scope | Reference | Issue', not 'Case | Scope | Reason | Reference | Issue'")]
    [InlineData("| Case | Scope | Reason | Reference |", "the columns of the table are 'Case | Scope | Reason | Reference', not 'Case | Scope | Reason | Reference | Issue'")]
    [InlineData("| Case | Scope | Signature | Verdict | Severity | Reason | Issue |", "the columns of the table are 'Case | Scope | Signature | Verdict | Severity | Reason | Issue', not 'Case | Scope | Reason | Reference | Issue'")]
    public static void WrongColumns_AreEng800AtTheHeader(string header, string message)
    {
        string text = "# List\n\n" + header + "\n|---|---|---|---|---|\n";

        FindingAssert.Single(SkipList.ReadCelSpec(CelSpecPath, text, Commit).Findings, EngCodes.Eng800, CelSpecPath, 3, 1, message);
    }

    [Fact]
    public void CelSpecColumnsInTheCorpusList_AreEng800AtTheHeader()
    {
        FindingAssert.Single(
            SkipList.ReadCorpus(CorpusPath, CelSpecHead, Registry()).Findings,
            EngCodes.Eng800,
            CorpusPath,
            5,
            1,
            "the columns of the table are 'Case | Scope | Reason | Reference | Issue', not 'Case | Scope | Signature | Verdict | Severity | Reason | Issue'");
    }

    [Theory]
    [InlineData("| basic/a/a | * * * | Reason. | | |", 4, "the line after the header is not the separator row of 5 cells")]
    [InlineData("|---|---|---|---|", 4, "the line after the header is not the separator row of 5 cells")]
    [InlineData("|---|---|- -|---|---|", 4, "the line after the header is not the separator row of 5 cells")]
    [InlineData("|---|---|::-|---|---|", 4, "the line after the header is not the separator row of 5 cells")]
    [InlineData(null, 3, "the header is the last line: the separator row is missing")]
    public static void MissingSeparator_IsEng800(string? separator, int line, string message)
    {
        string text = "# List\n\n| Case | Scope | Reason | Reference | Issue |" + (separator is null ? string.Empty : "\n" + separator + "\n");

        FindingAssert.Single(SkipList.ReadCelSpec(CelSpecPath, text, Commit).Findings, EngCodes.Eng800, CelSpecPath, line, 1, message);
    }

    // ---- ENG800: an entry of the cel-spec list ----

    // One case per line: the entry on line 8 of the list, " => ", the message of its finding.
    public static TheoryData<string, string> InvalidCelSpecEntries { get; } = Cases($"""
        | zeta/b/c | * * * | Reason. | | => the row has 4 cells, not 5 cells
        | zeta/b/c | * * * | Reason. | | | | => the row has 6 cells, not 5 cells
        | zeta | * * * | Reason. | | | => entry 'zeta': the case is not of the form <file>/<section>/<name>
        | zeta/b | * * * | Reason. | | | => entry 'zeta/b': the case is not of the form <file>/<section>/<name>
        | zeta//c | * * * | Reason. | | | => entry 'zeta//c': the case is not of the form <file>/<section>/<name>
        | zeta/b/ | * * * | Reason. | | | => entry 'zeta/b/': the case is not of the form <file>/<section>/<name>
        | zeta/b/c | jit * | Reason. | | | => entry 'zeta/b/c': the scope 'jit *' is not three parts separated by a space: the mode, the operating system and the target
        | zeta/b/c | jit  * * | Reason. | | | => entry 'zeta/b/c': the scope 'jit  * *' is not three parts separated by a space: the mode, the operating system and the target
        | zeta/b/c | | Reason. | | | => entry 'zeta/b/c': the scope '' is not three parts separated by a space: the mode, the operating system and the target
        | zeta/b/c | aot * * | Reason. | | | => entry 'zeta/b/c': the scope has the unknown value 'aot'; a part is jit, native-aot, a list of them or *
        | zeta/b/c | jit,* * * | Reason. | | | => entry 'zeta/b/c': the scope has the unknown value '*'; a part is jit, native-aot, a list of them or *
        | zeta/b/c | * linux-x64,linux * | Reason. | | | => entry 'zeta/b/c': the scope has the unknown value 'linux'; a part is linux-x64, macos, a list of them or *
        | zeta/b/c | * macos, * | Reason. | | | => entry 'zeta/b/c': the scope has the unknown value ''; a part is linux-x64, macos, a list of them or *
        | zeta/b/c | * * net9.0 | Reason. | | | => entry 'zeta/b/c': the scope has the unknown value 'net9.0'; a part is net10.0, net11.0, a list of them or *
        | zeta/b/c | native-aot macos * | Reason. | | | => entry 'zeta/b/c': the scope 'native-aot macos *' has no cell of the matrix
        | zeta/b/c | * * * | | | | => entry 'zeta/b/c': the reason is empty
        | zeta/b/c | * * * | Reason. | | https://github.com/great-parts/great-parts-cel/issues/012 | => entry 'zeta/b/c': the issue 'https://github.com/great-parts/great-parts-cel/issues/012' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | zeta/b/c | * * * | Reason. | | https://github.com/great-parts/great-parts-cel/issues/ | => entry 'zeta/b/c': the issue 'https://github.com/great-parts/great-parts-cel/issues/' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | zeta/b/c | * * * | Reason. | | https://github.com/great-parts/great-parts-cel/pull/12 | => entry 'zeta/b/c': the issue 'https://github.com/great-parts/great-parts-cel/pull/12' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | zeta/b/c | * * * | Reason. | | https://github.com/google/cel-go/issues/12 | => entry 'zeta/b/c': the issue 'https://github.com/google/cel-go/issues/12' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | zeta/b/c | * * * | Reason. | | #12 | => entry 'zeta/b/c': the issue '#12' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | zeta/b/c | * * * | Reason. | | https://github.com/great-parts/great-parts-cel/issues/12a | => entry 'zeta/b/c': the issue 'https://github.com/great-parts/great-parts-cel/issues/12a' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | zeta/b/c | * * * | Reason. | https://github.com/google/cel-spec/blob/0000000000000000000000000000000000000000/doc/langdef.md#macros | | => entry 'zeta/b/c': the reference has the link 'https://github.com/google/cel-spec/blob/0000000000000000000000000000000000000000/doc/langdef.md#macros', which is not {Langdef}<section>
        | zeta/b/c | * * * | Reason. | https://github.com/google/cel-spec/blob/8bef98f1caadc2036fe63d5516cf1df65dc4b741/doc/intro.md#macros | | => entry 'zeta/b/c': the reference has the link 'https://github.com/google/cel-spec/blob/8bef98f1caadc2036fe63d5516cf1df65dc4b741/doc/intro.md#macros', which is not {Langdef}<section>
        | zeta/b/c | * * * | Reason. | {Langdef} | | => entry 'zeta/b/c': the reference has the link '{Langdef}', which is not {Langdef}<section>
        | zeta/b/c | * * * | Reason. | {Langdef}Macros | | => entry 'zeta/b/c': the reference has the link '{Langdef}Macros', which is not {Langdef}<section>
        | zeta/b/c | * * * | Reason. | Minimal case: see http://example.com/case for the data. | | => entry 'zeta/b/c': the reference has the link 'http://example.com/case', which is not {Langdef}<section>
        | zeta/b/c | * * * | Reason. | [Macros]({Langdef}macros) | | => entry 'zeta/b/c': the reference has the link '[Macros]({Langdef}macros)', which is not {Langdef}<section>
        | basic/a/a | * * * | Another reason. | | | => entry 'basic/a/a': the entry repeats the entry at line 7
        | basic/a/a | jit,native-aot linux-x64 * | Another reason. | | | => entry 'basic/a/a': the scope overlaps the scope of the entry at line 7
        | basic/0/a | * * * | Reason. | | | => entry 'basic/0/a': the entry is out of order: it follows 'basic/a/a'
        | Basic/b/c | * * * | Reason. | | | => entry 'Basic/b/c': the entry is out of order: it follows 'basic/a/a'
        """);

    public static TheoryData<string, string> InvalidCorpusEntries { get; } = Cases("""
        | corpus/b/c | * * * | size(string) we-value | pending | S1 | Reason. | => the row has 6 cells, not 7 cells
        | corpus/b/c | * * * | | pending | S1 | Reason. | | => entry 'corpus/b/c': the signature is empty
        | corpus/b/c | * * * | size(string) we-value | fixed | S1 | Reason. | | => entry 'corpus/b/c': the verdict 'fixed' is not one of pending, our-bug, cel-go-bug, by-design
        | corpus/b/c | * * * | size(string) we-value | | S1 | Reason. | | => entry 'corpus/b/c': the verdict '' is not one of pending, our-bug, cel-go-bug, by-design
        | corpus/b/c | * * * | size(string) we-value | pending | S3 | Reason. | | => entry 'corpus/b/c': the severity 'S3' is not one of S1, S2
        | corpus/b/c | * * * | size(string) we-value | pending | s1 | Reason. | | => entry 'corpus/b/c': the severity 's1' is not one of S1, S2
        | corpus/b/c | * * * | size(string) we-value | cel-go-bug | S1 | Reason. | | => entry 'corpus/b/c': the verdict 'cel-go-bug' needs a row of the registry of known differences with the signature 'size(string) we-value'
        | corpus/b/c | * * * | size(string) we-value | by-design | S1 | Reason. | | => entry 'corpus/b/c': the verdict 'by-design' needs a row of the registry of known differences with the signature 'size(string) we-value'
        | corpus/b/c | * * * | size(string) we-value | pending | S1 | | | => entry 'corpus/b/c': the reason is empty
        | corpus/b/c | * * * | size(string) we-value | pending | S1 | Reason. | issue 12 | => entry 'corpus/b/c': the issue 'issue 12' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>
        | corpus/b/c | macos * * | size(string) we-value | pending | S1 | Reason. | | => entry 'corpus/b/c': the scope has the unknown value 'macos'; a part is jit, native-aot, a list of them or *
        | corpus/b/c | native-aot macos net10.0 | size(string) we-value | pending | S1 | Reason. | | => entry 'corpus/b/c': the scope 'native-aot macos net10.0' has no cell of the matrix
        | corpus/b | * * * | size(string) we-value | pending | S1 | Reason. | | => entry 'corpus/b': the case is not of the form <file>/<section>/<name>
        | corpus/a/a | * * * | add_int64(int, int) we-error | cel-go-bug | S2 | A known difference. | | => entry 'corpus/a/a': the entry repeats the entry at line 7
        | corpus/a/a | jit macos net11.0 | size(string) we-value | pending | S1 | Reason. | | => entry 'corpus/a/a': the scope overlaps the scope of the entry at line 7
        | corpus/0/0 | * * * | size(string) we-value | pending | S1 | Reason. | | => entry 'corpus/0/0': the entry is out of order: it follows 'corpus/a/a'
        """);

    [Theory]
    [MemberData(nameof(InvalidCelSpecEntries))]
    public static void InvalidCelSpecEntry_IsEng800AtItsLine(string row, string message)
    {
        SkipList list = SkipList.ReadCelSpec(CelSpecPath, CelSpecHead + "\n" + row + "\n", Commit);

        FindingAssert.Single(list.Findings, EngCodes.Eng800, CelSpecPath, 8, 1, message);
    }

    // ---- ENG800: an entry of the corpus list ----

    [Theory]
    [MemberData(nameof(InvalidCorpusEntries))]
    public static void InvalidCorpusEntry_IsEng800AtItsLine(string row, string message)
    {
        SkipList list = SkipList.ReadCorpus(CorpusPath, CorpusHead + "\n" + row + "\n", Registry(Signature));

        FindingAssert.Single(list.Findings, EngCodes.Eng800, CorpusPath, 8, 1, message);
    }

    // ---- ENG800: several problems and the entries that remain ----

    [Fact]
    public void EntryWithSeveralProblems_IsOneEng800ForEachProblem()
    {
        string text = CorpusHead + "\n| corpus/0 | jit | size(string) we-value | cel-go-bug | S3 | | #1 |\n";

        SkipList list = SkipList.ReadCorpus(CorpusPath, text, Registry(Signature));

        Assert.All(list.Findings, finding => Assert.Matches(Finding.Pattern, finding.ToString()));
        Assert.Equal(
            [
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the case is not of the form <file>/<section>/<name>",
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the scope 'jit' is not three parts separated by a space: the mode, the operating system and the target",
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the reason is empty",
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the issue '#1' is not a link https://github.com/great-parts/great-parts-cel/issues/<number>",
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the severity 'S3' is not one of S1, S2",
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the verdict 'cel-go-bug' needs a row of the registry of known differences with the signature 'size(string) we-value'",
                "tests/Great.Parts.Cel.Conformance/Regression/skips.md(8,1): error ENG800: entry 'corpus/0': the entry is out of order: it follows 'corpus/a/a'",
            ],
            list.Findings.Select(finding => finding.ToString()));
    }

    [Fact]
    public void InvalidEntries_AreLeftOutAndTheFindingsAreByLine()
    {
        string text = CelSpecHead + "\n" + string.Join(
            "\n",
            "| basic/b/b | jit | Reason. | | |",
            "| basic/c/c | * * * | | | |",
            "| basic/c/c | jit macos * | Reason. | | |",
            "| basic/d |",
            "| basic/e | * * * | Reason. | | |",
            "");

        SkipList list = SkipList.ReadCelSpec(CelSpecPath, text, Commit);

        Assert.Equal([8, 9, 10, 11, 12], list.Findings.Select(finding => finding.Line));
        Assert.Equal([(7, "basic/a/a"), (9, "basic/c/c")], list.Entries.Select(entry => (entry.Line, entry.Case)));
    }

    [Fact]
    public void RowsOutsideTheTableLines_AreText()
    {
        string text = CelSpecHead + "\n\nSome text | with a bar.\n| basic/b/b | * * * | Reason. | | |\r\n";

        SkipList list = SkipList.ReadCelSpec(CelSpecPath, text, Commit);

        Assert.Empty(list.Findings);
        Assert.Equal([(7, "basic/a/a"), (10, "basic/b/b")], list.Entries.Select(entry => (entry.Line, entry.Case)));
    }

    private static HashSet<string> Registry(params string[] signatures) => [.. signatures];

    private static TheoryData<string, string> Cases(string text)
    {
        TheoryData<string, string> cases = [];
        // A checkout with CRLF line endings gives the raw string literal CRLF too.
        foreach (string line in text.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            int arrow = line.IndexOf(" => ", StringComparison.Ordinal);
            cases.Add(line[..arrow], line[(arrow + " => ".Length)..]);
        }

        return cases;
    }

    private static IEnumerable<string> Describe(IReadOnlyList<SkipEntry> entries) => entries.Select(entry =>
        $"{entry.Line} {entry.Case}: " + string.Join(", ", entry.Scope.Select(cell => $"{cell.Mode} {cell.OperatingSystem} {cell.Target}").Order(StringComparer.Ordinal)));
}
