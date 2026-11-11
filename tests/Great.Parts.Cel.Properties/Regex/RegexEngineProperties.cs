using System.Globalization;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Properties.RegexEngine;

// The engine against a naive search (docs/design/tests.md, "Tests of matches"): a pattern is built as a tree, printed as text,
// parsed and built into a program by the library, and run on a short subject; the answer must be the answer of a search that the
// test writes over the tree itself, by the sets of the positions where each node can end. That search shares no code with the
// parser, the program or the engine, so the property checks the three of them together.
public sealed class RegexEngineProperties
{
    private const string Property = "RegexEngineProperties.NfaEqualsNaiveSearch";
    private const ulong Seed = 20261109;
    private const int Cases = 4000;

    [Fact]
    public static void NfaEqualsNaiveSearch()
    {
        PropertyLoop.Check(Property, RegexCases.Case, Seed, Cases, Violation, RegexCases.Show);
    }

    // The ends of lines: only the line feed ends a line, so the end of a line stands before it and not before a carriage return.
    [Fact]
    public static void NfaEqualsNaiveSearchOnLines()
    {
        PropertyLoop.Check("RegexEngineProperties.NfaEqualsNaiveSearchOnLines", RegexCases.LineCase, Seed, Cases, Violation, RegexCases.Show);
    }

    // The reason why the engine breaks the property on a case, or null.
    private static string? Violation(RegexCase input)
    {
        if (!PatternParser.TryParse(input.Pattern, out PatternTree? tree, out RegexError? error))
        {
            return string.Create(CultureInfo.InvariantCulture, $"the library refuses the pattern at {error!.Offset}: {error.Reason}");
        }

        CompiledPattern program;
        using (tree)
        {
            program = ProgramBuilder.Build(tree!);
        }

        long steps = 0;
        bool actual = Matcher.IsMatch(program, input.Text, ref steps);
        bool expected = RegexCases.Matches(input);
        return actual == expected ? null : string.Create(CultureInfo.InvariantCulture, $"the engine says {actual}, the naive search says {expected}");
    }
}
