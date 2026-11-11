using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.MathSet.MathEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.MathSet;

// The cost of the macros and the functions of the sets math and bindings, row by row of the cost table: a call is 1 unit, a list form
// of math.@max or math.@min adds 1 for each element, a macro costs its expansion, and the three or more arguments of math.greatest
// and math.least are a list literal of 1 + n. The operands of the rows are literals, which cost 0, or the variables i and j, which
// cost 1 each. The cost is exact: the evaluation passes with it as the limit and stops with CostLimitExceeded at one unit less.
public sealed class MathCostTests
{
    // "expression | units"
    private const string Rows = """
        math.ceil(1.5) | 1
        math.floor(1.5) | 1
        math.round(1.5) | 1
        math.trunc(1.5) | 1
        math.isInf(1.5) | 1
        math.isNaN(1.5) | 1
        math.isFinite(1.5) | 1
        math.abs(5) | 1
        math.abs(5u) | 1
        math.abs(5.5) | 1
        math.sign(5) | 1
        math.sign(5u) | 1
        math.sign(5.5) | 1
        math.sqrt(4) | 1
        math.sqrt(4u) | 1
        math.sqrt(4.0) | 1
        math.bitAnd(6, 3) | 1
        math.bitOr(6u, 3u) | 1
        math.bitXor(6, 3) | 1
        math.bitNot(6) | 1
        math.bitShiftLeft(1, 2) | 1
        math.bitShiftRight(8u, 2) | 1
        math.greatest(5) | 1
        math.least(5.5) | 1
        math.greatest(1, 2) | 1
        math.least(1u, 2) | 1
        math.greatest(1, 2.5) | 1
        math.greatest(1, 2, 3) | 8
        math.least(1, 2, 3, 4) | 10
        math.greatest([5]) | 4
        math.least([5, 1]) | 6
        math.greatest([1.5, 2.5, 3.5]) | 8
        math.greatest(math.ceil(1.5), math.floor(2.5)) | 3
        """;

    public static TheoryData<string, string> CostRows => CostData();

    [Theory]
    [MemberData(nameof(CostRows))]
    public static void Expression_CostsItsUnits(string expression, string units)
    {
        ulong cost = ulong.Parse(units, CultureInfo.InvariantCulture);

        Assert.True(Evaluate(expression, null, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, cost - 1).Errors).Code);
    }

    // An error that comes after the formula of the call: the least int and a NaN are met with the cost charged before the function ran.
    [Fact]
    public static void ErrorOfTheFunction_IsReachedAfterTheFormulaOfTheCall()
    {
        Assert.NotEqual(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("math.abs(-9223372036854775807 - 1)", null, 3).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("math.abs(-9223372036854775807 - 1)", null, 1).Errors).Code);
        Assert.NotEqual(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("math.greatest(1.0, 0.0 / 0.0)", null, 3).Errors).Code);
    }

    // The list form charges 1 + n before it reads the first element: a list of 10 elements stops a limit of 10 before any comparison.
    [Fact]
    public static void ListForm_ChargesItsElementsBeforeItReadsThem()
    {
        const string Expression = "math.greatest(x)";
        CelValue[] items = new CelValue[10];
        for (int index = 0; index < items.Length; index++)
        {
            items[index] = CelValue.FromInt64(index);
        }

        CelBindings ten = new CelBindings().Add("x", ListOf(items));

        Assert.True(Evaluate(Expression, ten, 12).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(Expression, ten, 11).Errors).Code);
    }

    // "expression | units" of cel.bind: the empty list that is the range costs 1, no step runs, the expressions of the author cost by
    // their rows and each read of the bound name in the result is 1.
    [Theory]
    [InlineData("cel.bind(v, 1, 2)", 1)]
    [InlineData("cel.bind(v, 1, v)", 2)]
    [InlineData("cel.bind(v, 1, v + 1)", 3)]
    [InlineData("cel.bind(v, 1 + 2, v + v)", 5)]
    [InlineData("cel.bind(a, 1, cel.bind(b, a, b))", 4)]
    [InlineData("cel.bind(v, i, 2)", 2)]
    [InlineData("cel.bind(v, i, v)", 3)]
    public static void Bind_CostsItsExpansion(string expression, int units)
    {
        ulong cost = (ulong)units;

        Assert.True(Evaluate(expression, null, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, cost - 1).Errors).Code);
    }

    private static TheoryData<string, string> CostData()
    {
        TheoryData<string, string> data = [];
        foreach (string row in Rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1]);
        }

        return data;
    }
}
