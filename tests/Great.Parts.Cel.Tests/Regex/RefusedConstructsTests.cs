using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The refusals of the parser, construct by construct. The syntax page of RE2 marks a number of constructs NOT SUPPORTED; a pattern that
// has one is an error of the library where Go gives an error: the offset of the fragment that Go names, in UTF-16 code units, and the
// reason, which is the code of the error of Go (for "invalid or unsupported Perl syntax" the library writes "unsupported Perl syntax:"
// and the fragment). Every row below was checked against syntax.Parse with the flags syntax.Perl of Go 1.27.3: the code, the fragment
// that the error names, and the offset of that fragment in the pattern. The forms that Go reads as text (x{}, x{-}, x=, %(x), \zs) are
// not errors and are with the trees in PatternParserTests. The examples of the library tests check the text of the error of
// "matches"; these tests check the parser for each construct, which the examples cannot reach one by one.
public sealed class RefusedConstructsTests
{
    // The assertions and groups of Perl and PCRE that the syntax page of RE2 marks NOT SUPPORTED and that Go refuses: the offset is
    // that of the "(", and the fragment of the Perl error is the text that Go names.
    [Theory]
    [InlineData("a(?=b)", 1, "unsupported Perl syntax: (?=")]
    [InlineData("a(?!b)", 1, "unsupported Perl syntax: (?!")]
    [InlineData("a(?<=b)", 1, "invalid named capture")]
    [InlineData("a(?<!b)", 1, "invalid named capture")]
    [InlineData("a(?>b)", 1, "unsupported Perl syntax: (?>")]
    [InlineData("a(?|b|c)", 1, "unsupported Perl syntax: (?|")]
    [InlineData("a(?#c)", 1, "unsupported Perl syntax: (?#")]
    [InlineData("a(?'n'b)", 1, "unsupported Perl syntax: (?'")]
    [InlineData("a(?{x})", 1, "unsupported Perl syntax: (?{")]
    [InlineData("a(??{x})", 1, "unsupported Perl syntax: (??")]
    [InlineData("a(?C)", 1, "unsupported Perl syntax: (?C")]
    [InlineData("a(?(1)b|c)", 1, "unsupported Perl syntax: (?(")]
    [InlineData("a(?R)", 1, "unsupported Perl syntax: (?R")]
    [InlineData("a(?1)", 1, "unsupported Perl syntax: (?1")]
    [InlineData("a(?+1)", 1, "unsupported Perl syntax: (?+")]
    [InlineData("a(?-1)", 1, "unsupported Perl syntax: (?-1")]
    [InlineData("a(?&n)", 1, "unsupported Perl syntax: (?&")]
    [InlineData("a(?P>n)", 1, "unsupported Perl syntax: (?P")]
    [InlineData("a(?P=n)", 1, "unsupported Perl syntax: (?P")]
    [InlineData("a(?i)(?=b)", 5, "unsupported Perl syntax: (?=")]
    [InlineData("a(?'n", 1, "unsupported Perl syntax: (?'")]
    public static void PerlAndPcreGroups_AreRefused(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The control verbs of PCRE read as a repetition with nothing before it: the error is at the "*".
    [Theory]
    [InlineData("(*ACCEPT)", 1, "missing argument to repetition operator")]
    [InlineData("a(*COMMIT)", 2, "missing argument to repetition operator")]
    [InlineData("a(*FAIL)", 2, "missing argument to repetition operator")]
    [InlineData("a(*SKIP)", 2, "missing argument to repetition operator")]
    [InlineData("(*ANYCRLF)a", 1, "missing argument to repetition operator")]
    [InlineData("a(*BSR_UNICODE)", 2, "missing argument to repetition operator")]
    public static void ControlVerbs_AreRefusedAtTheAsterisk(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // A repetition that follows a repetition, the possessive forms among them; "a**" included. The offset is that of the first
    // character of the first operator.
    [Theory]
    [InlineData("ax*+", 2, "invalid nested repetition operator")]
    [InlineData("ax++", 2, "invalid nested repetition operator")]
    [InlineData("ax?+", 2, "invalid nested repetition operator")]
    [InlineData("ax{2,3}+", 2, "invalid nested repetition operator")]
    [InlineData("ba**", 2, "invalid nested repetition operator")]
    [InlineData("ba+*", 2, "invalid nested repetition operator")]
    [InlineData("ba?*", 2, "invalid nested repetition operator")]
    [InlineData("ba*??", 2, "invalid nested repetition operator")]
    [InlineData("ba{2}*", 2, "invalid nested repetition operator")]
    [InlineData("ba*{2}", 2, "invalid nested repetition operator")]
    [InlineData("a{1000}{2}", 1, "invalid nested repetition operator")]
    [InlineData("a**b", 1, "invalid nested repetition operator")]
    [InlineData("\\pN{2}**", 3, "invalid nested repetition operator")]
    public static void RepetitionOnARepetition_IsRefusedAtTheFirstOperator(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The backreferences of RE2 and PCRE, a number from 1 to 9 and the named forms: escapes that Go does not have.
    [Theory]
    [InlineData("a\\1b", 1, "invalid escape sequence")]
    [InlineData("a(b)\\2", 4, "invalid escape sequence")]
    [InlineData("a\\3", 1, "invalid escape sequence")]
    [InlineData("a\\4", 1, "invalid escape sequence")]
    [InlineData("a\\5", 1, "invalid escape sequence")]
    [InlineData("a\\6", 1, "invalid escape sequence")]
    [InlineData("a\\7", 1, "invalid escape sequence")]
    [InlineData("a\\8", 1, "invalid escape sequence")]
    [InlineData("a\\9", 1, "invalid escape sequence")]
    [InlineData("a\\g1", 1, "invalid escape sequence")]
    [InlineData("a\\g{n}", 1, "invalid escape sequence")]
    [InlineData("a\\g<n>", 1, "invalid escape sequence")]
    [InlineData("a\\k<n>", 1, "invalid escape sequence")]
    [InlineData("a\\k'n'", 1, "invalid escape sequence")]
    public static void Backreferences_AreRefused(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The escapes that RE2 or Perl has and Go has not, the one of the single byte among them, in the text and inside a class, and the
    // ones that no escape of Go uses.
    [Theory]
    [InlineData("a\\Cb", 1, "invalid escape sequence")]
    [InlineData("a\\Zb", 1, "invalid escape sequence")]
    [InlineData("a\\Gb", 1, "invalid escape sequence")]
    [InlineData("a\\Kb", 1, "invalid escape sequence")]
    [InlineData("a\\Xb", 1, "invalid escape sequence")]
    [InlineData("a\\Rb", 1, "invalid escape sequence")]
    [InlineData("a\\hb", 1, "invalid escape sequence")]
    [InlineData("a\\Hb", 1, "invalid escape sequence")]
    [InlineData("a\\Vb", 1, "invalid escape sequence")]
    [InlineData("a\\N{n}", 1, "invalid escape sequence")]
    [InlineData("a\\cXb", 1, "invalid escape sequence")]
    [InlineData("a\\eb", 1, "invalid escape sequence")]
    [InlineData("a\\lb", 1, "invalid escape sequence")]
    [InlineData("a\\Lb", 1, "invalid escape sequence")]
    [InlineData("a\\ub", 1, "invalid escape sequence")]
    [InlineData("a\\Ub", 1, "invalid escape sequence")]
    [InlineData("a\\p{L&}", 1, "invalid character class range")]
    [InlineData("a[\\C]", 2, "invalid escape sequence")]
    [InlineData("\\E", 0, "invalid escape sequence")]
    [InlineData("a\\y", 1, "invalid escape sequence")]
    public static void EscapesOfPerlThatGoHasNot_AreRefused(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The error of a construct that is refused wins by its place in the text: the first one stops the parse.
    [Theory]
    [InlineData("a**(?=b)", 1, "invalid nested repetition operator")]
    [InlineData("(?=b)a**", 0, "unsupported Perl syntax: (?=")]
    [InlineData("a\\Cb\\1", 1, "invalid escape sequence")]
    [InlineData("a(*FAIL)\\C", 2, "missing argument to repetition operator")]
    public static void FirstRefusedConstructInTheText_IsTheError(string pattern, int offset, string reason)
    {
        ParseHelper.AssertRefused(pattern, offset, reason);
    }

    // The limits of a pattern, each on both sides, with the table of Go 1.27.3: a count of 1000 and the product of nested counts of
    // 1000 are accepted, one more is "invalid repeat count" at the "{" of the repetition that makes the product too large.
    [Theory]
    [InlineData("a{1000}")]
    [InlineData("a{1000,1000}")]
    [InlineData("a{0,1000}")]
    [InlineData("a{1000,}")]
    [InlineData("x(?:a{2}){500}")]
    [InlineData("x(?:a{10}){100}")]
    [InlineData("x(?:a{500}){2}")]
    [InlineData("(?:(?:a{10}){10}){10}")]
    [InlineData("x(?:a{1}){1000}")]
    [InlineData("x(?:a*){1000}")]
    [InlineData("x(?:a{0}){1000}")]
    [InlineData("x(?:a{1000})*")]
    [InlineData("x(?:a{1000}){1}")]
    [InlineData("x(?:a{1000}){0}")]
    [InlineData("x(?:a{1000}){0,1}")]
    [InlineData("x(?:a{2}){1,500}")]
    public static void CountsAtTheLimit_AreAccepted(string pattern)
    {
        using var tree = ParseHelper.Parse(pattern);

        Assert.True(tree.Size > 0);
    }

    [Theory]
    [InlineData("a{1001}", 1)]
    [InlineData("a{0,1001}", 1)]
    [InlineData("a{1001,}", 1)]
    [InlineData("a{1001,2000}", 1)]
    [InlineData("xa{2,1}", 2)]
    [InlineData("x(?:a{2}){501}", 9)]
    [InlineData("x(?:a{10}){101}", 10)]
    [InlineData("x(?:a{500}){3}", 11)]
    [InlineData("(?:(?:a{10}){10}){11}", 17)]
    [InlineData("(?:(?:a{10}){11}){10}", 17)]
    [InlineData("x(?:a{1}){1001}", 9)]
    [InlineData("x(?:a*){1001}", 7)]
    [InlineData("x(?:a{0}){1001}", 9)]
    [InlineData("x(?:a{1000}){2}", 12)]
    [InlineData("x(?:a{2}){0,501}", 9)]
    [InlineData("x(?:a{2}){501,}", 9)]
    public static void CountsOverTheLimit_AreRefusedAtTheRepetitionThatPassesIt(string pattern, int offset)
    {
        ParseHelper.AssertRefused(pattern, offset, "invalid repeat count");
    }
}
