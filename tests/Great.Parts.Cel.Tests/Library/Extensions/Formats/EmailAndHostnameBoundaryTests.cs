using System.Linq;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Formats;

// The boundaries of isEmail and isHostname that the examples of the set do not cover: the lengths of names and labels at 63, 64, 253 and
// 254, the dots, the hyphens, the characters of the local part, and the units that are no ASCII. The values come from the grammar of the
// HTML standard for e-mail and from the rules of a host name, not from the scanners. Characters outside ASCII are written as numbers.
[Trait("Requirement", "F-28")]
public sealed class EmailAndHostnameBoundaryTests
{
    private const string AllLocalCharacters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!#$%&'*+-/=?^_`{|}~";

    [Theory]
    [InlineData("foo.bar@example.com", true)]
    [InlineData(".@example.com", true)]
    [InlineData("...@example.com", true)]
    [InlineData("foo..bar@example.com", true)]
    [InlineData(AllLocalCharacters + "@example.com", true)]
    [InlineData("a@b", true)]
    [InlineData("a@b-c", true)]
    [InlineData("foo@a-b.a--b", true)]
    [InlineData("foo@0.1.2.3.4.5.6.7.8.9", true)]
    [InlineData("foo@1", true)]
    [InlineData("foo@a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q.r.s.t.u.v.w.x.y.z.A.B.C.D.E.F.G.H.I.J.K.L.M.N.O.P.Q.R.S.T.U.V.W.X.Y.Z", true)]
    [InlineData("foo@abcdefghijklmnopqrstuvwxyz-ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", true)]
    [InlineData("foo@example.com.", false)]
    [InlineData("foo@.example.com", false)]
    [InlineData("foo@example..com", false)]
    [InlineData("foo@-a", false)]
    [InlineData("foo@a-", false)]
    [InlineData("foo@a.-b", false)]
    [InlineData("foo@a-.b", false)]
    [InlineData("foo@", false)]
    [InlineData("@", false)]
    [InlineData("foo", false)]
    [InlineData("foo@bar@example.com", false)]
    [InlineData("foo bar@example.com", false)]
    [InlineData("foo@ example.com", false)]
    [InlineData("foo@exa mple.com", false)]
    [InlineData("foo,bar@example.com", false)]
    [InlineData("foo;bar@example.com", false)]
    [InlineData("foo:bar@example.com", false)]
    [InlineData("foo(bar)@example.com", false)]
    [InlineData("foo[bar]@example.com", false)]
    [InlineData("foo\\bar@example.com", false)]
    [InlineData("foo<bar>@example.com", false)]
    [InlineData("foo\"bar@example.com", false)]
    [InlineData("\"foo bar\"@example.com", false)]
    [InlineData("\"foo..bar\"@example.com", false)]
    [InlineData("postmaster@[123.123.123.123]", false)]
    [InlineData("John Doe <john@example.com>", false)]
    [InlineData("foo@example.com (comment)", false)]
    [InlineData("\nfoo@example.com", false)]
    [InlineData("foo@example.com\n", false)]
    [InlineData("foo\U000000B5@example.com", false)]
    [InlineData("foo@exa\U000000B5mple.com", false)]
    [InlineData("foo@\U00004F60\U0000597D.com", false)]
    [InlineData("\U0001F600@example.com", false)]
    public static void IsEmail_OnTheGrammarOfTheHtmlStandard(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Email(text));
    }

    [Fact]
    public static void IsEmail_LocalPartHasNoLengthLimit_AndLabelsAreAtMost63()
    {
        Assert.True(FormatScanners.Email(new string('a', 64) + "@example.com"));
        Assert.True(FormatScanners.Email(new string('a', 65) + "@example.com"));
        Assert.True(FormatScanners.Email(new string('a', 5000) + "@example.com"));
        Assert.True(FormatScanners.Email("foo@" + new string('a', 63) + ".com"));
        Assert.False(FormatScanners.Email("foo@" + new string('a', 64) + ".com"));
        Assert.True(FormatScanners.Email("foo@com." + new string('1', 63)));
        Assert.False(FormatScanners.Email("foo@com." + new string('1', 64)));
        Assert.True(FormatScanners.Email("foo@" + new string('a', 63) + "." + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 63)));
    }

    [Theory]
    [InlineData("example.com", true)]
    [InlineData("a", true)]
    [InlineData("1a", true)]
    [InlineData("a1", true)]
    [InlineData("a.b-c.d", true)]
    [InlineData("a.1a", true)]
    [InlineData("1.2.3.a", true)]
    [InlineData("a.b.", true)]
    [InlineData("a-b", true)]
    [InlineData("a--b", true)]
    [InlineData("0.1.2.3.4.5.6.7.8.9.com", true)]
    [InlineData("a.a0.a1.a2.a3.a4.a5.a6.a7.a8.a9", true)]
    [InlineData("0.1.2.3.4.5.6.7.8.9.0a.1a.2a.3a.4a.5a.6a.7a.8a.9a", true)]
    [InlineData("abcdefghijklmnopqrstuvwxyz-ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", true)]
    [InlineData("1.2.3.4", false)]
    [InlineData("example.123", false)]
    [InlineData("123", false)]
    [InlineData("123.", false)]
    [InlineData("a.123", false)]
    [InlineData("-", false)]
    [InlineData("a--", false)]
    [InlineData("--a", false)]
    [InlineData("a.-b", false)]
    [InlineData("a-.b", false)]
    [InlineData(".", false)]
    [InlineData("..", false)]
    [InlineData("a..", false)]
    [InlineData("a.b..", false)]
    [InlineData(".a.", false)]
    [InlineData("a b", false)]
    [InlineData("a\n", false)]
    [InlineData("a.b ", false)]
    [InlineData("a@b", false)]
    [InlineData("a:80", false)]
    [InlineData("a/b", false)]
    [InlineData("a_b", false)]
    [InlineData("a%41", false)]
    [InlineData("\U000000E9.com", false)]
    [InlineData("a.\U000000E9", false)]
    [InlineData("\U0001F600", false)]
    public static void IsHostname_OnTheRulesOfAHostName(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Hostname(text));
    }

    [Fact]
    public static void IsHostname_LabelsAreAtMost63_InTheMiddleAndAtTheEnd()
    {
        string label63 = new('a', 63);
        string label64 = new('a', 64);

        Assert.True(FormatScanners.Hostname(label63));
        Assert.False(FormatScanners.Hostname(label64));
        Assert.True(FormatScanners.Hostname("x." + label63 + ".y"));
        Assert.False(FormatScanners.Hostname("x." + label64 + ".y"));
        Assert.True(FormatScanners.Hostname("x." + label63));
        Assert.False(FormatScanners.Hostname("x." + label64));
        Assert.True(FormatScanners.Hostname(label63 + "."));
        Assert.False(FormatScanners.Hostname(label64 + "."));
        Assert.True(FormatScanners.Hostname(new string('1', 62) + "a"));
        Assert.False(FormatScanners.Hostname(new string('1', 63)));
        Assert.False(FormatScanners.Hostname("a." + new string('1', 63)));
        Assert.False(FormatScanners.Hostname("a." + new string('1', 64)));
    }

    [Fact]
    public static void IsHostname_NameIsAtMost253_WithoutTheTrailingDot()
    {
        // 63 + 1 + 63 + 1 + 63 + 1 + 61 = 253 characters, and one more in the last label makes 254.
        string name253 = new string('a', 63) + "." + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 61);
        string name254 = new string('a', 63) + "." + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 62);

        Assert.Equal(253, name253.Length);
        Assert.Equal(254, name254.Length);
        Assert.True(FormatScanners.Hostname(name253));
        Assert.True(FormatScanners.Hostname(name253 + "."));
        Assert.False(FormatScanners.Hostname(name254));
        Assert.False(FormatScanners.Hostname(name254 + "."));
        Assert.False(FormatScanners.Hostname(name253 + ".."));
    }

    [Fact]
    public static void IsHostname_ManyShortLabels_AreAName_UpToTheLimitOfTheLength()
    {
        // 127 labels of one character and 126 dots are 253 characters; 128 labels are 255.
        Assert.True(FormatScanners.Hostname(string.Join('.', Enumerable.Repeat("a", 127))));
        Assert.False(FormatScanners.Hostname(string.Join('.', Enumerable.Repeat("a", 128))));
    }

    // A NUL, a lone surrogate, a surrogate pair and the other units that are in no ASCII class: both functions refuse them wherever they
    // stand.
    [Fact]
    public static void UnitsThatAreNoAsciiCharacters_AreRefused_WhereverTheyStand()
    {
        string[] units = ["\0", "\ud800", "\udc00", "\U0001F600", "\U00000085", "\U00002028", "\U000000A0", "\U0000007F", "\U0000001F"];
        foreach (string unit in units)
        {
            Assert.False(FormatScanners.Email(unit + "foo@example.com"));
            Assert.False(FormatScanners.Email("foo" + unit + "@example.com"));
            Assert.False(FormatScanners.Email("foo@example.com" + unit));
            Assert.False(FormatScanners.Email("foo@exa" + unit + "mple.com"));
            Assert.False(FormatScanners.Hostname(unit + "example.com"));
            Assert.False(FormatScanners.Hostname("example" + unit + ".com"));
            Assert.False(FormatScanners.Hostname("example.com" + unit));
        }
    }
}
