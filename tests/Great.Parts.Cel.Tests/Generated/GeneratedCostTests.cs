using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Generator.Samples.Families;
using Great.Parts.Cel.Tests.Examples.Generated;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// The units that the created code charges for the read of a member of the sample class Collections ("Charges of a read" of the page of the
// created code): the unit of the selection is the evaluation's, and the description adds the units beyond it through FieldReadCost, only
// with a cost limit, only for a selection, and before the copy that it pays for. A member that is a list costs the unit of the selection
// only, so the evaluation of the same text on the list member is the base of every comparison.
public sealed class GeneratedCostTests
{
    private static readonly CelEnvironment Environment = GeneratedExample.Builder(CollectionContext.Default)
        .AddVariable<Collections>("c")
        .Build()
        .GetEnvironmentOrThrow();

    // A set of n elements: 1 + c * n * bits(n) (c is 1 for numbers); 7 numbers is 1 + 7 * 3 = 22, and none is 1.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void Set_ChargesBeyondTheSelectionByItsFormula()
    {
        Assert.Equal(1UL, Extra(new Collections(), "c.hash"));
        Assert.Equal(22UL, Extra(new Collections { Hash = [7, 6, 5, 4, 3, 2, 1] }, "c.hash"));
        Assert.Equal(22UL, Extra(new Collections { ReadOnlySet = new HashSet<long> { 1, 2, 3, 4, 5, 6, 7 } }, "c.readOnlySet"));
    }

    // The element of a set that is a string makes c = 1 + ceil(2 * Lmax / 10): three strings, the longest of 10 units, is 1 + 3 * 3 * 2 = 19.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void SetOfStrings_ChargesByTheLongestElement()
    {
        var collections = new Collections { Sorted = new SortedSet<string> { "abcdefghij", "a", "bb" } };

        Assert.Equal(19UL, Extra(collections, "c.sorted"));
    }

    // A collection without an index: 1 + n.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void CollectionWithoutAnIndex_ChargesOnePlusItsSize()
    {
        var collections = new Collections { Queue = new Queue<long>([1, 2, 3, 4]), Stack = new Stack<string>(["a", "b", "c", "d"]), Linked = new LinkedList<long>([1, 2]) };

        Assert.Equal(5UL, Extra(collections, "c.queue"));
        Assert.Equal(5UL, Extra(collections, "c.stack"));
        Assert.Equal(3UL, Extra(collections, "c.linked"));
    }

    // A structure that is a list is boxed: 1 + ceil(S / 64) with S of the 24 bytes of three longs; the view over the box costs nothing more.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void StructureThatIsAList_ChargesTheBox()
    {
        Assert.Equal(2UL, Extra(new Collections { Fixed = new Triple(1, 2, 3) }, "c.fixed"));
    }

    // A member that is null reads as UnexpectedNull and copies nothing: no units beyond the selection. has() asks no description.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void NullSet_AndPresenceTest_ChargeNothingBeyondTheSelection()
    {
        var collections = new Collections { Hash = null!, Queue = new Queue<long>([1, 2, 3]) };

        Assert.Equal(0UL, Extra(collections, "c.hash"));
        Assert.Equal(0UL, Extra(collections, "has(c.queue)"));
        Assert.Equal(0UL, Extra(collections, "has(c.hash)"));
    }

    // The charges of one text add up: two selections of the set pay twice.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void TwoSelectionsOfASet_PayTwice()
    {
        var collections = new Collections { Hash = [1, 2, 3] };

        // Three elements are 1 + 1 * 3 * 2 = 7 units for each selection.
        Assert.Equal(7UL, Extra(collections, "c.hash"));
        Assert.Equal(14UL, Extra(collections, "c.hash.size() + c.hash.size()"));
    }

    // The limit stops the read before the copy: with the units of the selection and 21 of the 22 of the set the evaluation is
    // CostLimitExceeded at the selection, and with the 22 it reads the sorted list.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void LimitBelowTheCharge_IsCostLimitExceededAtTheSelectionBeforeTheCopy()
    {
        var collections = new Collections { Hash = [7, 6, 5, 4, 3, 2, 1] };
        const string Text = "c.hash";
        ulong baseline = Charged(new Collections(), "c.mutable", ulong.MaxValue);
        Plan plan = Environment.Compile<CelValue>(Text).GetProgramOrThrow().Plan;

        CelValue refused = plan.Run(plan.CreateFrame(Bind(collections), baseline + 21, CancellationToken.None));
        CelValue accepted = plan.Run(plan.CreateFrame(Bind(collections), baseline + 22, CancellationToken.None));

        Assert.True(refused.TryGetError(out CelError? error));
        Assert.Equal((CelErrorCode.CostLimitExceeded, Text.IndexOf('.', StringComparison.Ordinal)), (error!.Code, error.Location.Offset));
        Assert.True(accepted.TryGetList(out CelList? list));
        Assert.Equal(7, list!.Count);
    }

    // Without a limit the charge is not asked and the read is the same.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void WithoutALimit_TheSetIsReadSortedAsAlways()
    {
        var collections = new Collections { Hash = [7, 6, 5] };
        Plan plan = Environment.Compile<bool>("c.hash == [5, 6, 7]").GetProgramOrThrow().Plan;

        CelValue result = plan.Run(plan.CreateFrame(Bind(collections), null, CancellationToken.None));

        Assert.Equal(CelValue.FromBoolean(true), result);
    }

    // The units that the evaluation of the text charged beyond those of the same text on the list member c.mutable.
    private static ulong Extra(Collections collections, string text)
    {
        string baselineText = text.Replace("c.hash", "c.mutable", StringComparison.Ordinal)
            .Replace("c.readOnlySet", "c.mutable", StringComparison.Ordinal)
            .Replace("c.sorted", "c.mutable", StringComparison.Ordinal)
            .Replace("c.queue", "c.mutable", StringComparison.Ordinal)
            .Replace("c.stack", "c.mutable", StringComparison.Ordinal)
            .Replace("c.linked", "c.mutable", StringComparison.Ordinal)
            .Replace("c.fixed", "c.mutable", StringComparison.Ordinal);
        return Charged(collections, text, ulong.MaxValue) - Charged(new Collections(), baselineText, ulong.MaxValue);
    }

    private static ulong Charged(Collections collections, string text, ulong limit)
    {
        Plan plan = Environment.Compile<CelValue>(text).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(Bind(collections), limit, CancellationToken.None);
        _ = plan.Run(frame);
        return frame.ChargedUnits;
    }

    private static IReadOnlyDictionary<string, CelValue> Bind(Collections collections) =>
        new CelBindings().Add("c", collections).Values;
}
