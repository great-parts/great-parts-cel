using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The engine of sets of states goes to the next place of the literal prefix of its program when no thread is alive, and stops after
// position 0 when every match of the program begins at the start of the text. Whatever it skips, the answer is that of the same
// program run without either, and the steps are not more.
public sealed class MatcherSkipTests
{
    // What a program records at its build: the literal prefix, read through the conditions of zero width, and whether every path
    // from its first instruction meets the beginning of the text before it reads a code point.
    [Theory]
    [InlineData("abc", "abc", false)]
    [InlineData("^abc", "abc", true)]
    [InlineData("(?m)^ab", "ab", false)]
    [InlineData("\\bfoo\\b", "foo", false)]
    [InlineData("\\Bfoo", "foo", false)]
    [InlineData("\\Aab|\\Acd", null, true)]
    [InlineData("^a*", null, true)]
    [InlineData("^", null, true)]
    [InlineData("^$", null, true)]
    [InlineData("(?m)^a", null, false)]
    [InlineData("a|b", null, false)]
    [InlineData("^a|b", null, false)]
    [InlineData("$", null, false)]
    [InlineData("(?i)abc", null, false)]
    public static void Program_RecordsItsPrefixAndItsAnchor(string pattern, string? prefix, bool anchored)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);

        Assert.Equal(prefix, program.Prefix is null ? null : new string(program.Prefix));
        Assert.Equal(anchored, program.Anchored);
    }

    // The patterns that the skips touch: a prefix with a condition of a line or a word before it or after it, an anchor, a prefix that
    // is a pair of surrogates, a folded character, and patterns that have none.
    [Theory]
    [InlineData("abc")]
    [InlineData("^abc")]
    [InlineData("^abc$")]
    [InlineData("(?m)^ab")]
    [InlineData("(?m)^abc$")]
    [InlineData("\\bfoo\\b")]
    [InlineData("\\Bfoo")]
    [InlineData("foo\\b")]
    [InlineData("ab(?:c|d)+e")]
    [InlineData("ab.*cd")]
    [InlineData("^ab|^cd")]
    [InlineData("\\Aab$")]
    [InlineData("ab\\z")]
    [InlineData("(?i)ab")]
    [InlineData("\U0001F600z")]
    [InlineData("\U0001F600+")]
    [InlineData("a*")]
    [InlineData("^")]
    [InlineData("$")]
    public static void Skips_AnswerAsTheEngineWithoutThem_AndNeverTakeMoreSteps(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        CompiledPattern program = ProgramHelper.Compile(pattern);
        CompiledPattern plain = Plain(program);
        var random = new DfaHelper.Sequence(35);
        foreach (string subject in Subjects().Concat(DfaHelper.RandomSubjects(random, pattern, 400)))
        {
            bool expected = ProgramHelper.IsMatch(plain, subject, out long reading);
            bool actual = ProgramHelper.IsMatch(program, subject, out long skipping);

            Assert.True(expected == actual, $"pattern {DfaHelper.Show(pattern)} subject {DfaHelper.Show(subject)}: with the skips {actual}, without them {expected}");
            Assert.True(skipping <= reading, $"pattern {DfaHelper.Show(pattern)} subject {DfaHelper.Show(subject)}: {skipping} steps with the skips, {reading} without them");
        }
    }

    // A text with no match: the prefix is found by a search of the text and the program is entered at no other place, and the
    // anchored program stops after position 0.
    [Fact]
    public static void Prefix_OfALongTextWithoutAMatch_TakesFewSteps()
    {
        string text = new('x', 5000);

        _ = ProgramHelper.IsMatch(ProgramHelper.Compile("abc[0-9]"), text, out long prefixed);
        _ = ProgramHelper.IsMatch(ProgramHelper.Compile("^abc"), text, out long anchored);
        _ = ProgramHelper.IsMatch(ProgramHelper.Compile("[a-c]bc"), text, out long plain);

        Assert.True(prefixed <= 8, $"the prefix takes {prefixed} steps");
        Assert.True(anchored <= 8, $"the anchor takes {anchored} steps");
        Assert.True(plain >= text.Length, $"a pattern without a prefix takes {plain} steps");
    }

    // The conditions at the place that the prefix is found at, which the engine reads from the code point before it: a word boundary
    // after a letter, a digit, an underscore, a line feed and a pair of surrogates, and the beginning of a line.
    [Theory]
    [InlineData("\\bfoo", "xfoo", false)]
    [InlineData("\\bfoo", "_foo", false)]
    [InlineData("\\bfoo", "7foo", false)]
    [InlineData("\\bfoo", "\nfoo", true)]
    [InlineData("\\bfoo", "\U0001F600foo", true)]
    [InlineData("\\bfoo", "\uD800foo", true)]
    [InlineData("\\Bfoo", "xfoo", true)]
    [InlineData("\\Bfoo", "\U0001F600foo", false)]
    [InlineData("(?m)^foo", "x\nfoo", true)]
    [InlineData("(?m)^foo", "xfoo", false)]
    [InlineData("(?m)^foo", "\U0001F600foo", false)]
    [InlineData("^foo", "x\nfoo", false)]
    [InlineData("^foo", "foo", true)]
    public static void Prefix_FoundAfterACodePoint_KeepsTheConditionsOfTheEngine(string pattern, string subject, bool expected)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);

        Assert.Equal(expected, ProgramHelper.IsMatch(program, subject, out _));
        Assert.Equal(expected, ProgramHelper.IsMatch(Plain(program), subject, out _));
    }

    // The same program without the prefix and the anchor: the arrays are the ones of the program, which never change.
    private static CompiledPattern Plain(CompiledPattern program) =>
        new(program.Instructions.ToArray(), program.Ranges.ToArray(), program.Size, program.Start, null, false);

    private static IEnumerable<string> Subjects()
    {
        string[] words = ["", "a", "ab", "abc", "abcd", "abd", "abce", "cd", "foo", "foob", "afoo", "foo_", "_foo", "7foo", "\nfoo", "foo\n"];
        foreach (string word in words)
        {
            yield return word;
            yield return "x" + word;
            yield return word + "x";
            yield return "x" + word + "x";
            yield return word + word;
            yield return "\n" + word;
            yield return word + "\n" + word;
            yield return "\U0001F600" + word;
            yield return "\uD83D" + word;
            yield return word + "\uDE00";
            yield return new string('x', 300) + word;
        }

        yield return "\U0001F600z";
        yield return "\uD83D\U0001F600z";
        yield return "\U0001F600\uDE00z";
        yield return string.Concat(Enumerable.Repeat("ab", 200));
    }
}
