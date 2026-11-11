using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The cost of matches, the rows of the cost page, checked as the rows of CostTableTests are: "bindings |
// expression | cost", the total cost of the expression is exact, so evaluation gets as far as its end with
// that cost as the limit and fails with CostLimitExceeded at one unit less. The total is the nodes around the
// call (a literal 0, a read of a variable 1) and the call:
//   parse of the pattern, 1 + the length L of the pattern over 10 rounded up — only if the pattern is not
//     a constant: a constant is prepared at planning, which is not charged;
//   build of the program, the size m over 4 rounded up — likewise, and only if the pattern parsed and its
//     size is within the limit;
//   match, 1 + (n + 1) times m over 10 rounded up, n the length of the subject; at every evaluation.
// L and n are in UTF-16 code units, whatever the characters are. m is the size of the program: the
// instructions, the match one included, and the ranges of the classes not taken from a table, as the limit
// counts them (MatchesLimitTests): 'abc' is 4, \d\d\d is 4, \pL is 2, ^.*$ is 5, a{10} is 11,
// a{2,5} is 9, [a-z]{3} is 5 (3 instructions, 1 range, the match), the empty pattern is 1.
// The cache of patterns does not change the cost, and a failed preparation of a pattern costs what the
// steps that were done cost: every row is evaluated three times with the same program, so the second and the
// third evaluation find the pattern in the cache and charge the same. The rows with an error give the cost at
// which the error is the result; one unit less is CostLimitExceeded, as the parse is charged before it runs.
// The bindings are the pairs name=value (the value a string, taken as written).
[Trait("Requirement", "R-3")]
[Trait("Requirement", "R-4")]
public sealed class MatchesCostTests
{
    private const string Rows = """
        s=xabcx | matches(s, 'abc') | 5
        s=xabcx | s.matches('abc') | 5
        s=xabcx;p=abc | matches(s, p) | 9
        s=xabcx;p=abc | s.matches(p) | 9
        - | matches('xabcx', 'abc') | 4
        s=abcdefghijkl | matches(s, 'abcdefghijk') | 18
        s=abcdefghijkl;p=abcdefghijk | matches(s, p) | 25
        s=abcdefghij;p=abcdefghij | matches(s, p) | 21
        s=x | matches(s, '') | 3
        s=x;p= | matches(s, p) | 6
        s= | matches(s, 'a') | 3
        s=a1 | matches(s, r'\d\d\d') | 4
        s=a1;p=\d\d\d | matches(s, p) | 8
        s=ab | matches(s, '[a-z]{3}') | 4
        s=ab;p=[a-z]{3} | matches(s, p) | 9
        s=é | matches(s, r'\pL') | 3
        s=hello | matches(s, '^.*$') | 5
        s=x;p=^.*$ | matches(s, p) | 8
        s=aaaaaaaaaaaa | matches(s, 'a{10}') | 17
        s=a;p=a{2,5} | matches(s, p) | 10
        - | matches('\U0001F600\U0001F600', '^..$') | 4
        s=😀😀;p=^..$ | matches(s, p) | 10
        """;

    // The pattern that is refused: the cost of the steps done, the parse only, and the code of the error.
    private const string ErrorRows = """
        s=x;p=( | matches(s, p) | 4
        s=x;p=a** | s.matches(p) | 4
        s=x;p=a{1001} | matches(s, p) | 4
        s=x;p=(?=abcdefghijk) | matches(s, p) | 5
        s=x;p=[a- | s.matches(p) | 4
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_CostsWhatTheCostPageSays()
    {
        var failures = new List<string>();
        foreach (string row in Rows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            foreach (string failure in Check(cells[1], MatchesRows.Bindings(cells[0]), ulong.Parse(cells[2], CultureInfo.InvariantCulture), null))
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void RefusedPattern_CostsTheStepsThatWereDone()
    {
        var failures = new List<string>();
        foreach (string row in ErrorRows.Split('\n'))
        {
            string[] cells = row.Trim().Split(" | ");
            foreach (string failure in Check(cells[1], MatchesRows.Bindings(cells[0]), ulong.Parse(cells[2], CultureInfo.InvariantCulture), CelErrorCode.EvalInvalidRegex))
            {
                failures.Add(row.Trim() + ": " + failure);
            }
        }

        Assert.Empty(failures);
    }

    // m is 100 000 in the pattern, the most that is accepted; the subject is x, so n is 1 and the pattern is 699 units.
    // As a variable: 2 reads, the parse 1 + 70, the build 25 000, the match 1 + 20 000; as a literal: 1 read and the match.
    [Fact]
    public void ProgramOfTheLimit_CostsItsBuildAndItsMatch()
    {
        string pattern = string.Concat(Enumerable.Repeat("a{1000}", 99)) + "a{999}";
        string asVariable = "s=x;p=" + pattern;
        string asLiteral = "s=x";

        Assert.Empty(Check("matches(s, p)", MatchesRows.Bindings(asVariable), 2 + 71 + 25_000 + 20_001, null));
        Assert.Empty(Check("matches(s, r'" + pattern + "')", MatchesRows.Bindings(asLiteral), 1 + 20_001, null));
    }

    // 101 parts a{1000} are 101 001 instructions, over the limit; the pattern is 707 units, so the parse is 1 + 71,
    // and the build is not charged: the limit is checked after the parse, before the build.
    [Fact]
    public void ProgramOverTheLimit_CostsOnlyItsParse()
    {
        string pattern = string.Concat(Enumerable.Repeat("a{1000}", 101));

        Assert.Empty(Check("matches(s, p)", MatchesRows.Bindings("s=x;p=" + pattern), 2 + 72, CelErrorCode.EvalInvalidRegex));
    }

    // Evaluates the program three times at the limit cost, which must reach the end (the value, or the error
    // wanted), and three times at one unit less, which must be CostLimitExceeded at the call.
    private List<string> Check(string expression, CelBindings bindings, ulong cost, CelErrorCode? error)
    {
        var failures = new List<string>();
        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(expression);
        if (!compiled.IsSuccess)
        {
            failures.Add("compile error " + compiled.Errors[0].Code);
            return failures;
        }

        CelProgram<CelValue> program = compiled.GetProgramOrThrow();
        for (int round = 1; round <= 3; round++)
        {
            CelEvalResult<CelValue> atCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost }, CancellationToken.None);
            bool reached = error is null ? atCost.IsSuccess : atCost.Errors.Length == 1 && atCost.Errors[0].Code == error;
            if (!reached)
            {
                failures.Add("round " + round.ToString(CultureInfo.InvariantCulture) + ", limit " + cost.ToString(CultureInfo.InvariantCulture) + ": " + (atCost.IsSuccess ? "a value" : atCost.Errors[0].Code.ToString()));
            }

            CelEvalResult<CelValue> belowCost = program.Evaluate(bindings, new CelEvalOptions { CostLimit = cost - 1 }, CancellationToken.None);
            if (belowCost.Errors.Length != 1 || belowCost.Errors[0].Code != CelErrorCode.CostLimitExceeded)
            {
                failures.Add("round " + round.ToString(CultureInfo.InvariantCulture) + ", limit " + (cost - 1).ToString(CultureInfo.InvariantCulture) + " is enough");
            }
        }

        return failures;
    }
}
