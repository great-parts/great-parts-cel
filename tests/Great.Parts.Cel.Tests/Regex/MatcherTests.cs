using System;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The engine on patterns and subjects of its own: does some part of the subject match. The answers are those of a search of Go
// (regexp.MatchString) on the same pattern and subject, which the tests of the corpus compare as a whole.
public sealed class MatcherTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("", "abc", true)]
    [InlineData("a", "", false)]
    [InlineData("a", "a", true)]
    [InlineData("a", "ba", true)]
    [InlineData("a", "bab", true)]
    [InlineData("a", "b", false)]
    [InlineData("abc", "xxabcxx", true)]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "xxabxcx", false)]
    [InlineData("abc", "ab", false)]
    [InlineData("ab|cd", "xcd", true)]
    [InlineData("ab|cd", "acbd", false)]
    [InlineData("a|b|c", "xxc", true)]
    [InlineData("a*", "", true)]
    [InlineData("a+", "", false)]
    [InlineData("a+", "baa", true)]
    [InlineData("a?b", "b", true)]
    [InlineData("a?b", "ab", true)]
    [InlineData("a?b", "ac", false)]
    [InlineData("a.c", "abc", true)]
    [InlineData("a.c", "ac", false)]
    public static void IsMatch_WithLiteralsAndRepeats_FindsAPartOfTheSubject(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // The place of the match in the subject — at the start, in the middle, at the end, at no place.
    [Theory]
    [InlineData("xyz", "xyz--", true)]
    [InlineData("xyz", "--xyz--", true)]
    [InlineData("xyz", "--xyz", true)]
    [InlineData("xyz", "--xy-z--", false)]
    [InlineData("(?:xy)+z", "xyxyxyz", true)]
    [InlineData("(?:xy)+z", "xyxyxy", false)]
    [InlineData("x{2,3}y", "xxxxy", true)]
    [InlineData("x{2,3}y", "xy", false)]
    [InlineData("^(?:x{2,3})y$", "xxxy", true)]
    [InlineData("^(?:x{2,3})y$", "xxxxy", false)]
    [InlineData("^x{0}$", "", true)]
    [InlineData("^x{0}$", "x", false)]
    [InlineData("a{0}b", "b", true)]
    [InlineData("^a{2,}$", "aaaaa", true)]
    [InlineData("^a{2,}$", "a", false)]
    [InlineData("^(?:ab){2}$", "abab", true)]
    [InlineData("^(?:ab){2}$", "ababab", false)]
    public static void IsMatch_ByPlace_AndByCount(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // The conditions of zero width: the beginning and the end of the subject and of a line, the word boundary.
    [Theory]
    [InlineData("^a", "ab", true)]
    [InlineData("^a", "ba", false)]
    [InlineData("a$", "ba", true)]
    [InlineData("a$", "ab", false)]
    [InlineData("^$", "", true)]
    [InlineData("^$", "a", false)]
    [InlineData("^$", "\n", false)]
    [InlineData("\\Aa", "ab", true)]
    [InlineData("\\Aa", "ba", false)]
    [InlineData("a\\z", "ba", true)]
    [InlineData("a\\z", "a\n", false)]
    [InlineData("a$", "a\n", false)]
    [InlineData("^b", "a\nb", false)]
    [InlineData("(?m)^b", "a\nb", true)]
    [InlineData("a$", "a\nb", false)]
    [InlineData("(?m)a$", "a\nb", true)]
    [InlineData("(?m)^$", "\n", true)]
    [InlineData("(?m)^$", "a", false)]
    [InlineData("(?m)\\Ab", "a\nb", false)]
    [InlineData("(?m)a\\z", "a\nb", false)]
    [InlineData("\\bfoo\\b", "a foo b", true)]
    [InlineData("\\bfoo\\b", "afoob", false)]
    [InlineData("\\bfoo\\b", "foo", true)]
    [InlineData("\\Boo\\B", "afoob", true)]
    [InlineData("\\Boo\\B", "oo", false)]
    [InlineData("\\b", "", false)]
    [InlineData("\\B", "", true)]
    [InlineData("\\b", "-", false)]
    [InlineData("\\b", "a", true)]
    [InlineData("(?:^)*a", "a", true)]
    [InlineData("(?:\\b)+x", "-x", true)]
    public static void IsMatch_WithConditionsOfZeroWidth_ReadsTheNeighbours(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // A word character for a boundary is ASCII only: é is not one.
    [Theory]
    [InlineData("\\bé", "é", false)]
    [InlineData("\\Bé", "é", true)]
    [InlineData("\\bé", "aé", true)]
    [InlineData("a\\b", "aé", true)]
    [InlineData("a\\B", "aé", false)]
    [InlineData("\\w", "é", false)]
    [InlineData("\\W", "é", true)]
    public static void IsMatch_WithANonAsciiLetter_TreatsItAsNoWordCharacter(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // The dot matches any code point but the line feed, and with (?s) the line feed too.
    [Theory]
    [InlineData(".", "", false)]
    [InlineData(".", "\n", false)]
    [InlineData("(?s).", "\n", true)]
    [InlineData("a.c", "a\nc", false)]
    [InlineData("(?s)a.c", "a\nc", true)]
    [InlineData("^.$", "x", true)]
    [InlineData("^.$", "xy", false)]
    [InlineData("^.*$", "x\ny", false)]
    [InlineData("(?s)^.*$", "x\ny", true)]
    [InlineData("(?m)^.*$", "x\ny", true)]
    public static void IsMatch_WithTheDot_StopsAtTheLineFeed(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // A surrogate pair is one code point, so the dot takes both units and a count of two is two pairs.
    [Theory]
    [InlineData("^.$", "😀", true)]
    [InlineData("^..$", "😀", false)]
    [InlineData("^.$", "a😀", false)]
    [InlineData("^..$", "a😀", true)]
    [InlineData("^.{2}$", "😀😁", true)]
    [InlineData("😀", "x😀y", true)]
    [InlineData("😁", "x😀y", false)]
    [InlineData("^😀+$", "😀😀😀", true)]
    [InlineData("[😀-😂]", "😁", true)]
    [InlineData("[😀-😂]", "😃", false)]
    [InlineData("[^😀]", "😀", false)]
    [InlineData("[^😀]", "😁", true)]
    public static void IsMatch_OnSurrogatePairs_ReadsOneCodePointAtATime(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // A lone surrogate is U+FFFD and is one position by itself, as Go reads a byte that is not valid UTF-8; a high surrogate
    // followed by something other than a low one, and a low surrogate after a high one that was taken, are lone. The subjects are
    // in a table of the test and not in the arguments of an attribute, which cannot hold a lone surrogate.
    [Fact]
    public static void IsMatch_OnALoneSurrogate_ReadsReplacementCharacter()
    {
        (string Pattern, string Subject, bool Expected)[] rows =
        [
            ("^.$", "\uD800", true),
            ("^.$", "\uDC00", true),
            ("^\\x{FFFD}$", "\uD800", true),
            ("^\\x{FFFD}$", "\uDC00", true),
            ("^\\x{FFFD}$", "\uFFFD", true),
            ("^\\x{FFFD}$", "a", false),
            ("^\\x{FFFD}{2}$", "\uD800\uD800", true),
            ("^\\x{FFFD}$", "\uD800\uD800", false),
            ("^\\x{FFFD}\\x{FFFD}$", "\uDE00\uD83D", true),
            ("^..$", "\uD83Da", true),
            ("^.a$", "\uD83Da", true),
            ("^.$", "😀", true),
            ("^\\x{FFFD}$", "😀", false),
            ("^a.$", "a\uD83D", true),
            ("[^a]", "\uD800", true),
            ("\\W", "\uD800", true),
            ("\\w", "\uD800", false),
        ];

        foreach ((string pattern, string subject, bool expected) in rows)
        {
            Assert.True(expected == ProgramHelper.IsMatch(pattern, subject), $"{pattern} on {ParseHelper.Show(subject)} should be {expected}");
        }
    }

    // (?i) folds by the orbits of simple case folding: the Kelvin sign and the long s fold into k and s, and ß into ẞ and no further.
    [Theory]
    [InlineData("(?i)abc", "xABCx", true)]
    [InlineData("abc", "ABC", false)]
    [InlineData("(?i)k", "K", true)]
    [InlineData("(?i)k", "K", true)]
    [InlineData("(?i)K", "k", true)]
    [InlineData("(?i)K", "K", true)]
    [InlineData("k", "K", false)]
    [InlineData("(?i)s", "ſ", true)]
    [InlineData("(?i)ſ", "S", true)]
    [InlineData("(?i)ß", "ẞ", true)]
    [InlineData("(?i)ẞ", "ß", true)]
    [InlineData("(?i)ß", "SS", false)]
    [InlineData("(?i)σ", "ς", true)]
    [InlineData("(?i)ς", "Σ", true)]
    [InlineData("(?i)é", "É", true)]
    [InlineData("(?i)a", "b", false)]
    [InlineData("(?i)[k-m]", "K", true)]
    [InlineData("(?i)[^k]", "K", false)]
    [InlineData("(?i)[^k]", "x", true)]
    [InlineData("(?i)\\w", "K", true)]
    [InlineData("\\w", "K", false)]
    [InlineData("(?i)\\W", "ſ", false)]
    [InlineData("(?i)a(?-i)b", "AB", false)]
    [InlineData("(?i)a(?-i)b", "Ab", true)]
    public static void IsMatch_UnderFoldCase_FollowsTheOrbits(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // Classes of ranges, of tables, of a negation and of nothing.
    [Theory]
    [InlineData("[ab]", "xxb", true)]
    [InlineData("[ab]", "xxc", false)]
    [InlineData("[^ab]", "ab", false)]
    [InlineData("[^ab]", "abc", true)]
    [InlineData("[^ab]", "\n", true)]
    [InlineData("[a-c]+", "xxbx", true)]
    [InlineData("^[a-c]+$", "abcabc", true)]
    [InlineData("^[a-c]+$", "abcd", false)]
    [InlineData("\\d", "abc", false)]
    [InlineData("\\d", "a1", true)]
    [InlineData("\\D", "1", false)]
    [InlineData("\\D", "1a", true)]
    [InlineData("\\s", " ", true)]
    [InlineData("\\s", " ", false)]
    [InlineData("\\S", " ", true)]
    [InlineData("[[:alpha:]]", "é", false)]
    [InlineData("[[:alpha:]]", "a", true)]
    [InlineData("[[:^alpha:]]", "a", false)]
    [InlineData("\\p{Greek}", "α", true)]
    [InlineData("\\p{Greek}", "a", false)]
    [InlineData("\\P{Greek}", "α", false)]
    [InlineData("\\p{^Greek}", "a", true)]
    [InlineData("^\\pN$", "٣", true)]
    [InlineData("^\\pN$", "Ⅷ", true)]
    [InlineData("^\\pN$", "x", false)]
    [InlineData("\\PN", "123", false)]
    [InlineData("\\PN", "12a", true)]
    [InlineData("\\p{Lu}", "aBc", true)]
    [InlineData("\\p{Lu}", "abc", false)]
    [InlineData("(?i)\\p{Lu}", "abc", true)]
    [InlineData("\\p{^Lu}", "ABC", false)]
    [InlineData("[\\pN\\p{Greek}]", "α", true)]
    [InlineData("[^\\pL]", "é", false)]
    [InlineData("\\p{Any}", "\uD800", true)]
    [InlineData("[^\\x00-\\x{10FFFF}]", "a", false)]
    [InlineData("[^\\x00-\\x{10FFFF}]", "", false)]
    [InlineData("[^\\x00-\\x{10FFFF}]|b", "b", true)]
    public static void IsMatch_WithClasses_TestsOneCodePoint(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // Loops over what can be empty end: the sets refuse a state they hold, so no path is taken twice at one position.
    [Theory]
    [InlineData("^(?:)*$", "", true)]
    [InlineData("^(?:a*)*$", "aaa", true)]
    [InlineData("^(?:a*)+$", "", true)]
    [InlineData("^(?:a*)*$", "aab", false)]
    [InlineData("(a*)*b", "aaaa", false)]
    [InlineData("(a*)*b", "aaaab", true)]
    [InlineData("(a|aa)+$", "aaaaaaaaaa", true)]
    [InlineData("^(a+)+$", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaab", false)]
    [InlineData("(?:\\b)*x", "x", true)]
    [InlineData("(?:^|a)*b", "aab", true)]
    [InlineData("(?:a?){3}a{3}", "aaa", true)]
    [InlineData("^(?:a?){3}a{3}$", "aa", false)]
    [InlineData("^(?:a?){3}a{3}$", "aaaaaa", true)]
    [InlineData("^(?:a?){3}a{3}$", "aaaaaaa", false)]
    public static void IsMatch_WithLoopsThatCanBeEmpty_Ends(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, ProgramHelper.IsMatch(pattern, subject));
    }

    // The steps are the entries of an instruction into a set. "a" is Char and Match; on "a" the first instruction enters at
    // position 0 and Match enters from it.
    [Theory]
    [InlineData("a", "", false, 1)]
    [InlineData("a", "b", false, 2)]
    [InlineData("a", "a", true, 2)]
    [InlineData("ab", "ab", true, 4)]
    [InlineData("a*", "", true, 3)]
    [InlineData("^a", "ba", false, 4)]
    [InlineData("", "", true, 1)]
    public static void Steps_OfSmallRuns_AreTheEntriesOfInstructions(string pattern, string subject, bool matches, long steps)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);

        Assert.Equal(matches, ProgramHelper.IsMatch(program, subject, out long counted));
        Assert.Equal(steps, counted);
    }

    // Steps are added to the counter that the caller passes, so a run does not reset it.
    [Fact]
    public static void Steps_AreAddedToTheCounterOfTheCaller()
    {
        CompiledPattern program = ProgramHelper.Compile("a");
        long steps = 100;

        Matcher.IsMatch(program, "a", ref steps);

        Assert.Equal(102, steps);
    }

    // The same program on the same subject gives the same steps whatever the arrays of the pool held before.
    [Fact]
    public static void Steps_DoNotDependOnTheContentsOfRentedArrays()
    {
        CompiledPattern big = ProgramHelper.Compile("(?:a|b|c)*d(?:e|f)*[x-z]+");
        CompiledPattern small = ProgramHelper.Compile("ab|ac");
        string subject = "abcabcdefxyzabcdef";

        ProgramHelper.IsMatch(big, subject, out long first);
        ProgramHelper.IsMatch(small, subject, out _);
        ProgramHelper.IsMatch(big, subject, out long second);

        Assert.Equal(first, second);
    }

    // The conditions at a position are a mask: beginning and end of the text and of a line, and a boundary or none.
    [Theory]
    [InlineData(-1, -1, EmptyCondition.BeginText | EmptyCondition.BeginLine | EmptyCondition.EndText | EmptyCondition.EndLine | EmptyCondition.NoWordBoundary)]
    [InlineData(-1, (int)'a', EmptyCondition.BeginText | EmptyCondition.BeginLine | EmptyCondition.WordBoundary)]
    [InlineData((int)'a', -1, EmptyCondition.EndText | EmptyCondition.EndLine | EmptyCondition.WordBoundary)]
    [InlineData((int)'a', (int)'b', EmptyCondition.NoWordBoundary)]
    [InlineData((int)'a', (int)' ', EmptyCondition.WordBoundary)]
    [InlineData((int)' ', (int)'-', EmptyCondition.NoWordBoundary)]
    [InlineData((int)'\n', (int)'a', EmptyCondition.BeginLine | EmptyCondition.WordBoundary)]
    [InlineData((int)'a', (int)'\n', EmptyCondition.EndLine | EmptyCondition.WordBoundary)]
    [InlineData((int)'\n', (int)'\n', EmptyCondition.BeginLine | EmptyCondition.EndLine | EmptyCondition.NoWordBoundary)]
    [InlineData((int)'_', (int)'9', EmptyCondition.NoWordBoundary)]
    [InlineData((int)'é', (int)'a', EmptyCondition.WordBoundary)]
    [InlineData(0x10000, (int)'a', EmptyCondition.WordBoundary)]
    public static void Context_OfTwoCodePoints_IsTheMaskOfTheConditionsThatHold(int before, int after, int expected)
    {
        Assert.Equal(expected, Matcher.Context(before, after));
    }

    // A program cannot be run without being given.
    [Fact]
    public static void IsMatch_WithoutAProgram_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
            {
                long steps = 0;
                return Matcher.IsMatch(null!, "a", ref steps);
            });
    }
}
