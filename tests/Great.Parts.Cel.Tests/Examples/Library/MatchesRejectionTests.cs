using System;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The patterns that matches refuses, each with the offset in the pattern of the fragment the error is about
// and the reason where the standard fixes its text. Each row is a pattern that Go 1.27.3 refuses
// (regexp/syntax with the flags syntax.Perl); the offset is where the fragment begins that Go names in its
// error, in UTF-16 code units, and 0 for an error about the whole pattern (an unclosed "(" or "[" with
// nothing before it is the whole pattern too: "x(abc" is at 0, and "x[a" at 1, because Go names the class
// from its "[" on); a trailing backslash is at the backslash. The reasons the standard fixes are
// "missing closing )", "invalid repeat count" and "unsupported Perl syntax: (?="; the other rows end with "*":
// the place and the code are fixed, the reason is the library's own text.
// Each row is checked in four expressions, see MatchesRows: a constant pattern gives InvalidRegex at the
// literal, a pattern in a variable gives EvalInvalidRegex at the call, and the message is the same.
//   NotSupported — every construct that the RE2 syntax page marks NOT SUPPORTED and Go refuses: the
//     assertions of look-around, the possessive and atomic forms, nested repetition (a** and the like), the
//     backreferences, \C, \Z, \G, \K, \X and the other escapes that RE2 does not know, the groups and calls
//     of Perl and PCRE, the control forms (*ACCEPT) and the like. The Vim forms that Go reads as text are in
//     MatchesSyntaxTests.
//   Syntax — the other errors of syntax: unclosed and unexpected brackets, a repetition without an operand,
//     a bad range of a class, a bad escape, an unknown class, a bad name of a group, bad flags. The counts of
//     a repetition and the limits are in MatchesLimitTests.
[Trait("Requirement", "F-6")]
[Trait("Requirement", "F-4")]
public sealed class MatchesRejectionTests
{
    private const string NotSupported = """
        (?=x) | 0 | unsupported Perl syntax: (?=
        (?!x) | 0 | *
        (?<=x) | 0 | *
        (?<!x) | 0 | *
        ab(?=c) | 2 | unsupported Perl syntax: (?=
        (?>x) | 0 | *
        (?'name'x) | 0 | *
        (?#text) | 0 | *
        (?|x|y|z) | 0 | *
        x*+ | 1 | *
        x++ | 1 | *
        x?+ | 1 | *
        x{2,3}+ | 1 | *
        x{2,}+ | 1 | *
        x{2}+ | 1 | *
        \C | 0 | *
        a\C | 1 | *
        a** | 1 | *
        a+* | 1 | *
        a?* | 1 | *
        a*?? | 1 | *
        a{2}* | 1 | *
        a*{2} | 1 | *
        \1 | 0 | *
        (x)\1 | 3 | *
        \9 | 0 | *
        \g1 | 0 | *
        \g{1} | 0 | *
        \g{+1} | 0 | *
        \g{-1} | 0 | *
        \g{name} | 0 | *
        \g<name> | 0 | *
        \g'name' | 0 | *
        \k<name> | 0 | *
        \k'name' | 0 | *
        (?P=name) | 0 | *
        (?P>name) | 0 | *
        \g | 0 | *
        \G | 0 | *
        \Z | 0 | *
        \cK | 0 | *
        \e | 0 | *
        \lX | 0 | *
        \ux | 0 | *
        \Lx\E | 0 | *
        \K | 0 | *
        \N{name} | 0 | *
        \R | 0 | *
        \Ux\E | 0 | *
        \X | 0 | *
        \h | 0 | *
        \H | 0 | *
        \V | 0 | *
        \i | 0 | *
        \I | 0 | *
        \k | 0 | *
        \F | 0 | *
        \o | 0 | *
        \O | 0 | *
        \l | 0 | *
        \L | 0 | *
        \u | 0 | *
        \U | 0 | *
        \c | 0 | *
        \m | 0 | *
        \M | 0 | *
        (?{code}) | 0 | *
        (??{code}) | 0 | *
        (?1) | 0 | *
        (?+1) | 0 | *
        (?-1) | 0 | *
        (?C) | 0 | *
        (?R) | 0 | *
        (?&name) | 0 | *
        (?(1)a|b) | 0 | *
        (?(1)a) | 0 | *
        (*ACCEPT) | 1 | *
        (*COMMIT) | 1 | *
        (*FAIL) | 1 | *
        (*SKIP) | 1 | *
        (*ANYCRLF) | 1 | *
        (*BSR_UNICODE) | 1 | *
        \p{L&} | 0 | *
        """;

    private const string Syntax = """
        (abc | 0 | missing closing )
        x(abc | 0 | missing closing )
        abc) | 0 | *
        a)b | 0 | *
        [a- | 0 | *
        [a | 0 | *
        x[a | 1 | *
        [] | 0 | *
        [^] | 0 | *
        [z-a] | 1 | *
        x[z-a] | 2 | *
        [a-\d] | 3 | *
        [[:foo:]] | 1 | *
        [[:alpha:] | 0 | *
        *a | 0 | *
        + | 0 | *
        ? | 0 | *
        a|* | 2 | *
        (*) | 1 | *
        x|? | 2 | *
        {1} | 0 | *
        a\ | 1 | *
        x\ | 1 | *
        \8 | 0 | *
        \x{110000} | 0 | *
        \x{zz} | 0 | *
        \xZ | 0 | *
        \x4 | 0 | *
        \y | 0 | *
        \pX | 0 | *
        \p{Foo} | 0 | *
        x\p{ | 1 | *
        \p | 0 | *
        \p{Script=Greek} | 0 | *
        \p{IsGreek} | 0 | *
        (?P<n | 0 | *
        (?P<>x) | 0 | *
        (?P<a-b>x) | 0 | *
        (?P<a b>x) | 0 | *
        (?<n | 0 | *
        (?<>x) | 0 | *
        (?i | 0 | *
        (?z) | 0 | *
        (?-) | 0 | *
        (?i-) | 0 | *
        (?--i) | 0 | *
        (?i-i-s) | 0 | *
        (? | 0 | *
        a{2,1} | 1 | invalid repeat count
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryNotSupportedConstruct_IsAnErrorOfThePattern()
    {
        Assert.Empty(MatchesRows.CheckRejections(environment, NotSupported));
    }

    [Fact]
    public void EveryOtherErrorOfSyntax_IsAnErrorOfThePattern()
    {
        Assert.Empty(MatchesRows.CheckRejections(environment, Syntax));
    }

    // The three that the standard names: \C, a** and a look-ahead.
    [Fact]
    public void ConstructsNamedByTheStandard_AreInTheTable()
    {
        string[] rows = NotSupported.Split('\n', StringSplitOptions.TrimEntries);

        Assert.Contains("\\C | 0 | *", rows);
        Assert.Contains("a** | 1 | *", rows);
        Assert.Contains("(?=x) | 0 | unsupported Perl syntax: (?=", rows);
    }
}
