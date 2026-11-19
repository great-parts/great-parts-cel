using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Library.Regex;
using Great.Parts.Cel.Library.Text;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// How the automaton goes on from the state where no thread of the match is alive. The condition of the choice: a literal prefix of at
// least two code units that every match begins with, with no (?i) at its beginning and no U+FFFD in it, is found by SubstringSearch;
// otherwise, when the code units that leave the state are at most three, or all ASCII (any number of them), they are found by a
// search of SearchValues; otherwise each code point is read. Whatever is chosen, the answer is the answer of the engine of sets of states.
public sealed class DfaSkipTests
{
    // The prefix is read through the instructions that read one code point each and through the conditions of zero width, and stops
    // at a fork, a class or a folded character.
    [Theory]
    [InlineData("ORD-[0-9]+", "ORD-")]
    [InlineData("abc", "abc")]
    [InlineData("error: .*", "error: ")]
    [InlineData("ab(?i)cd", "ab")]
    [InlineData("(?:ab)+c", "ab")]
    [InlineData("ab\\z", "ab")]
    [InlineData("abc|abd", "ab")]
    [InlineData("\U0001F600z", "\U0001F600z")]
    [InlineData("\U0001F600+", "\U0001F600")]
    public static void Skip_OfALiteralPrefixOfTwoUnitsOrMore_IsThePrefix(string pattern, string prefix)
    {
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(DfaSkip.Prefix, automaton.Skip);
        Assert.Equal(prefix, automaton.Prefix.ToString());
        Assert.Equal(0, automaton.UnitCount);
    }

    // A long prefix is searched too: one of 80 units by the passes of SubstringSearch for a long pattern. One of 300 units, which has
    // an automaton only when the limit of the states is raised (it takes 301 states), is cut at the 256 units that SubstringSearch
    // keeps on the stack, and the automaton still answers as the engine does.
    [Fact]
    public static void Skip_OfALongPrefix_IsThePrefixCutAtTheLengthOnTheStack()
    {
        string eighty = string.Concat(Enumerable.Repeat("ab", 40));
        string threeHundred = new('q', 300);
        CompiledPattern program = ProgramHelper.Compile(threeHundred + "[0-9]");

        Dfa shorter = DfaHelper.Build(eighty + "[0-9]");
        Dfa? longer = DfaBuilder.TryBuild(program, 400, 8, DfaBuilder.MaxWork, out _);

        Assert.Equal((DfaSkip.Prefix, eighty), (shorter.Skip, shorter.Prefix.ToString()));
        Assert.NotNull(longer);
        Assert.Equal((DfaSkip.Prefix, SubstringSearch.StackPatternLength), (longer.Skip, longer.Prefix.Length));
        Assert.Equal(threeHundred[..SubstringSearch.StackPatternLength], longer.Prefix.ToString());
        foreach (string subject in new[] { threeHundred + "7", "x" + threeHundred + "7", threeHundred, threeHundred[..^1] + "7", new string('q', 299) + "x" + threeHundred + "7" })
        {
            DfaHelper.AssertSame(program, longer, "q{300}[0-9]", subject);
        }
    }

    // A prefix of one code unit, a prefix that begins with a folded character, and a pattern without a prefix go by the units that
    // leave the state, when they are at most three — of the orbit of a letter under (?i), the letters of a class — or all ASCII, as
    // the digits and the letters of a class are, and the ones that do not leave the state are not counted: the x of x* stays in it.
    [Theory]
    [InlineData("a+b", 1)]
    [InlineData("(?i)ord-[0-9]+", 2)]
    [InlineData("(?i)k", 3)]
    [InlineData("[ab]c", 2)]
    [InlineData("[abc]+d", 3)]
    [InlineData("x*abc|x*abd", 1)]
    [InlineData("x*[ab]", 2)]
    [InlineData("a[\U0001F600x]", 1)]
    [InlineData("[0-9]{4}-[0-9]{2}", 10)]
    [InlineData("[A-Za-z]+@", 52)]
    [InlineData("\\w+@", 63)]
    [InlineData("[\\x00-\\x7F]x", 128)]
    public static void Skip_OfAFewUnits_IsTheSearchOfThem(string pattern, int units)
    {
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(DfaSkip.Units, automaton.Skip);
        Assert.Equal(units, automaton.UnitCount);
        Assert.Equal(0, automaton.Prefix.Length);
    }

    // No skip: more than three units leave the state and one of them is not ASCII, a class that holds many code points, the code point
    // of a lone surrogate (which no search for units finds), a code point outside the Basic Multilingual Plane among the units, a
    // program that begins with the beginning of the text, and a program that matches at once.
    [Theory]
    [InlineData("[a-zé]x")]
    [InlineData("[^a]b")]
    [InlineData("\\pL+x")]
    [InlineData("�x")]
    [InlineData("[\U0001F600a]b")]
    [InlineData("^abc")]
    [InlineData("\\Aabc")]
    [InlineData("a*")]
    [InlineData("")]
    public static void Skip_WhenNothingCanBeSearched_ReadsEachCodePoint(string pattern)
    {
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(DfaSkip.None, automaton.Skip);
    }

