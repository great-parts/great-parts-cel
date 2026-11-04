using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The functions of strings on texts that an expression cannot write: lone surrogates, which bindings can hold.
public sealed class StringsTests
{
    private static readonly LineTable Lines = new("x");

    [Theory]
    [InlineData("", 0)]
    [InlineData("kiwi", 4)]
    [InlineData("😀", 1)]
    [InlineData("a😀b😁", 4)]
    [InlineData("\uD800a\uDC00", 3)]
    [InlineData("\uDC00\uD800", 2)]
    [InlineData("😀\uD83D", 2)]
    [InlineData("\uD800𐀀", 2)]
    public static void Size_CountsCodePoints(string text, long size)
    {
        CelValue result = Strings.StringSize([CelValue.FromString(text)], new CallSite("size", 0, Lines));

        Assert.True(result.TryGetInt64(out long count));
        Assert.Equal(size, count);
    }

    // Prefixes and suffixes compare code units: half of a surrogate pair is a prefix or a suffix of the pair.
    [Theory]
    [InlineData("😀x", "\uD83D", true, false)]
    [InlineData("x😀", "\uDE00", false, true)]
    [InlineData("😀", "😁", false, false)]
    public static void StartsAndEndsWith_CompareCodeUnits(string text, string part, bool starts, bool ends)
    {
        CelValue[] arguments = [CelValue.FromString(text), CelValue.FromString(part)];

        Assert.Equal(CelValue.FromBoolean(starts), Strings.StartsWith(arguments, new CallSite("startsWith", 0, Lines)));
        Assert.Equal(CelValue.FromBoolean(ends), Strings.EndsWith(arguments, new CallSite("endsWith", 0, Lines)));
    }
}
