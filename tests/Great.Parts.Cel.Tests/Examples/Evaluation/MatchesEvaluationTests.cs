using Great.Parts.Cel.Tests.Examples.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// matches in evaluation. A pattern that is not a constant is prepared when the call is evaluated, after the
// operands, and its error is EvalInvalidRegex (3021) with the place of the call, the "(" after the name,
// for the receiver form too, and the same text as the compile error of a constant pattern. What is not a
// constant: a variable, any call (dyn('('), string('(')), a concatenation, a ?: of literals, an element of
// a list or a map; only a string literal is. So '(' + '' is no compile error, though its value is the same
// as that of the literal '('. An error of an operand is the result before the pattern is looked at;
// && and || take the error as CEL does: it is the value of the call only if the other operand does not
// decide the result, and a call that is not evaluated gives none. In a macro the call is at its "(" in the
// text, whichever element it is evaluated for.
// Without type checking, the internal entry for evaluation that the cases of the suite use, the pattern
// is never prepared at planning, so a pattern in a literal is an error of evaluation too, at the call; a
// value that is not a string is EvalNoMatchingOverload as at any dyn operand.
// Each row is "bindings | expression | outcome" in the form of MatchesRows.
[Trait("Requirement", "F-6")]
[Trait("Requirement", "F-4")]
public sealed class MatchesEvaluationTests
{
    private const string Rows = """
        p=( | matches('abc', p) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        p=( | 'abc'.matches(p) | error EvalInvalidRegex 13: invalid regular expression at offset 0: missing closing )
        p=a** | matches('abc', p) | error EvalInvalidRegex 7: invalid regular expression at offset 1: *
        p=a{1001} | matches('abc', p) | error EvalInvalidRegex 7: invalid regular expression at offset 1: invalid repeat count
        p=(?=x) | 'abc'.matches(p) | error EvalInvalidRegex 13: invalid regular expression at offset 0: unsupported Perl syntax: (?=
        p=abc | matches('xabcx', p) | bool true
        p=abc | 'xabcx'.matches(p) | bool true
        - | matches('abc', dyn('(')) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | matches('abc', '(' + '') | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | matches('abc', ('(' + '')) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | matches('abc', true ? '(' : 'a') | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | matches('abc', string('(')) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | matches('abc', ['('][0]) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | matches('abc', {'k': '('}.k) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | 'abc'.matches('a' + '(') | error EvalInvalidRegex 13: invalid regular expression at offset 0: missing closing )
        p=( | false && matches('abc', p) | bool false
        p=( | true || matches('abc', p) | bool true
        p=( | true ? true : matches('abc', p) | bool true
        p=( | matches('abc', p) || true | bool true
        p=( | matches('abc', p) && false | bool false
        p=( | [].all(x, matches(x, p)) | bool true
        p=( | matches('abc', p) || false | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        p=( | matches('abc', p) && true | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        p=( | ['a', 'b'].all(x, matches(x, p)) | error EvalInvalidRegex 25: invalid regular expression at offset 0: missing closing )
        p=( | ['a', 'b'].exists(x, matches(x, p)) | error EvalInvalidRegex 28: invalid regular expression at offset 0: missing closing )
        p=a | ['a', 'b'].all(x, matches(x, p)) | bool false
        p=[ab] | ['a', 'b'].all(x, matches(x, p)) | bool true
        p=( | matches(string(1 / 0), p) | error DivisionByZero 17: *
        """;

    private const string WithoutTypeCheck = """
        - | matches('abc', 'b') | bool true
        - | 'abc'.matches('b') | bool true
        - | matches('abc', '(') | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | 'abc'.matches('(') | error EvalInvalidRegex 13: invalid regular expression at offset 0: missing closing )
        - | matches('abc', 'a**') | error EvalInvalidRegex 7: invalid regular expression at offset 1: *
        p=( | matches('abc', p) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        p=a | 'abc'.matches(p) | bool true
        - | matches(1, 'a') | error EvalNoMatchingOverload 7: no such overload: matches(int, string)
        - | matches('abc', 1) | error EvalNoMatchingOverload 7: no such overload: matches(string, int)
        - | matches('abc') | error EvalNoMatchingOverload 7: no such overload: matches(string)
        - | 'abc'.matches(1) | error EvalNoMatchingOverload 13: no such overload: matches(*
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

    [Fact]
    public void WithoutTypeChecking_EveryRowIsItsOutcome()
    {
        Assert.Empty(MatchesRows.Check(environment, WithoutTypeCheck, checkTypes: false));
    }
}
