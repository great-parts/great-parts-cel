using System.Linq;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The limits of a pattern: each with the pattern at the limit, which is accepted, and the pattern one over, which
// is refused with the reason "invalid repeat count", "expression nests too deeply" or "expression too large",
// as a constant (InvalidRegex at the literal) and as a pattern in a variable (EvalInvalidRegex at the call).
// All limits are checked before the counted repetitions are expanded, so a pattern that would expand to
// billions of instructions is refused by its counts, and the work of the refusal does not depend on them.
//   the count of a repetition {n}, {n,}, {n,m}: n and m at most 1000; the form {n,} is not limited in its
//     upper end, and * + ? are not counts;
//   the product of nested counts at most 1000: the outer count is the one that is refused, so the offset is
//     that of the outer repetition. The count that applies is the upper one of {n,m}, and n of {n,}. The
//     product is checked from the repetition {2} and {0,2} on: (?:a{1000}){1} and {0,1} are accepted;
//   the height of the tree at most 1000: a leaf is 1 and each group that holds a submatch adds 1, so
//     999 groups around a character are accepted and 1000 are refused; a group (?:...) is no node of the
//     tree and adds nothing; the error is about the whole pattern, so its offset is 0;
//   the size of the program m at most 100 000: the instructions, the instruction that ends the program
//     included, plus the ranges of the classes that are not taken from a table. The tables are the
//     classes written as one escape, \d, \pL and the like; a class in brackets owns its ranges, counted
//     once for the class in the text, however many times a repetition copies it. a{n} is n instructions,
//     a{0,n} is n characters and n alternatives, and so on as Go expands the repetitions. The size is known
//     after the whole pattern is read, so a pattern with a syntax error later in the text is refused for
//     the syntax error, not for its size.
// The values of the accepted patterns, on the subject "x", are those of Go 1.27.3, whose own limit is far
// above these (about 3.3 million instructions); the size limit is a decision of the project, and the
// patterns between the two limits are the entries of the register of known differences
// (MatchesKnownDifferenceTests).
[Trait("Requirement", "F-6")]
[Trait("Requirement", "R-3")]
public sealed class MatchesLimitTests
{
    private const string Accepted = """
        "x" | a{1000} | false
        "x" | a{1000,} | false
        "x" | a{0,1000} | true
        "x" | a{1000,1000} | false
        "x" | a{1} | false
        "x" | (?:a{10}){100} | false
        "x" | (?:a{4}){250} | false
        "x" | (?:(?:a{10}){10}){10} | false
        "x" | (?:a{1000}){1} | false
        "x" | (?:a{1000}){0} | true
        "x" | (?:a{1000}){0,1} | true
        "x" | (a{2}){500} | false
        "x" | (?:a{2,}){500} | false
        "x" | (?:a*){1000} | true
        """;

    private const string Rejected = """
        a{1001} | 1 | invalid repeat count
        a{1001,} | 1 | invalid repeat count
        a{0,1001} | 1 | invalid repeat count
        a{1001,1001} | 1 | invalid repeat count
        (?:a{10}){101} | 9 | invalid repeat count
        (?:a{4}){251} | 8 | invalid repeat count
        (?:(?:a{10}){10}){11} | 17 | invalid repeat count
        (?:a{1000}){2} | 11 | invalid repeat count
        (a{2}){501} | 6 | invalid repeat count
        (?:a{2,}){501} | 9 | invalid repeat count
        (?:a*){1001} | 6 | invalid repeat count
        (?:a{1000}){1000} | 11 | invalid repeat count
        a{1000}{1000} | 1 | *
        (?:(?:a{1000}){1000}){1000} | 14 | invalid repeat count
        a{3}{4} | 1 | *
        (?:a{1000}){999} | 11 | invalid repeat count
        (?:(?:a{1000}){999}){1000} | 14 | invalid repeat count
        (?:a{1000}){1000}b | 11 | invalid repeat count
        """;

    // m is 100 000 in each of these: 99 times 1000 and 999 and the match; 99 times 1000 and 999 of a class from
    // a table and the match; 99 times (1000 and one range) and 900 and the match; 49 times 2000, 1998, 1 and
    // the match.
    private static readonly string[] SizeAtTheLimit =
    [
        Repeat("a{1000}", 99) + "a{999}",
        Repeat("\\d{1000}", 99) + "\\d{999}",
        Repeat("[a-z]{1000}", 99) + "a{900}",
        Repeat("a{0,1000}", 49) + "a{0,999}" + "a",
    ];

    // m is 100 001 in each of these: one instruction more.
    private static readonly string[] SizeOverTheLimit =
    [
        Repeat("a{1000}", 100),
        Repeat("\\d{1000}", 100),
        Repeat("[a-z]{1000}", 99) + "a{901}",
        Repeat("a{0,1000}", 50),
    ];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void CountsAtTheLimit_AreAccepted()
    {
        Assert.Empty(MatchesRows.CheckValues(environment, Accepted));
    }

    [Fact]
    public void CountsOverTheLimit_AreRefusedAtTheOffsetOfTheRepetition()
    {
        Assert.Empty(MatchesRows.CheckRejections(environment, Rejected));
    }

    [Fact]
    public void HeightOfTheTree_999GroupsAreAcceptedAnd1000AreRefused()
    {
        string accepted = Repeat("(", 999) + "a" + Repeat(")", 999);
        string refused = Repeat("(", 1000) + "a" + Repeat(")", 1000);
        string withoutNodes = Repeat("(?:", 2000) + "a" + Repeat(")", 2000);

        Assert.Empty(MatchesRows.CheckValues(environment, "\"x\" | " + accepted + " | false"));
        Assert.Empty(MatchesRows.CheckRejections(environment, refused + " | 0 | expression nests too deeply"));
        Assert.Empty(MatchesRows.CheckValues(environment, "\"x\" | " + withoutNodes + " | false"));
    }

    [Fact]
    public void SizeOfTheProgram_100000IsAcceptedAnd100001IsRefused()
    {
        Assert.Empty(MatchesRows.CheckValues(environment, string.Join('\n', SizeAtTheLimit.Select(pattern => "\"x\" | " + pattern + " | false"))));
        Assert.Empty(MatchesRows.CheckRejections(environment, string.Join('\n', SizeOverTheLimit.Select(pattern => pattern + " | 0 | expression too large"))));
    }

    // 101 pattern parts a{1000} are 101 001 instructions, and the parse stops at the missing ")" first.
    [Fact]
    public void SyntaxErrorAfterAnOversizedPattern_IsTheSyntaxError()
    {
        Assert.Empty(MatchesRows.CheckRejections(environment, Repeat("a{1000}", 101) + "( | 0 | missing closing )"));
    }

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));
}
