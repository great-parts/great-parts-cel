using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Regex;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The automaton of a program against the engine of sets of states: the two answer the same on every pattern of the corpus of
// RE2 and of the other lists here, on the subjects of the corpus and on random strings made from the characters of the pattern and a
// few that stress the reading of code points; on the patterns that are literals, anchors and prefixes; on a pattern past each limit,
// which the automaton refuses and the engine runs alone; and through the call site, a literal and the same pattern from data.
public sealed class DfaEquivalenceTests
{
    private static readonly string[] CelSpecPatterns = ["ubb", "foo|bar", string.Empty, "bc", "gr(a|e)y", "ba(na)*", "a+ñ+a+", "(a|\U0001F600){2}"];

    private static readonly string[] OtherPatterns =
    [
        "^[^@]+@[^@]+[.][a-z]+$",
        "^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$",
        "^[0-9]{4}-[0-9]{2}-[0-9]{2}$",
        "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        "^\\+?[0-9]{10,15}$",
        "(?i)^[a-z0-9_]{3,16}$",
        "ORD-[0-9]+",
        "(?i)ord-[0-9]+",
        "error: .*",
        "^\\d{1,3}(\\.\\d{1,3}){3}$",
        "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$",
        "^(GET|POST|PUT|DELETE) /[a-z/]*$",
        "[a-z]+@[a-z]+[.]com",
        "^\\w+$",
        "^[a-z]{1,64}$",
        "^(?:[a-z]+-){3}[a-z]+$",
        "(a|b)*a(a|b){7}",
        "\\p{L}+",
        "^\\p{Greek}+$",
        "^[\\p{L} -]+$",
        "\\z",
        "$^",
        "^$",
        "a$|b",
        "^a|b$",
        "(^|x)a($|y)",
        "�a",
        "(?i)k",
        "ab(?i)cd",
        "\U0001F600z",
        "x*abc",
        "(?:ab)+c",
    ];

    // The patterns that the automaton refuses for a condition of zero width other than the beginning and the end of the text.
    private static readonly string[] ConditionPatterns = ["\\bfoo\\b", "(?m)^a", "a\\B", "(?m)a$", "(?m)$", "\\bfoo|bar"];

