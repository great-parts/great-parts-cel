using System.Collections.Generic;
using System.Linq;
using Cel.Expr.Conformance.Test;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

public sealed class EmbeddedDataTests
{
    [Fact]
    public void CelSpecTestData_HasTheFilesAndCasesOfThePinnedCommit()
    {
        IReadOnlyList<string> files = EmbeddedData.TextFiles(EmbeddedData.CelSpecTestData);
        List<SimpleTestFile> parsed = [.. files.Select(file => SimpleTestFile.Parser.ParseFrom(EmbeddedData.ReadBinary(file)))];
        List<SimpleTest> cases = [.. parsed.SelectMany(file => file.Section).SelectMany(section => section.Test)];

        Assert.Equal(32, files.Count);
        Assert.Equal(2592, cases.Count);
        Assert.Equal(141, cases.Count(testCase => testCase.DisableCheck));
    }

    [Fact]
    public void CelSpecTestData_BinaryFormMatchesTheTextForm()
    {
        string path = EmbeddedData.CelSpecTestData + "basic.textproto";

        SimpleTestFile file = SimpleTestFile.Parser.ParseFrom(EmbeddedData.ReadBinary(path));

        Assert.Equal("basic", file.Name);
        Assert.Contains("name: \"basic\"", EmbeddedData.ReadText(path), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Commit_IsTheFullShaOfThePinnedCommit()
    {
        string commit = EmbeddedData.ReadText("tests/cel-spec/COMMIT").Trim();

        Assert.Equal(40, commit.Length);
        Assert.All(commit, character => Assert.True(char.IsAsciiHexDigitLower(character)));
    }

    [Fact]
    public void SuiteLists_AreEmbedded()
    {
        Assert.Contains("cel-spec conformance skip list", EmbeddedData.ReadText("tests/Great.Parts.Cel.Conformance/CelSpec/skips.md"), System.StringComparison.Ordinal);
        Assert.Contains("Regression corpus skip list", EmbeddedData.ReadText("tests/Great.Parts.Cel.Conformance/Regression/skips.md"), System.StringComparison.Ordinal);
        Assert.Contains("Known differences", EmbeddedData.ReadText("tests/Great.Parts.Cel.Conformance/Regression/known-differences.md"), System.StringComparison.Ordinal);
    }
}
