using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.StringsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Sets;

// The functions of the set sets on lists of mixed numeric kinds, which only an operand of the type dyn can hold, and their cost, row by
// row of the cost table. The elements compare by CEL equality: 1, 1u and 1.0 are equal, a NaN equals nothing, values of different kinds
// are unequal without an error, and lists compare by content.
public sealed class SetsFunctionTests
{
    // "a | b | function | result": a and b are the values of the variables a and b of the type dyn.
    private const string Rows = """
        [1, 2, 3] | [3u, 2.0] | contains | true
        [1, 2, 3] | [3u, 2.5] | contains | false
        [1.5] | [1.5] | contains | true
        [] | [] | contains | true
        [1] | [] | contains | true
        [] | [1] | contains | false
        [[1, 2]] | [[1.0, 2u]] | contains | true
        ['a', 'b'] | ['b'] | contains | true
        [double('NaN')] | [double('NaN')] | contains | false
        [1, 'a'] | ['a'] | contains | true
        [1, 'a'] | [2] | contains | false
        [null] | [null] | contains | true
        [1.5, 2] | [2u] | intersects | true
        [1.5] | [1] | intersects | false
        [] | [1] | intersects | false
        [1] | [] | intersects | false
        [1, 2] | [3, 4] | intersects | false
        [double('NaN')] | [double('NaN')] | intersects | false
        [1, 2, 2] | [2.0, 1u] | equivalent | true
        [1, 2] | [1, 3] | equivalent | false
        [] | [] | equivalent | true
        [1] | [] | equivalent | false
        [1, 1] | [1] | equivalent | true
        [1.0, 2.5] | [2.5, 1u] | equivalent | true
        """;

    // "expression | units"
    private const string Costs = """
        sets.contains([5, 6], [6]) | 9
        sets.contains([5, 6], [7, 5]) | 10
        sets.contains([], [1]) | 5
        sets.intersects([1, 2, 3], [3]) | 13
        sets.intersects([1], []) | 5
        sets.contains(['aa', 'b'], ['b']) | 11
        sets.equivalent([1], [2, 1]) | 8
        sets.equivalent([1, 2], [1, 2]) | 17
        """;

    public static TheoryData<string, string, string, bool> ResultRows => ResultData();

    public static TheoryData<string, string> CostRows => CostData();

    [Theory]
    [MemberData(nameof(ResultRows))]
    public static void Function_ComparesByCelEquality(string first, string second, string function, bool expected)
    {
        CelBindings bindings = new CelBindings().Add("a", Evaluate(first).GetValueOrThrow()).Add("b", Evaluate(second).GetValueOrThrow());

        CelEvalResult<CelValue> result = Evaluate("sets." + function + "(a, b)", bindings);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool actual));
        Assert.Equal(expected, actual);
    }

    // The same rows with static types: lists of one element type.
    [Theory]
    [InlineData("sets.contains(['a'], ['a', 'b'])", false)]
    [InlineData("sets.intersects([1], [2])", false)]
    [InlineData("sets.intersects(['x', 'y'], ['y'])", true)]
    [InlineData("sets.equivalent(['a', 'b'], ['b', 'a', 'a'])", true)]
    [InlineData("sets.equivalent([1], [1, 2])", false)]
    public static void Function_OnListsOfOneType(string expression, bool expected)
    {
        Assert.True(Evaluate(expression).GetValueOrThrow().TryGetBoolean(out bool actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(CostRows))]
    public static void Expression_CostsItsUnits(string expression, string units)
    {
        ulong cost = ulong.Parse(units, CultureInfo.InvariantCulture);

        Assert.True(Evaluate(expression, null, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, cost - 1).Errors).Code);
    }

    // Two reads of variables at 1 each, and then 1 for the call and, for each element looked up, 1 and the comparisons made.
    [Fact]
    public static void Cost_OfListsOfMixedNumbers_CountsEachComparison()
    {
        CelBindings bindings = new CelBindings()
            .Add("a", Evaluate("[1, 2]").GetValueOrThrow())
            .Add("b", Evaluate("[2.0, 1u]").GetValueOrThrow());

        Assert.True(Evaluate("sets.contains(a, b)", bindings, 8).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("sets.contains(a, b)", bindings, 7).Errors).Code);
    }

    [Theory]
    [InlineData("sets.contains(a, b)")]
    [InlineData("sets.intersects(a, b)")]
    [InlineData("sets.equivalent(a, b)")]
    public static void ArgumentThatIsNoList_IsNoMatchingOverload(string expression)
    {
        CelBindings bindings = new CelBindings().Add("a", 5L).Add("b", Evaluate("[1]").GetValueOrThrow());

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(Evaluate(expression, bindings).Errors).Code);
    }

    // An error value that an element is comes out as the value of the call, whether it is looked up or the list it is looked up in.
    [Theory]
    [InlineData("sets.contains(a, b)")]
    [InlineData("sets.intersects(a, b)")]
    [InlineData("sets.equivalent(a, b)")]
    [InlineData("sets.contains(b, a)")]
    [InlineData("sets.intersects(b, a)")]
    [InlineData("sets.equivalent(b, a)")]
    public static void ElementThatIsAnError_IsTheValueOfTheCall(string expression)
    {
        CelList unreadable = CelList.CreateView<string>(
            ["x"],
            static _ => throw new InvalidOperationException("unreadable"));
        CelBindings bindings = new CelBindings().Add("a", CelValue.FromList(unreadable)).Add("b", Evaluate("[1]").GetValueOrThrow());

        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(Evaluate(expression, bindings).Errors).Code);
    }

    private static TheoryData<string, string, string, bool> ResultData()
    {
        TheoryData<string, string, string, bool> data = [];
        foreach (string row in Rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1], cells[2], cells[3] == "true");
        }

        return data;
    }

    private static TheoryData<string, string> CostData()
    {
        TheoryData<string, string> data = [];
        foreach (string row in Costs.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1]);
        }

        return data;
    }
}
