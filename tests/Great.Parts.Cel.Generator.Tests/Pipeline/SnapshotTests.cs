using System;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Pipeline;

// The created text of each context of the catalog of the examples is compared with its file in Snapshots/. The files are the form of the
// created code, which is a contract of the minor versions (the page of the created code, "What is contract"): a change of the generator
// that changes a snapshot is reviewed as a change of the contract. With GPC_SNAPSHOT_DIR set, a test writes the actual text to the
// directory (absolute, or relative to the working copy: tests/Great.Parts.Cel.Generator.Tests/Snapshots) instead of reading the
// expected file and fails, so a snapshot is never accepted by a passing run.
public sealed class SnapshotTests
{
    public static TheoryData<string> Names => SnapshotContexts.Names;

    [Theory]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-2")]
    [MemberData(nameof(Names))]
    public static void CreatedText_IsTheSnapshot(string name)
    {
        SnapshotContext context = SnapshotContexts.Get(name);
        CSharpCompilation compilation = GeneratorExample.Compile(
            context.Source,
            nullable: context.Nullable,
            more: context.Stub is null ? null : [context.Stub],
            references: context.Foreign is null ? null : [GeneratorExample.Library(context.Foreign)]);
        GeneratorDriverRunResult result = GeneratorExample.Run(compilation);
        string actual = CreatedText(result);

        string? directory = Environment.GetEnvironmentVariable(SnapshotText.DirectoryVariable);
        if (!string.IsNullOrEmpty(directory))
        {
            SnapshotText.Write(directory, name, actual);
            Assert.Fail("snapshot written: review it (" + name + ")");
        }

        string? difference = SnapshotText.FirstDifference(SnapshotText.Read(name), actual, name);
        Assert.True(difference is null, difference);
    }

    // The text of the file that the context makes; a source with two contexts makes two files, which the snapshot holds one after the other
    // in the order of their hint names, each after a line that names it.
    private static string CreatedText(GeneratorDriverRunResult result)
    {
        GeneratedSourceResult[] sources = [.. Assert.Single(result.Results).GeneratedSources.OrderBy(source => source.HintName, StringComparer.Ordinal)];
        Assert.NotEmpty(sources);
        return sources.Length == 1
            ? sources[0].SourceText.ToString()
            : string.Join("\n", sources.Select(source => "// ===== " + source.HintName + " =====\n" + source.SourceText));
    }

    // The set of the files is the set of the contexts of the catalog: a snapshot without a context of the examples is added only with a
    // reason, and a context without a file is a context that nobody compares.
    [Fact]
    [Trait("Requirement", "S-2")]
    public void SnapshotFiles_AreTheContextsOfTheCatalog()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Snapshots");
        string[] files = Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory, "*.g.cs").Select(path => Path.GetFileName(path)[..^".g.cs".Length]).Order(StringComparer.Ordinal)]
            : [];

        string[] contexts = [.. SnapshotContexts.All.Select(context => context.Name).Order(StringComparer.Ordinal)];

        Assert.Equal(contexts, files);
    }

    // The created text that the snapshots hold has no trace of the machine and of the run: it is the same for another version of the
    // generator (the version is the marker) and has the line ends of the repository whatever they are on the file system.
    [Fact]
    [Trait("Requirement", "S-2")]
    public void Snapshot_HoldsTheMarkerOfTheVersionAndNoCarriageReturn()
    {
        foreach (SnapshotContext context in SnapshotContexts.All)
        {
            string? text = SnapshotText.Read(context.Name);

            Assert.NotNull(text);
            Assert.Contains("GeneratedCode(\"Great.Parts.Cel.Generator\", \"" + SnapshotText.VersionMarker + "\")", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\r\n", text, StringComparison.Ordinal);
        }
    }

    // The comparison itself, which is the code of this project.
    [Fact]
    public void Comparison_IgnoresTheLineEndsAndTheVersion()
    {
        const string Expected = "a\n[GeneratedCode(\"Great.Parts.Cel.Generator\", \"<version>\")]\nb\n";
        const string Actual = "a\r\n[GeneratedCode(\"Great.Parts.Cel.Generator\", \"1.2.3-preview\")]\r\nb\r\n";

        Assert.Null(SnapshotText.FirstDifference(Expected, Actual, "Smallest"));
    }

    [Fact]
    public void Comparison_NamesTheFirstDifferentLine()
    {
        string? difference = SnapshotText.FirstDifference("a\nb\nc\nd\n", "a\nb\nX\nY\n", "Smallest");

        Assert.Equal(
            "first difference at tests/Great.Parts.Cel.Generator.Tests/Snapshots/Smallest.g.cs line 3: expected \"c\", actual \"X\"",
            difference);
    }

    [Theory]
    [InlineData("a\nb\n", "a\nb\nc\n", 3, "<end of the text>", "c")]
    [InlineData("a\nb\nc\n", "a\nb\n", 3, "c", "<end of the text>")]
    public static void Comparison_ATextThatIsShorter_IsTheDifferenceAtTheLineAfterIt(string expected, string actual, int line, string expectedLine, string actualLine)
    {
        string? difference = SnapshotText.FirstDifference(expected, actual, "Kinds");

        Assert.Equal(
            "first difference at tests/Great.Parts.Cel.Generator.Tests/Snapshots/Kinds.g.cs line " + line + ": expected \"" + expectedLine + "\", actual \"" + actualLine + "\"",
            difference);
    }

    [Fact]
    public void Comparison_AMissingFile_IsTheDifferenceAtTheFirstLine()
    {
        string? difference = SnapshotText.FirstDifference(null, "a\n", "Nested");

        Assert.NotNull(difference);
        Assert.StartsWith("first difference at tests/Great.Parts.Cel.Generator.Tests/Snapshots/Nested.g.cs line 1", difference, StringComparison.Ordinal);
    }

    // A snapshot that is written is normalized: the version is the marker and the lines end with "\n".
    [Fact]
    public void Written_Snapshot_HoldsTheMarkerAndTheNormalizedLineEnds()
    {
        string directory = Path.Combine(Path.GetTempPath(), "gpc-snapshot-" + Guid.NewGuid().ToString("N"));
        try
        {
            SnapshotText.Write(directory, "Written", "x\r\n[GeneratedCode(\"Great.Parts.Cel.Generator\", \"9.9\")]\r\n");

            string text = File.ReadAllText(Path.Combine(directory, "Written.g.cs"));
            Assert.Equal("x\n[GeneratedCode(\"Great.Parts.Cel.Generator\", \"<version>\")]\n", text);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