    [Fact]
    public static void Automaton_AnswersAsTheEngineOfSetsOfStates()
    {
        var failures = new List<string>();
        int built = 0;
        int refused = 0;
        var random = new DfaHelper.Sequence(20261119);
        foreach ((string pattern, List<string> subjects) in AllPatterns())
        {
            if (!PatternParser.TryParse(pattern, out PatternTree? tree, out _))
            {
                continue;
            }

            CompiledPattern program;
            try
            {
                program = ProgramBuilder.Build(tree!);
            }
            finally
            {
                tree!.Dispose();
            }

            Dfa? automaton = DfaBuilder.TryBuild(program);
            if (automaton is null)
            {
                refused++;
                continue;
            }

            built++;
            foreach (string subject in subjects.Concat(DfaHelper.RandomSubjects(random, pattern, 120)))
            {
                if (DfaHelper.Engine(program, subject) != automaton.IsMatch(subject))
                {
                    failures.Add($"pattern {DfaHelper.Show(pattern)} subject {DfaHelper.Show(subject)}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join('\n', failures.Take(20)));
        Assert.True(built > 100, $"only {built} programs have an automaton, {refused} have none");
        Assert.True(refused > 100, $"{refused} programs have none, the corpus has many with the conditions that the automaton refuses");
    }

    // The answer itself, and not only the agreement of the two: literals, the anchors at each end and both, a prefix, the empty
    // pattern, the end of the text, a pair of surrogates and each half alone, and the code point that a lone surrogate is read as.
    [Theory]
    [InlineData("abc", "xxabcxx", true)]
    [InlineData("abc", "xxabxcx", false)]
    [InlineData("abc", "ab", false)]
    [InlineData("abc", "", false)]
    [InlineData("^abc", "abcx", true)]
    [InlineData("^abc", "xabc", false)]
    [InlineData("^abc", "xabc abc", false)]
    [InlineData("abc$", "xabc", true)]
    [InlineData("abc$", "abcx", false)]
    [InlineData("^abc$", "abc", true)]
    [InlineData("^abc$", "abcc", false)]
    [InlineData("^abc$", "abc\n", false)]
    [InlineData("(^|x)abc", "yxabc", true)]
    [InlineData("(^|x)abc", "yabc", false)]
    [InlineData("ORD-[0-9]+", "see ORD-17 now", true)]
    [InlineData("ORD-[0-9]+", "ORD-x ORD-", false)]
    [InlineData("ORD-[0-9]+", "ORD-ORD-5", true)]
    [InlineData("(?i)ord-[0-9]+", "xORd-9", true)]
    [InlineData("(?i)ord-[0-9]+", "xORd-", false)]
    [InlineData("a|b", "c", false)]
    [InlineData("a|b", "xxb", true)]
    [InlineData("", "", true)]
    [InlineData("", "abc", true)]
    [InlineData("a*", "", true)]
    [InlineData("\\z", "abc", true)]
    [InlineData("\\z", "", true)]
    [InlineData("^$", "", true)]
    [InlineData("^$", "a", false)]
    [InlineData("a\\z|b", "xa", true)]
    [InlineData("a\\z|b", "ax", false)]
    [InlineData("[a-z]+@[a-z]+[.]com", "mail bob@example.com.", true)]
    [InlineData("[a-z]+@[a-z]+[.]com", "mail bob@example.org.", false)]
    [InlineData("(a|b)*a(a|b){2}", "bbbabb", true)]
    [InlineData("(a|b)*a(a|b){2}", "bbbbbb", false)]
    [InlineData("\U0001F600+", "x\U0001F600", true)]
    [InlineData("\U0001F600z", "😀z", true)]
    [InlineData("\U0001F600z", "\uD83Dz", false)]
    [InlineData("\U0001F600z", "\uDE00z", false)]
    [InlineData("�a", "\uD800a", true)]
    [InlineData("�a", "�a", true)]
    [InlineData("�a", "xa", false)]
    [InlineData("(?i)k", "K", true)]
    [InlineData("(?i)k", "x", false)]
    [InlineData("x*abc", "xxxabxabc", true)]
    [InlineData("x*abc", "xxxabxab", false)]
    public static void Automaton_GivesTheAnswerOfTheEngine_OnLiteralsAnchorsAndPrefixes(string pattern, string subject, bool expected)
    {
        CompiledPattern program = ProgramHelper.Compile(pattern);
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(expected, DfaHelper.Engine(program, subject));
        Assert.Equal(expected, automaton.IsMatch(subject));
    }

    // A match that is anywhere in a long text, or nowhere, near the end, is found the same.
    [Fact]
    public static void Automaton_OnALongText_AnswersAsTheEngine()
    {
        string[] patterns = ["ORD-[0-9]+", "^ORD-[0-9]+$", "(?i)ord-[0-9]+", "x*abc", "[a-z]+@[a-z]+[.]com"];
        foreach (string pattern in patterns)
        {
            CompiledPattern program = ProgramHelper.Compile(pattern);
            Dfa automaton = DfaHelper.Build(pattern);
            string filler = new('x', 5000);
            foreach (string subject in new[] { filler, filler + "ORD-1", "ORD-1" + filler, filler + "ORD-" + filler, filler + "abc", "abc" + filler, filler + "bob@example.com", filler + "bob@example.co" })
            {
                DfaHelper.AssertSame(program, automaton, pattern, subject);
            }
        }
    }

    // The patterns that are literals give the same answer through the call site as the same patterns from data: the program of the
    // literal has an automaton, the program of a pattern from data has none.
    [Fact]
    public static void LiteralPattern_AndPatternFromData_GiveTheSameAnswer()
    {
        var random = new DfaHelper.Sequence(20261120);
        int withAutomaton = 0;
        foreach (string pattern in OtherPatterns.Concat(CelSpecPatterns).Concat(ConditionPatterns))
        {
            var literalCache = new PatternCache();
            var dataCache = new PatternCache();
            var preparation = Matches.Preparer(pattern, literalCache);
            var program = (CompiledPattern)preparation.Prepared!;
            withAutomaton += program.Automaton is null ? 0 : 1;
            foreach (string subject in DfaHelper.RandomSubjects(random, pattern, 60).Concat(["", "ORD-1", "bob@example.com", "2026-11-19"]))
            {
                bool literal = DfaHelper.Call(subject, pattern, program, null);
                bool fromData = DfaHelper.Call(subject, pattern, null, dataCache);
                Assert.True(literal == fromData, $"pattern {DfaHelper.Show(pattern)} subject {DfaHelper.Show(subject)}: literal {literal}, from data {fromData}");
            }

            Assert.True(dataCache.TryGet(pattern, out PatternEntry? entry));
            Assert.Null(entry.Program!.Automaton);
        }

        Assert.True(withAutomaton > 20);
    }

    // A pattern with a condition of zero width of a line or of a word has no automaton: the engine of sets of states runs it.
    [Theory]
    [InlineData("\\bfoo\\b")]
    [InlineData("(?m)^a")]
    [InlineData("a\\B")]
    [InlineData("(?m)a$")]
    [InlineData("(?m)$")]
    [InlineData("\\bfoo|bar")]
    public static void TryBuild_WithAConditionOfALineOrAWord_IsRefused(string pattern)
    {
        Dfa? automaton = DfaBuilder.TryBuild(ProgramHelper.Compile(pattern), DfaBuilder.MaxStates, DfaBuilder.MaxClasses, DfaBuilder.MaxWork, out DfaRefusal refusal);

        Assert.Null(automaton);
        Assert.Equal(DfaRefusal.Conditions, refusal);
    }

    // The limit of the states: a pattern that makes 1 024 states has no automaton at 256 and has one when the limit is raised — and
    // then the automaton answers as the engine does, on strings of the two letters where its 1 024 states are all met.
    [Fact]
    public static void TryBuild_PastTheLimitOfTheStates_IsRefused_AndARaisedLimitBuildsTheSameAnswers()
    {
        const string Pattern = "(a|b)*a(a|b){10}";
        CompiledPattern program = ProgramHelper.Compile(Pattern);

        Assert.Null(DfaBuilder.TryBuild(program, DfaBuilder.MaxStates, DfaBuilder.MaxClasses, DfaBuilder.MaxWork, out DfaRefusal refusal));
        Assert.Equal(DfaRefusal.States, refusal);
        Assert.Null(DfaBuilder.TryBuild(program));

        Dfa? raised = DfaBuilder.TryBuild(program, 2000, 8, 5_000_000, out refusal);
        Assert.NotNull(raised);
        Assert.Equal(DfaRefusal.None, refusal);
        Assert.InRange(raised.StateCount, 257, 2000);
        var random = new DfaHelper.Sequence(7);
        for (int i = 0; i < 400; i++)
        {
            int length = 1 + random.Next(30);
            string subject = string.Concat(Enumerable.Range(0, length).Select(_ => random.Next(2) == 0 ? "a" : "b"));
            DfaHelper.AssertSame(program, raised, Pattern, subject);
        }
    }

    // The limit of the states is met exactly: 128 states fit, and a pattern past 256 does not.
    [Fact]
    public static void TryBuild_UpToTheLimitOfTheStates_Builds()
    {
        Dfa automaton = DfaHelper.Build("(a|b)*a(a|b){7}");

        Assert.InRange(automaton.StateCount, 100, DfaBuilder.MaxStates);
    }

    // The limit of the classes: 63 letters and the rest of the code points are 64 classes and fit, one letter more and the pattern
    // has no automaton; a lower limit, given to the build, refuses a smaller pattern the same way.
    [Fact]
    public static void TryBuild_PastTheLimitOfTheClasses_IsRefused()
    {
        string sixtyThree = Letters(63);
        string sixtyFour = Letters(64);

        Dfa fits = DfaHelper.Build(sixtyThree);
        Assert.Equal(DfaBuilder.MaxClasses, fits.ClassCount);
        Assert.Null(DfaBuilder.TryBuild(ProgramHelper.Compile(sixtyFour), DfaBuilder.MaxStates, DfaBuilder.MaxClasses, DfaBuilder.MaxWork, out DfaRefusal refusal));
        Assert.Equal(DfaRefusal.Classes, refusal);

        CompiledPattern small = ProgramHelper.Compile("abc");
        Assert.Null(DfaBuilder.TryBuild(small, DfaBuilder.MaxStates, 3, DfaBuilder.MaxWork, out refusal));
        Assert.Equal(DfaRefusal.Classes, refusal);
        Assert.Equal(4, DfaBuilder.TryBuild(small, DfaBuilder.MaxStates, 4, DfaBuilder.MaxWork, out _)!.ClassCount);
    }

    // The classes are counted after the intervals of one set of instructions are merged: a class of the letters with a space and a
    // hyphen has hundreds of intervals and is four classes, so the pattern has an automaton.
    [Fact]
    public static void Classes_AreMergedByTheSetOfInstructions_BeforeTheyAreCounted()
    {
        CompiledPattern program = ProgramHelper.Compile("^[\\p{L} -]+$");

        Dfa automaton = DfaHelper.Build("^[\\p{L} -]+$");

        Assert.InRange(automaton.ClassCount, 2, 5);
        foreach (string subject in new[] { "", "abc", "ab c-d", "éж x", "abc1", "abc\n", "a\U0001F600", "中文" })
        {
            DfaHelper.AssertSame(program, automaton, "^[\\p{L} -]+$", subject);
        }
    }

    // The limit of the work: the evaluations of an instruction that the classes and the transitions take. Too little for the classes
    // refuses at once; a little more lets the classes pass and the transitions stop the build; the limit of the library builds the
    // same patterns.
    [Fact]
    public static void TryBuild_PastTheLimitOfTheWork_IsRefused()
    {
        CompiledPattern classesOnly = ProgramHelper.Compile("[a-c]+x");
        Assert.Null(DfaBuilder.TryBuild(classesOnly, DfaBuilder.MaxStates, DfaBuilder.MaxClasses, 5, out DfaRefusal refusal));
        Assert.Equal(DfaRefusal.Work, refusal);

        CompiledPattern transitions = ProgramHelper.Compile("(a|b)*a(a|b){8}");
        Assert.Null(DfaBuilder.TryBuild(transitions, DfaBuilder.MaxStates, DfaBuilder.MaxClasses, 2000, out refusal));
        Assert.Equal(DfaRefusal.Work, refusal);

        Assert.NotNull(DfaBuilder.TryBuild(classesOnly));
        Assert.NotNull(DfaBuilder.TryBuild(transitions, 300, 8, DfaBuilder.MaxWork, out _));
    }

    // Past each limit the pattern is run by the engine alone, through the call site, and the answer is the one of a pattern from
    // data.
    [Fact]
    public static void PatternPastALimit_IsRunByTheEngine_WithTheSameAnswers()
    {
        string[] patterns = ["(a|b)*a(a|b){10}", Letters(64), "\\bfoo\\b", "(?m)^a"];
        var random = new DfaHelper.Sequence(11);
        foreach (string pattern in patterns)
        {
            var preparation = Matches.Preparer(pattern, new PatternCache());
            var program = (CompiledPattern)preparation.Prepared!;
            Assert.Null(program.Automaton);
            foreach (string subject in DfaHelper.RandomSubjects(random, pattern, 80).Concat(["", "foo", "a foo b", "aaaaaaaaaaaa", "bbbbbbbbbbbbbbb"]))
            {
                Assert.Equal(DfaHelper.Engine(program, subject), DfaHelper.Call(subject, pattern, program, null));
            }
        }
    }

    // A pattern that begins with \A or ^ has no state of no live thread: after the first position that cannot go on the automaton
    // has nothing left to read, and it has no way to skip.
    [Theory]
    [InlineData("^abc")]
    [InlineData("\\Aabc")]
    [InlineData("^[^@]+@[^@]+[.][a-z]+$")]
    public static void AnchoredAtTheBeginning_HasNoSkip(string pattern)
    {
        Dfa automaton = DfaHelper.Build(pattern);

        Assert.Equal(DfaSkip.None, automaton.Skip);
    }

    private static IEnumerable<(string Pattern, List<string> Subjects)> AllPatterns()
    {
        foreach (Re2SearchCorpusTests.Entry entry in Re2SearchCorpusTests.Corpus.Load().Entries)
        {
            yield return (entry.Pattern, [.. entry.Results.Select(result => result.Subject)]);
        }

        foreach (string pattern in CelSpecPatterns)
        {
            yield return (pattern, ["hubba", "", "cows", "abcd", "grey", "banana", "mañana", "\U0001F431\U0001F600\U0001F600"]);
        }

        foreach (string pattern in OtherPatterns.Concat(ConditionPatterns))
        {
            yield return (pattern, ["buyer@example.com", "no-at-sign", "ORD-12", "x ord-7 y", "error: boom", "foo bar", "2026-11-19"]);
        }
    }

    // A run of the given number of different letters of the CJK block: each is a class of its own. (An alternative of single letters
    // would be one class of the pattern, as in Go.)
    private static string Letters(int count) => string.Concat(Enumerable.Range(0x4E00, count).Select(char.ConvertFromUtf32));
}