    // The search of a set of ASCII units, whose size is not three: the answers themselves, and the engine's on random subjects.
    [Theory]
    [InlineData("[0-9]{4}-[0-9]{2}", "see 2024-05 here", true)]
    [InlineData("[0-9]{4}-[0-9]{2}", "see 2024-5 here", false)]
    [InlineData("[0-9]{4}-[0-9]{2}", "12345-67", true)]
    [InlineData("[0-9]{4}-[0-9]{2}", "123-45-67", false)]
    [InlineData("[0-9]{4}-[0-9]{2}", "١٢٣٤-12", false)]
    [InlineData("[0-9]{4}-[0-9]{2}", "", false)]
    [InlineData("[A-Za-z]+@", "123 abc@x", true)]
    [InlineData("[A-Za-z]+@", "@@@ 123@", false)]
    [InlineData("[A-Za-z]+@", "é@ é@", false)]
    [InlineData("[A-Za-z]+@", "éa@", true)]
    [InlineData("[a-zé]x", "éx", true)]
    [InlineData("[a-zé]x", "èx", false)]
    public static void Skip_OfAsciiUnits_AnswersAsTheEngine(string pattern, string subject, bool expected)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        CompiledPattern program = ProgramHelper.Compile(pattern);
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(expected, DfaHelper.Engine(program, subject));
        Assert.Equal(expected, automaton.IsMatch(subject));
        var random = new DfaHelper.Sequence(7);
        foreach (string text in DfaHelper.RandomSubjects(random, pattern, 200))
        {
            DfaHelper.AssertSame(program, automaton, pattern, text);
        }
    }

    // Whichever way the search goes on, the answer is the engine's: the places of the prefix and of the units in the text, partial
    // prefixes, the prefix twice in a row, a prefix cut by the end of the text, and the surrogates.
    [Theory]
    [InlineData("ORD-[0-9]+")]
    [InlineData("error: .*")]
    [InlineData("(?:ab)+c")]
    [InlineData("\U0001F600z")]
    [InlineData("a+b")]
    [InlineData("(?i)ord-[0-9]+")]
    [InlineData("(?i)k")]
    [InlineData("[ab]c")]
    [InlineData("x*abc")]
    [InlineData("abc|abd")]
    [InlineData("�x")]
    [InlineData("\U0001F600[0-9]")]
    public static void Skip_AnswersAsTheEngine_OnTheEdgesOfTheText(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        CompiledPattern program = ProgramHelper.Compile(pattern);
        Dfa automaton = DfaHelper.Build(pattern);
        var random = new DfaHelper.Sequence(99);
        foreach (string subject in Subjects(pattern).Concat(DfaHelper.RandomSubjects(random, pattern, 300)))
        {
            DfaHelper.AssertSame(program, automaton, pattern, subject);
        }
    }

    // A prefix of 80 and of 200 units, with the text around it: before it, after it, twice, cut one unit short, and far from the start.
    [Fact]
    public static void Skip_OfALongPrefix_AnswersAsTheEngine()
    {
        foreach (string literal in new[] { string.Concat(Enumerable.Repeat("ab", 40)), new string('q', 200) })
        {
            string pattern = literal + "[0-9]";
            CompiledPattern program = ProgramHelper.Compile(pattern);
            Dfa automaton = DfaHelper.Build(pattern);
            string cut = literal[..^1];
            string[] subjects =
            [
                literal + "7",
                "x" + literal + "7",
                literal,
                cut + "7",
                cut + literal + "7",
                literal + literal + "7",
                literal + "x" + literal + "7",
                new string('z', 4000) + literal + "7",
                new string('z', 4000) + literal + "z",
                new string('z', 4000),
                string.Concat(Enumerable.Repeat(cut + "z", 30)),
            ];
            foreach (string subject in subjects)
            {
                DfaHelper.AssertSame(program, automaton, pattern, subject);
            }
        }
    }

    // The answers themselves, so that the agreement is not two wrong answers: the prefix of a lone surrogate is never searched, and
    // the pair of surrogates of the prefix is found only as a pair.
    [Theory]
    [InlineData("ORD-[0-9]+", "\uD800ORD-7", true)]
    [InlineData("ORD-[0-9]+", "ORD\uD800-7", false)]
    [InlineData("ORD-[0-9]+", "ORD-\uD8007", false)]
    [InlineData("\U0001F600z", "\uD83D😀z", true)]
    [InlineData("\U0001F600z", "😀\uDE00z", false)]
    [InlineData("\U0001F600z", "\uDE00😀z", true)]
    [InlineData("�x", "\uDC00x", true)]
    [InlineData("�x", "😀x", false)]
    [InlineData("(?i)k", "\U0001F600K", true)]
    [InlineData("(?i)k", "\uD83DK", true)]
    public static void Skip_PassesTheSurrogatesAsTheEngineReadsThem(string pattern, string subject, bool expected)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(expected, DfaHelper.Engine(program, subject));
        Assert.Equal(expected, automaton.IsMatch(subject));
    }

    // The text that each way of searching meets: the empty text, each prefix of the literal, the literal itself with a character
    // before and after it, twice, and a partial one before a whole one. The literal is the pattern with its metacharacters left out.
    private static IEnumerable<string> Subjects(string pattern)
    {
        string literal = string.Concat(pattern.Where(c => char.IsLetterOrDigit(c) || c is '-' or ':' or ' '));
        yield return string.Empty;
        for (int length = 1; length <= literal.Length; length++)
        {
            string head = literal[..length];
            yield return head;
            yield return "x" + head;
            yield return head + "x";
            yield return head + head;
            yield return literal[..^(literal.Length > 1 ? 1 : 0)] + literal;
        }

        yield return new string('x', 1000) + literal;
        yield return new string('x', 1000);
    }
}
