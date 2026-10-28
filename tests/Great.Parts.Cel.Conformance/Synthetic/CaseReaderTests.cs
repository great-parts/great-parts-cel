using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cel.Expr.Conformance.Test;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The common model of a case and the positions of the test tokens (docs/design/conformance.md, "Data and build", "Core and suites").
public sealed class CaseReaderTests
{
    private const string SuitePath = "tests/Great.Parts.Cel.Conformance/Synthetic/synthetic.textproto";

    [Fact]
    public void SyntheticSuite_CasesHaveTheirNamesAndTheTestTokensOfTheText()
    {
        IReadOnlyList<ConformanceCase> cases = CaseReader.ReadEmbedded(SuitePath);

        Assert.Equal(
            [
                "synthetic/failing/wrong_expectation (12,3) 1 + 1 value",
                "synthetic/passing/int (20,3) 1 + 1 value",
                "synthetic/passing/no_matcher (26,3) {\"test\": 1}[\"test\"] == 1 none",
            ],
            cases.Select(testCase => $"{testCase.Name} ({testCase.Line},{testCase.Column}) {testCase.Expression} {testCase.Expected.Matcher}"));
        Assert.All(cases, testCase => Assert.Equal(SuitePath, testCase.Path));
    }

    [Fact]
    public void CelSpecSuite_EveryCaseHasAPositionInItsFileAndAUniqueName()
    {
        List<ConformanceCase> cases = [.. EmbeddedData.TextFiles(EmbeddedData.CelSpecTestData).SelectMany(CaseReader.ReadEmbedded)];

        Assert.Equal(2592, cases.Count);
        Assert.Equal(2592, cases.Select(testCase => testCase.Name).Distinct(System.StringComparer.Ordinal).Count());
        Assert.Equal(141, cases.Count(testCase => testCase.Check == CheckMode.WithoutCheck));
        Assert.Contains(cases, testCase => testCase.Name == "basic/self_eval_zeroish/self_eval_int_zero" && (testCase.Line, testCase.Column) == (9, 3));
    }

    [Theory]
    [InlineData(false, false, "Check")]
    [InlineData(true, false, "WithoutCheck")]
    [InlineData(false, true, "CheckOnly")]
    public static void CheckMode_IsTakenFromDisableCheckAndCheckOnly(bool disableCheck, bool checkOnly, string mode)
    {
        SimpleTestFile file = File(new SimpleTest { Name = "t", DisableCheck = disableCheck, CheckOnly = checkOnly });

        Assert.Equal(mode, Assert.Single(CaseReader.Read("f.textproto", file, "section {\n  test {}\n}")).Check.ToString());
    }

    [Theory]
    [InlineData("section { }")]
    [InlineData("section { test {} test {} }")]
    [InlineData("section { name: 'a' } section { test {} }")]
    [InlineData("section { case { test {} } }")]
    public static void TextWithOtherCasesThanTheFile_Throws(string text)
    {
        SimpleTestFile file = File(new SimpleTest { Name = "t" });

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => CaseReader.Read("f.textproto", file, text));
        Assert.StartsWith("The scan of 'f.textproto' finds sections with [", exception.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Positions_SkipCommentsAndStrings_AndCountColumnsFromOne()
    {
        string text = string.Join(
            "\n",
            "# section { test {",
            "section {",
            "  name: \"test { section\" description: 'it\\'s { test'",
            "  test { name: 'a' }  test{",
            "  }",
            "  tests { }",
            "}",
            "section: {",
            "\ttest {",
            "    test { }",
            "  }",
            "}");

        IReadOnlyList<IReadOnlyList<TextPosition>> positions = CasePositions.Read(text);

        Assert.Equal(["(4,3) (4,23)", "(9,2)"], positions.Select(section => string.Join(" ", section.Select(position => $"({position.Line},{position.Column})"))));
    }

    private static SimpleTestFile File(SimpleTest test) =>
        new() { Name = "f", Section = { new SimpleTestSection { Name = "s", Test = { test } } } };
}
