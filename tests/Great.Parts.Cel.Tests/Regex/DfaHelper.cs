using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Regex;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The helpers of the tests of the automaton: the build of one from a pattern, the answer of the engine of sets of states to compare
// with, the subjects that make a match likely and the ones that stress the reading of code points, and a call of matches through the
// call site, as a literal pattern and as a pattern from data.
internal static class DfaHelper
{
    private static readonly LineTable Lines = new("x");

    // The pieces of the subjects that stress the reading of code points: separators, a line feed, the letters whose case folds into
    // each other (the Kelvin sign), a pair of surrogates and each half of it alone, digits.
    private static readonly string[] Pieces = [" ", "@", ".", "-", "\n", "é", "İ", "k", "K", "K", "\U0001F600", "\uD800", "\uDC00", "�", "_", "0", "9"];

    // The automaton of a pattern; the test fails when the pattern has none.
    internal static Dfa Build(string pattern)
    {
        Dfa? automaton = DfaBuilder.TryBuild(ProgramHelper.Compile(pattern));

        Assert.NotNull(automaton);
        return automaton;
    }

    // The answer of the engine of sets of states.
    internal static bool Engine(CompiledPattern program, string subject) => ProgramHelper.IsMatch(program, subject, out _);

    // Fails with the pattern and the subject when the automaton and the engine answer differently; the answer is returned.
    internal static bool AssertSame(CompiledPattern program, Dfa automaton, string pattern, string subject)
    {
        bool expected = Engine(program, subject);
        bool actual = automaton.IsMatch(subject);

        Assert.True(expected == actual, $"pattern {Show(pattern)} subject {Show(subject)}: sets of states {expected}, automaton {actual}");
        return actual;
    }

    // Strings of the characters of the pattern, which make matches likely, and the pieces that stress the reading of code points.
    internal static IEnumerable<string> RandomSubjects(Sequence random, string pattern, int count)
    {
        var alphabet = new List<string>(Pieces);
        foreach (char c in pattern)
        {
            if (c is >= ' ' and < '\u007F' && !alphabet.Contains(c.ToString()))
            {
                alphabet.Add(c.ToString());
            }
        }

        for (int i = 0; i < count; i++)
        {
            int length = random.Next(11);
            var text = new StringBuilder();
            for (int k = 0; k < length; k++)
            {
                text.Append(alphabet[random.Next(alphabet.Count)]);
            }

            yield return text.ToString();
        }
    }

    // The answer of matches for a subject and a pattern, through the call site: the preparation of a literal and the program it
    // gives, or the cache of an environment for a pattern from data.
    internal static bool Call(string subject, string pattern, object? prepared, PatternCache? cache)
    {
        CelValue value = Matches.Implementation(
            [CelValue.FromString(subject), CelValue.FromString(pattern)],
            new CallSite("matches", 0, Lines, null, prepared: prepared, cache: cache));

        Assert.True(value.TryGetBoolean(out bool result), "matches of " + Show(pattern) + " on " + Show(subject) + " is no bool");
        return result;
    }

    internal static string Show(string text)
    {
        var shown = new StringBuilder("\"");
        foreach (char c in text)
        {
            shown.Append(c switch
            {
                '\n' => "\\n",
                '\\' => "\\\\",
                '"' => "\\\"",
                < ' ' or >= (char)0x7F => string.Create(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}"),
                _ => c.ToString(),
            });
        }

        return shown.Append('"').ToString();
    }

    // A deterministic sequence of numbers for the subjects: a linear congruential generator.
    internal sealed class Sequence(uint seed)
    {
        private uint state = seed;

        internal int Next(int bound)
        {
            state = unchecked((state * 1664525) + 1013904223);
            return (int)((state >> 8) % (uint)bound);
        }
    }
}
