using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.FilterCorpus;

// The corpus of the filtering package on the library (docs/design/filtering-corpus.md): every case passes, there are enough of them, and
// they cover every code that a filter reports.
public sealed class FilteringSuiteTests
{
    // The topics of the files of the corpus.
    private static readonly string[] Topics = ["aip_examples", "functions", "has", "limits", "search", "syntax", "traversal", "values", "wildcards"];

    // The codes that the filtering package or its type checking reports for a filter.
    private static readonly string[] ErrorCodes =
    [
        "ExpressionTooLong",
        "FilterSyntaxError",
        "InvalidFilterRestriction",
        "InvalidFilterValue",
        "NestingTooDeep",
        "NoMatchingOverload",
        "NoSuchField",
        "TooManyErrors",
        "UndeclaredReference",
    ];

    [Fact]
    public void AllCases_Pass() =>
        SuiteAssert.PassOrAreSkipped(FilteringSuite.Run(RunCell.Current), FilteringSuite.Source);

    [Fact]
    public void Corpus_HasAtLeastTwoHundredCases()
    {
        IReadOnlyList<FilterCase> cases = FilteringSuite.ReadCases();

        Assert.True(cases.Count >= 200, string.Create(CultureInfo.InvariantCulture, $"the corpus has {cases.Count} cases"));
        Assert.Equal(cases.Count, FilteringSuite.Run(RunCell.Current).Total);
    }

    [Fact]
    public void Corpus_HasAFileForEveryTopic()
    {
        string[] files = [.. FilteringSuite.ReadCases().Select(testCase => testCase.Path).Distinct()];

        Assert.Equal(Topics.Select(topic => $"tests/filtering/testdata/{topic}.textproto"), files);
    }

    [Fact]
    public void Corpus_HasAnErrorCaseForEveryCode()
    {
        string[] codes =
        [
            .. FilteringSuite.ReadCases()
                .SelectMany(testCase => new[] { testCase.Test.Error?.Code, testCase.Test.Error?.LastCode })
                .OfType<string>()
                .Where(code => code.Length > 0)
                .Distinct()
                .Order(),
        ];

        Assert.Equal(ErrorCodes, codes);
    }

    [Fact]
    public void Corpus_HasCasesThatEvaluateOnItems()
    {
        IReadOnlyList<FilterCase> cases = FilteringSuite.ReadCases();

        Assert.True(cases.Count(testCase => testCase.Test.Resource.Count > 0) >= 100);
        Assert.True(cases.Count(testCase => testCase.Test.Cel.Length > 0) >= 100);
        Assert.True(cases.Count(testCase => testCase.Test.Error is not null) >= 50);
    }
}
