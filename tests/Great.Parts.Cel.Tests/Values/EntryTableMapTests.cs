using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// EntryTableMap on both sides of the bound of its representations: the empty map, a map of up to 8 entries without an
// index and a larger map with an index. Each finds its keys by CEL key equality, refuses a key kind before a repeat,
// keeps the order of insertion and takes at most 64 bytes for each unit of the cost of its literal, 1 + 2n.
public sealed class EntryTableMapTests
{
    public static TheoryData<int> Sizes => [0, 1, 8, 9, 1000];

    public static TheoryData<int> NonEmptySizes => [1, 8, 9, 1000];

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void TryGetValue_FindsEveryKeyByEachKindOfItsValue(int count)
    {
        EntryTableMap map = Create(count);

        for (int position = 0; position < count; position++)
        {
            foreach (CelValue probe in Probes(position))
            {
                Assert.True(map.TryGetValue(probe, out CelValue value), $"{count}: {probe}");
                Assert.Equal(CelValue.FromInt64(position), value);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void TryGetValue_OfAMissingKeyOrAValueThatIsNoKey_FindsNothing(int count)
    {
        EntryTableMap map = Create(count);
        CelValue[] probes =
        [
            CelValue.FromInt64((count * 10) + 7),
            CelValue.FromUInt64((ulong)(count * 10) + 7),
            CelValue.FromDouble((count * 10) + 7),
            CelValue.FromDouble(0.5),
            CelValue.FromDouble(double.NaN),
            CelValue.FromString("absent"),
            CelValue.FromBoolean(true),
            CelValue.Null,
            CelValue.FromList(CelList.Create()),
        ];

        foreach (CelValue probe in probes)
        {
            Assert.False(map.TryGetValue(probe, out CelValue value), $"{count}: {probe}");
            Assert.Equal(CelValue.Null, value);
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void Enumeration_FollowsTheOrderOfInsertion(int count)
    {
        CelValue[] keys = Keys(count);

        EntryTableMap map = Create(count);

        Assert.Equal(count, map.Count);
        Assert.Equal(keys, map.Select(entry => entry.Key));
        Assert.Equal(Enumerable.Range(0, count).Select(position => CelValue.FromInt64(position)), map.Select(entry => entry.Value));
    }

    // A key equal to an earlier one by another kind, or by the same value, is refused at its own position, the later one.
    [Theory]
    [MemberData(nameof(NonEmptySizes))]
    public static void TryCreate_WithARepeatedKey_RefusesTheLaterKey(int count)
    {
        CelValue[] keys = Keys(count);
        CelValue[] repeats = [CelValue.FromUInt64(0), keys[^1], keys[count / 2]];

        foreach (CelValue repeat in repeats)
        {
            Assert.Null(EntryTableMap.TryCreate(Entries([.. keys, repeat]), out EntryTableMap.Refusal refusal, out int index));
            Assert.Equal((EntryTableMap.Refusal.DuplicateKey, count), (refusal, index));
        }

        Assert.Null(EntryTableMap.TryCreate(Entries([repeats[1], .. keys]), out EntryTableMap.Refusal first, out int firstIndex));
        Assert.Equal((EntryTableMap.Refusal.DuplicateKey, count), (first, firstIndex));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void TryCreate_WithAKeyOfAnotherKind_RefusesItBeforeARepeat(int count)
    {
        CelValue[] keys = Keys(count);

        Assert.Null(EntryTableMap.TryCreate(
            Entries([.. keys, CelValue.FromInt64(0), CelValue.FromDouble(1)]), out EntryTableMap.Refusal refusal, out int index));

        Assert.Equal((EntryTableMap.Refusal.KeyKind, count + 1), (refusal, index));
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void Create_FindsAndEnumeratesAsTheLiteralDoes(int count)
    {
        CelValue[] keys = Keys(count);

        CelMap map = CelMap.Create(Entries(keys));

        Assert.Equal(keys, map.Select(entry => entry.Key));
        for (int position = 0; position < count; position++)
        {
            Assert.True(map.TryGetValue(Probes(position)[^1], out CelValue value));
            Assert.Equal(CelValue.FromInt64(position), value);
        }

        if (count > 0)
        {
            Assert.Throws<ArgumentException>("entries", () => CelMap.Create(Entries([.. keys, CelValue.FromUInt64(0)])));
        }
    }

    [Fact]
    public static void EmptyMap_IsOneInstanceForTheLiteralAndCreate()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        Plan plan = environment.Compile<CelValue>("{}").GetProgramOrThrow().Plan;

        Assert.Same(EntryTableMap.Empty, EntryTableMap.TryCreate([], out EntryTableMap.Refusal refusal, out int index));
        Assert.Equal((EntryTableMap.Refusal.None, -1), (refusal, index));
        Assert.Same(EntryTableMap.Empty, CelMap.Create([]));
        for (int run = 0; run < 2; run++)
        {
            Assert.True(plan.Run(plan.CreateFrame(new Dictionary<string, CelValue>(), 1000, CancellationToken.None)).TryGetMap(out CelMap? map));
            Assert.Same(EntryTableMap.Empty, map);
        }
    }

    // The array of entries, which the literal fills, and the map it becomes take at most 64 bytes for each unit of 1 + 2n.
    [Theory]
    [MemberData(nameof(Sizes))]
    public static void EntriesAndMap_TakeAtMost64BytesForEachUnitOfTheirCost(int count)
    {
        CelValue[] keys = [.. Enumerable.Range(0, count).Select(position => CelValue.FromInt64(position))];
        _ = Fill(keys);

        long before = GC.GetAllocatedBytesForCurrentThread();
        EntryTableMap? map = Fill(keys);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotNull(map);
        Assert.True(bytes <= 64 * (1 + (2 * (long)count)), $"{count} entries: {bytes.ToString(CultureInfo.InvariantCulture)} bytes");
    }

    // The entries as a map literal creates them, then the map.
    private static EntryTableMap? Fill(CelValue[] keys)
    {
        KeyValuePair<CelValue, CelValue>[] entries = keys.Length == 0 ? [] : new KeyValuePair<CelValue, CelValue>[keys.Length];
        for (int position = 0; position < keys.Length; position++)
        {
            entries[position] = new(keys[position], keys[position]);
        }

        return EntryTableMap.TryCreate(entries, out _, out _);
    }

    // The map of Keys(count), each key with its position as the value.
    private static EntryTableMap Create(int count) =>
        EntryTableMap.TryCreate(Entries(Keys(count)), out _, out _) ?? throw new InvalidOperationException("The keys are refused.");

    // Keys of the kinds int, uint and string by turns, and negative ints, none equal to another.
    private static CelValue[] Keys(int count) => [.. Enumerable.Range(0, count).Select(Key)];

    private static CelValue Key(int position) => (position % 4) switch
    {
        0 => CelValue.FromInt64(position),
        1 => CelValue.FromUInt64((ulong)position),
        2 => CelValue.FromString("k" + position.ToString(CultureInfo.InvariantCulture)),
        _ => CelValue.FromInt64(-position),
    };

    // The values equal to the key at the position by CEL key equality, the key itself last.
    private static CelValue[] Probes(int position) => (position % 4) switch
    {
        0 or 1 => [CelValue.FromInt64(position), CelValue.FromUInt64((ulong)position), CelValue.FromDouble(position), Key(position)],
        2 => [CelValue.FromString("k" + position.ToString(CultureInfo.InvariantCulture)), Key(position)],
        _ => [CelValue.FromDouble(-position), Key(position)],
    };

    private static KeyValuePair<CelValue, CelValue>[] Entries(CelValue[] keys) =>
        [.. keys.Select((key, position) => new KeyValuePair<CelValue, CelValue>(key, CelValue.FromInt64(position)))];
}
