using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

public sealed class CelMapTests
{
    // Keys of every key kind, mixed, in an order that is neither sorted nor the order of their hash codes.
    private static readonly CelValue[] MixedKeys =
    [
        CelValue.FromString("z"),
        CelValue.FromInt64(7),
        CelValue.FromBoolean(true),
        CelValue.FromUInt64(0),
        CelValue.FromString("a"),
        CelValue.FromInt64(-3),
        CelValue.FromBoolean(false),
        CelValue.FromUInt64(ulong.MaxValue),
        CelValue.FromString(""),
    ];

    [Fact]
    public void Enumeration_WithKeysOfEveryKind_FollowsTheOrderOfInsertion()
    {
        CelMap map = CelMap.Create(Entries(MixedKeys));

        Assert.Equal(MixedKeys, map.Select(entry => entry.Key));
        Assert.Equal(Values(MixedKeys.Length), map.Select(entry => entry.Value));
        Assert.Equal(MixedKeys, ((IEnumerable)map).Cast<KeyValuePair<CelValue, CelValue>>().Select(entry => entry.Key));
        Assert.Equal(MixedKeys.Length, map.Count);
    }

    // Each set of keys in ascending, descending and an interleaved order: enumeration gives the keys as they were inserted.
    [Theory]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("string")]
    public static void Enumeration_OfOneSetOfKeysInDifferentOrders_FollowsEachOrder(string kind)
    {
        CelValue[] ascending = [.. Enumerable.Range(0, 64).Select(number => Key(kind, number))];
        CelValue[][] orders = [ascending, [.. ascending.Reverse()], [.. ascending.Where((_, i) => i % 2 == 1), .. ascending.Where((_, i) => i % 2 == 0)]];

        foreach (CelValue[] keys in orders)
        {
            Assert.Equal(keys, CelMap.Create(Entries(keys)).Select(entry => entry.Key));
        }
    }

    [Fact]
    public void Create_WithoutEntries_IsEmpty()
    {
        CelMap map = CelMap.Create([]);

        Assert.Empty(map);
        Assert.False(map.TryGetValue(CelValue.FromInt64(0), out _));
    }

    [Fact]
    public void Create_CopiesTheEntries()
    {
        var source = new List<KeyValuePair<CelValue, CelValue>> { new(CelValue.FromInt64(1), CelValue.FromString("one")) };

        CelMap map = CelMap.Create(source);
        source.Add(new(CelValue.FromInt64(2), CelValue.FromString("two")));

        Assert.Single(map);
        Assert.False(map.TryGetValue(CelValue.FromInt64(2), out _));
    }

    // "<probe> → <value found> | none" in the map {1: 'int', 2u: 'uint', true: 'bool', 'k': 'string'}.
    private const string Lookups = """
        int 1 → int
        uint 1 → int
        double 1 → int
        double -0 → none
        uint 2 → uint
        int 2 → uint
        double 2 → uint
        double 1.5 → none
        double NaN → none
        double Infinity → none
        bool true → bool
        int 3 → none
        string k → string
        string K → none
        null → none
        list → none
        """;

    public static TheoryData<string, string> LookupCases => Syntax.CaseTable.Parse(Lookups);

    [Theory]
    [MemberData(nameof(LookupCases))]
    public static void TryGetValue_FindsTheKeyByCelEquality(string probe, string expected)
    {
        CelMap map = CelMap.Create(
        [
            new(CelValue.FromInt64(1), CelValue.FromString("int")),
            new(CelValue.FromUInt64(2), CelValue.FromString("uint")),
            new(CelValue.FromBoolean(true), CelValue.FromString("bool")),
            new(CelValue.FromString("k"), CelValue.FromString("string")),
        ]);

        bool found = map.TryGetValue(KeyRulesTests.Value(probe), out CelValue value);

        if (expected == "none")
        {
            Assert.False(found);
            Assert.Equal(CelValue.Null, value);
        }
        else
        {
            Assert.True(found);
            Assert.Equal(CelValue.FromString(expected), value);
        }
    }

    [Fact]
    public void TryGetValue_AfterANegativeZeroKey_FindsZero()
    {
        CelMap map = CelMap.Create([new(CelValue.FromInt64(0), CelValue.FromBoolean(true))]);

        Assert.True(map.TryGetValue(CelValue.FromDouble(-0.0), out CelValue value));
        Assert.Equal(CelValue.FromBoolean(true), value);
    }

    // "<probe> → <value found> | none" in the map {0: 'zero', false: 'false'}: both keys have the hash 0, the
    // hash of a probe that the key rules give no hash.
    private const string ZeroHashLookups = """
        int 0 → zero
        uint 0 → zero
        double -0 → zero
        bool false → false
        double 0.5 → none
        double NaN → none
        double Infinity → none
        null → none
        list → none
        bytes → none
        """;

    public static TheoryData<string, string> ZeroHashLookupCases => Syntax.CaseTable.Parse(ZeroHashLookups);

    [Theory]
    [MemberData(nameof(ZeroHashLookupCases))]
    public static void TryGetValue_AmongKeysOfHashZero_FindsOnlyAnEqualKey(string probe, string expected)
    {
        CelMap map = CelMap.Create(
        [
            new(CelValue.FromInt64(0), CelValue.FromString("zero")),
            new(CelValue.FromBoolean(false), CelValue.FromString("false")),
        ]);

        bool found = map.TryGetValue(KeyRulesTests.Value(probe), out CelValue value);

        Assert.Equal(expected != "none", found);
        Assert.Equal(expected == "none" ? CelValue.Null : CelValue.FromString(expected), value);
    }

    // "<keys> → <refusal> <position>": a key of a kind that cannot be a key is refused before any repeated key.
    private const string Refusals = """
        int 1, uint 1 → DuplicateKey 1
        string k, string k → DuplicateKey 1
        bool true, int 1, bool true → DuplicateKey 2
        int -1, uint 18446744073709551615 → None -1
        double 1 → KeyKind 0
        int 1, double 1 → KeyKind 1
        int 1, uint 1, double 1 → KeyKind 2
        null → KeyKind 0
        int 1, list → KeyKind 1
        """;

    public static TheoryData<string, string> RefusalCases => Syntax.CaseTable.Parse(Refusals);

    [Theory]
    [MemberData(nameof(RefusalCases))]
    public static void TryCreate_RefusesAKindBeforeARepeat(string keys, string expected)
    {
        CelValue[] values = [.. KeyRulesTests.Split(keys, ", ").Select(KeyRulesTests.Value)];

        EntryTableMap? map = EntryTableMap.TryCreate(Entries(values), out EntryTableMap.Refusal refusal, out int index);

        Assert.Equal(expected, $"{refusal} {index.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        Assert.Equal(refusal == EntryTableMap.Refusal.None, map is not null);
    }

    [Theory]
    [MemberData(nameof(RefusalCases))]
    public static void Create_WithARefusedKey_Throws(string keys, string expected)
    {
        CelValue[] values = [.. KeyRulesTests.Split(keys, ", ").Select(KeyRulesTests.Value)];

        if (expected == "None -1")
        {
            Assert.Equal(values, CelMap.Create(Entries(values)).Select(entry => entry.Key));
        }
        else
        {
            Assert.Equal("entries", Assert.Throws<ArgumentException>(() => CelMap.Create(Entries(values))).ParamName);
        }
    }

    [Fact]
    public void Create_WithAnErrorValue_Throws()
    {
        CelValue error = CelValue.FromError(new CelError(CelErrorCode.InternalError, CelErrorStage.Evaluate, "failed", default, null, null));

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CelMap.Create([new(CelValue.FromInt64(1), error)]));

        Assert.Equal("entries", exception.ParamName);
    }

    [Fact]
    public void Create_WithNull_Throws()
    {
        Assert.Equal("entries", Assert.Throws<ArgumentNullException>(() => CelMap.Create(null!)).ParamName);
    }

    private static CelValue Key(string kind, int number) => kind switch
    {
        "int" => CelValue.FromInt64(number * 7919L),
        "uint" => CelValue.FromUInt64((ulong)number * 104729UL),
        _ => CelValue.FromString("key" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)),
    };

    private static CelValue[] Values(int count) => [.. Enumerable.Range(0, count).Select(index => CelValue.FromInt64(index))];

    private static KeyValuePair<CelValue, CelValue>[] Entries(CelValue[] keys)
    {
        CelValue[] values = Values(keys.Length);
        return [.. keys.Select((key, index) => new KeyValuePair<CelValue, CelValue>(key, values[index]))];
    }
}
