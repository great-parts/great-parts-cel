using System;
using System.Linq;
using Great.Parts.Cel.Library.Text;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The search for a substring: the index of the first occurrence by UTF-16 code units, and the steps it adds.
public sealed class SubstringSearchTests
{
    // A pattern of up to 64 code units is searched by .NET and a longer one by Knuth–Morris–Pratt: on both sides of the limit, 63, 64
    // and 65 code units, the hybrid finds the occurrence that the ordinal search of .NET and the search by KMP find, on texts with
    // long partial matches (a^n, (ab)^n) and near misses, with the pattern at the start, in the middle, at the end and absent.
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public static void Hybrid_AroundTheLimit_FindsTheOccurrenceThatDotNetAndKmpFind(int length)
    {
        string[] patterns =
        [
            new string('a', length - 1) + "b",
            string.Concat(Enumerable.Repeat("ab", length))[..(length - 2)] + "bb",
        ];
        string nearMiss = new string('a', length - 1) + "c";
        int found = 0;

        foreach (string pattern in patterns)
        {
            string[] texts =
            [
                pattern + "xyz",
                new string('q', 50) + pattern,
                nearMiss + nearMiss + pattern + nearMiss,
                new string('a', 1000),
                string.Concat(Enumerable.Repeat("ab", 2000)),
                string.Concat(Enumerable.Repeat("ab", 2000)) + pattern,
                pattern[..^1],
            ];
            foreach (string text in texts)
            {
                long steps = 0;
                long kmpSteps = 0;
                int expected = text.IndexOf(pattern, StringComparison.Ordinal);

                Assert.Equal(expected, SubstringSearch.IndexOf(text, pattern, ref steps));
                Assert.Equal(expected, SubstringSearch.IndexOfKmp(text, pattern, ref kmpSteps));
                found += expected >= 0 ? 1 : 0;
            }
        }

        Assert.True(found >= 8, $"{found} texts with the pattern");
    }

    // The steps tell the branch: up to the limit one for each place of the text where an occurrence may start, above it those of KMP.
    [Theory]
    [InlineData(63, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public static void Hybrid_SearchesByDotNetUpToTheLimitAndByKmpAbove(int length, bool byDotNet)
    {
        string text = new('a', 1000);
        string pattern = new string('a', length - 1) + "b";
        long steps = 0;
        long kmpSteps = 0;

        Assert.Equal(-1, SubstringSearch.IndexOf(text, pattern, ref steps));
        Assert.Equal(-1, SubstringSearch.IndexOfKmp(text, pattern, ref kmpSteps));

        if (byDotNet)
        {
            Assert.Equal(text.Length - length + 1, steps);
            Assert.True(kmpSteps > steps, $"KMP {kmpSteps} steps, the hybrid {steps}");
        }
        else
        {
            Assert.Equal(kmpSteps, steps);
        }
    }

    // A found occurrence counts the places up to it, the one that starts there included.
    [Fact]
    public static void Hybrid_FoundByDotNet_CountsThePlacesUpToTheOccurrence()
    {
        long steps = 0;

        Assert.Equal(5, SubstringSearch.IndexOf("xxxxxkiwi", "kiwi", ref steps));
        Assert.Equal(6, steps);
    }

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
