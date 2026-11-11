using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// What matches takes as a character. The unit of matching is the Unicode character, a code point, though a
// string of .NET is UTF-16: a pair of surrogates is one character (a supplementary character is one for ".",
// for a class and for "{n}"), and a letter with a combining mark is two (no normalization: the composed é
// and e followed by U+0301 are different texts). A lone surrogate in the subject, a high one not followed by
// a low one or a low one not preceded by a high one, is read as the character U+FFFD, one for each, as Go
// reads a byte of invalid UTF-8; so "." matches it, it is in [^a-z], and a pattern that names U+D800 does not
// match it. A lone surrogate in a pattern is an error of the pattern, as invalid UTF-8 is for Go, at the
// place of the surrogate: such a pattern comes only from a variable, a literal of CEL cannot hold one, so it is
// EvalInvalidRegex; its text is the library's own. The rows are checked in four expressions, see MatchesRows;
// the lone surrogates are in the arrays below, which give the subject in a variable.
// The values of the rows are those of regexp.MatchString of Go 1.27.3.
[Trait("Requirement", "F-6")]
[Trait("Requirement", "R-5")]
public sealed class MatchesInputTests
{
    private const string Rows = """
        "\U0001f600" | ^.$ | true
        "é" | ^.$ | true
        "e\u0301" | ^.$ | false
        "" | ^.$ | false
        "\U0001f600" | ^..$ | false
        "e\u0301" | ^..$ | true
        "ab" | ^..$ | true
        "a" | ^..$ | false
        "\U0001f600" | ^\x{1F600}$ | true
        "\U0001f601" | ^\x{1F600}$ | false
        "\U0001f600" | ^[\x{1F600}-\x{1F64F}]$ | true
        "\U0001f610" | ^[\x{1F600}-\x{1F64F}]$ | true
        "A" | ^[\x{1F600}-\x{1F64F}]$ | false
        "\U0001f650" | ^[\x{1F600}-\x{1F64F}]$ | false
        "\U0001f600" | ^\p{So}$ | true
        "a" | ^\p{So}$ | false
        "\U0001d49c" | ^\pL$ | true
        "\U0001f600" | ^\pL$ | false
        "\U0001d49c" | ^\w$ | false
        "a" | ^\w$ | true
        "\U0001f600" | ^[^a]$ | true
        "a" | ^[^a]$ | false
        "a\U0001f600b" | ^a.b$ | true
        "a\u0301b" | ^a.b$ | true
        "ab" | ^a.b$ | false
        "e\u0301" | ^e\x{301}$ | true
        "é" | ^e\x{301}$ | false
        "e\u0301" | ^é$ | false
        "é" | ^é$ | true
        "\U0001f600\U0001f600" | \x{1F600}+ | true
        "\U0001f601" | \x{1F600}+ | false
        """;

    // A lone surrogate cannot be an argument of an attribute, so the cases are in arrays.
    private static readonly (string Subject, string Pattern, bool Expected)[] LoneSurrogateInSubject =
    [
        ("a\uD800b", "^a.b$", true),
        ("a\uD800b", "^a..b$", false),
        ("a\uD800b", "^a\\x{FFFD}b$", true),
        ("a\uD800b", "^a[^a-z]b$", true),
        ("a\uD800b", "^a\\p{So}b$", true),
        ("a\uD800b", "\\x{D800}", false),
        ("\uD800", "^.$", true),
        ("\uDC00", "^\\x{FFFD}$", true),
        ("\uDC00\uD800", "^..$", true),
        ("\uDC00\uD800", "^.$", false),
        ("\uD800a", "^\\x{FFFD}a$", true),
        ("😀", "^.$", true),
        ("😀", "^\\x{1F600}$", true),
        ("\uD83D😀", "^.\\x{1F600}$", true),
        ("😀\uDE00", "^\\x{1F600}.$", true),
    ];

    private static readonly (string Pattern, int Offset)[] LoneSurrogateInPattern =
    [
        ("a\uD800b", 1),
        ("\uDC00", 0),
        ("[a\uD800]", 2),
        ("ab\uD83D", 2),
    ];

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_GivesItsValueInAllFourForms()
    {
        Assert.Empty(MatchesRows.CheckValues(environment, Rows));
    }

    [Fact]
    public void LoneSurrogateInTheSubject_IsOneReplacementCharacter()
    {
        var failures = new List<string>();
        foreach ((string subject, string pattern, bool expected) in LoneSurrogateInSubject)
        {
            CelBindings bindings = new CelBindings().Add("s", subject).Add("p", pattern);
            string value = expected ? "bool true" : "bool false";
            foreach (string expression in new[] { "matches(s, p)", "s.matches(" + MatchesRows.PatternLiteral(pattern) + ")" })
            {
                string actual = MatchesRows.Outcome(environment, expression, bindings);
                if (actual != value)
                {
                    failures.Add(pattern + " in " + expression + " (actual: " + actual + ")");
                }
            }
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void LoneSurrogateInThePattern_IsEvalInvalidRegexAtItsOffset()
    {
        foreach ((string pattern, int offset) in LoneSurrogateInPattern)
        {
            CelBindings bindings = new CelBindings().Add("s", "x").Add("p", pattern);
            string prefix = "error EvalInvalidRegex 7: invalid regular expression at offset " + offset.ToString(CultureInfo.InvariantCulture) + ": ";

            Assert.StartsWith(prefix, MatchesRows.Outcome(environment, "matches(s, p)", bindings), StringComparison.Ordinal);
        }
    }
}
