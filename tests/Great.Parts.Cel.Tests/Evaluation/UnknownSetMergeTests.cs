using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Tests.Examples.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The union of two sets of unknown values looks an entry up among those of its id by a hash of the trail when the groups of an id are
// long. The result, the order of the entries and the entries kept are those of comparing every entry with every one of its id, which
// the reference below does.
public sealed class UnknownSetMergeTests
{
    private const long TwoToThe53 = 1L << 53;

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void LongGroupsOfOneId_UniteAsComparingEveryEntryDoes()
    {
        var firstEntries = new List<UnknownEntry> { AttributeEntry(6, "a"), AttributeEntry(9, "z") };
        firstEntries.AddRange(Enumerable.Range(0, 200).Select(k => AttributeEntry(7, "d", CelValue.FromInt64(k))));
        var secondEntries = new List<UnknownEntry> { AttributeEntry(8, "b"), AttributeEntry(9, "z") };
        secondEntries.AddRange(Enumerable.Range(100, 200).Select(k => AttributeEntry(7, "d", Spelling(k))));
        UnknownSet first = SetOf(firstEntries);
        UnknownSet second = SetOf(secondEntries);

        UnknownSet merged = AssertUnion(first, second);
        AssertUnion(second, first);

        // An int, a uint and a double of one value are one trail: 200 of the first, 100 of the second that are not in it.
        Assert.Equal(300, merged.Entries.Count(entry => entry.Id == 7));
    }

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void LongGroupsWithSpecialTrails_UniteAsComparingEveryEntryDoes()
    {
        var firstEntries = new List<UnknownEntry>
        {
            new(3, "d", [null, CelValue.FromInt64(1)]),
            AttributeEntry(3, "d", CelValue.FromDouble(double.NaN)),
            AttributeEntry(3, "d", CelValue.FromInt64(TwoToThe53)),
            AttributeEntry(3, "d", CelValue.FromInt64(TwoToThe53 + 1)),
            AttributeEntry(3, "d", CelValue.FromDouble(double.PositiveInfinity)),
            AttributeEntry(3, "d", Unhashed(1)),
            new(3, "f", "f_int", []),
        };
        firstEntries.AddRange(Enumerable.Range(0, 40).Select(k => AttributeEntry(3, "d", CelValue.FromInt64(k))));
        firstEntries.AddRange(Enumerable.Range(0, 10).Select(k => AttributeEntry(3, "d", CelValue.FromString("a"), CelValue.FromInt64(k))));
        var secondEntries = new List<UnknownEntry>
        {
            new(3, "d", [null, CelValue.FromUInt64(1)]),
            AttributeEntry(3, "d", CelValue.FromDouble(double.NaN)),
            AttributeEntry(3, "d", CelValue.FromDouble(TwoToThe53)),
            AttributeEntry(3, "d", CelValue.FromUInt64((ulong)TwoToThe53 + 1)),
            AttributeEntry(3, "d", CelValue.FromDouble(double.PositiveInfinity)),
            AttributeEntry(3, "d", Unhashed(2)),
            new(3, "f", "f_int", []),
        };
        secondEntries.AddRange(Enumerable.Range(20, 40).Select(k => AttributeEntry(3, "d", CelValue.FromUInt64((ulong)k))));
        secondEntries.AddRange(Enumerable.Range(5, 10).Select(k => AttributeEntry(3, "d", CelValue.FromString("a"), CelValue.FromUInt64((ulong)k))));
        UnknownSet first = SetOf(firstEntries);
        UnknownSet second = SetOf(secondEntries);

        AssertUnion(first, second);
        AssertUnion(second, first);
    }

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void LongGroupThatHoldsTheOther_IsTheResultItself()
    {
        UnknownSet large = SetOf(Enumerable.Range(0, 100).Select(k => AttributeEntry(5, "d", CelValue.FromInt64(k))));
        UnknownSet part = SetOf(Enumerable.Range(20, 60).Select(k => AttributeEntry(5, "d", CelValue.FromUInt64((ulong)k))));

        Assert.Same(large, UnknownSet.Merge(large, part));
        Assert.Same(large, UnknownSet.Merge(part, large));
    }

