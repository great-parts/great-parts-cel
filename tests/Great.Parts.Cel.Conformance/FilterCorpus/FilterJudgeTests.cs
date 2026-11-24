using System;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.FilterCorpus;

// The judge of the corpus must fail a case whose expectation is wrong; a judge that passes everything would make the corpus worthless.
public sealed class FilterJudgeTests
{
    private static FilterTest Case(string filter, string cel = "", ErrorExpectation? error = null)
    {
        FilterTest test = new() { Name = "case", Filter = filter, Cel = cel, Error = error };
        test.Resource.Add(new Resource { Value = new Item { SingleInt64 = 5 }, Match = true });
        test.Resource.Add(new Resource { Value = new Item { SingleInt64 = 6 }, Match = false });
        return test;
    }

    private static Judgement Judge(FilterTest test) => new FilterJudge().Judge(test);

    [Fact]
    public void Case_WithTheRightTreeAndResults_Passes()
    {
        Assert.True(Judge(Case("single_int64 = 5", "single_int64 == 5")).Passed);
    }

    [Fact]
    public void Case_WithAWrongTree_Fails()
    {
        Judgement judgement = Judge(Case("single_int64 = 5", "single_int64 == 6"));

        Assert.False(judgement.Passed);
        Assert.Contains("the tree differs", judgement.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Case_WithAWrongResult_Fails()
    {
        Judgement judgement = Judge(Case("single_int64 > 5"));

        Assert.False(judgement.Passed);
        Assert.Contains("resource 1", judgement.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Case_ThatDoesNotTranslateButExpectsNoError_Fails()
    {
        Judgement judgement = Judge(Case("single_int64 = abc"));

        Assert.False(judgement.Passed);
        Assert.Contains("does not translate", judgement.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorCase_WithTheRightCodeAndPlace_Passes()
    {
        ErrorExpectation error = new() { Code = "InvalidFilterValue", Start = 15, Length = 3, Subject = "single_int64" };

        Assert.True(Judge(Case("single_int64 = abc", error: error)).Passed);
    }

    [Theory]
    [InlineData("FilterSyntaxError", 15, 3)]
    [InlineData("InvalidFilterValue", 14, 3)]
    [InlineData("InvalidFilterValue", 15, 4)]
    public static void ErrorCase_WithAWrongCodeOrPlace_Fails(string code, int start, int length)
    {
        ErrorExpectation error = new() { Code = code, Start = start, Length = length };

        Assert.False(Judge(Case("single_int64 = abc", error: error)).Passed);
    }

    [Fact]
    public void ErrorCase_WithAWrongSubjectOrCount_Fails()
    {
        Assert.False(Judge(Case("single_int64 = abc", error: new ErrorExpectation { Code = "InvalidFilterValue", Start = 15, Length = 3, Subject = "single_int32" })).Passed);
        Assert.False(Judge(Case("single_int64 = abc", error: new ErrorExpectation { Code = "InvalidFilterValue", Start = 15, Length = 3, ErrorCount = 2 })).Passed);
        Assert.False(Judge(Case("single_int64 = abc", error: new ErrorExpectation { Code = "InvalidFilterValue", Start = 15, Length = 3, LastCode = "TooManyErrors" })).Passed);
    }

    [Fact]
    public void ErrorCase_ForAFilterThatCompiles_Fails()
    {
        Judgement judgement = Judge(Case("single_int64 = 5", error: new ErrorExpectation { Code = "InvalidFilterValue", Start = 15, Length = 1 }));

        Assert.False(judgement.Passed);
        Assert.Contains("the filter compiles", judgement.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Case_WithRepeatAndTail_IsTheUnitRepeatedAndTheTail()
    {
        FilterTest test = new() { Name = "chain", Filter = "single_int64 = 5 AND ", Repeat = 3, Tail = "single_int64 = 5", Cel = "single_int64 == 5 && single_int64 == 5 && single_int64 == 5 && single_int64 == 5" };
        test.Resource.Add(new Resource { Value = new Item { SingleInt64 = 5 }, Match = true });

        Assert.True(Judge(test).Passed);
    }
}
