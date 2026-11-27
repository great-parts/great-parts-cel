using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The union of two sets of unknown values in a partial evaluation is charged before it is made: 1 + ⌈(a + b)/10⌉ units, a and b the
// numbers of entries of the two sets, also when one set already holds the other. A loop that is unknown in every run makes a union of
// growing size in each one, so the units bound the square that the time and the memory would otherwise grow as.
public sealed class UnknownMergeCostTests
{
    private const ulong Limit = 1_000_000;

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(5, 5, 2)]
    [InlineData(5, 6, 3)]
    [InlineData(29, 1, 4)]
    [InlineData(100, 1, 12)]
    [InlineData(100, 100, 21)]
    public static void MergeUnits_IsOnePlusTheEntriesOfBothSetsPerTenRoundedUp(int first, int second, int units)
    {
        Assert.Equal((ulong)units, UnknownSet.MergeUnits(SetOf(0, first), SetOf(first, second)));
    }

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void UnionOfTwoSmallSets_IsChargedTwoUnits()
    {
        CelBindings bindings = PartialTestSupport.Bindings();
        var patterns = PartialTestSupport.Patterns("x,y");

        ulong whole = Cost("y + x", patterns, bindings);

        Assert.Equal(Cost("x", patterns, bindings) + Cost("y", patterns, bindings) + 2, whole);
    }

    // The set of the first operand has 29 trails, d[0] to d[28], and that of the second one, the whole d.
    [Fact]
    [Trait("Requirement", "R-3")]
    public static void UnionOfALargeSetAndASmallOne_IsChargedForTheSizeOfBoth()
    {
        const int Count = 29;
        long[] items = [.. Enumerable.Range(0, Count).Select(index => (long)index)];
        CelBindings bindings = new CelBindings().Add("l", PartialTestSupport.IntList(items)).Add("d", PartialTestSupport.IntList(items));
        var patterns = new[] { PartialTestSupport.P("d", PartialTestSupport.Any) };
        const string Large = "size(l.map(i, d[i]))";

        ulong whole = Cost(Large + " + size(d)", patterns, bindings);

        Assert.Equal(Cost(Large, patterns, bindings) + Cost("size(d)", patterns, bindings) + 4, whole);
    }

    // The two nodes of y are two entries with different places, so the union of their sets is a union of two sets of one entry.
    [Fact]
    [Trait("Requirement", "R-3")]
    public static void UnionOfTheSetsOfTwoNodesOfOneVariable_IsChargedTwoUnits()
    {
        CelBindings bindings = PartialTestSupport.Bindings();
        var patterns = PartialTestSupport.Patterns("y");

        ulong whole = Cost("y + y", patterns, bindings);

        Assert.Equal((2 * Cost("y", patterns, bindings)) + 2, whole);
    }

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void LoopThatIsUnknownInEveryRun_StopsAtTheCostLimit()
    {
        long[] items = [.. Enumerable.Range(0, 1000).Select(index => (long)index)];
        CelBindings bindings = new CelBindings().Add("l", PartialTestSupport.IntList(items)).Add("d", PartialTestSupport.IntList(items));
        var options = new CelEvalOptions { CostLimit = 20_000 };

        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate(
            "l.map(i, d[i])",
            [PartialTestSupport.P("d", PartialTestSupport.Any)],
            bindings,
            options);

        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(result.Errors).Code);
        Assert.True(result.Cost <= 20_000);
    }

    private static ulong Cost(string expression, IReadOnlyList<CelAttributePattern> patterns, CelBindings bindings)
    {
        var options = new CelEvalOptions { CostLimit = Limit };
        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate(expression, patterns, bindings, options);

        Assert.True(result.IsUnknown);
        return result.Cost!.Value;
    }

    // A set of count entries, one for each of the ids from firstId on.
    private static UnknownSet SetOf(int firstId, int count)
    {
        UnknownSet set = UnknownSet.Of(new UnknownEntry(firstId, "x", []));
        for (int id = firstId + 1; id < firstId + count; id++)
        {
            set = UnknownSet.Merge(set, UnknownSet.Of(new UnknownEntry(id, "x", [])));
        }

        return set;
    }
}