    // l.map(i, l.map(j, d[j])) unites, in each run of the outer loop, the set of every trail of d with the same set built again.
    [Fact]
    [Trait("Requirement", "R-3")]
    public static void NestedLoopThatIsUnknownInEveryRun_GivesTheTrailsOfAllItsRuns()
    {
        const int Count = 300;
        long[] items = [.. Enumerable.Range(0, Count).Select(index => (long)index)];
        CelBindings bindings = new CelBindings().Add("l", PartialTestSupport.IntList(items)).Add("d", PartialTestSupport.IntList(items));

        CelPartialEvalResult<CelValue> result = PartialTestSupport.Evaluate(
            "l.map(i, l.map(j, d[j]))",
            [PartialTestSupport.P("d", PartialTestSupport.Any)],
            bindings);

        Assert.True(result.IsUnknown);
        Assert.Equal(
            Enumerable.Range(0, Count).Select(k => "d[" + k.ToString(CultureInfo.InvariantCulture) + "]").Order(StringComparer.Ordinal),
            result.Unknowns.Select(unknown => unknown.Attribute!.ToString()).Order(StringComparer.Ordinal));
    }

    // The union of the sets checked against the reference, which is the union that compares every entry with every one of its id.
    private static UnknownSet AssertUnion(UnknownSet first, UnknownSet second)
    {
        UnknownSet merged = UnknownSet.Merge(first, second);
        Assert.Equal<UnknownEntry>(Reference(first, second), merged.Entries.ToArray());
        return merged;
    }

    private static UnknownEntry[] Reference(UnknownSet first, UnknownSet second)
    {
        if (Holds(first.Entries, second.Entries))
        {
            return [.. first.Entries];
        }

        if (Holds(second.Entries, first.Entries))
        {
            return [.. second.Entries];
        }

        var result = new List<UnknownEntry>();
        foreach (int id in first.Entries.Concat(second.Entries).Select(entry => entry.Id).Distinct().Order())
        {
            UnknownEntry[] own = [.. first.Entries.Where(entry => entry.Id == id)];
            result.AddRange(own);
            result.AddRange(second.Entries.Where(entry => entry.Id == id && !own.Any(held => Same(held, entry))));
        }

        return [.. result];
    }

    private static bool Holds(ImmutableArray<UnknownEntry> set, ImmutableArray<UnknownEntry> other) =>
        other.All(entry => set.Any(held => held.Id == entry.Id && Same(held, entry)));

    private static bool Same(UnknownEntry left, UnknownEntry right)
    {
        if (left.Variable is null || right.Variable is null)
        {
            return left.Variable is null && right.Variable is null;
        }

        return left.Variable == right.Variable
            && left.Qualifiers.Length == right.Qualifiers.Length
            && left.Qualifiers.Zip(right.Qualifiers).All(pair => QualifiersEqual(pair.First, pair.Second));
    }

    private static bool QualifiersEqual(CelValue? left, CelValue? right) =>
        left is { } a && right is { } b
            ? CelValue.CelEquals(a, b).TryGetBoolean(out bool equal) && equal
            : left is null && right is null;

    // A set of the entries, ordered by id as a union orders them.
    private static UnknownSet SetOf(IEnumerable<UnknownEntry> entries)
    {
        UnknownSet? set = null;
        foreach (UnknownEntry entry in entries)
        {
            set = set is null ? UnknownSet.Of(entry) : UnknownSet.Merge(set, UnknownSet.Of(entry));
        }

        return set!;
    }

    private static UnknownEntry AttributeEntry(int id, string variable, params CelValue[] qualifiers) =>
        new(id, variable, [.. qualifiers.Select(qualifier => (CelValue?)qualifier)]);

    // An int, a uint or a double of the value of k, by turns.
    private static CelValue Spelling(int k) => (k % 3) switch
    {
        0 => CelValue.FromInt64(k),
        1 => CelValue.FromUInt64((ulong)k),
        _ => CelValue.FromDouble(k),
    };

    // A qualifier that has no hash: a value that is an error is compared one by one.
    private static CelValue Unhashed(int id) => CelValue.FromUnknown(UnknownSet.Of(new UnknownEntry(id, "u", [])));
}
