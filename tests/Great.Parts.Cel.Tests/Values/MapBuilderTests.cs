using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// MapBuilder, the accumulator of transformMap and transformMapEntry, on both sides of the sizes at which its index starts (the ninth
// entry) and at which its array and index grow. It refuses a repeated key by CEL key equality, finds a key at any size while it grows,
// keeps the order of insertion, and its map has the same entries and the same lookups as a map made at once.
public sealed class MapBuilderTests
{
    public static TheoryData<int> Sizes => [0, 1, 4, 5, 8, 9, 10, 16, 17, 100, 1000];

    public static TheoryData<int> NonEmptySizes => [1, 4, 5, 8, 9, 10, 16, 17, 100, 1000];

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void TryAdd_OfDistinctKeys_AddsThemAndFindsEachOneWhileTheBuilderGrows(int count)
    {
        var builder = new MapBuilder();

        for (int position = 0; position < count; position++)
        {
            Assert.True(builder.TryAdd(Key(position), CelValue.FromInt64(position)), position.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(position + 1, builder.Count);
            Assert.True(builder.TryGetValue(Key(position), out CelValue value));
            Assert.Equal(CelValue.FromInt64(position), value);
        }

        for (int position = 0; position < count; position++)
        {
            Assert.True(builder.TryGetValue(Key(position), out CelValue value));
            Assert.Equal(CelValue.FromInt64(position), value);
        }
    }

    // A key that equals one in the builder by CEL equality — the same value, or an int and a uint of one number — is refused, the count
    // stays, and the builder takes the next distinct key as before.
    [Theory]
    [MemberData(nameof(NonEmptySizes))]
    public static void TryAdd_OfARepeatedKey_IsRefusedAndChangesNothing(int count)
    {
        MapBuilder builder = Filled(count);
        CelValue[] repeats = [Key(0), Key(count - 1), Key(count / 2), CelValue.FromUInt64(0)];

        foreach (CelValue repeat in repeats)
        {
            Assert.False(builder.TryAdd(repeat, CelValue.FromString("again")));
            Assert.Equal(count, builder.Count);
        }

        Assert.True(builder.TryGetValue(Key(0), out CelValue first));
        Assert.Equal(CelValue.FromInt64(0), first);
        Assert.True(builder.TryAdd(CelValue.FromString("next"), CelValue.FromInt64(count)));
        Assert.Equal(count + 1, builder.Count);
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public static void Enumeration_FollowsTheOrderOfInsertion(int count)
    {
        MapBuilder builder = Filled(count);

        Assert.Equal(count, builder.Count);
        Assert.Equal(Enumerable.Range(0, count).Select(Key), builder.Select(entry => entry.Key));
        Assert.Equal(Enumerable.Range(0, count).Select(position => CelValue.FromInt64(position)), builder.Select(entry => entry.Value));
    }

    // The map of the builder has its entries in the order they came, and finds a key by each kind of its value; a key that was
    // never added is not found.
    [Theory]
    [MemberData(nameof(Sizes))]
    public static void ToMap_HasTheEntriesAndTheLookupsOfAMapMadeAtOnce(int count)
    {
        CelMap map = Filled(count).ToMap();

        Assert.Equal(count, map.Count);
        Assert.Equal(Enumerable.Range(0, count).Select(Key), map.Select(entry => entry.Key));
        for (int position = 0; position < count; position++)
        {
            Assert.True(map.TryGetValue(Key(position), out CelValue value));
            Assert.Equal(CelValue.FromInt64(position), value);
        }

        Assert.False(map.TryGetValue(CelValue.FromString("absent"), out _));
        Assert.False(map.TryGetValue(CelValue.FromDouble(0.5), out _));
        Assert.False(map.TryGetValue(CelValue.FromBoolean(false), out _));
    }

    // A map that was made once more by CelMap.Create from the same entries refuses nothing, so the two agree on every key.
    [Theory]
    [MemberData(nameof(Sizes))]
    public static void ToMap_AgreesWithCreateOnEveryKey(int count)
    {
        MapBuilder builder = Filled(count);
        CelMap built = builder.ToMap();
        CelMap created = CelMap.Create(built.ToArray());

        Assert.Equal(created.Count, built.Count);
        foreach (CelValue probe in Enumerable.Range(0, count + 5).SelectMany(position => new[] { Key(position), CelValue.FromInt64(-position - 1) }))
        {
            Assert.Equal(created.TryGetValue(probe, out CelValue expected), built.TryGetValue(probe, out CelValue actual));
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public static void ToMap_OfAnEmptyBuilder_IsTheEmptyMapOfTheProcess()
    {
        Assert.Same(EntryTableMap.Empty, new MapBuilder().ToMap());
    }

    // The builder with the keys of Key(0) to Key(count - 1) and the position of each key as its value.
    private static MapBuilder Filled(int count) => Fill([.. Enumerable.Range(0, count).Select(Key)]);

    private static MapBuilder Fill(CelValue[] keys)
    {
        var builder = new MapBuilder();
        for (int position = 0; position < keys.Length; position++)
        {
            Assert.True(builder.TryAdd(keys[position], CelValue.FromInt64(position)));
        }

        return builder;
    }

    // Keys of the kinds int, uint, string and bool by turns, none equal to another.
    private static CelValue Key(int position) => (position % 4) switch
    {
        0 => CelValue.FromInt64(position),
        1 => CelValue.FromUInt64((ulong)position),
        2 => CelValue.FromString("k" + position.ToString(CultureInfo.InvariantCulture)),
        _ => position == 3 ? CelValue.FromBoolean(true) : CelValue.FromInt64(-position),
    };
}
