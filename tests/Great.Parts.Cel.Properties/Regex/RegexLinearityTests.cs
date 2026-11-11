using System.Globalization;
using CsCheck;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Properties.RegexEngine;

// The engine takes linear time (docs/design/tests.md, "Tests of matches"): on each pattern and subject the steps that it counts are
// at most (n + 1) · m, n the length of the subject in UTF-16 code units and m the size of the program, and at least 1. No time is
// measured: the counter of the engine counts the entries of an instruction into a set. The patterns are those that make a
// backtracking engine take exponential time, patterns with large counts and with classes of tables, and the patterns of the
// generator of the other property on subjects of up to 20 000 code points.
public sealed class RegexLinearityTests
{
    private const string Property = "RegexLinearityTests.StepsBounded";
    private const ulong Seed = 20261110;
    private const int RandomCases = 300;
    private const int LongestRandomSubject = 20_000;
    private const int RandomSubjects = 10;

    private static readonly string[] Exponential =
        ["(a*)*b", "(a|aa)+$", "^(a+)+$", "(x+x+)+y", "(a?){25}a{25}", ".*.*.*=.*"];

    private static readonly string[] Counted = ["a{0,1000}", "[a-z]{1000}", "\\pL{50}", "(?:a{10}){10}b", "(?i)(?:k|s){0,100}x"];

    private static readonly Gen<string> TwoLetters = Gen.OneOfConst('a', 'b').Array[1, 5000].Select(letters => new string(letters));

    [Fact]
    [Trait("Requirement", "R-3")]
    public static void StepsBounded()
    {
        foreach (string pattern in Exponential)
        {
            foreach (string subject in LinearSubjects())
            {
                Check(pattern, subject);
            }
        }

        foreach (string pattern in Exponential)
        {
            string name = $"{Property} {pattern}";
            PropertyLoop.Check(name, TwoLetters, Seed, RandomSubjects, subject => Reason(pattern, subject), subject => $"subject of {subject.Length} code units");
        }

        foreach (string pattern in Counted)
        {
            Check(pattern, new string('a', 20_000));
            Check(pattern, new string('a', 999) + "b" + new string('a', 3000));
        }

        PropertyLoop.Check(Property, RegexCases.CaseWithSubjectOfUpTo(LongestRandomSubject), Seed, RandomCases, Violation, RegexCases.Show);
    }

    // The subjects of a repeated letter, of the lengths that show the growth.
    private static string[] LinearSubjects()
    {
        int[] lengths = [0, 1, 10, 1000, 100_000];
        string[] subjects = new string[lengths.Length * 2];
        for (int i = 0; i < lengths.Length; i++)
        {
            subjects[2 * i] = new string('a', lengths[i]);
            subjects[(2 * i) + 1] = new string('x', lengths[i]);
        }

        return subjects;
    }

    private static void Check(string pattern, string subject)
    {
        if (Reason(pattern, subject) is { } reason)
        {
            Assert.Fail(string.Create(CultureInfo.InvariantCulture, $"{Property}: pattern \"{pattern}\", subject of {subject.Length} code units: {reason}"));
        }
    }

    private static string? Violation(RegexCase input) => Reason(input.Pattern, input.Text);

    // The reason why the steps of a run break the bound, or null.
    private static string? Reason(string pattern, string subject)
    {
        if (!PatternParser.TryParse(pattern, out PatternTree? tree, out RegexError? error))
        {
            return string.Create(CultureInfo.InvariantCulture, $"the library refuses the pattern at {error!.Offset}: {error.Reason}");
        }

        CompiledPattern program;
        using (tree)
        {
            program = ProgramBuilder.Build(tree!);
        }

        long steps = 0;
        Matcher.IsMatch(program, subject, ref steps);
        long bound = ((long)subject.Length + 1) * program.Size;
        if (steps < 1)
        {
            return "the run counted no step";
        }

        return steps <= bound
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{steps} steps > (n + 1) · m = {bound}, m = {program.Size}");
    }
}
