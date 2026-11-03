using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks error-codes` (docs/design/eng-docs.md, "error-codes"). The library is a small enumeration CelErrorCode that Roslyn
// compiles in memory into the place of the Release build, with its XML documentation next to it, so the command reads a real assembly
// and real documentation. The command runs through Program, so the options and the exit codes are the ones of the command line.
public sealed class ErrorCodesPageTests
{
    private const string PagePath = "docs/error-codes.md";
    private const string Header = "| Code | Name | Stage | When it occurs |";
    private const string Separator = "| :- | :- | :- | :- |";

    private const string SyntaxRow = "| 1000 | `SyntaxError` | Parse | The text does not follow the grammar. Location: the `token`. |";
    private const string UndeclaredRow = "| 2002 | `UndeclaredReference` | Check | A name is not declared; compare `SyntaxError`. Chains of `\\|\\|` and `&&` do not add depth. |";
    private const string DivisionRow = "| 3001 | `DivisionByZero` | Evaluate | The divisor is zero. |";
    private const string DuplicateRow = "| 4000 | `DuplicateDeclaration` | Build | A name is declared twice. |";
    private const string InternalRow = "| 9001 | `InternalError` | Any | An error of the library, shown as `null`. |";

    // The lines of the page: the header of the table is at line 5, the rows at the lines 7 to 11.
    private const int HeaderLine = 5;
    private const int FirstRowLine = 7;

    private const string LibrarySource = """
        namespace Great.Parts.Cel.Diagnostics;

        /// <summary>The published codes.</summary>
        public enum CelErrorCode
        {
            /// <summary>
            /// The text does not follow the grammar.
            /// Location: the <c>token</c>.
            /// </summary>
            SyntaxError = 1000,

            /// <summary>A name is not declared; compare <see cref="SyntaxError"/>. Chains of <c>||</c> and <c>&amp;&amp;</c> do not add depth.</summary>
            UndeclaredReference = 2002,

            /// <summary>The divisor is zero.</summary>
            DivisionByZero = 3001,

            /// <summary>A name is declared twice.</summary>
            DuplicateDeclaration = 4000,

            /// <summary>An error of the library, shown as <see langword="null"/>.</summary>
            InternalError = 9001,
        }
        """;

    private static readonly string[] WriteOptions = ["--write"];

    private static readonly string[] Rows = [SyntaxRow, UndeclaredRow, DivisionRow, DuplicateRow, InternalRow];

    [Fact]
    public void ErrorCodesCommand_IsInTheRegistryAndTakesNoArgument()
    {
        ICheckCommand command = Assert.Single(CommandRegistry.All, candidate => candidate.Name == "error-codes");

        Assert.Null(command.ArgumentName);
    }

    [Fact]
    public void APageThatMatchesTheMembers_HasNoFindings()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Rows));

        Result result = Check(root);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void RowsInAnotherOrderAndANameWithoutCodeSpan_AreStillTheSameCodes()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        string[] rows = [InternalRow, DuplicateRow, SyntaxRow.Replace("`SyntaxError`", "SyntaxError", StringComparison.Ordinal), UndeclaredRow, DivisionRow];
        root.Write(PagePath, Page(rows));

        Result result = Check(root);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Eng303_ARowWithAnotherSummary_IsReportedAtTheRow()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Changed(InternalRow, ("An error of the library", "An error of the program"))));

        Result result = Check(root);

        AssertErrors(result, Error(FirstRowLine + 4, "row 'InternalError' differs from CelErrorCode: summary differs from the XML documentation, rebuild the page with --write"));
    }

    [Fact]
    public void Eng303_ARowWithAnotherNumberAndAnotherSummary_NamesBothDifferences()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Changed(UndeclaredRow, ("| 2002 |", "| 2003 |"), ("`\\|\\|`", "`or`"))));

        Result result = Check(root);

        AssertErrors(result, Error(FirstRowLine + 1, "row 'UndeclaredReference' differs from CelErrorCode: number 2003, expected 2002; summary differs from the XML documentation, rebuild the page with --write"));
    }

    [Fact]
    public void Eng303_APageWithoutTheRowOfAMember_IsReportedAtTheFirstLineOfTheTable()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page([.. Rows.Where(row => row != DivisionRow)]));

        Result result = Check(root);

        AssertErrors(result, Error(HeaderLine, "member 'DivisionByZero' of CelErrorCode has no row in the table"));
    }

    [Fact]
    public void Eng303_ARowWithoutAMember_IsReportedAtTheRow()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page([.. Rows, "| 3002 | `Ghost` | Evaluate | There is no such member. |"]));

        Result result = Check(root);

        AssertErrors(result, Error(FirstRowLine + Rows.Length, "row 'Ghost' has no member in CelErrorCode"));
    }

    [Fact]
    public void Eng303_ARowWithAnotherNumber_IsReportedAtTheRow()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Changed(DivisionRow, ("| 3001 |", "| 3021 |"))));

        Result result = Check(root);

        AssertErrors(result, Error(FirstRowLine + 2, "row 'DivisionByZero' differs from CelErrorCode: number 3021, expected 3001"));
    }

    [Fact]
    public void Eng303_ARowWithAnotherStage_IsReportedAtTheRow()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Changed(DivisionRow, ("| Evaluate |", "| Check |"))));

        Result result = Check(root);

        AssertErrors(result, Error(FirstRowLine + 2, "row 'DivisionByZero' differs from CelErrorCode: stage Check, expected Evaluate"));
    }

    [Fact]
    public void Eng303_ARowWithAnotherNumberAndAnotherStage_NamesBothDifferences()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Changed(InternalRow, ("| 9001 |", "| 9002 |"), ("| Any |", "| Parse |"))));

        Result result = Check(root);

        AssertErrors(result, Error(FirstRowLine + 4, "row 'InternalError' differs from CelErrorCode: number 9002, expected 9001; stage Parse, expected Any"));
    }

    [Fact]
    public void Eng303_ARowWithAnotherName_IsAMemberWithoutARowAndARowWithoutAMember()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page(Changed(DuplicateRow, ("`DuplicateDeclaration`", "`DuplicateName`"))));

        Result result = Check(root);

        AssertErrors(
            result,
            Error(HeaderLine, "member 'DuplicateDeclaration' of CelErrorCode has no row in the table"),
            Error(FirstRowLine + 3, "row 'DuplicateName' has no member in CelErrorCode"));
    }

    [Fact]
    public void Eng303_ARepeatedRowAndARowWithTheWrongNumberOfCells_AreReportedAtTheRows()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, Page([.. Rows, DivisionRow, "| 1 | two |"]));

        Result result = Check(root);

        AssertErrors(
            result,
            Error(FirstRowLine + Rows.Length, "row 'DivisionByZero' repeats an earlier row"),
            Error(FirstRowLine + Rows.Length + 1, "row has 2 cells, expected 4"));
    }

    [Fact]
    public void TheTableIsTheFirstOneWithTheColumns_AndFencedCodeIsNotSearched()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        string page = string.Join(
            "\n",
            "# Error codes",
            "",
            "| Name | Meaning |",
            "| :- | :- |",
            "| `X` | not the table |",
            "",
            "```text",
            Header,
            Separator,
            "| 1 | `Fenced` | Parse | not the table |",
            "```",
            "",
            Header,
            Separator,
            string.Join("\n", Rows),
            "") + "\n";
        root.Write(PagePath, page);

        Result result = Check(root);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Write_RebuildsTheTableFromTheMembersAndTheirSummariesAndKeepsTheRestOfThePage()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        string stale = "| 1000 | `SyntaxError` | Check | old |\n| 3001 | `Gone` | Evaluate | old |";
        root.Write(PagePath, "# Error codes\n\nBefore the table.\n\n" + Header + "\n" + Separator + "\n" + stale + "\n\nAfter the table.\n");

        Result result = Check(root, write: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(Page(Rows, intro: "Before the table.", outro: "After the table."), File.ReadAllText(Path.Combine(root.Path, PagePath)));
    }

    [Fact]
    public void Write_KeepsTheLineEndingsOfThePageAndLeavesAnUpToDatePageUnchanged()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        string crlf = Page([.. Rows.Where(row => row != DivisionRow)]).Replace("\n", "\r\n", StringComparison.Ordinal);
        string path = Path.Combine(root.Path, PagePath);
        root.Write(PagePath, crlf);

        Result first = Check(root, write: true);
        string written = File.ReadAllText(path);
        DateTime stamp = File.GetLastWriteTimeUtc(path);
        Result second = Check(root, write: true);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(Page(Rows).Replace("\n", "\r\n", StringComparison.Ordinal), written);
        Assert.Equal(0, second.ExitCode);
        Assert.Equal(written, File.ReadAllText(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Write_PutsTheTableInOrderOfNumbers_WhateverTheOrderInTheLibrary()
    {
        using TempDirectory root = new();
        Build(
            root,
            """
            namespace Great.Parts.Cel.Diagnostics;

            public enum CelErrorCode
            {
                /// <summary>The second.</summary>
                Second = 3001,

                /// <summary>The first.</summary>
                First = 1000,

                /// <summary>Not documented as the others: the summary is empty.</summary>
                Third = 9001,
            }
            """);
        root.Write(PagePath, Page([]));

        Result result = Check(root, write: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            Page(["| 1000 | `First` | Parse | The first. |", "| 3001 | `Second` | Evaluate | The second. |", "| 9001 | `Third` | Any | Not documented as the others: the summary is empty. |"]),
            File.ReadAllText(Path.Combine(root.Path, PagePath)));
    }

    [Fact]
    public void AMemberWithoutASummary_IsARowWithAnEmptyLastCell()
    {
        using TempDirectory root = new();
        Build(
            root,
            """
            namespace Great.Parts.Cel.Diagnostics;

            public enum CelErrorCode
            {
                Undocumented = 1000,
            }
            """,
            documented: false);
        root.Write(PagePath, Page([]));

        Result result = Check(root, write: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(Page(["| 1000 | `Undocumented` | Parse |  |"]), File.ReadAllText(Path.Combine(root.Path, PagePath)));
    }

    [Fact]
    public void WithoutThePageOption_TheInvocationIsInvalid()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);

        Result result = Run(root, ["error-codes"]);

        AssertInputError(result, "error-codes", "--page");
    }

    [Fact]
    public void APageThatDoesNotExist_IsAnInputError()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);

        Result result = Check(root);

        AssertInputError(result, PagePath, "does not exist");
    }

    [Fact]
    public void APageWithoutTheTable_IsAnInputErrorAndIsNotWritten()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write(PagePath, "# Error codes\n\nNo table here.\n");

        Result result = Check(root, write: true);

        AssertInputError(result, PagePath, "no table", "Code, Name, Stage and When it occurs");
        Assert.Equal("# Error codes\n\nNo table here.\n", File.ReadAllText(Path.Combine(root.Path, PagePath)));
    }

    [Fact]
    public void ALibraryThatWasNotBuilt_IsAnInputError()
    {
        using TempDirectory root = new();
        root.Write(PagePath, Page(Rows));

        Result result = Check(root);

        AssertInputError(result, "Great.Parts.Cel.dll", "does not exist", "-c Release");
    }

    [Fact]
    public void ALibraryWithoutItsDocumentation_IsAnInputError()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        File.Delete(Path.Combine(root.Path, "src/Great.Parts.Cel/bin/Release/net10.0/Great.Parts.Cel.xml"));
        root.Write(PagePath, Page(Rows));

        Result result = Check(root);

        AssertInputError(result, "Great.Parts.Cel.xml", "does not exist");
    }

    [Fact]
    public void ALibraryWithoutTheEnumeration_IsAnInputError()
    {
        using TempDirectory root = new();
        Build(root, "namespace Great.Parts.Cel.Diagnostics;\n\npublic enum CelErrorStage { Parse = 1 }\n");
        root.Write(PagePath, Page(Rows));

        Result result = Check(root);

        AssertInputError(result, "Great.Parts.Cel.Diagnostics.CelErrorCode");
    }

    [Fact]
    public void AFileThatIsNotAnAssembly_IsAnInputError()
    {
        using TempDirectory root = new();
        Build(root, LibrarySource);
        root.Write("src/Great.Parts.Cel/bin/Release/net10.0/Great.Parts.Cel.dll", "not an assembly");
        root.Write(PagePath, Page(Rows));

        Result result = Check(root);

        AssertInputError(result, "Great.Parts.Cel.dll", "cannot be read");
    }

    [Fact]
    public void ANumberInNoRangeOfTheStages_IsAnInputError()
    {
        using TempDirectory root = new();
        Build(root, "namespace Great.Parts.Cel.Diagnostics;\n\npublic enum CelErrorCode { Stray = 5000 }\n");
        root.Write(PagePath, Page(Rows));

        Result result = Check(root);

        AssertInputError(result, "5000", "CelErrorCode.Stray", "none of the ranges");
    }

    [Theory]
    [InlineData(1000, "Parse")]
    [InlineData(1999, "Parse")]
    [InlineData(2000, "Check")]
    [InlineData(2999, "Check")]
    [InlineData(3000, "Evaluate")]
    [InlineData(3999, "Evaluate")]
    [InlineData(4000, "Build")]
    [InlineData(4999, "Build")]
    [InlineData(9000, "Any")]
    [InlineData(9999, "Any")]
    [InlineData(999, null)]
    [InlineData(5000, null)]
    [InlineData(8999, null)]
    [InlineData(10000, null)]
    public static void TheStage_FollowsFromTheNumber(int number, string? stage) =>
        Assert.Equal(stage, ErrorCodeStages.Of(number));

    // The page: a title, an introduction, the table at the lines 5 to 11 and a closing line.
    private static string Page(IReadOnlyList<string> rows, string intro = "The codes.", string outro = "End.") =>
        string.Join("\n", ["# Error codes", "", intro, "", Header, Separator, .. rows, "", outro, ""]);

    // The rows of the page with the text of one row edited: pairs of the old and the new text.
    private static string[] Changed(string row, params (string Old, string New)[] edits)
    {
        string changed = edits.Aggregate(row, (text, edit) => text.Replace(edit.Old, edit.New, StringComparison.Ordinal));
        return [.. Rows.Select(candidate => candidate == row ? changed : candidate)];
    }

    private static Result Check(TempDirectory root, bool write = false) =>
        Run(root, ["error-codes", "--page", PagePath, .. write ? WriteOptions : []]);

    private static Result Run(TempDirectory root, IReadOnlyList<string> args)
    {
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new() { NewLine = "\n" };
        int exitCode = Program.Run([.. args, "--root", root.Path], [new ErrorCodesCommand()], root.Path, output, error);
        return new Result(exitCode, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString());
    }

    private static void AssertErrors(Result result, params Finding[] expected)
    {
        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(1, result.ExitCode);
        foreach (string line in result.Lines)
        {
            Assert.Matches(Finding.Pattern, line);
        }

        Assert.Equal(expected.Select(finding => finding.ToString()).Order(StringComparer.Ordinal), result.Lines.Order(StringComparer.Ordinal));
    }

    private static void AssertInputError(Result result, params string[] messageParts)
    {
        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        foreach (string part in messageParts)
        {
            Assert.Contains(part, result.Error, StringComparison.Ordinal);
        }
    }

    private static Finding Error(int line, string message) => FindingAssert.Error(EngCodes.Eng303, PagePath, line, 1, message);

    // Compiles the source into the place of the Release build with the XML documentation next to it, as `dotnet build` leaves them.
    private static void Build(TempDirectory root, string source, bool documented = true)
    {
        string dll = Path.Combine(root.Path, ErrorCodesCommand.LibraryPath);
        Directory.CreateDirectory(Path.GetDirectoryName(dll) ?? throw new InvalidOperationException("The library has no directory."));
        using FileStream dllStream = File.Create(dll);
        using FileStream xmlStream = File.Create(Path.ChangeExtension(dll, ".xml"));
        EmitResult result = Compile(source).Emit(dllStream, xmlDocumentationStream: documented ? xmlStream : null);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        if (!documented)
        {
            xmlStream.Write(Encoding.UTF8.GetBytes("<doc><members /></doc>"));
        }
    }

    private static CSharpCompilation Compile(string source) => CSharpCompilation.Create(
        "Great.Parts.Cel",
        [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp14, DocumentationMode.Diagnose))],
        [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

    private sealed record Result(int ExitCode, string[] Lines, string Error);
}
