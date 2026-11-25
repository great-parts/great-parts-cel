using System.Threading;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The search of a substring of contains and of the set strings. A literal pattern is searched through the SearchValues<string> that
// planning prepared, a pattern from the data by the ordinal search: the answers and the costs are the same both ways. The term
// ceil(La / 10) of the cost is multiplied by q = max(1, ceil(m / 64)), m the length of the pattern in UTF-16 units: a pattern of at
// most 64 units costs what it cost before, a longer one more. The cost of an expression adds 1 for each read of a variable; a
// literal costs 0. The text is 100 units of 'a' and the pattern m - 1 units of 'a' and a 'b', which the text does not hold.
[Trait("Requirement", "R-3")]
[Trait("Requirement", "F-1")]
public sealed class SubstringSearchTests
{
    private static readonly string Text = new('a', 100);

    private static readonly string Emoji = char.ConvertFromUtf32(0x1F600);

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddVariable<string>("s")
        .AddVariable<string>("t")
        .Build()
        .GetEnvironmentOrThrow();

    // contains, m = 64: 1 + ceil(164 / 10) = 18, and 2 reads. m = 65: 1 + 17 · 2 = 35. m = 128: 1 + 23 · 2 = 47. m = 129:
    // 1 + 23 · 3 = 70. replace: La = 100 + 65 + 1, 1 + 17 · 2, and the copy of the receiver 10. split: 1 + 17 · 2, one part of 100
    // (10) and 1 for the part.
    [Theory]
    [InlineData("s.contains(t)", 64, 20UL)]
    [InlineData("s.contains(t)", 65, 37UL)]
    [InlineData("s.contains(t)", 128, 49UL)]
    [InlineData("s.contains(t)", 129, 72UL)]
    [InlineData("s.indexOf(t)", 65, 37UL)]
    [InlineData("s.lastIndexOf(t)", 65, 37UL)]
    [InlineData("s.replace(t, 'x')", 65, 47UL)]
    [InlineData("s.split(t)", 65, 48UL)]
    public void CostOfASearch_IsMultipliedAbove64UnitsOfPattern(string expression, int length, ulong cost)
    {
        Assert.Equal(cost, Cost(expression, Pattern(length)));
    }

    // The literal path costs what the variable path costs, less the read of t.
    [Theory]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(200)]
    public void LiteralPattern_CostsAsAVariablePattern(int length)
    {
        string pattern = Pattern(length);

        Assert.Equal(Cost("s.contains(t)", pattern) - 1, Cost("s.contains('" + pattern + "')", pattern));
        Assert.Equal(Cost("s.indexOf(t)", pattern) - 1, Cost("s.indexOf('" + pattern + "')", pattern));
    }

    [Theory]
    [InlineData("abcabcabd", "abd", 6L)]
    [InlineData("aaaa", "aa", 0L)]
    [InlineData("xyz", "q", -1L)]
    [InlineData("", "", 0L)]
    public void IndexOf_IsTheSameForALiteralAndAVariable(string text, string pattern, long index)
    {
        Assert.Equal(index, Evaluate<long>("s.indexOf(t)", text, pattern));
        Assert.Equal(index, Evaluate<long>("s.indexOf('" + pattern + "')", text, pattern));
    }

    [Fact]
    public void LastIndexOfReplaceAndSplit_AreTheSameForALiteralAndAVariable()
    {
        Assert.Equal(6L, Evaluate<long>("s.lastIndexOf(t)", "abcabcabc", "abc"));
        Assert.Equal(6L, Evaluate<long>("s.lastIndexOf('abc')", "abcabcabc", "abc"));
        Assert.Equal("a-b-c", Evaluate<string>("s.replace(t, '-')", "aXbXc", "X"));
        Assert.Equal("a-b-c", Evaluate<string>("s.replace('X', '-')", "aXbXc", "X"));
        Assert.True(Evaluate<bool>("s.split(t) == ['a', 'b', '', 'c']", "a,b,,c", ","));
        Assert.True(Evaluate<bool>("s.split(',') == ['a', 'b', '', 'c']", "a,b,,c", ","));
    }

    // Indexes count code points: a surrogate pair is one.
    [Fact]
    public void IndexOf_CountsCodePoints_OnBothPaths()
    {
        string text = "x" + Emoji + Emoji + "y";
        string pattern = Emoji + "y";

        Assert.Equal(2L, Evaluate<long>("s.indexOf(t)", text, pattern));
        Assert.Equal(2L, Evaluate<long>("s.indexOf('" + pattern + "')", text, pattern));
    }

    // A pattern from the data that is a lone low surrogate would cut the pair of the text: it is not a match.
    [Fact]
    public void LoneSurrogateFromTheData_DoesNotCutAPair()
    {
        string lowSurrogate = ((char)0xDE00).ToString();

        Assert.Equal(-1L, Evaluate<long>("s.indexOf(t)", Emoji, lowSurrogate));
    }

    private static string Pattern(int length) => new string('a', length - 1) + "b";

    private T Evaluate<T>(string expression, string text, string pattern) =>
        environment.Compile<T>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("s", text).Add("t", pattern), cancellationToken: CancellationToken.None)
            .GetValueOrThrow();

    private ulong? Cost(string expression, string pattern) =>
        environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("s", Text).Add("t", pattern), new CelEvalOptions { CostLimit = ulong.MaxValue }, CancellationToken.None)
            .Cost;
}
