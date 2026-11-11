using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Library.Extensions.Strings;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Library;

// The search of the functions indexOf, lastIndexOf, replace and split of the set strings takes linear time (docs/design/tests.md,
// "Substring search of the extension sets"): on each input a call takes at most 4 · (n + m) steps, n the length of the text and m the
// length of the pattern in UTF-16 code units, the preparation of the pattern included, and gives the result that a search written
// here over code points gives. The inputs are the worst cases of a naive search and random strings over small alphabets. No time is
// measured: the counter counts the comparisons of two code units.
public sealed class StringSearchLinearityTests
{
    private const ulong Seed = 20261109;
    private const int Cases = 2000;

    private static readonly string LoneHigh = ((char)0xD83D).ToString();
    private static readonly string LoneLow = ((char)0xDE00).ToString();

    // The alphabets of the random strings: two and three code units, pairs, and lone surrogates beside pairs.
    private static readonly string[][] Alphabets =
    [
        ["a", "b"],
        ["a", "b", "c"],
        ["a", "😀"],
        ["😀", "😁", "a"],
        ["a", LoneLow, "b"],
        ["a", LoneHigh, "😀"],
    ];

    private static readonly Gen<SearchCase> RandomCase =
        from tokens in Gen.OneOfConst(Alphabets)
        from text in Gen.OneOfConst(tokens).Array[0, 200]
        from pattern in Gen.OneOfConst(tokens).Array[0, 6]
        from replacement in Gen.OneOfConst("zz", string.Empty, "é😀", "a")
        from number in Gen.Int[-2, 250]
        select new SearchCase(string.Concat(text), string.Concat(pattern), replacement, number);

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void IndexOfStepsBounded() => Check("StringSearchLinearityTests.IndexOfStepsBounded", IndexOfViolation, Offsets);

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void LastIndexOfStepsBounded() => Check("StringSearchLinearityTests.LastIndexOfStepsBounded", LastIndexOfViolation, Offsets);

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void ReplaceStepsBounded() => Check("StringSearchLinearityTests.ReplaceStepsBounded", ReplaceViolation, static _ => Limits);

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void SplitStepsBounded() => Check("StringSearchLinearityTests.SplitStepsBounded", SplitViolation, static _ => Limits);

