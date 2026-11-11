using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Strings.FormatEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The cost of format, row by row of the cost table: 1 for the call, n for the size of the list and ⌈Lₐ/10⌉ for the length of the format
// string, all charged before the call runs, then ⌈Lᵣ/10⌉ for the length of the result, charged as the result grows, with the pieces of
// nested lists and maps in it. The cost is exact: the evaluation passes with it as the limit and stops with CostLimitExceeded at one unit
// less. A list literal is 1 and 1 for each element, and a read of a variable 1; the operands that are literals cost nothing.
public sealed class FormatCostTests
{
    // "expression | units"
    private const string Rows = """
        '%s'.format(['abcdefghijk']) | 7
        'ab%sab'.format(['xyz']) | 6
        '%s'.format([[1, 2, 3]]) | 10
        '%d%d%d'.format([1, 2, 3]) | 10
        '%%'.format([]) | 4
        '%.100f'.format([1.5]) | 16
        """;

    public static TheoryData<string, string> CostRows => RowData();

    [Theory]
    [MemberData(nameof(CostRows))]
    public static void Expression_CostsItsUnits(string expression, string units)
    {
        ulong cost = ulong.Parse(units, CultureInfo.InvariantCulture);

        Assert.True(Evaluate(expression, null, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, null, cost - 1).Errors).Code);
    }

    // The elements past the last clause are in n, and the read of the two variables is 1 each: 2 + (1 + 3 + 1 + 1).
    [Fact]
    public static void Elements_PastTheClauses_AreInTheSizeOfTheList()
    {
        CelBindings bindings = Bindings("%s", CelValue.FromString("a"), CelValue.FromInt64(1), CelValue.FromInt64(2));

        AssertCost("f.format(items)", bindings, 8);
    }

    // A format string of 25 code units without a clause: 1 + 0 + 3 for the call and 3 for the result, and 1 for the list literal.
    [Fact]
    public static void FormatString_IsChargedByItsLength()
    {
        AssertCost("'" + new string('a', 25) + "'.format([])", null, 8);
    }

    [Fact]
    public static void LongString_IsChargedByItsLength()
    {
        CelBindings bindings = Bindings("%s", CelValue.FromString(new string('x', 1000)));

        AssertCost("f.format(items)", bindings, 2 + 1 + 1 + 1 + 100);
    }

    [Fact]
    public static void TwoPieces_AddUpToTheLengthOfTheResult()
    {
        CelBindings bindings = Bindings("%s%s", CelValue.FromString(new string('x', 25)), CelValue.FromString(new string('y', 25)));

        AssertCost("f.format(items)", bindings, 2 + 1 + 2 + 1 + 5);
    }

    // The map is charged for its texts as they are written and for the characters between them ahead, so that the units are
    // those of the length of the result: 12 characters are 2 units.
    [Fact]
    public static void Map_IsChargedByTheLengthOfItsText()
    {
        CelValue map = CelValue.FromMap(CelMap.Create(
        [
            KeyValuePair.Create(CelValue.FromString("b"), CelValue.FromInt64(2)),
            KeyValuePair.Create(CelValue.FromString("a"), CelValue.FromInt64(1)),
        ]));

        AssertCost("f.format(items)", Bindings("%s", map), 2 + 1 + 1 + 1 + 2);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    [InlineData(40)]
    public static void MapOfPairs_AndTheListsAround_AreChargedByTheLengthOfTheResult(int count)
    {
        var entries = new List<KeyValuePair<CelValue, CelValue>>();
        for (int i = 0; i < count; i++)
        {
            entries.Add(KeyValuePair.Create(CelValue.FromString("key" + i.ToString(CultureInfo.InvariantCulture)), CelValue.FromList(CelList.Create(CelValue.FromInt64(i)))));
        }

        CelValue map = CelValue.FromMap(CelMap.Create(entries));
        CelBindings bindings = Bindings("<%s>", CelValue.FromList(CelList.Create(map, map)));
        int resultLength = Format("<%s>", CelValue.FromList(CelList.Create(map, map))).Length;

        AssertCost("f.format(items)", bindings, 2 + 1 + 1 + 1 + (ulong)((resultLength + 9) / 10));
    }

    // The error of a clause comes after the argument cost and the text before it, and has no result to charge.
    [Fact]
    public static void Error_IsReachedAfterTheUnitsChargedBeforeIt()
    {
        CelBindings kind = Bindings("%d", CelValue.FromString("a"));
        CelBindings afterText = Bindings("abcdefghijk%d", CelValue.FromString("x"));

        Assert.Equal(CelErrorCode.InvalidArgument, Assert.Single(Evaluate("f.format(items)", kind, 5).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("f.format(items)", kind, 4).Errors).Code);
        Assert.Equal(CelErrorCode.InvalidArgument, Assert.Single(Evaluate("f.format(items)", afterText, 8).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("f.format(items)", afterText, 7).Errors).Code);
    }

    // The limit stops the build between its pieces, so that a result of 2 000 code units that costs 200 never gets its memory when 150
    // units remain.
    [Fact]
    public static void Limit_StopsTheBuildBeforeTheResultIsComplete()
    {
        CelBindings bindings = Bindings("%s", CelValue.FromString(new string('x', 2000)));

        Assert.True(Evaluate("f.format(items)", bindings, 2 + 1 + 1 + 1 + 200).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("f.format(items)", bindings, 150).Errors).Code);
    }

    private static void AssertCost(string expression, CelBindings? bindings, ulong units)
    {
        Assert.True(Evaluate(expression, bindings, units).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, bindings, units - 1).Errors).Code);
    }

    private static TheoryData<string, string> RowData()
    {
        TheoryData<string, string> data = [];
        foreach (string row in Rows.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1]);
        }

        return data;
    }
}
