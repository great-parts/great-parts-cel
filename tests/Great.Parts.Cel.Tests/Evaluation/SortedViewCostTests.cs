using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The enumeration of a map view that is ordered by ascending key sorts the keys at each enumeration, and the operation that
// enumerates charges 1 + c · n · ⌈log₂(n + 1)⌉ before the first entry: c is 1 for integer keys and 1 + ⌈2 · Lmax/10⌉ for string
// keys, Lmax the longest. A view in the order of its source charges nothing. The operations are a comprehension over the map,
// the equality of two maps (the left one is enumerated) and the check of a value against a declared map type.
public sealed class SortedViewCostTests
{
    private static readonly string[] Keys = ["a", "0123456789", "0123456789A"];

    // n = 3 string keys of 1, 10 and 11 code units: c = 1 + ⌈22/10⌉ = 4, ⌈log₂ 4⌉ = 2.
    [Fact]
    public static void ComprehensionOverAViewOfStringKeys_ChargesTheSortOfTheKeys()
    {
        Assert.Equal(1 + (4UL * 3 * 2), Extra("m.all(k, true)", inOrder => Strings(Keys, inOrder), CelType.Dyn));
    }

    // n = 5 integer keys: c = 1, ⌈log₂ 6⌉ = 3.
    [Fact]
    public static void ComprehensionOverAViewOfIntegerKeys_ChargesTheSortOfTheKeys()
    {
        Assert.Equal(1 + (1UL * 5 * 3), Extra("m.all(k, true)", inOrder => Integers(5, inOrder), CelType.Dyn));
    }

    // An empty view still pays the unit before its first entry.
    [Fact]
    public static void ComprehensionOverAnEmptyView_ChargesOneUnit()
    {
        Assert.Equal(1UL, Extra("m.all(k, true)", inOrder => Strings([], inOrder), CelType.Dyn));
    }

    // Guid keys are 36 long: c = 1 + ⌈72/10⌉ = 9, n = 2, ⌈log₂ 3⌉ = 2.
    [Fact]
    public static void ComprehensionOverAViewOfGuidKeys_UsesTheLengthOfAGuid()
    {
        var source = new Wrapped<Guid, long>(new Dictionary<Guid, long> { [Guid.NewGuid()] = 1, [Guid.NewGuid()] = 2 });

        Assert.Equal(
            1 + (9UL * 2 * 2),
            Extra("m.all(k, true)", inOrder => CelMap.CreateView(source, CelValue.FromInt64, inOrder), CelType.Dyn));
    }

    // The left map of an equality is enumerated; the right one is only searched.
    [Fact]
    public static void EqualityOfMaps_ChargesTheSortOfTheLeftViewOnly()
    {
        ulong leftSorted = Units("a == b", ("a", Strings(Keys, false)), ("b", Strings(Keys, true)));
        ulong leftInOrder = Units("a == b", ("a", Strings(Keys, true)), ("b", Strings(Keys, true)));
        ulong rightSorted = Units("a == b", ("a", Strings(Keys, true)), ("b", Strings(Keys, false)));

        Assert.Equal((1 + (4UL * 3 * 2), 0UL), (leftSorted - leftInOrder, rightSorted - leftInOrder));
    }

    // Maps of different sizes are unequal before any entry is enumerated.
    [Fact]
    public static void EqualityOfMapsOfDifferentSizes_ChargesNoSort()
    {
        ulong sorted = Units("a == b", ("a", Strings(Keys, false)), ("b", Strings([], false)));
        ulong inOrder = Units("a == b", ("a", Strings(Keys, true)), ("b", Strings([], false)));

        Assert.Equal(inOrder, sorted);
    }

    // A variable of type map(string, int) checks the entries of the view: n units and the sort.
    [Fact]
    public static void CheckAgainstADeclaredMapType_ChargesTheSortOfTheView()
    {
        Assert.Equal(
            1 + (4UL * 3 * 2),
            Extra("m", inOrder => Strings(Keys, inOrder), CelType.MapOf(CelType.String, CelType.Int)));
    }

    // The units of the view that sorts beyond those of the same view in the order of its source.
    private static ulong Extra(string expression, Func<bool, CelMap> view, CelType declared) =>
        Units(expression, [("m", view(false))], declared) - Units(expression, [("m", view(true))], declared);

    private static ulong Units(string expression, params (string Name, CelMap Map)[] maps) => Units(expression, maps, CelType.Dyn);

    private static ulong Units(string expression, (string Name, CelMap Map)[] maps, CelType declared)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        var bindings = new Dictionary<string, CelValue>();
        foreach ((string name, CelMap map) in maps)
        {
            builder.AddVariable(name, declared);
            bindings[name] = CelValue.FromMap(map);
        }

        Plan plan = builder.Build().GetEnvironmentOrThrow().Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings, 1_000_000, CancellationToken.None);

        Assert.NotEqual(CelValueKind.Error, plan.Run(frame).Kind);
        return frame.ChargedUnits;
    }

    // A view over a dictionary of a type that the library does not know to enumerate in an order of its own, so that without
    // keepSourceOrder the view sorts.
    private static CelMap Strings(string[] keys, bool inOrder)
    {
        var source = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (string key in keys)
        {
            source[key] = 1;
        }

        return CelMap.CreateView(new Wrapped<string, long>(source), CelValue.FromInt64, inOrder);
    }

    private static CelMap Integers(int count, bool inOrder)
    {
        var source = new Dictionary<long, long>();
        for (int i = 0; i < count; i++)
        {
            source[i] = 1;
        }

        return CelMap.CreateView(new Wrapped<long, long>(source), CelValue.FromInt64, inOrder);
    }

    private sealed class Wrapped<TKey, TValue>(Dictionary<TKey, TValue> inner) : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        public int Count => inner.Count;

        public IEnumerable<TKey> Keys => inner.Keys;

        public IEnumerable<TValue> Values => inner.Values;

        public TValue this[TKey key] => inner[key];

        public bool ContainsKey(TKey key) => inner.ContainsKey(key);

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => inner.TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
