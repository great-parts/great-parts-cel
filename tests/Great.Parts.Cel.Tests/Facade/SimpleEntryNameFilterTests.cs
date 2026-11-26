using System;
using System.Collections.Generic;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The simple entry binds an anonymous object with the names that the plan of the program can read (Plan.ReadNames): the value of every
// property is still converted and checked, but the name that no node reads is not inserted into the dictionary of the bindings. The
// filter lasts for the binding of one call. Each test has property names of its own, because two anonymous types of the same shape are
// one type, and a registration of a type is made once.
[Trait("Requirement", "U-7")]
[Trait("Requirement", "R-4")]
public sealed class SimpleEntryNameFilterTests
{
    private static long Run(SimpleEntryCache cache, string text, object data) =>
        SimpleEntryCall.Evaluate<long>(cache, text, data, CelExpression.DefaultCostLimit, TestContext.Current.CancellationToken);

    // The bindings that the thread keeps after a call hold no value and no filter: a name that no program read goes in as in any bindings.
    private static void AssertKeptBindingsAreClearOfValuesAndFilter()
    {
        CelBindings kept = Assert.IsType<CelBindings>(SimpleEntryCall.ThreadBindings);
        Assert.Empty(kept.Values);
        try
        {
            kept.Add("probeOfTheFilter", 1L);
            Assert.Single(kept.Values);
        }
        finally
        {
            kept.Clear();
        }
    }

    // The delegate of the registration adds all three properties; the bindings hold the one that the program reads, or the two.
    [Fact]
    public void ProgramThatReadsOneOfSeveralProperties_FindsOnlyThatOneBound()
    {
        var seen = new List<string>();
        SimpleEntryFixtures.Register(
            new { sieveOne = default(long), sieveTwo = default(long), sieveThree = default(long) },
            static builder => builder.AddVariable<long>("sieveOne").AddVariable<long>("sieveTwo").AddVariable<long>("sieveThree"),
            (data, bindings) =>
            {
                bindings.Add("sieveOne", data.sieveOne).Add("sieveTwo", data.sieveTwo).Add("sieveThree", data.sieveThree);
                seen.Clear();
                seen.AddRange(bindings.Values.Keys);
            });
        var cache = new SimpleEntryCache();

        Assert.Equal(21L, Run(cache, "sieveTwo + 1", new { sieveOne = 1L, sieveTwo = 20L, sieveThree = 3L }));
        Assert.Equal(["sieveTwo"], seen);

        seen.Clear();
        Assert.Equal(4L, Run(cache, "sieveOne + sieveThree", new { sieveOne = 1L, sieveTwo = 20L, sieveThree = 3L }));
        seen.Sort(StringComparer.Ordinal);
        Assert.Equal(["sieveOne", "sieveThree"], seen);

        seen.Clear();
        Assert.Equal(1L, Run(cache, "1", new { sieveOne = 1L, sieveTwo = 20L, sieveThree = 3L }));
        Assert.Empty(seen);

        AssertKeptBindingsAreClearOfValuesAndFilter();
    }

    // The filter is the set of the program, not of the type: two programs over one anonymous type, one after the other on the same
    // thread, each read the property that they name, and a program that reads both reads both.
    [Fact]
    public void TwoProgramsOverOneType_InTurn_EachReadTheirOwnProperty()
    {
        for (long round = 0; round < 3; round++)
        {
            var data = new { twinA = round, twinB = round * 10 };
            Assert.Equal(round + 1, CelExpression.Evaluate<long>("twinA + 1", data, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal((round * 10) + 5, CelExpression.Evaluate<long>("twinB + 5", data, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(round * 11, CelExpression.Evaluate<long>("twinA + twinB", data, cancellationToken: TestContext.Current.CancellationToken));
        }

        AssertKeptBindingsAreClearOfValuesAndFilter();
    }

    // The value of a property that the program does not read is converted and checked as before: a DateTime that is not UTC is an
    // ArgumentException, and the program that does not read it gets no result.
    [Fact]
    public void UnreadPropertyWithARefusedValue_IsStillAnArgumentException()
    {
        var local = new { refusedRead = 1L, refusedStamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) };
        var utc = new { refusedRead = 1L, refusedStamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("refusedRead + 1", local, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2L, CelExpression.Evaluate<long>("refusedRead + 1", utc, cancellationToken: TestContext.Current.CancellationToken));
    }

    // The exception of the binding leaves the bindings of the thread without values and without the filter: the next call, of another
    // program over the same type, finds the property that it reads.
    [Fact]
    public void AfterTheExceptionOfTheBinding_TheNextCallOfAnotherProgramFindsItsProperty()
    {
        var refused = new { failFirst = 1L, failSecond = 2L, failStamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Local) };
        var accepted = new { failFirst = 1L, failSecond = 2L, failStamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };

        Assert.Throws<ArgumentException>(() => CelExpression.Evaluate<long>("failFirst + 1", refused, cancellationToken: TestContext.Current.CancellationToken));
        AssertKeptBindingsAreClearOfValuesAndFilter();

        Assert.Equal(3L, CelExpression.Evaluate<long>("failSecond + 1", accepted, cancellationToken: TestContext.Current.CancellationToken));
        AssertKeptBindingsAreClearOfValuesAndFilter();
    }

    // A variable that a macro reads in its body, and a presence test over a property that is a map, are reads of the plan like any other.
    [Fact]
    public void VariableInAMacro_AndPresenceTestOverAProperty_AreRead()
    {
        var data = new
        {
            macroItems = new List<long> { 1, 2, 3 },
            macroMin = 0L,
            macroSkipped = 99L,
            macroPrices = new Dictionary<string, double> { ["a"] = 1.5 },
        };

        Assert.True(CelExpression.Evaluate<bool>("macroItems.all(i, i > macroMin)", data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(CelExpression.Evaluate<bool>("has(macroPrices.a) && !has(macroPrices.b)", data, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(3L, CelExpression.Evaluate<long>("macroItems.filter(i, i > macroMin).size()", data, cancellationToken: TestContext.Current.CancellationToken));
    }

    // A property with the name of the variable of a macro is not read where the macro variable hides it: the program gives what it gave
    // before, and the property is not bound for it.
    [Fact]
    public void PropertyHiddenByAMacroVariable_IsNotReadByTheProgram()
    {
        var seen = new List<string>();
        IReadOnlyList<long> none = new List<long>();
        IReadOnlyList<long> items = new List<long> { 1, 2, -3 };
        SimpleEntryFixtures.Register(
            new { shadowItems = none, shadowX = default(long) },
            static builder => builder.AddVariable<IReadOnlyList<long>>("shadowItems").AddVariable<long>("shadowX"),
            (data, bindings) =>
            {
                bindings.Add("shadowItems", data.shadowItems).Add("shadowX", data.shadowX);
                seen.Clear();
                seen.AddRange(bindings.Values.Keys);
            });
        var cache = new SimpleEntryCache();

        Assert.Equal(2L, Run(cache, "shadowItems.filter(shadowX, shadowX > 0).size()", new { shadowItems = items, shadowX = -5L }));

        Assert.Equal(["shadowItems"], seen);
    }
}
