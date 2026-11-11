using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.StringsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The cost of the functions of the set strings, row by row of the cost table: a call is 1 unit and ⌈Lₐ/10⌉ for the lengths of the
// strings it is given, then ⌈Lᵣ/10⌉ for the length of its result, which replace and split and the other builders charge as the result
// grows, and split also 1 for each part. The operands of the rows are literals, which cost 0, so the cost of a row is that of its call.
// The cost is exact: the evaluation passes with it as the limit and stops with CostLimitExceeded at one unit less.
public sealed class StringCostTests
{
    // "expression | units"
    private const string Rows = """
        'abcdefghijk'.charAt(2) | 4
        'a😀b'.charAt(1) | 3
        'abcdefghijk'.substring(3, 8) | 4
        'abcdefghijk'.substring(3) | 4
        'abcdefghijk'.trim() | 5
        'abcdefghijk'.upperAscii() | 5
        'abcdefghijk'.lowerAscii() | 5
        'abcdefghijk'.reverse() | 5
        strings.quote('abc') | 3
        'abcdefghijk'.indexOf('cd') | 3
        'abcdefghijk'.indexOf('cd', 4) | 3
        'abcdefghijk'.lastIndexOf('cd') | 3
        'abcdefghijk'.lastIndexOf('cd', 5) | 3
        'a,b,c'.split(',') | 6
        'a,b,c'.split(',', 2) | 5
        'a,b,c'.split(',', 0) | 2
        'aXbXc'.replace('X', '--') | 3
        'aXbXc'.replace('X', '--', 1) | 3
        'abc'.replace('z', 'yy') | 3
        ['a', 'b'].join('-') | 8
        ['ab', 'cd', 'ef'].join() | 10
        [].join() | 2
        """;

    // A string of 25 code units takes three units, ⌈25/10⌉, in each of the terms, and the pieces that a builder charges one by one add
    // up to the same.
    private static readonly string Long = "'" + new string('a', 25) + "'";

    public static TheoryData<string, string> CostRows => CostData();

    [Theory]
    [MemberData(nameof(CostRows))]
    public static void Expression_CostsItsUnits(string expression, string units)
    {
        ulong cost = ulong.Parse(units, CultureInfo.InvariantCulture);

        Assert.True(Evaluate(expression, null, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, cost - 1).Errors).Code);
    }

    // The formula of the call is charged before an index error, which has no result to charge: the error comes with 1 + ⌈Lₐ/10⌉.
    [Theory]
    [InlineData("'abc'.charAt(5)")]
    [InlineData("'abc'.substring(2, 1)")]
    [InlineData("'abc'.substring(4)")]
    [InlineData("'abc'.indexOf('a', 4)")]
    public static void Error_IsReachedAfterTheFormulaOfTheCall(string expression)
    {
        Assert.NotEqual(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, 2).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, 1).Errors).Code);
    }

    // The limit stops a build between its pieces: a result of 50 code units that costs 5 never gets its memory when 4 remain.
    [Fact]
    public static void Replace_StopsAtTheLimitBeforeTheResultIsComplete()
    {
        string expression = Long + ".replace('a', 'bb')";

        Assert.True(Evaluate(expression, null, 9).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, 8).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, 5).Errors).Code);
    }

    private static TheoryData<string, string> CostData()
    {
        TheoryData<string, string> data = [];
        foreach (string row in Rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1]);
        }

        data.Add(Long + ".replace('a', 'bb')", "9");
        data.Add(Long + ".split('')", "32");
        data.Add(Long + ".reverse()", "7");
        data.Add(Long + ".indexOf('ab')", "4");
        data.Add(Long + ".trim()", "7");
        data.Add(Long + ".substring(5, 20)", "6");
        return data;
    }
}
