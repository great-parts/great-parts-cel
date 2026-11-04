using Great.Parts.Cel.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The search for a substring: the index of the first occurrence by UTF-16 code units, and the steps it adds.
public sealed class SubstringSearchTests
{
    [Theory]
    [InlineData("kiwi", "kiwi", 0)]
    [InlineData("kiwi", "wi", 2)]
    [InlineData("kiwi", "i", 1)]
    [InlineData("kiwi", "iwis", -1)]
    [InlineData("aaab", "aab", 1)]
    [InlineData("abababc", "ababc", 2)]
    [InlineData("abcabd", "abd", 3)]
    [InlineData("xyz", "q", -1)]
    [InlineData("😀😁", "\uDE01", 3)]
    [InlineData("a\uD83D", "😀", -1)]
    public static void IndexOf_IsTheFirstOccurrence(string text, string pattern, int index)
    {
        long steps = 0;

        Assert.Equal(index, SubstringSearch.IndexOf(text, pattern, ref steps));
    }

    [Theory]
    [InlineData("kiwi", "")]
    [InlineData("", "")]
    public static void EmptyPattern_IsFoundAtZeroWithoutSteps(string text, string pattern)
    {
        long steps = 0;

        Assert.Equal(0, SubstringSearch.IndexOf(text, pattern, ref steps));
        Assert.Equal(0, steps);
    }

    [Fact]
    public static void PatternLongerThanTheText_IsNotFoundWithoutSteps()
    {
        long steps = 0;

        Assert.Equal(-1, SubstringSearch.IndexOf("kiwi", "kiwis", ref steps));
        Assert.Equal(0, steps);
    }

    // The steps are added to the counter, which already holds the steps of earlier searches.
    [Fact]
    public static void Steps_AreAddedToTheCounter()
    {
        long steps = 1000;

        _ = SubstringSearch.IndexOf("aaaaaaaa", "aab", ref steps);

        Assert.InRange(steps, 1001, 1000 + (4 * (8 + 3)));
    }

    // Patterns up to 256 code units keep the prefix function on the stack, longer ones in an array: both find
    // an occurrence that only a correct prefix function finds after a long partial match.
    [Theory]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(1000)]
    public static void LongPattern_IsFoundAfterAPartialMatch(int length)
    {
        string pattern = new string('a', length - 1) + "b";
        string text = new string('a', length + 5) + "b" + "c";
        long steps = 0;

        Assert.Equal(6, SubstringSearch.IndexOf(text, pattern, ref steps));
        Assert.InRange(steps, 1, 4L * (text.Length + pattern.Length));
    }
}
