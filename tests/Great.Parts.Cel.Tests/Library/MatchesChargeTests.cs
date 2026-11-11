using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Library.Regex;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The charges of matches one by one, as the implementation makes them through the call site: the parse 1 + ⌈L/10⌉, the build ⌈m/4⌉ and
// the match 1 + ⌈(n + 1) · m/10⌉, in that order, each before the work it pays for. L and n are UTF-16 code units, m is the size of the
// program. The tests take their data of their own, and call the implementation directly so that each charge is seen alone.
public sealed class MatchesChargeTests
{
    private static readonly LineTable Lines = new("x");

    // The charges of 'abc' on a subject of 5 units: the parse, the build and the match.
    private static readonly ulong[] AbcOnFive = [2, 1, 4];

    // The charge of the match alone.
    private static readonly ulong[] MatchOnly = [4];

    // The patterns of the refusals: the offset and the reason, then the units of the parse. The last is 101 repeats of a{1000}, a
    // program of 101 001 instructions, one over the limit, L = 707.
    public static TheoryData<string, int, string, ulong> Refusals => new()
    {
        { "(", 0, "missing closing )", 2 },
        { "a**", 1, "invalid nested repetition operator", 2 },
        { "a{1001}", 1, "invalid repeat count", 2 },
        { "(?=abcdefghijk)", 0, "unsupported Perl syntax: (?=", 3 },
        { string.Concat(Enumerable.Repeat("a{1000}", 101)), 0, "expression too large", 72 },
    };

    // Subject, pattern, then the three charges. 'abc' is 4 instructions with the match, '' is 1, 'a{3}b' is 5 (a, a, a, b, the match),
    // 'x*' is 3 (the fork and the character, the match), '[a-z]{3}' is 5 (three instructions, one range, the match), '^.$' is 4 and
    // '^..$' is 5. The length of a subject counts code units: the pair of '😀' is two.
    [Theory]
    [InlineData("xabcx", "abc", 2UL, 1UL, 4UL)]
    [InlineData("", "", 1UL, 1UL, 2UL)]
    [InlineData("", "abc", 2UL, 1UL, 2UL)]
    [InlineData("abcdefghijkl", "abcdefghijk", 3UL, 3UL, 17UL)]
    [InlineData("zzzaaabzzz", "a{3}b", 2UL, 2UL, 7UL)]
    [InlineData("xxxx", "x*", 2UL, 1UL, 3UL)]
    [InlineData("ab", "[a-z]{3}", 2UL, 2UL, 3UL)]
    [InlineData("😀", "^.$", 2UL, 1UL, 3UL)]
    [InlineData("😀😀", "^..$", 2UL, 2UL, 4UL)]
    public static void PatternFromData_ChargesTheParseTheBuildAndTheMatchInThatOrder(string subject, string pattern, ulong parse, ulong build, ulong match)
    {
        var meter = new RecordingMeter();

        CelValue result = Call(subject, pattern, meter, new PatternCache());

        Assert.False(result.TryGetError(out _));
        Assert.Equal(new[] { parse, build, match }, meter.Charges);
    }

    // The cache changes no charge: the second call finds the entry and pays the parse and the build as the first did.
    [Fact]
    public static void PatternFromData_FoundInTheCache_ChargesAsAMiss()
    {
        var cache = new PatternCache();
        var first = new RecordingMeter();
        var second = new RecordingMeter();

        CelValue missed = Call("xabcx", "abc", first, cache);
        int afterFirst = cache.Count;
        CelValue hit = Call("xabcx", "abc", second, cache);

        Assert.Equal((true, true, 1, 1), (missed.TryGetBoolean(out bool a) && a, hit.TryGetBoolean(out bool b) && b, afterFirst, cache.Count));
        Assert.Equal(AbcOnFive, first.Charges);
        Assert.Equal(first.Charges, second.Charges);
    }

    // A refused pattern pays the parse alone — whether the parse is made now or the refusal is found in the cache — and the error is
    // the same each time, at the call, with the offset and the reason of the pattern.
    [Theory]
    [MemberData(nameof(Refusals))]
    public static void RefusedPattern_ChargesTheParseAlone_AlsoFromTheCache(string pattern, int offset, string reason, ulong parse)
    {
        var cache = new PatternCache();
        string expected = "invalid regular expression at offset " + offset + ": " + reason;

        for (int round = 1; round <= 2; round++)
        {
            var meter = new RecordingMeter();
            CelValue result = Call("x", pattern, meter, cache);

            Assert.True(result.TryGetError(out CelError? error), "round " + round);
            Assert.Equal((CelErrorCode.EvalInvalidRegex, CelErrorStage.Evaluate, expected), (error.Code, error.Stage, error.Message));
            Assert.Equal(new[] { parse }, meter.Charges);
            Assert.Equal((1, 1), (cache.Count, cache.Weight));
        }
    }

    // A prepared pattern was parsed and built when the expression was checked: the call pays the match alone, and looks neither at the
    // cache nor at the pattern argument.
    [Fact]
    public static void PreparedPattern_ChargesTheMatchAlone()
    {
        var cache = new PatternCache();
        ConstantPreparation preparation = Matches.Preparer("abc", cache);
        var meter = new RecordingMeter();

        CelValue result = Call("xabcx", "abc", meter, cache: null, preparation.Prepared);

        Assert.Equal((true, 1), (result.TryGetBoolean(out bool value) && value, cache.Count));
        Assert.Equal(MatchOnly, meter.Charges);
    }

