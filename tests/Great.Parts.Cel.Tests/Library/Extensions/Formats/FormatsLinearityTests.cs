using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Formats;

// The time of every function of the set formats is linear in the length of the string: the steps that its scanner counts — one for each
// unit that it reads — are at most c · (n + 1) for a string of n units, on the strings that make a scanner read a unit again and again
// if it backtracks (64 KiB of one character such as ':', '[' or '%', long runs of labels, groups and segments, text without the terminator
// that the scanner looks for) and on random strings of a fixed seed. The scanners return a bool for every string and never throw.
[Trait("Requirement", "R-12")]
[Trait("Requirement", "F-28")]
public sealed class FormatsLinearityTests
{
    // A unit is read a few times by the scanners that cut a string first (the end of the authority, the last bracket) and read each part
    // afterwards; 10 is far above any of them and far below a scan that goes back.
    private const int StepsPerUnit = 10;

    private const int Kibibytes64 = 65536;

    private const string Alphabet = ":.[]%@/?#-_~!$&'()*+,;=0123456789abcdefABCDEFgvVx \\";

    private static readonly int[] Lengths = [0, 1, 2, 3, 64, 1000, Kibibytes64];

    private static readonly string[] RepeatedUnits = ["a.", "a:", "a@", "[::", "%41", "a/", "1.", "a-"];

    private static readonly string[] WorstUnits =
    [
        ":", ".", "[", "]", "%", "@", "/", "?", "#", "-", "a", "1", "0", "f", "v", " ", "\\", "a.", "a:", "1:", "a@", "[::", "%4", "%41", "a/", ".a", "::", "1.", "a-",
    ];

    [Theory]
    [InlineData("isEmail")]
    [InlineData("isHostname")]
    [InlineData("isHostAndPort(false)")]
    [InlineData("isHostAndPort(true)")]
    [InlineData("isIpPrefix()")]
    [InlineData("isIpPrefix(4, true)")]
    [InlineData("isIpPrefix(6, true)")]
    [InlineData("isUri")]
    [InlineData("isUriRef")]
    public static void WorstStrings_TakeStepsInProportionToTheirLength(string function)
    {
        foreach (string text in WorstStrings())
        {
            (_, long steps) = FormatScanners.Run(function, text);

            Assert.True(steps <= StepsPerUnit * (text.Length + 1L), Describe(function, text, steps));
        }
    }

    [Theory]
    [InlineData("isEmail", 1)]
    [InlineData("isHostname", 2)]
    [InlineData("isHostAndPort(false)", 3)]
    [InlineData("isHostAndPort(true)", 4)]
    [InlineData("isIpPrefix()", 5)]
    [InlineData("isIpPrefix(4, true)", 6)]
    [InlineData("isIpPrefix(6, true)", 7)]
    [InlineData("isUri", 8)]
    [InlineData("isUriRef", 9)]
    public static void RandomStrings_TakeStepsInProportionToTheirLength(string function, int seed)
    {
        var random = new Sequence((ulong)seed);

        for (int round = 0; round < 600; round++)
        {
            string text = RandomText(random, round < 300 ? 40 : 3000);

            (_, long steps) = FormatScanners.Run(function, text);

            Assert.True(steps <= StepsPerUnit * (text.Length + 1L), Describe(function, text, steps));
        }
    }

    [Theory]
    [InlineData("isEmail")]
    [InlineData("isHostname")]
    [InlineData("isHostAndPort(false)")]
    [InlineData("isIpPrefix()")]
    [InlineData("isUri")]
    [InlineData("isUriRef")]
    public static void TheStepsOfAStringOfTwiceTheLength_AreAtMostAboutTwiceAsMany(string function)
    {
        foreach (string unit in RepeatedUnits)
        {
            long small = FormatScanners.Run(function, Repeat(unit, 4096)).Steps;
            long large = FormatScanners.Run(function, Repeat(unit, 8192)).Steps;

            Assert.True(large <= (2 * small) + 64, string.Create(CultureInfo.InvariantCulture, $"{function} on '{unit}': {small} steps for 4096 repeats, {large} for 8192"));
        }
    }

    private static IEnumerable<string> WorstStrings()
    {
        foreach (string unit in WorstUnits)
        {
            foreach (int length in Lengths)
            {
                yield return Repeat(unit, length / unit.Length);
            }
        }

        string big = Repeat("a", Kibibytes64);
        yield return big + "@x";
        yield return "x@" + big;
        yield return "http://" + big;
        yield return "http://" + big + ":80";
        yield return "http://[" + big + "]";
        yield return "http://[::1%25" + big + "]";
        yield return "http://u@" + big + "@h";
        yield return "http://h/" + big + "?" + big + "#" + big;
        yield return "a:" + Repeat("%41", Kibibytes64 / 3);
        yield return big + ":" + big;
        yield return "[" + big;
        yield return big + "]:80";
        yield return big + "/8";
        yield return Repeat("1:", Kibibytes64 / 2) + "/8";
        yield return Repeat("1.", Kibibytes64 / 2) + "/8";
        yield return "::" + Repeat("1:", Kibibytes64 / 2);
        yield return Repeat("a.", Kibibytes64 / 2);
        yield return Repeat("a--", Kibibytes64 / 3);
    }

    private static string Repeat(string unit, int count) => count <= 0 ? string.Empty : string.Concat(Enumerable.Repeat(unit, count));

    private static string RandomText(Sequence random, int maxLength)
    {
        int length = random.Next(0, maxLength + 1);
        char[] units = new char[length];
        for (int i = 0; i < units.Length; i++)
        {
            units[i] = Alphabet[random.Next(0, Alphabet.Length)];
        }

        return new string(units);
    }

    private static string Describe(string function, string text, long steps)
    {
        string shown = text.Length <= 80 ? text : text[..80] + "...";
        return string.Create(CultureInfo.InvariantCulture, $"{function}: {steps} steps for {text.Length} units, more than {StepsPerUnit} per unit; text {shown}");
    }

    // SplitMix64: a fixed sequence for a seed, the same in every run on every platform.
    private sealed class Sequence(ulong seed)
    {
        private ulong _state = seed;

        internal int Next(int minInclusive, int maxExclusive) => minInclusive + (int)(NextBits() % (ulong)(maxExclusive - minInclusive));

        private ulong NextBits()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
