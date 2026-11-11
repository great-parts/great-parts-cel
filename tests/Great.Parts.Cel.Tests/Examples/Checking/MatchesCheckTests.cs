using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// matches in type checking. The call is bool, its pattern may be a constant: a string literal, in any of
// its forms (quoted, raw, triple-quoted, in parentheses) and nothing else. A constant pattern is prepared
// at planning, so its error is a compile error, InvalidRegex (2009), with the place of the literal, and
// "invalid regular expression at offset N: reason" for the text: N counts UTF-16 code units of the
// value of the literal (what the pattern is after the escapes of CEL are read), not of its text in the
// expression, so '\U0001F600a**' has its fault at 3, where "**" begins, and '\x28' is the pattern "(" and
// the fault of it is at 0. The error is found whether or not the call is evaluated: in a branch that is never
// taken, in a macro, in an operand of && and ||. Compilation goes on after it and returns the errors of the
// whole expression in the order of their places. The subject is not looked at, and no other function takes
// the pattern for a pattern: 'a(b'.contains('(') compiles. A pattern in a variable is checked at evaluation
// only (see the examples of evaluation).
// cel-go v0.32.0 with its validator of regex literals finds the error of a constant pattern at compilation in
// the receiver form only: the validator reads the first argument of the call, which is the pattern of
// 'abc'.matches('(') and the subject of matches('abc', '('). So for the global form cel-go gives the error of
// the pattern at evaluation, and an error at compilation for a subject that is no valid pattern
// (matches('(', 'a')), where here the subject is not looked at.
// Each row is "bindings | expression | outcome" in the form of MatchesRows; the offset of a literal in the
// expression is its first character, the "r" of a raw literal and the quote of a triple-quoted one included.
[Trait("Requirement", "F-6")]
[Trait("Requirement", "F-4")]
public sealed class MatchesCheckTests
{
    private const string Rows = """
        - | matches('abc', '(') | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing )
        - | 'abc'.matches('(') | compile InvalidRegex 14: invalid regular expression at offset 0: missing closing )
        - | matches('abc', r'(') | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing )
        - | matches('abc', '''(''') | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing )
        - | matches('abc', "(") | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing )
        - | matches('abc', ('(')) | compile InvalidRegex 16: invalid regular expression at offset 0: missing closing )
        - | matches('abc', '\x28') | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing )
        - | matches('abc', '\u00e9\u00e9a**') | compile InvalidRegex 15: invalid regular expression at offset 3: *
        - | matches('abc', '\U0001F600a**') | compile InvalidRegex 15: invalid regular expression at offset 3: *
        - | matches('abc', 'a**') | compile InvalidRegex 15: invalid regular expression at offset 1: *
        - | matches('abc', r'\C') | compile InvalidRegex 15: invalid regular expression at offset 0: *
        - | 'abc'.matches('a{1001}') | compile InvalidRegex 14: invalid regular expression at offset 1: invalid repeat count
        - | matches('abc', '(?=x)') | compile InvalidRegex 15: invalid regular expression at offset 0: unsupported Perl syntax: (?=
        - | matches('abc', '(') && matches('b', 'a**') | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing ) ; InvalidRegex 36: invalid regular expression at offset 1: *
        - | matches('abc', '(') && matches(1, 'b') | compile InvalidRegex 15: invalid regular expression at offset 0: missing closing ) ; NoMatchingOverload 30: found no matching overload for 'matches' applied to '(int, string)'
        - | false && matches('abc', '(') | compile InvalidRegex 24: invalid regular expression at offset 0: missing closing )
        - | true || matches('abc', '(') | compile InvalidRegex 23: invalid regular expression at offset 0: missing closing )
        - | ['a'].all(x, matches(x, '(')) | compile InvalidRegex 24: invalid regular expression at offset 0: missing closing )
        - | [1].all(x, matches(x, 'a')) | compile NoMatchingOverload 18: found no matching overload for 'matches' applied to '(int, string)'
        - | matches('(', 'a') | bool false
        - | '(abc'.contains('(') | bool true
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_IsItsOutcome()
    {
        Assert.Empty(MatchesRows.Check(environment, Rows));
    }

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("matches('abc', 'b')")]
    [InlineData("'abc'.matches('b')")]
    [InlineData("matches(s, p)")]
    [InlineData("s.matches(p)")]
    [InlineData("matches('abc', 'b') && s.matches(p)")]
    public void Call_IsBool(string expression)
    {
        CelCompileResult<bool> compiled = environment.Compile<bool>(expression);

        Assert.True(compiled.IsSuccess);
        Assert.Equal(CelType.Bool, compiled.GetProgramOrThrow().ResultType);
    }
}