    // The preparation of a refused literal is the text of its error, the same text that the evaluation of the pattern from data gives,
    // and the refusal is kept in the cache.
    [Fact]
    public static void Preparation_OfARefusedPattern_IsTheTextOfTheError()
    {
        var cache = new PatternCache();

        ConstantPreparation preparation = Matches.Preparer("a**", cache);

        Assert.Null(preparation.Prepared);
        Assert.Equal("invalid regular expression at offset 1: invalid nested repetition operator", preparation.Error);
        Assert.Equal(1, cache.Count);
    }

    // Without a cache — a call site made outside an evaluation — the call charges and answers the same.
    [Fact]
    public static void WithoutACache_TheChargesAreTheSame()
    {
        var meter = new RecordingMeter();

        CelValue result = Call("xabcx", "abc", meter, cache: null);

        Assert.True(result.TryGetBoolean(out bool value) && value);
        Assert.Equal(AbcOnFive, meter.Charges);
    }

    // Without a meter nothing is charged: the answer is the same and the pattern is kept.
    [Fact]
    public static void WithoutALimit_TheAnswerIsTheSameAndThePatternIsKept()
    {
        var cache = new PatternCache();

        CelValue result = Call("xabcx", "abc", meter: null, cache);

        Assert.Equal((true, 1), (result.TryGetBoolean(out bool value) && value, cache.Count));
    }

    // The limit is exceeded by each of the three charges in turn. The charges of 'abc' on a subject of 5 are 2, 1 and 4, 7 in all: with
    // the limit 1 the parse does not run, with 2 the build does not, with 3 and with 6 the match does not, and in each case nothing is
    // kept — the match is charged before the program is built, so a limit that stops it leaves the cache as it was.
    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(6UL)]
    public static void LimitThatStopsAStep_LeavesNothingInTheCache(ulong limit)
    {
        var cache = new PatternCache();
        var meter = new RecordingMeter(limit);

        Assert.Throws<CostLimitException>(() =>
        {
            _ = Call("xabcx", "abc", meter, cache);
        });

        Assert.Equal((0, 0), (cache.Count, cache.Weight));
    }

    // A limit equal to the sum is allowed, and then the pattern is kept.
    [Fact]
    public static void LimitEqualToTheSum_RunsTheCall()
    {
        var cache = new PatternCache();
        var meter = new RecordingMeter(7);

        CelValue result = Call("xabcx", "abc", meter, cache);

        Assert.Equal((true, 7UL, 1), (result.TryGetBoolean(out bool value) && value, meter.Total, cache.Count));
    }

    // A refused pattern whose parse the limit stops keeps no entry either: the parse is charged before it runs.
    [Fact]
    public static void RefusedPattern_StoppedByTheLimitOfItsParse_LeavesNothingInTheCache()
    {
        var cache = new PatternCache();

        Assert.Throws<CostLimitException>(() =>
        {
            _ = Call("x", "(", new RecordingMeter(1), cache);
        });

        Assert.Equal(0, cache.Count);
    }

    // An operand of another kind is EvalNoMatchingOverload before anything is charged or parsed.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void OperandOfAnotherKind_IsNoMatchingOverloadAndChargesNothing(bool subjectIsWrong)
    {
        var meter = new RecordingMeter();
        var cache = new PatternCache();
        CelValue[] arguments = [CelValue.FromString("a"), CelValue.FromString("a")];
        arguments[subjectIsWrong ? 0 : 1] = CelValue.FromInt64(1);

        CelValue result = Matches.Implementation(arguments, new CallSite("matches", 0, Lines, meter, cache: cache));

        Assert.True(result.TryGetError(out CelError? error));
        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, 0UL, 0), (error.Code, meter.Total, cache.Count));
    }

    // The same call, whatever the cache holds: a cache of one entry, which evicts at every new pattern, gives the answers and the
    // charges of a cache that keeps everything.
    [Fact]
    public static void CacheThatEvictsAtEveryPattern_ChangesNeitherTheAnswersNorTheCharges()
    {
        string[] patterns = ["abc", "b+", "(", "[a-c]{2}", "a**", "^x"];
        var small = new PatternCache(1, 1000);
        var large = new PatternCache();

        foreach (string pattern in patterns.Concat(patterns))
        {
            var smallMeter = new RecordingMeter();
            var largeMeter = new RecordingMeter();
            CelValue smallResult = Call("abcb", pattern, smallMeter, small);
            CelValue largeResult = Call("abcb", pattern, largeMeter, large);

            Assert.Equal(Describe(largeResult), Describe(smallResult));
            Assert.Equal(largeMeter.Charges, smallMeter.Charges);
        }
    }

    // The answer of a call as text: an error value is a new object at every call, so values are compared by what they say.
    private static string Describe(CelValue value) =>
        value.TryGetError(out CelError? error) ? error.Code + ": " + error.Message : value.TryGetBoolean(out bool answer) ? "bool " + answer : "other";

    private static CelValue Call(string subject, string pattern, RecordingMeter? meter, PatternCache? cache, object? prepared = null) =>
        Matches.Implementation(
            [CelValue.FromString(subject), CelValue.FromString(pattern)],
            new CallSite("matches", 0, Lines, meter, prepared: prepared, cache: cache));
}