    private static void Check(string property, Func<SearchCase, string?> violation, Func<SearchCase, long[]> variations)
    {
        foreach (SearchCase chosen in ChosenInputs())
        {
            foreach (long number in variations(chosen))
            {
                SearchCase input = chosen with { Number = number };
                if (violation(input) is { } reason)
                {
                    Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"{property}: input {chosen.Name}, number {number}: {reason}"));
                }
            }
        }

        PropertyLoop.Check(property, RandomCase, Seed, Cases, violation, Show);
    }

    // The offsets of indexOf and lastIndexOf: at the start, in the middle, at the end and past the end, in code points.
    private static long[] Offsets(SearchCase input)
    {
        long count = Decode(input.Text).Length;
        return [0, count / 2, count, count + 1];
    }

    // The limits of replace and split: none, 0, 1, 2 and more than the matches.
    private static readonly long[] Limits = [-1, 0, 1, 2, 1_000_000_000];

    // The inputs on which a naive search compares about n · m code units, and the edges of the search.
    private static List<SearchCase> ChosenInputs()
    {
        string abab = Repeat("ab", 500_000);
        string shortAbab = Repeat("ab", 100_000);
        string manyA = new('a', 1_000_000);
        string surrogates = Repeat("😀😁", 100_000);
        return
        [
            new("(ab)^500000 with (ab)^10 c", abab, Repeat("ab", 10) + "c"),
            new("(ab)^100000 with (ab)^200 b", shortAbab, Repeat("ab", 200) + "b"),
            new("(ab)^100000 with (ab)^199 aa", shortAbab, Repeat("ab", 199) + "aa"),
            new("(ab)^100000 with (ab)^200 c at its end", shortAbab[..^1] + "c", Repeat("ab", 200)),
            new("a^1000000 with a^9 b", manyA, new string('a', 9) + "b"),
            new("a^50000 with a^999 b", new string('a', 50_000), new string('a', 999) + "b"),
            new("a^100000 with b a^299, which differs at the first unit from the end", new string('a', 100_000), "b" + new string('a', 299)),
            new("a^100000 with a^299 b at its end", new string('a', 99_999) + "b", new string('a', 299) + "b"),
            new("a prefix of (ab)^500000 of 100000 units", abab, abab[..100_000]),
            new("(abc)^30000 with a pattern of 257 units", Repeat("abc", 30_000), Repeat("abc", 85) + "aa"),
            new("an empty pattern", shortAbab, string.Empty),
            new("a pattern longer than the text", "abab", "ababa"),
            new("an empty text and an empty pattern", string.Empty, string.Empty),
            new("surrogate pairs", surrogates, Repeat("😀😁", 50) + "😂"),
            new("a pair that ends the text", surrogates, "😁😀😁"),
            new("a lone surrogate in the text", Repeat("a", 1000) + LoneHigh + Repeat("b", 1000) + LoneLow + "a", LoneLow + "a"),
            new("a lone low surrogate against pairs", surrogates, LoneLow),
            new("ab in (ab)^500000 with a new string", abab, "ab"),
            new("aa in a^1000000", manyA, "aa"),
            new("a pattern that never matches", abab, "c"),
            new("many matches with a long new string", Repeat("ab", 100), "ab", Repeat("z", 5000)),
            new("a in a^1000000", manyA, "a"),
            new("a^5 in a^1000000", manyA, new string('a', 5)),
            new("a^4 b in a^1000000", manyA, new string('a', 4) + "b"),
        ];
    }

    private static string? IndexOfViolation(SearchCase input)
    {
        long steps = 0;
        long actual = StringFunctions.IndexOf(input.Text, input.Pattern, input.Number, ref steps);
        long expected = ReferenceIndexOf(input.Text, input.Pattern, input.Number);
        return Verdict(steps, input, actual == expected ? null : $"result {actual}, the reference gives {expected}");
    }

    private static string? LastIndexOfViolation(SearchCase input)
    {
        long steps = 0;
        long actual = StringFunctions.LastIndexOf(input.Text, input.Pattern, input.Number, ref steps);
        long expected = ReferenceLastIndexOf(input.Text, input.Pattern, input.Number);
        if (Verdict(steps, input, actual == expected ? null : $"with the offset {input.Number}: result {actual}, the reference gives {expected}") is { } reason)
        {
            return reason;
        }

        steps = 0;
        actual = StringFunctions.LastIndexOf(input.Text, input.Pattern, null, ref steps);
        expected = ReferenceLastIndexOf(input.Text, input.Pattern, null);
        return Verdict(steps, input, actual == expected ? null : $"without an offset: result {actual}, the reference gives {expected}");
    }

    private static string? ReplaceViolation(SearchCase input)
    {
        long steps = 0;
        string actual = StringFunctions.Replace(input.Text, input.Pattern, input.Replacement, input.Number, null, ref steps);
        string expected = ReferenceReplace(input.Text, input.Pattern, input.Replacement, input.Number);
        return Verdict(steps, input, string.Equals(actual, expected, StringComparison.Ordinal) ? null : "the result differs from the reference of the limit " + input.Number);
    }

    private static string? SplitViolation(SearchCase input)
    {
        long steps = 0;
        CelValue list = StringFunctions.Split(input.Text, input.Pattern, input.Number, null, ref steps);
        string[] actual = list.TryGetList(out CelList? parts) ? [.. parts.Select(static part => part.TryGetString(out string? text) ? text : "?")] : [];
        List<string> expected = ReferenceSplit(input.Text, input.Pattern, input.Number);
        return Verdict(steps, input, actual.SequenceEqual(expected, StringComparer.Ordinal) ? null : "the parts differ from the reference of the limit " + input.Number);
    }

    // The reason why a call breaks the property: a result that is not the reference's, or more than 4 · (n + m) steps.
    private static string? Verdict(long steps, SearchCase input, string? mismatch)
    {
        long bound = 4L * (input.Text.Length + input.Pattern.Length);
        if (steps > bound)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{steps} steps > 4 · (n + m) = {bound}");
        }

        return mismatch;
    }

    // The index in code points of the first occurrence at or after the offset, as the page of the set says: an empty pattern is at the
    // offset, a non-empty one is not at the end, an offset outside the string is OffsetOutOfRange.
    private static long ReferenceIndexOf(string text, string pattern, long offset)
    {
        int[] t = Decode(text);
        int[] p = Decode(pattern);
        if (offset < 0 || offset > t.Length)
        {
            return StringFunctions.OffsetOutOfRange;
        }

        if (p.Length == 0)
        {
            return offset;
        }

        for (int start = (int)offset; start + p.Length <= t.Length; start++)
        {
            if (Matches(t, start, p))
            {
                return start;
            }
        }

        return -1;
    }

    // The last occurrence: with an offset, the last that starts at or before it; the empty pattern gives the offset, or the number of
    // code points without one; at the offset equal to the length a non-empty pattern is not found.
    private static long ReferenceLastIndexOf(string text, string pattern, long? offset)
    {
        int[] t = Decode(text);
        int[] p = Decode(pattern);
        long from = t.Length - p.Length;
        if (offset is long given)
        {
            if (given < 0 || given > t.Length)
            {
                return StringFunctions.OffsetOutOfRange;
            }

            if (p.Length == 0)
            {
                return given;
            }

            if (given == t.Length)
            {
                return -1;
            }

            from = Math.Min(given, from);
        }
        else if (p.Length == 0)
        {
            return t.Length;
        }

        for (long start = from; start >= 0; start--)
        {
            if (Matches(t, (int)start, p))
            {
                return start;
            }
        }

        return -1;
    }

    // Go's Replace: at most limit replacements, all below 0, none for 0; an empty old string is inserted before each code point and at the
    // end, up to the limit.
    private static string ReferenceReplace(string text, string oldText, string newText, long limit)
    {
        if (limit == 0)
        {
            return text;
        }

        int[] t = Decode(text);
        int[] o = Decode(oldText);
        StringBuilder result = new();
        if (o.Length == 0)
        {
            long inserted = 0;
            int position = 0;
            while (true)
            {
                if (limit > 0 && inserted == limit)
                {
                    AppendPoints(result, t, position, t.Length);
                    break;
                }

                result.Append(newText);
                inserted++;
                if (position == t.Length)
                {
                    break;
                }

                AppendPoints(result, t, position, position + 1);
                position++;
            }

            return result.ToString();
        }

        long replaced = 0;
        for (int i = 0; i < t.Length;)
        {
            if ((limit < 0 || replaced < limit) && Matches(t, i, o))
            {
                result.Append(newText);
                i += o.Length;
                replaced++;
            }
            else
            {
                AppendPoints(result, t, i, i + 1);
                i++;
            }
        }

        return result.ToString();
    }

    // Go's SplitN: at most limit parts, the last holding the rest, all of them below 0 and none for 0; an empty separator splits into code
    // points and an empty string gives no part.
    private static List<string> ReferenceSplit(string text, string separator, long limit)
    {
        List<string> parts = [];
        if (limit == 0)
        {
            return parts;
        }

        int[] t = Decode(text);
        int[] s = Decode(separator);
        if (s.Length == 0)
        {
            for (int i = 0; i < t.Length; i++)
            {
                if (limit > 0 && parts.Count == limit - 1)
                {
                    parts.Add(Encode(t, i, t.Length));
                    break;
                }

                parts.Add(Encode(t, i, i + 1));
            }

            return parts;
        }

        int start = 0;
        int index = 0;
        while (index + s.Length <= t.Length && (limit < 0 || parts.Count < limit - 1))
        {
            if (Matches(t, index, s))
            {
                parts.Add(Encode(t, start, index));
                index += s.Length;
                start = index;
            }
            else
            {
                index++;
            }
        }

        parts.Add(Encode(t, start, t.Length));
        return parts;
    }

    private static bool Matches(int[] text, int at, int[] pattern)
    {
        if (at + pattern.Length > text.Length)
        {
            return false;
        }

        for (int i = 0; i < pattern.Length; i++)
        {
            if (text[at + i] != pattern[i])
            {
                return false;
            }
        }

        return true;
    }

    // The code points of a string: a pair is one, and every other code unit, a lone surrogate included, is one.
    private static int[] Decode(string text)
    {
        List<int> points = new(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                points.Add(char.ConvertToUtf32(text[i], text[i + 1]));
                i++;
            }
            else
            {
                points.Add(text[i]);
            }
        }

        return [.. points];
    }

    private static string Encode(int[] points, int start, int end)
    {
        StringBuilder result = new();
        AppendPoints(result, points, start, end);
        return result.ToString();
    }

    private static void AppendPoints(StringBuilder result, int[] points, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (points[i] >= 0x10000)
            {
                result.Append(char.ConvertFromUtf32(points[i]));
            }
            else
            {
                result.Append((char)points[i]);
            }
        }
    }

    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static string Show(SearchCase input) =>
        string.Create(CultureInfo.InvariantCulture, $"text \"{input.Text}\", pattern \"{input.Pattern}\", replacement \"{input.Replacement}\", number {input.Number}");

    private sealed record SearchCase(string Name, string Text, string Pattern, string Replacement, long Number)
    {
        internal SearchCase(string name, string text, string pattern)
            : this(name, text, pattern, "zz", 0)
        {
        }

        internal SearchCase(string name, string text, string pattern, string replacement)
            : this(name, text, pattern, replacement, 0)
        {
        }

        internal SearchCase(string text, string pattern, string replacement, long number)
            : this("random", text, pattern, replacement, number)
        {
        }
    }
}
