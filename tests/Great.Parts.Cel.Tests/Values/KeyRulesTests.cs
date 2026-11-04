using System;
using System.Globalization;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// EqualityRules and KeyHashRules: map keys meet on one number line, and the hash agrees with that equality.
public sealed class KeyRulesTests
{
    // "<left>; <right> → equal | different".
    private const string Pairs = """
        int 1; uint 1 → equal
        int 1; double 1 → equal
        uint 1; double 1 → equal
        int 0; double -0 → equal
        int -1; double -1 → equal
        int -1; uint 18446744073709551615 → different
        int -9223372036854775808; double -9223372036854775808 → different
        int -9223372036854775807; double -9223372036854774784 → different
        int -9223372036854774784; double -9223372036854774784 → equal
        int 9223372036854775807; double 9223372036854775808 → different
        uint 9223372036854775808; double 9223372036854775808 → equal
        uint 18446744073709551615; double 18446744073709551616 → different
        int 9007199254740993; double 9007199254740992 → different
        double 1.5; double 1.5 → equal
        double NaN; double NaN → different
        int 1; double 1.5 → different
        bool true; bool true → equal
        bool true; bool false → different
        bool true; int 1 → different
        string a; string a → equal
        string a; string A → different
        string 1; int 1 → different
        null; null → different
        """;

    // Values with one integral value of several kinds: "<value>; <value>…".
    private const string SameIntegralValue = """
        int 0; uint 0; double 0; double -0
        int 1; uint 1; double 1
        int -1; double -1
        int 9007199254740992; uint 9007199254740992; double 9007199254740992
        int -9223372036854774784; double -9223372036854774784
        uint 9223372036854775808; double 9223372036854775808
        uint 18446744073709549568; double 18446744073709549568
        """;

    public static TheoryData<string, string> PairCases => CaseTable.Parse(Pairs);

    public static TheoryData<string> SameIntegralValueCases => [.. SameIntegralValue.ReplaceLineEndings("\n").Split('\n')];

    [Theory]
    [MemberData(nameof(PairCases))]
    public static void KeysEqual_ComparesOnOneNumberLine(string pair, string expected)
    {
        string[] sides = Split(pair, "; ");
        CelValue left = Value(sides[0]);
        CelValue right = Value(sides[1]);

        Assert.Equal(expected == "equal", EqualityRules.KeysEqual(left, right));
        Assert.Equal(expected == "equal", EqualityRules.KeysEqual(right, left));
    }

    [Theory]
    [MemberData(nameof(SameIntegralValueCases))]
    public static void Hash_OfOneIntegralValue_IsTheSameForEveryKind(string values)
    {
        string[] samples = Split(values, "; ");
        Assert.True(KeyHashRules.TryGetHash(Value(samples[0]), out int expected));

        foreach (string sample in samples)
        {
            Assert.True(KeyHashRules.TryGetHash(Value(sample), out int hash), sample);
            Assert.Equal(expected, hash);
            Assert.True(EqualityRules.KeysEqual(Value(samples[0]), Value(sample)), sample);
        }
    }

    [Theory]
    [InlineData("double 1.5")]
    [InlineData("double NaN")]
    [InlineData("double Infinity")]
    [InlineData("double -Infinity")]
    [InlineData("double 18446744073709551616")]
    [InlineData("double -9223372036854777856")]
    [InlineData("double -9223372036854775808")]
    [InlineData("null")]
    [InlineData("list")]
    public static void TryGetHash_OfAValueThatEqualsNoKey_IsFalse(string sample)
    {
        Assert.False(KeyHashRules.TryGetHash(Value(sample), out int hash));
        Assert.Equal(0, hash);
    }

    // The hash of an integral value mixes in the random seed of the process: it is not the hash of its bits, which keys
    // chosen to collide in an index would know.
    [Fact]
    public void Hash_OfAnIntegralValue_IsNotTheHashOfItsBits()
    {
        var hashes = new int[16];
        var bitHashes = new int[16];
        for (int value = 0; value < hashes.Length; value++)
        {
            Assert.True(KeyHashRules.TryGetHash(CelValue.FromInt64(value), out hashes[value]));
            bitHashes[value] = ((ulong)value).GetHashCode();
        }

        Assert.NotEqual(bitHashes, hashes);
    }

    [Fact]
    public void Hash_OfEqualStringsAndBooleans_IsTheSame()
    {
        Assert.True(KeyHashRules.TryGetHash(CelValue.FromString(new string('k', 2)), out int first));
        Assert.True(KeyHashRules.TryGetHash(CelValue.FromString("kk"), out int second));
        Assert.Equal(first, second);
        Assert.True(KeyHashRules.TryGetHash(CelValue.FromBoolean(true), out int trueHash));
        Assert.True(KeyHashRules.TryGetHash(CelValue.FromBoolean(false), out int falseHash));
        Assert.NotEqual(trueHash, falseHash);
    }

    [Fact]
    public void IsKeyKind_IsTrueOnlyForIntUIntBoolAndString()
    {
        foreach (CelValueKind kind in Enum.GetValues<CelValueKind>())
        {
            bool expected = kind is CelValueKind.Int or CelValueKind.UInt or CelValueKind.Bool or CelValueKind.String;
            Assert.True(expected == KeyHashRules.IsKeyKind(kind), kind.ToString());
        }
    }

    // A value from "<kind> <text>": int, uint, double (invariant culture, NaN and Infinity included), bool, string; or "null",
    // "list" (empty), "bytes" (01 02).
    internal static CelValue Value(string sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        int space = sample.IndexOf(' ', StringComparison.Ordinal);
        string text = space < 0 ? "" : sample[(space + 1)..];
        return (space < 0 ? sample : sample[..space]) switch
        {
            "int" => CelValue.FromInt64(long.Parse(text, CultureInfo.InvariantCulture)),
            "uint" => CelValue.FromUInt64(ulong.Parse(text, CultureInfo.InvariantCulture)),
            "double" => CelValue.FromDouble(double.Parse(text, CultureInfo.InvariantCulture)),
            "bool" => CelValue.FromBoolean(bool.Parse(text)),
            "string" => CelValue.FromString(text),
            "null" => CelValue.Null,
            "list" => CelValue.FromList(CelList.Create()),
            "bytes" => CelValue.FromBytes(new byte[] { 1, 2 }),
            _ => throw new ArgumentOutOfRangeException(nameof(sample), sample, null),
        };
    }

    // The parts of a case of a table: "<a>; <b>" or another separator.
    internal static string[] Split(string text, string separator)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split(separator);
    }
}
