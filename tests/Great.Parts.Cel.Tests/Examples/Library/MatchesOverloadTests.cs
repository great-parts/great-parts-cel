using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The two overloads of matches: matches (a global call, matches(string, string)) and matches_string (a call
// from a receiver, string.matches(string)), with the same meaning: whether some part of the string matches
// the pattern. Each row is "bindings | expression | outcome" in the form of MatchesRows.
//   the operands as strings, in both forms, with the empty string and the empty pattern (the empty pattern
//     matches every string, a pattern of "a*" matches the empty string, "a+" does not);
//   operands of the static type dyn, in any of the positions: type checking allows the call and the
//     overload is chosen at evaluation by the values; the pattern in a dyn is not a constant, so its
//     error is EvalInvalidRegex (3021) at the call, which is the "(" after the name, for the receiver form
//     too; a value that is not a string is EvalNoMatchingOverload (3000), whose text names the types of the
//     values (the receiver is the first);
//   an operand of another static type, or another number of arguments, is NoMatchingOverload (2001) at the
//     same place.
[Trait("Requirement", "F-6")]
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-4")]
public sealed class MatchesOverloadTests
{
    private const string Rows = """
        - | matches('hello', 'ell') | bool true
        - | 'hello'.matches('ell') | bool true
        - | matches('hello', '^ell') | bool false
        - | 'hello'.matches('^hel') | bool true
        - | matches('', '') | bool true
        - | ''.matches('') | bool true
        - | matches('abc', '') | bool true
        - | 'abc'.matches('') | bool true
        - | matches('', 'a') | bool false
        - | ''.matches('a') | bool false
        - | matches('', 'a*') | bool true
        - | ''.matches('^$') | bool true
        - | matches('', 'a+') | bool false
        - | matches(dyn('abc'), 'b') | bool true
        - | matches('abc', dyn('b')) | bool true
        - | matches(dyn('abc'), dyn('b')) | bool true
        - | dyn('abc').matches('b') | bool true
        - | 'abc'.matches(dyn('b')) | bool true
        - | dyn('abc').matches(dyn('^c')) | bool false
        - | matches(dyn(''), dyn('')) | bool true
        - | dyn('').matches(dyn('a')) | bool false
        - | matches('abc', dyn('(')) | error EvalInvalidRegex 7: invalid regular expression at offset 0: missing closing )
        - | 'abc'.matches(dyn('(')) | error EvalInvalidRegex 13: invalid regular expression at offset 0: missing closing )
        - | matches(dyn('abc'), dyn('a**')) | error EvalInvalidRegex 7: invalid regular expression at offset 1: *
        - | dyn('abc').matches(dyn('a{1001}')) | error EvalInvalidRegex 18: invalid regular expression at offset 1: invalid repeat count
        - | matches(dyn(1), 'a') | error EvalNoMatchingOverload 7: no such overload: matches(int, string)
        - | matches('a', dyn(1)) | error EvalNoMatchingOverload 7: no such overload: matches(string, int)
        - | matches(dyn(1), dyn(2)) | error EvalNoMatchingOverload 7: no such overload: matches(int, int)
        - | matches(dyn(b'a'), 'a') | error EvalNoMatchingOverload 7: no such overload: matches(bytes, string)
        - | matches('a', dyn(['a'])) | error EvalNoMatchingOverload 7: no such overload: matches(string, list)
        - | dyn(1).matches('a') | error EvalNoMatchingOverload 14: no such overload: matches(*
        - | 'a'.matches(dyn(1)) | error EvalNoMatchingOverload 11: no such overload: matches(*
        - | dyn(1).matches(dyn(2)) | error EvalNoMatchingOverload 14: no such overload: matches(*
        - | matches(1, 'a') | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(int, string)'
        - | matches('a', 1) | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(string, int)'
        - | matches(b'a', 'a') | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(bytes, string)'
        - | matches('a') | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(string)'
        - | matches('a', 'b', 'c') | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(string, string, string)'
        - | matches(null, 'a') | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(null_type, string)'
        - | matches(['a'], 'a') | compile NoMatchingOverload 7: found no matching overload for 'matches' applied to '(list(string), string)'
        - | 1.matches('a') | compile NoMatchingOverload 9: found no matching overload for 'matches' applied to '*
        - | 'a'.matches(1) | compile NoMatchingOverload 11: found no matching overload for 'matches' applied to '*
        - | b'a'.matches('a') | compile NoMatchingOverload 12: found no matching overload for 'matches' applied to '*
        - | 'a'.matches('b', 'c') | compile NoMatchingOverload 11: found no matching overload for 'matches' applied to '*
        - | ['a'].matches('a') | compile NoMatchingOverload 13: found no matching overload for 'matches' applied to '*
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
}
