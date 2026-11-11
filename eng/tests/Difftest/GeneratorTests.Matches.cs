using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cel.Expr;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Tests.Support;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The generator for matches and matches_string (docs/design/eng-difftest.md, "Profile: matches and matches_string"): the forms of the
// call, the patterns by the grammar of RE2, the faults of the refused ones, the subjects. The patterns of the library are checked
// by the library itself, through its public API; a call of matches type checks and runs only when the library has the function.
public sealed class MatchesGeneratorTests
{
    // Patterns made alone, from the generator state of the cases of a seed; a case holds one in about eighty.
    private const int Patterns = 100_000;

    // The cases per seed of the tests of the forms of a call of matches: about one case in eighty has one.
    private const int MatchesCases = 4000;

    [Fact]
    public void Matches_OccursInBothFormsWithEveryFormOfThePatternAndOfTheSubject()
    {
        List<Call> calls = [.. MatchesCalls()];

        Assert.True(calls.Count >= 50, calls.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(calls, call => Assert.Equal(2, call.Args.Count));
        Assert.Contains(calls, call => !call.Receiver);
        Assert.Contains(calls, call => call.Receiver);
        Assert.All(calls, call => Assert.NotNull(call.Pattern));

        // The pattern: a literal, and each form that is not one.
        Assert.Contains(calls, call => call.Args[1] is StringLiteral);
        Assert.Contains(calls, call => call.Args[1] is Call { Function: ProfileTables.Dyn, Args: [StringLiteral] });
        Assert.Contains(calls, call => call.Args[1] is Call { Function: ProfileTables.Add, Args: [StringLiteral, StringLiteral] });
        Assert.Contains(calls, call => call.Args[1] is Call { Function: ProfileTables.StringConversion, Args: [StringLiteral] });
        Assert.Contains(calls, call => call.Args[1] is Call { Function: ProfileTables.Conditional, Args: [BoolLiteral { Value: true }, StringLiteral, StringLiteral] });
        Assert.Contains(calls, call => call.Args[1] is Ident { Name: "s1" });
        int literals = calls.Count(call => call.Args[1] is StringLiteral);
        Assert.InRange(literals * 100 / calls.Count, 40, 80);

        // The subject: a literal, a variable and an expression of the type string.
        Assert.Contains(calls, call => call.Args[0] is StringLiteral);
        Assert.Contains(calls, call => call.Args[0] is Ident { Name: "s1" or "gp.test.r" });
        Assert.Contains(calls, call => call.Args[0] is Call);
    }

    [Fact]
    public void Matches_StandsAsThePredicateOfAMacroWithTheIterationVariableAsTheSubject()
    {
        List<Comprehension> macros = [.. GeneratorTests.Sample(MatchesCases).SelectMany(generated => GeneratorTests.Nodes(generated.Expression)).OfType<Comprehension>()];

        foreach (string macro in new[] { ProfileTables.All, ProfileTables.Exists, ProfileTables.ExistsOne, ProfileTables.Filter })
        {
            Assert.Contains(macros, comprehension => comprehension.Macro == macro && comprehension.Arguments.Any(IsMatchesOnVariable));
        }

        Assert.Contains(macros, comprehension => comprehension.Macro == ProfileTables.MapMacro && comprehension.Arguments.Any(IsMatchesOnVariable));
        Assert.All(
            macros.SelectMany(comprehension => comprehension.Arguments.Select(argument => (comprehension, argument))).Where(pair => IsMatchesOnVariable(pair.argument)),
            pair => Assert.Equal(pair.comprehension.Variable, ((Ident)((Call)Unwrapped(pair.argument)).Args[0]).Name));
        Assert.Contains(macros, comprehension => comprehension.Range is ListLiteral { Elements.Count: > 0 } list && list.Elements.All(element => element is StringLiteral));
    }

    [Fact]
    public void S1_IsBoundToThePatternOfACallThatTakesIt()
    {
        List<(GeneratedCase Case, Call Call)> takers = [.. GeneratorTests.Sample(MatchesCases).SelectMany(generated => GeneratorTests.Nodes(generated.Expression).OfType<Call>()
            .Where(call => call.Function == ProfileTables.Matches && call.Args[1] is Ident { Name: "s1" }).Select(call => (generated, call)))];

        Assert.True(takers.Count >= 5, takers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach ((GeneratedCase generated, Call call) in takers.Where(taker => taker.Case.Bindings.ContainsKey("s1")))
        {
            Assert.Equal(call.Pattern!.Text, generated.Bindings["s1"].StringValue);
        }

        Assert.Contains(takers, taker => !taker.Case.Bindings.ContainsKey("s1"));
        Assert.Contains(MatchesCalls(), call => call.Args[0] is Ident { Name: "s1" });
    }

    // The library is the checker these tests have without Go: a call of matches takes and gives the types of the profile, and a
    // literal pattern that is refused is the only error of its expression, with the code InvalidRegex at the literal.
    [Fact]
    public void Matches_TypeChecksAndARefusedLiteralPatternIsItsOnlyError()
    {
        CelEnvironment environment = GeneratorTests.Environment();
        int refused = 0;

        foreach (GeneratedCase generated in GeneratorTests.Sample(MatchesCases).Where(generated => GeneratorTests.Nodes(generated.Expression).Any(node => node is Call { Function: ProfileTables.Matches })))
        {
            List<Call> literals = [.. GeneratorTests.Nodes(generated.Expression).OfType<Call>().Where(call =>
                call is { Function: ProfileTables.Matches, Pattern.Fault: not PatternFault.None } && call.Args[1] is StringLiteral)];
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(generated.Text);

            if (literals.Count == 0)
            {
                Assert.True(compiled.IsSuccess, generated.Text + ": " + string.Join("; ", compiled.Errors.Select(error => error.Message)));
                continue;
            }

            refused += literals.Count;
            Assert.Equal(literals.Count, compiled.Errors.Length);
            foreach (CelError error in compiled.Errors)
            {
                Assert.Equal(CelErrorCode.InvalidRegex, error.Code);
                Assert.True(
                    literals.Any(call => InsideLiteral(generated.Text, ExprPrinter.Print(call.Args[1]), error.Location.Offset)),
                    generated.Text + ": the error is at " + error.Location.Offset);
            }
        }

        Assert.True(refused > 0);
    }

    // Both sides read the text of the expression: a pattern printed in a literal, in any of its forms, reaches them as the same
    // string. The parser of the library reads the literals back.
    [Fact]
    public void LiteralsOfCallsOfMatches_ReadBackToTheSameStringByTheParserOfTheLibrary()
    {
        LibrarySide side = BuildSide();
        Dictionary<string, Value> none = [];
        int checkedLiterals = 0;

        foreach (StringLiteral literal in MatchesCalls().SelectMany(call => GeneratorTests.Nodes(call).OfType<StringLiteral>()))
        {
            SideOutcome outcome = side.Evaluate(ExprPrinter.Print(literal), none);

            Assert.False(outcome.IsError, ExprPrinter.Print(literal) + ": " + outcome.Message);
            Assert.Equal(literal.Value, outcome.Result!.StringValue);
            checkedLiterals++;
        }

        Assert.True(checkedLiterals >= 100, checkedLiterals.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(MatchesCalls().SelectMany(call => GeneratorTests.Nodes(call).OfType<StringLiteral>()), literal => literal.Form.Raw is not null && literal.Value.Contains('\\', StringComparison.Ordinal));
        Assert.Contains(MatchesCalls().SelectMany(call => GeneratorTests.Nodes(call).OfType<StringLiteral>()), literal => literal.Form.Raw is null && literal.Value.Contains('\\', StringComparison.Ordinal));
    }

    [Fact]
    public void Patterns_HoldEveryConstructOfTheGrammar()
    {
        HashSet<string> seen = [.. PatternSample().SelectMany(pattern => pattern.Constructs)];

        Assert.DoesNotContain(PatternGenerator.Constructs, construct => !seen.Contains(construct));
        Assert.DoesNotContain(seen, construct => !PatternGenerator.Constructs.Contains(construct));
        Assert.Equal(PatternGenerator.Constructs.Count, PatternGenerator.Constructs.Distinct().Count());
    }

    [Fact]
    public void Patterns_KeepTheBoundsOfDepthAtomsCountsAndSize()
    {
        List<GeneratedPattern> patterns = [.. PatternSample()];
        List<GeneratedPattern> valid = [.. patterns.Where(pattern => pattern.Fault == PatternFault.None)];

        Assert.All(patterns, pattern => Assert.InRange(pattern.Depth, 0, PatternGenerator.MaxDepth));
        Assert.All(patterns, pattern => Assert.InRange(pattern.Atoms, 0, PatternGenerator.MaxAtoms));
        Assert.All(patterns, pattern => Assert.InRange(pattern.SizeBound, 1, PatternGenerator.SizeLimit));
        Assert.All(patterns, pattern => Assert.InRange(pattern.CountProduct, 1, PatternGenerator.CountLimit));
        Assert.Equal(PatternGenerator.MaxDepth, patterns.Max(pattern => pattern.Depth));
        Assert.Equal(PatternGenerator.MaxAtoms, patterns.Max(pattern => pattern.Atoms));

        // A count of the text is at most 1 000 outside a refusal; the counts of the other repetitions are 5 at most, and a larger
        // count is on one atom that no other count encloses, so at most one pattern in a few dozen has one.
        Regex count = new(@"\{(\d+)(?:,(\d*))?\}", RegexOptions.None, TimeSpan.FromSeconds(5));
        foreach (GeneratedPattern pattern in valid)
        {
            int[] numbers = [.. count.Matches(pattern.Text).SelectMany(match => match.Groups.Cast<Group>().Skip(1).Where(group => group.Success && group.Length > 0))
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture))];
            Assert.All(numbers, number => Assert.InRange(number, 0, PatternGenerator.CountLimit));
            Assert.Equal(numbers.Any(number => number > PatternGenerator.MaxSmallCount), pattern.Constructs.Contains("big count"));
        }

        int big = valid.Count(pattern => pattern.Constructs.Contains("big count"));
        Assert.InRange(big, valid.Count / 100, valid.Count / 15);
        Assert.True(valid.Max(pattern => pattern.CountProduct) > 625);
        Assert.All(patterns, pattern => Assert.Null(Record.Exception(() => new UTF8Encoding(false, true).GetBytes(pattern.Text))));
        Assert.All(patterns, pattern => Assert.NotEmpty(pattern.Text));
    }

    [Fact]
    public void Patterns_AreRefusedOneInTwelveEachByOneKindOfFault()
    {
        List<GeneratedPattern> patterns = [.. PatternSample()];
        List<GeneratedPattern> refused = [.. patterns.Where(pattern => pattern.Fault != PatternFault.None)];

        Assert.InRange(refused.Count, patterns.Count * 7 / 100, patterns.Count * 10 / 100);
        Assert.Equal(PatternGenerator.Faults.Order(), refused.Select(pattern => pattern.Fault).Distinct().Order());
        Assert.Equal(21, PatternGenerator.Faults.Count);
        Assert.All(refused, pattern => Assert.Contains(PatternGenerator.FaultText(pattern.Fault), pattern.Text, StringComparison.Ordinal));
        foreach (PatternFault fault in PatternGenerator.Faults)
        {
            Assert.InRange(refused.Count(pattern => pattern.Fault == fault), refused.Count / 40, refused.Count / 10);
        }

        // A class that is not closed ends the pattern, since a ] later in the text would close it.
        Assert.All(refused.Where(pattern => pattern.Fault == PatternFault.UnclosedClass), pattern => Assert.EndsWith("[ab", pattern.Text, StringComparison.Ordinal));
    }

    // Every pattern either parses as the library parses it or is refused by the fault the generator put in it: the pattern is the
    // value of the variable, so the refusal is an error of evaluation.
    [Fact]
    public void Patterns_ParseAsTheLibraryParsesThemOrAreRefusedByTheirFault()
    {
        LibrarySide side = BuildSide();

        foreach (GeneratedPattern pattern in PatternSample(5000))
        {
            SideOutcome outcome = side.Evaluate("matches('', s1)", new Dictionary<string, Value> { ["s1"] = new Value { StringValue = pattern.Text } });

            Assert.Equal(
                pattern.Fault == PatternFault.None ? OutcomeClass.Value : OutcomeClass.EvaluationError,
                outcome.Class);
        }
    }

    [Fact]
    public void Subjects_HaveAtMostEightCodePointsOfTheAlphabetAndHoldTheCharactersOfPatterns()
    {
        List<string> subjects = [.. Enumerable.Range(0, 20000).Select(n => PatternGenerator.Subject(CaseGenerator.CaseState(7, (ulong)n)))];
        HashSet<string> units = [.. subjects.SelectMany(subject => subject.EnumerateRunes().Select(rune => rune.ToString()))];

        Assert.All(subjects, subject => Assert.InRange(subject.EnumerateRunes().Count(), 0, ProfileTables.MaxCodePoints));
        Assert.Equal(ProfileTables.MaxCodePoints, subjects.Max(subject => subject.EnumerateRunes().Count()));
        Assert.Equal(0, subjects.Min(subject => subject.EnumerateRunes().Count()));
        foreach (string unit in new[] { "a", "b", "c", "A", "k", "K", "7", "_", " ", "é", "ſ", "K", "İ", "ß", "ẞ", "\U0001F600", "\n", "(", "\\", "^" })
        {
            Assert.Contains(unit, units);
        }

        Assert.All(units, unit => Assert.Null(Record.Exception(() => new UTF8Encoding(false, true).GetBytes(unit))));
    }

    [Fact]
    public void PatternGenerator_DependsOnTheStateAlone()
    {
        GeneratedPattern first = PatternGenerator.Generate(CaseGenerator.CaseState(20261104, 17));
        _ = PatternGenerator.Generate(CaseGenerator.CaseState(20261104, 3));
        GeneratedPattern again = PatternGenerator.Generate(CaseGenerator.CaseState(20261104, 17));

        Assert.Equal(first.Text, again.Text);
        Assert.Equal(first.Fault, again.Fault);
        Assert.True(Enumerable.Range(0, 50).Select(n => PatternGenerator.Generate(CaseGenerator.CaseState(1, (ulong)n)).Text).Distinct().Count() > 40);
    }

    // The nodes of a case but those of the patterns of its calls of matches, which a pattern generator makes.
    internal static IEnumerable<Expr> NodesWithoutPatterns(Expr expr)
    {
        HashSet<Expr> patterns = new(ReferenceEqualityComparer.Instance);
        foreach (Call call in GeneratorTests.Nodes(expr).OfType<Call>().Where(call => call.Function == ProfileTables.Matches))
        {
            patterns.UnionWith(GeneratorTests.Nodes(call.Args[1]));
        }

        return GeneratorTests.Nodes(expr).Where(node => !patterns.Contains(node));
    }

    internal static bool TakesS1AsPattern(GeneratedCase generated) =>
        GeneratorTests.Nodes(generated.Expression).Any(node => node is Call { Function: ProfileTables.Matches, Args: [_, Ident { Name: "s1" }] });

    internal static bool HasRefusedLiteralPattern(GeneratedCase generated) => GeneratorTests.Nodes(generated.Expression).Any(node =>
        node is Call { Function: ProfileTables.Matches, Pattern.Fault: not PatternFault.None } call && call.Args[1] is StringLiteral);

    internal static bool IsPatternOf(Call call, Expr argument) => call.Function == ProfileTables.Matches && ReferenceEquals(call.Args[1], argument);

    // The library side over the manifest and the zone file of the repository, the files that the oracle reads.
    private static LibrarySide BuildSide()
    {
        string root = RepositoryRoot.Find();
        Assert.True(
            LibrarySide.TryBuild(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, ProfileTables.ManifestPath)), System.IO.Path.Combine(root, ProfileTables.ZoneInfoPath), out LibrarySide? side, out string problem),
            problem);
        return side!;
    }

    // The calls of matches of the cases of the seeds.
    private static IEnumerable<Call> MatchesCalls() =>
        GeneratorTests.Sample(MatchesCases).SelectMany(generated => GeneratorTests.Nodes(generated.Expression)).OfType<Call>().Where(call => call.Function == ProfileTables.Matches);

    // Patterns of the generator states of the cases of the seeds.
    private static IEnumerable<GeneratedPattern> PatternSample(int count = Patterns)
    {
        for (int n = 0; n < count; n++)
        {
            yield return PatternGenerator.Generate(CaseGenerator.CaseState(GeneratorTests.Seeds[n % GeneratorTests.Seeds.Length], (ulong)(n / GeneratorTests.Seeds.Length)));
        }
    }

    // A predicate that is a call of matches whose subject is an iteration variable, in parentheses or not.
    private static bool IsMatchesOnVariable(Expr predicate) =>
        Unwrapped(predicate) is Call { Function: ProfileTables.Matches, Args: [Ident variable, _] } && ProfileTables.IterationVariables.Contains(variable.Name);

    private static Expr Unwrapped(Expr expr) => expr with { Parenthesized = false };

    // Whether an offset of the error is in an occurrence of the literal in the text, counted in UTF-16 units or in code points.
    private static bool InsideLiteral(string text, string literal, int offset)
    {
        for (int index = text.IndexOf(literal, StringComparison.Ordinal); index >= 0; index = text.IndexOf(literal, index + 1, StringComparison.Ordinal))
        {
            if (offset >= index && offset < index + literal.Length)
            {
                return true;
            }

            int start = text[..index].EnumerateRunes().Count();
            if (offset >= start && offset < start + literal.EnumerateRunes().Count())
            {
                return true;
            }
        }

        return false;
    }
}
