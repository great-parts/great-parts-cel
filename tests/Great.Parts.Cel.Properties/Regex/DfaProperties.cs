using System.Globalization;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Properties.RegexEngine;

// The automaton against the engine of sets of states (docs/design/tests.md, "Tests of matches"): a pattern is built as a tree,
// printed as text, parsed and built into a program by the library, and the automaton of the program, when it has one, answers on a
// short subject as the engine does. The patterns are those of the property of the engine — literals, the dot, classes of letters, \w
// and \pL, the anchors, groups, alternatives, repeats, the flags i, s and m — so the automaton is checked on the anchors of the text
// that it handles and on the conditions of a line and of a word, which it refuses.
public sealed class DfaProperties
{
    private const ulong Seed = 20261119;
    private const int Cases = 4000;

    [Fact]
    public static void DfaEqualsNfa()
    {
        int[] built = [0];

        PropertyLoop.Check("DfaProperties.DfaEqualsNfa", RegexCases.Case, Seed, Cases, input => Violation(input, built), RegexCases.Show);

        Assert.True(built[0] > Cases / 10, $"only {built[0]} of {Cases} patterns have an automaton");
    }

    // The ends of lines: only the line feed ends a line, and under (?m) the conditions of a line have no automaton, while the
    // anchors of the text of the same patterns have.
    [Fact]
    public static void DfaEqualsNfaOnLines()
    {
        int[] built = [0];

        PropertyLoop.Check("DfaProperties.DfaEqualsNfaOnLines", RegexCases.LineCase, Seed, Cases, input => Violation(input, built), RegexCases.Show);

        Assert.True(built[0] > Cases / 10, $"only {built[0]} of {Cases} patterns have an automaton");
    }

    // The engine that goes to the next place of the literal prefix of its program, and stops after the start of the text when the
    // program is anchored, against the same engine that reads every code point: the answer is the same and the steps are not more.
    // The patterns are those of the automaton, among them the conditions of a line and of a word, which only the engine runs, as a
    // pattern from data is run.
    [Fact]
    public static void EngineWithSkipsEqualsEngineWithout()
    {
        PropertyLoop.Check("DfaProperties.EngineWithSkipsEqualsEngineWithout", RegexCases.Case, Seed, Cases, SkipViolation, RegexCases.Show);
        PropertyLoop.Check("DfaProperties.EngineWithSkipsEqualsEngineWithoutOnLines", RegexCases.LineCase, Seed, Cases, SkipViolation, RegexCases.Show);
    }

    // The program of the pattern of a case, or the reason why the library refuses the pattern.
    private static string? TryBuild(RegexCase input, out CompiledPattern program)
    {
        if (!PatternParser.TryParse(input.Pattern, out PatternTree? tree, out RegexError? error))
        {
            program = null!;
            return string.Create(CultureInfo.InvariantCulture, $"the library refuses the pattern at {error!.Offset}: {error.Reason}");
        }

        using (tree)
        {
            program = ProgramBuilder.Build(tree!);
        }

        return null;
    }

    // The same program without the prefix and the anchor: the arrays are the ones of the program, which never change.
    private static CompiledPattern Plain(CompiledPattern program) =>
        new(program.Instructions.ToArray(), program.Ranges.ToArray(), program.Size, program.Start, null, false);

    private static string? SkipViolation(RegexCase input)
    {
        if (TryBuild(input, out CompiledPattern program) is { } refusal)
        {
            return refusal;
        }

        CompiledPattern plain = Plain(program);
        long skipping = 0;
        long reading = 0;
        bool expected = Matcher.IsMatch(plain, input.Text, ref reading);
        bool actual = Matcher.IsMatch(program, input.Text, ref skipping);
        if (actual != expected)
        {
            return string.Create(CultureInfo.InvariantCulture, $"the engine with the skips says {actual}, without them {expected}");
        }

        return skipping <= reading
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"the engine with the skips made {skipping} steps, without them {reading}");
    }

    // The reason why the automaton breaks the property on a case, or null; a program without an automaton is run by the engine alone.
    private static string? Violation(RegexCase input, int[] built)
    {
        if (TryBuild(input, out CompiledPattern program) is { } refusal)
        {
            return refusal;
        }

        Dfa? automaton = DfaBuilder.TryBuild(program);
        if (automaton is null)
        {
            return null;
        }

        built[0]++;
        long steps = 0;
        bool expected = Matcher.IsMatch(program, input.Text, ref steps);
        bool actual = automaton.IsMatch(input.Text);
        return actual == expected ? null : string.Create(CultureInfo.InvariantCulture, $"the automaton says {actual}, the engine says {expected}");
    }
}
