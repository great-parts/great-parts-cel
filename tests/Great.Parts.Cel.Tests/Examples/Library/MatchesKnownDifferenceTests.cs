using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The fifteen entries of the register of known differences with cel-go that belong to matches, each with the
// minimal case of the entry and the outcome of Great.Parts.Cel that the entry states (the outcome of cel-go
// is in the entry, and the differential run takes it from the oracle). The signature is that of the register.
//   matches(<kind>, string) stage-mismatch  the same for the subject of every other kind (bool, bytes, int, list,
//                                            map, null_type, type, uint), in the dyn of a value
//   matches(double, string) stage-mismatch   matches(dyn(1.5), '('): here a compile error at the literal; cel-go
//                                            gives the error of evaluation "no such overload", the validator
//                                            reads the first argument, which is not a pattern
//   matches(error, string) stage-mismatch    matches({'a': dyn(1)}['k'], '('): here a compile error at the
//                                            literal; cel-go gives the error of the subject, "no such key"
//   matches(string, error) stage-mismatch    matches('(', {'a': dyn(1)}['k']): here the error of evaluation of
//                                            the pattern, "no such key"; cel-go reads the subject as the pattern
//                                            and gives a compile error
//   matches(string, string) stage-mismatch   matches('abc', '('): here a compile error at the literal; cel-go
//                                            gives an error of evaluation, since its validator of the regular
//                                            expressions of a call reads the first argument, which in the
//                                            global form is the subject, not the pattern
//   matches(string, string) we-error         matches('x', p), p being 101 parts [a-z]{1000} in a variable
//   matches(string, string) we-value         matches('(', 'a'): here false, the subject is not looked at;
//                                            cel-go reads the subject as the pattern and gives a compile error
//   matches_string(string, string) we-error  'x'.matches(p), the same p
// The pattern is 101 times (1000 instructions and 1 range) and the match, 101 102 in the size of the program,
// over the limit of 100 000 of this library and far below that of cel-go (about 3.3 million instructions), which
// gives the value false: here the call is EvalInvalidRegex (3021) at the call, "expression too large". The same
// pattern as a literal is the compile error InvalidRegex (2009) at the literal. The boundary of this shape
// is between 99 parts (99 100, accepted) and 100 parts (100 101, refused).
[Trait("Requirement", "R-6")]
[Trait("Requirement", "F-6")]
public sealed class MatchesKnownDifferenceTests
{
    private const string TooLarge = "invalid regular expression at offset 0: expression too large";
    private const string MissingParen = "invalid regular expression at offset 0: missing closing )";

    private static readonly string[] Signatures =
    [
        "matches(bool, string) stage-mismatch",
        "matches(bytes, string) stage-mismatch",
        "matches(double, string) stage-mismatch",
        "matches(error, string) stage-mismatch",
        "matches(int, string) stage-mismatch",
        "matches(list, string) stage-mismatch",
        "matches(map, string) stage-mismatch",
        "matches(null_type, string) stage-mismatch",
        "matches(string, error) stage-mismatch",
        "matches(string, string) stage-mismatch",
        "matches(string, string) we-error",
        "matches(string, string) we-value",
        "matches(type, string) stage-mismatch",
        "matches(uint, string) stage-mismatch",
        "matches_string(string, string) we-error",
    ];

    private static readonly (string Signature, string Expression, string Expected)[] Entries =
    [
        ("matches(bool, string) stage-mismatch", "matches(dyn(true), '(')", "compile InvalidRegex 19: " + MissingParen),
        ("matches(bytes, string) stage-mismatch", "matches(dyn(b'a'), '(')", "compile InvalidRegex 19: " + MissingParen),
        ("matches(double, string) stage-mismatch", "matches(dyn(1.5), '(')", "compile InvalidRegex 18: " + MissingParen),
        ("matches(error, string) stage-mismatch", "matches({'a': dyn(1)}['k'], '(')", "compile InvalidRegex 28: " + MissingParen),
        ("matches(int, string) stage-mismatch", "matches(dyn(1), '(')", "compile InvalidRegex 16: " + MissingParen),
        ("matches(list, string) stage-mismatch", "matches(dyn([1]), '(')", "compile InvalidRegex 18: " + MissingParen),
        ("matches(map, string) stage-mismatch", "matches(dyn({'a': 1}), '(')", "compile InvalidRegex 23: " + MissingParen),
        ("matches(null_type, string) stage-mismatch", "matches(dyn(null), '(')", "compile InvalidRegex 19: " + MissingParen),
        ("matches(string, error) stage-mismatch", "matches('(', {'a': dyn(1)}['k'])", "error NoSuchKey 26: no such key: \"k\""),
        ("matches(string, string) stage-mismatch", "matches('abc', '(')", "compile InvalidRegex 15: " + MissingParen),
        ("matches(string, string) we-error", "matches('x', p)", "error EvalInvalidRegex 7: " + TooLarge),
        ("matches(string, string) we-value", "matches('(', 'a')", "bool false"),
        ("matches(type, string) stage-mismatch", "matches(dyn(int), '(')", "compile InvalidRegex 18: " + MissingParen),
        ("matches(uint, string) stage-mismatch", "matches(dyn(1u), '(')", "compile InvalidRegex 17: " + MissingParen),
        ("matches_string(string, string) we-error", "'x'.matches(p)", "error EvalInvalidRegex 11: " + TooLarge),
    ];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryEntry_IsTheOutcomeOfTheRegister()
    {
        CelBindings bindings = new CelBindings().Add("p", Parts(101));
        var failures = new List<string>();
        foreach ((string signature, string expression, string expected) in Entries)
        {
            string actual = MatchesRows.Outcome(environment, expression, bindings);
            if (actual != expected)
            {
                failures.Add(signature + ": " + expression + " (actual: " + actual + ")");
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void EveryEntryOfTheRegister_HasAnExample()
    {
        Assert.Equal(Signatures.Order(System.StringComparer.Ordinal), Entries.Select(entry => entry.Signature).Order(System.StringComparer.Ordinal));
    }

    [Fact]
    public void SamePatternAsALiteral_IsACompileErrorAtTheLiteral()
    {
        string literal = MatchesRows.PatternLiteral(Parts(101));

        Assert.Equal("compile InvalidRegex 13: " + TooLarge, MatchesRows.Outcome(environment, "matches('x', " + literal + ")"));
        Assert.Equal("compile InvalidRegex 12: " + TooLarge, MatchesRows.Outcome(environment, "'x'.matches(" + literal + ")"));
    }

    [Fact]
    public void Parts99AreTheValueFalse_AndParts100AreRefused()
    {
        CelBindings accepted = new CelBindings().Add("p", Parts(99));
        CelBindings refused = new CelBindings().Add("p", Parts(100));

        Assert.Equal("bool false", MatchesRows.Outcome(environment, "matches('x', p)", accepted));
        Assert.Equal("bool false", MatchesRows.Outcome(environment, "'x'.matches(p)", accepted));
        Assert.Equal("error EvalInvalidRegex 7: " + TooLarge, MatchesRows.Outcome(environment, "matches('x', p)", refused));
        Assert.Equal("error EvalInvalidRegex 11: " + TooLarge, MatchesRows.Outcome(environment, "'x'.matches(p)", refused));
    }

    private static string Parts(int count) => string.Concat(Enumerable.Repeat("[a-z]{1000}", count));
}
