using System;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Library;
using Xunit;

namespace Great.Parts.Cel.Properties.Examples.Library;

// The search for a substring of contains is linear in the worst case: on a text of n and a pattern of m
// UTF-16 code units it takes at most 4 · (n + m) steps, a step being one comparison of two code units, and
// finds the first occurrence. The inputs are words with many long partial matches — the Fibonacci word and
// the Thue–Morse word over 'a' and 'b' — with patterns cut from them, found and not found, of about a
// million code units of text. The internal SubstringSearch.IndexOf counts the steps; contains of the
// public API gives the same answer.
[Trait("Requirement", "R-3")]
public sealed class SubstringSearchStepsTests
{
    private static readonly string Fibonacci = FibonacciWord(1_000_000);
    private static readonly string ThueMorse = ThueMorseWord(1 << 20);

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("text")
        .AddVariable<string>("pattern")
        .Build()
        .GetEnvironmentOrThrow();

    // word: 0 is the Fibonacci word, 1 the Thue–Morse word. The pattern is the part of the word at start of
    // the given length, with its last code unit replaced by 'c' when lastUnit is 'c'.
    [Theory]
    [InlineData(0, 0, 10_946, ' ')]
    [InlineData(0, 0, 10_946, 'c')]
    [InlineData(0, 377, 6_765, ' ')]
    [InlineData(0, 500_000, 1_000, 'c')]
    [InlineData(1, 0, 4_096, 'c')]
    [InlineData(1, 300_001, 777, ' ')]
    [InlineData(1, 700_000, 65_536, ' ')]
    public void SearchInAWordWithLongPartialMatches_TakesLinearSteps(int word, int start, int length, char lastUnit)
    {
        string text = word == 0 ? Fibonacci : ThueMorse;
        string pattern = Pattern(text, start, length, lastUnit);
        long steps = 0;

        int index = SubstringSearch.IndexOf(text, pattern, ref steps);

        Assert.Equal(text.IndexOf(pattern, StringComparison.Ordinal), index);
        Assert.InRange(steps, 0, 4L * (text.Length + pattern.Length));
        Assert.Equal(index >= 0, Contains(text, pattern));
    }

    private bool Contains(string text, string pattern) =>
        environment.Compile<bool>("text.contains(pattern)").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("text", text).Add("pattern", pattern), cancellationToken: CancellationToken.None)
            .GetValueOrThrow();

    private static string Pattern(string text, int start, int length, char lastUnit)
    {
        string part = text.Substring(start, length);
        return lastUnit == ' ' ? part : part[..^1] + lastUnit;
    }

    // The prefix of the given length of the Fibonacci word: S(1) = "b", S(2) = "a", S(k) = S(k − 1) S(k − 2).
    private static string FibonacciWord(int length)
    {
        string previous = "b";
        string current = "a";
        while (current.Length < length)
        {
            (previous, current) = (current, current + previous);
        }

        return current[..length];
    }

    // The prefix of the given length of the Thue–Morse word: the unit i is 'b' when i has an odd number of
    // ones in binary.
    private static string ThueMorseWord(int length)
    {
        var word = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            word.Append(int.PopCount(i) % 2 == 0 ? 'a' : 'b');
        }

        return word.ToString();
    }
}
