using System.Globalization;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Library;
using Xunit;

namespace Great.Parts.Cel.Properties.Library;

// The search for a substring takes linear time (docs/design/tests.md, "Properties of evaluation"): on each input it takes at
// most 4 · (n + m) steps, n and m the lengths of the text and the pattern in UTF-16 code units, and finds the index that a
// naive search finds. The inputs are the worst cases of a naive search and random strings over small alphabets. No time is
// measured: the counter of the search counts its comparisons.
public sealed class SubstringSearchLinearityTests
{
    private const string Property = "SubstringSearchLinearityTests.StepsBounded";
    private const ulong Seed = 20261105;
    private const int Cases = 3000;

    private static readonly Gen<SearchCase> RandomCase =
        from alphabet in Gen.OneOfConst("ab", "abc")
        from text in Gen.Char[alphabet].Array[0, 3000]
        from pattern in Gen.Char[alphabet].Array[0, 16]
        select new SearchCase(new string(text), new string(pattern));

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void StepsBounded()
    {
        foreach ((string name, SearchCase input) in ChosenInputs())
        {
            if (Violation(input) is { } reason)
            {
                Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"{Property}: input {name}: {reason}"));
            }
        }

        PropertyLoop.Check(Property, RandomCase, Seed, Cases, Violation, Show);
    }

    // The inputs on which a naive search compares about n · m code units, and the edges of the search.
    private static (string Name, SearchCase Input)[] ChosenInputs()
    {
        string abab = Repeat("ab", 500_000);
        string shortAbab = Repeat("ab", 100_000);
        string surrogates = Repeat("😀😁", 100_000);
        return
        [
            ("(ab)^500000 with (ab)^10 c", new(abab, Repeat("ab", 10) + "c")),
            ("(ab)^100000 with (ab)^200 b", new(shortAbab, Repeat("ab", 200) + "b")),
            ("(ab)^100000 with (ab)^200 c at its end", new(shortAbab[..^1] + "cb", Repeat("ab", 200)[..^1] + "c")),
            ("a^1000000 with a^9 b", new(new string('a', 1_000_000), new string('a', 9) + "b")),
            ("a^50000 with a^999 b", new(new string('a', 50_000), new string('a', 999) + "b")),
            ("a^100000 with a^299 b at its end", new(new string('a', 99_999) + "b", new string('a', 299) + "b")),
            ("a prefix of (ab)^500000", new(abab, abab[..1001])),
            ("(abc)^30000 with a pattern of 257 code units", new(Repeat("abc", 30_000), Repeat("abc", 85) + "aa")),
            ("an empty pattern", new(abab, string.Empty)),
            ("a pattern longer than the text", new("abab", "ababa")),
            ("surrogate pairs", new(surrogates, Repeat("😀😁", 50) + "😂")),
        ];
    }

    // The reason why the search breaks the property on a case, or null.
    private static string? Violation(SearchCase input)
    {
        long steps = 0;
        int index = SubstringSearch.IndexOf(input.Text, input.Pattern, ref steps);
        long bound = 4L * (input.Text.Length + input.Pattern.Length);
        if (steps > bound)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{steps} steps > 4 · (n + m) = {bound}");
        }

        int expected = NaiveIndexOf(input.Text, input.Pattern);
        return index == expected
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"index {index}, the naive search finds {expected}");
    }

    // The search of the definition: each start in order, comparing code units until the first difference.
    private static int NaiveIndexOf(string text, string pattern)
    {
        for (int start = 0; start + pattern.Length <= text.Length; start++)
        {
            int matched = 0;
            while (matched < pattern.Length && text[start + matched] == pattern[matched])
            {
                matched++;
            }

            if (matched == pattern.Length)
            {
                return start;
            }
        }

        return -1;
    }

    private static string Repeat(string unit, int count) => new StringBuilder(unit.Length * count).Insert(0, unit, count).ToString();

    private static string Show(SearchCase input) => string.Create(CultureInfo.InvariantCulture, $"text \"{input.Text}\", pattern \"{input.Pattern}\"");

    private sealed record SearchCase(string Text, string Pattern);
}
