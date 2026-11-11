using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The syntax of RE2 as matches reads it: the forms that regexp/syntax of Go 1.27.3 reads with the flags
// syntax.Perl, and what each gives on the subjects of the row. matches asks only whether some part of the
// subject matches the pattern, so a row has one value, true or false. The values are those of
// regexp.MatchString of Go 1.27.3 (the oracle of the differential run takes the same).
//   concatenation, alternation (an empty branch matches the empty text), the repetitions * + ? {n} {n,}
//   {n,m} and their lazy forms (the value does not tell lazy from greedy: it is only whether there is a
//   match, so the lazy forms are rows of their own for being accepted);
//   a "{" that does not begin a repetition {n}, {n,} or {n,m} is the character "{": a{, a{,5}, a{x};
//   . does not match "\n" unless the flag s is set; ^ and $ are the begin and the end of the text unless
//   the flag m is set, and $ is not before a final "\n"; \A, \z, \b and \B; the flags i m s U, set and
//   cleared (?i), (?-i), (?i-s), with a group (?i:a) and with the scope of a flag set in a group, which
//   ends with it; groups (a), (?:a), (?P<name>a) and (?<name>a) (the groups hold no submatch: only the
//   value is asked); the escapes \x41, \x{41}, \x{1F600}, \101, \0, \a \f \t \n \r \v, the escape of a
//   punctuation character \. \* \_, and \Q...\E, which ends at the end of the pattern if there is no \E;
//   the classes [a-c], [^a-c], with a "]" first, "-" last or escaped, and the escapes in a class.
// The last four rows are forms that the RE2 syntax page lists as NOT SUPPORTED because they are Vim
// forms, and that Go reads as plain text: x{}, x{-}, x= and %(x) are the characters themselves, and \zs is
// \z followed by the character s, which never matches. They are read as Go reads them; cel-go gives the
// same values, so they are not entries of the register. The forms that Go refuses are in
// MatchesRejectionTests.
// Each row is checked in four expressions, see MatchesRows.
[Trait("Requirement", "F-6")]
public sealed class MatchesSyntaxTests
{
    private const string Rows = """
        "xabcx" | abc | true
        "xabdx" | abc | false
        "ABC" | abc | false
        "" | abc | false
        "xxbxx" | a|b | true
        "xxcxx" | a|b | false
        "" | a|b | false
        "a" | ^(?:a|b)$ | true
        "b" | ^(?:a|b)$ | true
        "ab" | ^(?:a|b)$ | false
        "" | ^(?:a|b)$ | false
        "" | a| | true
        "zzz" | a| | true
        "ac" | ab*c | true
        "abbc" | ab*c | true
        "abxc" | ab*c | false
        "ac" | ab+c | false
        "abc" | ab+c | true
        "abbbc" | ab+c | true
        "ac" | ab?c | true
        "abc" | ab?c | true
        "abbc" | ab?c | false
        "a" | ^a{2}$ | false
        "aa" | ^a{2}$ | true
        "aaa" | ^a{2}$ | false
        "a" | ^a{2,}$ | false
        "aa" | ^a{2,}$ | true
        "aaaa" | ^a{2,}$ | true
        "a" | ^a{2,3}$ | false
        "aa" | ^a{2,3}$ | true
        "aaa" | ^a{2,3}$ | true
        "aaaa" | ^a{2,3}$ | false
        "" | ^a{0}$ | true
        "a" | ^a{0}$ | false
        "" | ^a{0,1}$ | true
        "a" | ^a{0,1}$ | true
        "aa" | ^a{0,1}$ | false
        "" | ^a*?$ | true
        "aaa" | ^a*?$ | true
        "" | ^a+?$ | false
        "a" | ^a+?$ | true
        "aa" | ^a+?$ | true
        "" | ^a??$ | true
        "a" | ^a??$ | true
        "aa" | ^a??$ | false
        "a" | ^a{2,3}?$ | false
        "aa" | ^a{2,3}?$ | true
        "aaa" | ^a{2,3}?$ | true
        "aaaa" | ^a{2,3}?$ | false
        "a" | ^a{2}?$ | false
        "aa" | ^a{2}?$ | true
        "aaa" | ^a{2}?$ | false
        "a" | ^a{2,}?$ | false
        "aa" | ^a{2,}?$ | true
        "aaaa" | ^a{2,}?$ | true
        "" | (?U)^a+$ | false
        "a" | (?U)^a+$ | true
        "aaa" | (?U)^a+$ | true
        "" | ^(?:ab)*$ | true
        "ab" | ^(?:ab)*$ | true
        "abab" | ^(?:ab)*$ | true
        "aba" | ^(?:ab)*$ | false
        "a{" | a{ | true
        "a" | a{ | false
        "a{,5}" | a{,5} | true
        "aaaaa" | a{,5} | false
        "a{x}" | a{x} | true
        "a" | a{x} | false
        "a{1" | a{1 | true
        "a" | a{1 | false
        "a{1," | a{1, | true
        "a" | a{1, | false
        "{" | { | true
        "a" | { | false
        "a" | ^.$ | true
        "\n" | ^.$ | false
        "" | ^.$ | false
        "ab" | ^.$ | false
        "\n" | (?s)^.$ | true
        "a" | (?s)^.$ | true
        "" | (?s)^.$ | false
        "abc" | a.c | true
        "a\nc" | a.c | false
        "abc" | (?s)a.c | true
        "a\nc" | (?s)a.c | true
        "a\nb" | ^b | false
        "b" | ^b | true
        "a\nb" | (?m)^b | true
        "ab" | (?m)^b | false
        "b" | (?m)^b | true
        "a\nb" | a$ | false
        "a" | a$ | true
        "a\n" | a$ | false
        "a\nb" | (?m)a$ | true
        "a\n" | (?m)a$ | true
        "ab" | (?m)a$ | false
        "b" | \Ab | true
        "ab" | \Ab | false
        "a" | a\z | true
        "a\n" | a\z | false
        "ab" | a\z | false
        "" | ^$ | true
        "\n" | ^$ | false
        "\n" | (?m)^$ | true
        "a" | (?m)^$ | false
        "" | (?m)^$ | true
        "a foo b" | \bfoo\b | true
        "afoo" | \bfoo\b | false
        "foo" | \bfoo\b | true
        "foo_" | \bfoo\b | false
        "foo-bar" | \bfoo\b | true
        "afoo" | \Bfoo | true
        "foo" | \Bfoo | false
        "a" | ^*a | true
        "ba" | ^*a | true
        "ABC" | (?i)abc | true
        "aBc" | (?i)abc | true
        "abd" | (?i)abc | false
        "Ab" | (?i:a)b | true
        "AB" | (?i:a)b | false
        "ab" | (?i:a)b | true
        "Ab" | (?i)a(?-i)b | true
        "AB" | (?i)a(?-i)b | false
        "aB" | (?i)a(?-i)b | false
        "ab" | (?i)a(?-i)b | true
        "aB" | a(?i)b | true
        "AB" | a(?i)b | false
        "ab" | a(?i)b | true
        "A" | (?i)a|b | true
        "B" | (?i)a|b | true
        "c" | (?i)a|b | false
        "Ab" | (?:(?i)a)b | true
        "AB" | (?:(?i)a)b | false
        "a\nB" | (?im)^b$ | true
        "aB" | (?im)^b$ | false
        "A\n" | (?i-s)a. | false
        "A\u0001" | (?i-s)a. | true
        "a\n" | (?i-s)a. | false
        "a\n" | (?s-i)a. | true
        "A\n" | (?s-i)a. | false
        "ab" | (a)(b) | true
        "ba" | (a)(b) | false
        "ab" | (?P<first>a)(?P<second>b) | true
        "ba" | (?P<first>a)(?P<second>b) | false
        "ab" | (?<first>a)b | true
        "ba" | (?<first>a)b | false
        "ab" | (?:a)(?:b) | true
        "ba" | (?:a)(?:b) | false
        "abc" | ((a)(b))c | true
        "acb" | ((a)(b))c | false
        "abc" | (a|b)*c | true
        "c" | (a|b)*c | true
        "d" | (a|b)*c | false
        "A" | \x41 | true
        "a" | \x41 | false
        "A" | \x{41} | true
        "a" | \x{41} | false
        "\U0001f600" | \x{1F600} | true
        "a" | \x{1F600} | false
        "A" | \101 | true
        "a" | \101 | false
        "\0" | \0 | true
        "0" | \0 | false
        "\u0007" | \a | true
        "a" | \a | false
        "\f" | \f | true
        "f" | \f | false
        "\t" | \t | true
        "t" | \t | false
        "\n" | \n | true
        "n" | \n | false
        "\r" | \r | true
        "r" | \r | false
        "\v" | \v | true
        "v" | \v | false
        "." | \. | true
        "a" | \. | false
        "a*" | a\* | true
        "aa" | a\* | false
        "_" | \_ | true
        "a" | \_ | false
        "a.b" | \Qa.b\E | true
        "axb" | \Qa.b\E | false
        "a.b" | \Qa.b | true
        "axb" | \Qa.b | false
        "" | \Q\E | true
        "a" | \Q\E | true
        "a+b" | a\Q+\Eb | true
        "ab" | a\Q+\Eb | false
        "a" | ^[a-c]$ | true
        "b" | ^[a-c]$ | true
        "d" | ^[a-c]$ | false
        "d" | ^[^a-c]$ | true
        "a" | ^[^a-c]$ | false
        "\n" | ^[^a-c]$ | true
        "]" | ^[]a]$ | true
        "a" | ^[]a]$ | true
        "b" | ^[]a]$ | false
        "-" | ^[a-]$ | true
        "a" | ^[a-]$ | true
        "b" | ^[a-]$ | false
        "-" | ^[a\-z]$ | true
        "b" | ^[a\-z]$ | false
        "z" | ^[a\-z]$ | true
        "]" | ^[\]]$ | true
        "a" | ^[\]]$ | false
        "B" | ^[\x41-\x43]$ | true
        "D" | ^[\x41-\x43]$ | false
        "\n" | ^[\n]$ | true
        "n" | ^[\n]$ | false
        "x{}" | x{} | true
        "x" | x{} | false
        "x{-}" | x{-} | true
        "x" | x{-} | false
        "x=" | x= | true
        "x" | x= | false
        "%(x)" | %(x) | false
        "%x" | %(x) | true
        "s" | \zs | false
        "zs" | \zs | false
        "" | \zs | false
        """;

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
}
