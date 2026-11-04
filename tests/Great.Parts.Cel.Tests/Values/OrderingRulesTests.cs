using System;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// OrderingRules.Compare: numbers of the three kinds on one number line, strings by code points, bytes by unsigned
// values with a prefix first, false before true; a NaN is unordered, and other kinds have no ordering.
public sealed class OrderingRulesTests
{
    // "<left>; <right> → <ordering>", with values as KeyRulesTests.Value reads them.
    private const string Pairs = """
        int 3; int 4 → Less
        int -2; uint 0 → Less
        int 5; uint 5 → Equal
        uint 6; int 5 → Greater
        uint 18446744073709551615; int 9223372036854775807 → Greater
        int 2; double 2.5 → Less
        int 3; double 2.5 → Greater
        int 4; double 4 → Equal
        int 9223372036854775807; double 9223372036854775808 → Equal
        int -9223372036854775808; double -9223372036854775808 → Equal
        int -9223372036854775808; double -9223372036854777856 → Greater
        int 9223372036854775807; double 9223372036854777856 → Less
        uint 3; double 2.5 → Greater
        uint 0; double -0.5 → Greater
        uint 18446744073709551615; double 18446744073709551616 → Equal
        uint 18446744073709551615; double 18446744073709555712 → Less
        double -0; double 0 → Equal
        double 2.5; int 2 → Greater
        double 2.5; uint 3 → Less
        double NaN; int 1 → Unordered
        uint 1; double NaN → Unordered
        double NaN; double NaN → Unordered
        double Infinity; double 1 → Greater
        bool false; bool true → Less
        bool true; bool true → Equal
        string ab; string abc → Less
        string b; string abc → Greater
        string x; string x → Equal
        null; null → None
        list; list → None
        int 1; string 1 → None
        bool true; int 1 → None
        """;

    public static TheoryData<string, string> PairCases => CaseTable.Parse(Pairs);

    [Theory]
    [MemberData(nameof(PairCases))]
    public static void Compare_FollowsTheLanguageDefinition(string pair, string expected)
    {
        string[] sides = KeyRulesTests.Split(pair, "; ");

        Assert.Equal(expected, OrderingRules.Compare(KeyRulesTests.Value(sides[0]), KeyRulesTests.Value(sides[1])).ToString());
    }

    // U+E000–U+FFFF come before supplementary characters by code points, though their UTF-16 units are greater
    // than the high surrogate; below U+D800 the units compare as they are.
    [Theory]
    [InlineData("", "\U00010000", "Less")]
    [InlineData("�", "\U0010FFFF", "Less")]
    [InlineData("\U0001F642", "", "Greater")]
    [InlineData("a\U0001F642", "ab", "Greater")]
    [InlineData("퟿", "", "Less")]
    [InlineData("\U00010000", "\U00010001", "Less")]
    [InlineData("z", "", "Less")]
    public static void CompareStrings_IsByCodePoints(string left, string right, string expected)
    {
        Assert.Equal(expected, OrderingRules.CompareStrings(left, right).ToString());
        Assert.Equal(expected, OrderingRules.Compare(CelValue.FromString(left), CelValue.FromString(right)).ToString());
    }

    [Theory]
    [InlineData(new byte[] { 1, 2 }, new byte[] { 1, 2, 0 }, "Less")]
    [InlineData(new byte[] { 0x80 }, new byte[] { 0x7F, 0xFF }, "Greater")]
    [InlineData(new byte[] { }, new byte[] { }, "Equal")]
    public static void CompareBytes_IsByUnsignedValuesWithAPrefixFirst(byte[] left, byte[] right, string expected)
    {
        Assert.Equal(expected, OrderingRules.Compare(CelValue.FromBytes(left), CelValue.FromBytes(right)).ToString());
    }

    [Fact]
    public void CompareStrings_OfEqualTextInDifferentInstances_IsEqual()
    {
        Assert.Equal(ValueOrdering.Equal, OrderingRules.CompareStrings(new string('q', 3), "qqq".AsSpan()));
    }
}
