using System;
using System.Linq;
using Great.Parts.Cel.Library.Text;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The prepared pattern of the search for a substring: the prefix function is made once, and a search goes on from any position of the
// text — forward for the first occurrence at or after it, reversed for the last occurrence that ends at or before an end — in both the
// stack form of a short pattern and the array form of one over 256 code units.
public sealed class PreparedPatternTests
{
    private static readonly string Long = string.Concat(Enumerable.Repeat("ab", 150));

    [Theory]
    [InlineData("abcabcabd", "abc", 0, 0)]
    [InlineData("abcabcabd", "abc", 1, 3)]
    [InlineData("abcabcabd", "abc", 4, -1)]
    [InlineData("abcabcabd", "abd", 0, 6)]
    [InlineData("abcabcabd", "abd", 7, -1)]
    [InlineData("aaaa", "aa", 1, 1)]
    [InlineData("aaaa", "aa", 3, -1)]
    [InlineData("abc", "abcd", 0, -1)]
    [InlineData("abc", "c", 3, -1)]
    [InlineData("ababab", "abab", 1, 2)]
    public static void IndexOf_GoesOnFromAnyPosition(string text, string pattern, int from, int expected)
    {
        long steps = 0;
        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(pattern, false, buffer, ref steps);

        Assert.Equal(expected, prepared.IndexOf(text, from, ref steps));
    }

    [Theory]
    [InlineData("abcabcabd", "abc", 9, 3)]
    [InlineData("abcabcabd", "abc", 6, 3)]
    [InlineData("abcabcabd", "abc", 5, 0)]
    [InlineData("abcabcabd", "abc", 2, -1)]
    [InlineData("abcabcabd", "abd", 9, 6)]
    [InlineData("abcabcabd", "abd", 8, -1)]
    [InlineData("aaaa", "aa", 4, 2)]
    [InlineData("aaaa", "aa", 3, 1)]
    [InlineData("aaaa", "aa", 1, -1)]
    [InlineData("abc", "abcd", 3, -1)]
    [InlineData("ababab", "abab", 5, 0)]
    public static void LastIndexOf_ReadsFromTheEndAndEndsAtOrBeforeTheEnd(string text, string pattern, int end, int expected)
    {
        long steps = 0;
        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(pattern, true, buffer, ref steps);

        Assert.Equal(expected, prepared.LastIndexOf(text, end, ref steps));
    }

    // A pattern over 256 code units keeps its prefix function in an array and finds the same occurrences.
    [Fact]
    public static void LongPattern_IsFoundForwardAndBackward()
    {
        string text = Long + "c" + Long;
        long steps = 0;
        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern forward = SubstringSearch.PreparedPattern.Create(Long, false, buffer, ref steps);
        SubstringSearch.PreparedPattern backward = SubstringSearch.PreparedPattern.Create(Long, true, buffer, ref steps);

        Assert.Equal(0, forward.IndexOf(text, 0, ref steps));
        Assert.Equal(301, forward.IndexOf(text, 1, ref steps));
        Assert.Equal(-1, forward.IndexOf(text, 302, ref steps));
        Assert.Equal(301, backward.LastIndexOf(text, text.Length, ref steps));
        Assert.Equal(0, backward.LastIndexOf(text, text.Length - 1, ref steps));
    }

    // A pattern of 63, 64 or 65 code units, either side of the limit of the search by .NET, finds from any position the occurrence that
    // the ordinal search of .NET finds, forward and backward, and a pattern of up to 64 units makes no prefix function.
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    public static void AroundTheLimit_FindsTheOccurrencesOfDotNetFromAnyPosition(int length)
    {
        string pattern = string.Concat(Enumerable.Repeat("ab", length))[..(length - 1)] + "c";
        string text = string.Concat(Enumerable.Repeat("ab", 100)) + pattern + string.Concat(Enumerable.Repeat("ab", 50)) + pattern + "ab";
        long steps = 0;
        Span<byte> forwardBuffer = stackalloc byte[SubstringSearch.StackPatternLength];
        Span<byte> backwardBuffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern forward = SubstringSearch.PreparedPattern.Create(pattern, false, forwardBuffer, ref steps);
        long preparationSteps = steps;
        SubstringSearch.PreparedPattern backward = SubstringSearch.PreparedPattern.Create(pattern, true, backwardBuffer, ref steps);

        Assert.Equal(length <= SubstringSearch.HybridLimit, preparationSteps == 0);
        for (int from = 0; from <= text.Length; from += 7)
        {
            int relative = text.AsSpan(from).IndexOf(pattern.AsSpan(), StringComparison.Ordinal);
            Assert.Equal(relative < 0 ? -1 : from + relative, forward.IndexOf(text, from, ref steps));
        }

        for (int end = 0; end <= text.Length; end += 7)
        {
            Assert.Equal(text.AsSpan(0, end).LastIndexOf(pattern.AsSpan(), StringComparison.Ordinal), backward.LastIndexOf(text, end, ref steps));
        }
    }

    // The steps stay within 2m for the preparation and 3n for the passes over the text, which include the units that a jump to the
    // next first unit passes.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Steps_AreWithinTheBound(bool reversed)
    {
        string text = new('a', 5000);
        string pattern = new string('a', 19) + "b";
        long steps = 0;
        Span<byte> buffer = stackalloc byte[SubstringSearch.StackPatternLength];
        SubstringSearch.PreparedPattern prepared = SubstringSearch.PreparedPattern.Create(reversed ? "b" + new string('a', 19) : pattern, reversed, buffer, ref steps);

        int found = reversed ? prepared.LastIndexOf(text, text.Length, ref steps) : prepared.IndexOf(text, 0, ref steps);

        Assert.Equal(-1, found);
        Assert.True(steps <= (3 * text.Length) + (2 * pattern.Length), steps.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
