using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Formats;

// The boundaries of isUri and isUriRef that the examples of the set do not cover: every part of RFC 3986 (scheme, user information, IP
// literal in brackets, registered name, port, path, query, fragment), the percent-encodings with capital and small digits, the octets that
// are not UTF-8 in a host name and in the zone of an IP literal, and the references that are relative. The values come from the grammar of
// RFC 3986 and RFC 6874, not from the scanners. Characters outside ASCII are written as numbers.
[Trait("Requirement", "F-28")]
public sealed class UriBoundaryTests
{
    [Theory]
    [InlineData("a:", true)]
    [InlineData("A://", true)]
    [InlineData("a+b-c.d:x", true)]
    [InlineData("a1:x", true)]
    [InlineData("scheme0123456789azAZ+-.:x", true)]
    [InlineData("1a:x", false)]
    [InlineData("-a:x", false)]
    [InlineData("+a:x", false)]
    [InlineData(".a:x", false)]
    [InlineData("a b:x", false)]
    [InlineData("a_b:x", false)]
    [InlineData("a%41:x", false)]
    [InlineData(":x", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData("//h", false)]
    [InlineData("/p", false)]
    [InlineData("p", false)]
    [InlineData("?q", false)]
    [InlineData("#f", false)]
    [InlineData("./a:b", false)]
    public static void IsUri_TheSchemeIsRequired(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("http://", true)]
    [InlineData("http://host", true)]
    [InlineData("http://user@host", true)]
    [InlineData("http://user:pw@host:80", true)]
    [InlineData("http://@host", true)]
    [InlineData("http://u:p:q@host", true)]
    [InlineData("http://us%65r@host", true)]
    [InlineData("http://us%65R%2f@host", true)]
    [InlineData("http://u!$&'()*+,;=-._~@host", true)]
    [InlineData("http://u@@host", false)]
    [InlineData("http://us%6@host", false)]
    [InlineData("http://us%6g@host", false)]
    [InlineData("http://us r@host", false)]
    [InlineData("http://u[@host", false)]
    [InlineData("http://u]@host", false)]
    [InlineData("http://u/@host", true)]
    [InlineData("http://u?@host", true)]
    [InlineData("http://u#@host", true)]
    [InlineData("http://host:", true)]
    [InlineData("http://host:80", true)]
    [InlineData("http://host:0", true)]
    [InlineData("http://host:99999999", true)]
    [InlineData("http://host:8a", false)]
    [InlineData("http://host:80:80", false)]
    [InlineData("http://host: 80", false)]
    [InlineData("http://host:-1", false)]
    [InlineData("http://:80", true)]
    [InlineData("http://:", true)]
    [InlineData("http://a.b.c.d:1", true)]
    [InlineData("http://256.256.256.256", true)]
    [InlineData("http://a!$&'()*+,;=b", true)]
    [InlineData("http://a%20b", true)]
    [InlineData("http://a b", false)]
    [InlineData("http://a|b", false)]
    [InlineData("http://a^b", false)]
    [InlineData("http://a\"b", false)]
    [InlineData("http://a\\b", false)]
    [InlineData("http://a<b", false)]
    [InlineData("http://a%", false)]
    [InlineData("http://a%4", false)]
    [InlineData("http://a%zz", false)]
    [InlineData("http://\U000000E9", false)]
    [InlineData("http://host/\U000000E9", false)]
    [InlineData("http://host/p?\U000000E9", false)]
    [InlineData("http://host/p#\U000000E9", false)]
    [InlineData("http://u\U000000E9@host", false)]
    public static void IsUri_Authority(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("http://[::1]", true)]
    [InlineData("http://[::1]:80", true)]
    [InlineData("http://[::1]:", true)]
    [InlineData("http://[::1]/p", true)]
    [InlineData("http://u@[::1]:80/p", true)]
    [InlineData("http://[2001:db8::1]", true)]
    [InlineData("http://[::ffff:1.2.3.4]", true)]
    [InlineData("http://[::1%251]", true)]
    [InlineData("http://[::1%25eth0]", true)]
    [InlineData("http://[::1%25a%20b]", true)]
    [InlineData("http://[::1%25-._~]", true)]
    [InlineData("http://[::1%25%c3%96]", true)]
    [InlineData("http://[::1%25%C3%96]", true)]
    [InlineData("http://[::1%25%f0%9f%98%80]", true)]
    [InlineData("http://[::1%25]", false)]
    [InlineData("http://[::1%eth0]", false)]
    [InlineData("http://[::1%2]", false)]
    [InlineData("http://[::1%25!]", false)]
    [InlineData("http://[::1%25a b]", false)]
    [InlineData("http://[::1%25%c3]", false)]
    [InlineData("http://[::1%25%c3x]", false)]
    [InlineData("http://[::1%25%ff]", false)]
    [InlineData("http://[::1%25%80]", false)]
    [InlineData("http://[::1%25a%]", false)]
    [InlineData("http://[::1%25a%4]", false)]
    [InlineData("http://[v1.a]", true)]
    [InlineData("http://[V1.a]", true)]
    [InlineData("http://[vF.x:y]", true)]
    [InlineData("http://[v1F2a.-!$&'()*+,;=._~:]", true)]
    [InlineData("http://[v.a]", false)]
    [InlineData("http://[v1.]", false)]
    [InlineData("http://[v1a]", false)]
    [InlineData("http://[vg.a]", false)]
    [InlineData("http://[v1.a b]", false)]
    [InlineData("http://[v1.a%41]", false)]
    [InlineData("http://[v1.a]:80", true)]
    [InlineData("http://[]", false)]
    [InlineData("http://[", false)]
    [InlineData("http://]", false)]
    [InlineData("http://[::1", false)]
    [InlineData("http://[::1]x", false)]
    [InlineData("http://[::1]:x", false)]
    [InlineData("http://[::1]:80:80", false)]
    [InlineData("http://[::1]]", false)]
    [InlineData("http://[[::1]]", false)]
    [InlineData("http://[1::2::3]", false)]
    [InlineData("http://[1:2:3:4:5:6:7:8:9]", false)]
    [InlineData("http://[127.0.0.1]", false)]
    [InlineData("http://[example.com]", false)]
    [InlineData("http://::1", false)]
    [InlineData("http://2001:db8::1", false)]
    public static void IsUri_IpLiterals(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("http://a%41", true)]
    [InlineData("http://a%4a", true)]
    [InlineData("http://a%4A", true)]
    [InlineData("http://a%c3%96", true)]
    [InlineData("http://a%C3%96b", true)]
    [InlineData("http://a%e2%82%ac", true)]
    [InlineData("http://a%f0%9f%98%80", true)]
    [InlineData("http://a%f4%8f%bf%bf", true)]
    [InlineData("http://a%c3", false)]
    [InlineData("http://a%c3x", false)]
    [InlineData("http://a%c3%", false)]
    [InlineData("http://a%c3%28", false)]
    [InlineData("http://a%c0%80", false)]
    [InlineData("http://a%c1%bf", false)]
    [InlineData("http://a%80", false)]
    [InlineData("http://a%bf", false)]
    [InlineData("http://a%e0%80%80", false)]
    [InlineData("http://a%e0%9f%bf", false)]
    [InlineData("http://a%ed%a0%80", false)]
    [InlineData("http://a%ed%bf%bf", false)]
    [InlineData("http://a%f0%80%80%80", false)]
    [InlineData("http://a%f0%8f%bf%bf", false)]
    [InlineData("http://a%f4%90%80%80", false)]
    [InlineData("http://a%f5%80%80%80", false)]
    [InlineData("http://a%ff", false)]
    [InlineData("http://a%e2%82", false)]
    [InlineData("http://a%e2%82x", false)]
    [InlineData("http://a%f0%9f%98", false)]
    public static void IsUri_PercentEncodedOctetsOfAHostName_AreUtf8(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("a:p%41", true)]
    [InlineData("a:p%4a%4A", true)]
    [InlineData("a:/%c3", true)]
    [InlineData("a:/%c3x", true)]
    [InlineData("a://h/%ff", true)]
    [InlineData("a:?%ff", true)]
    [InlineData("a:#%ff", true)]
    [InlineData("http://u%ff@host", true)]
    [InlineData("a:p%4", false)]
    [InlineData("a:p%4g", false)]
    [InlineData("a:p%", false)]
    [InlineData("a:?%4", false)]
    [InlineData("a:?%", false)]
    [InlineData("a:#%4", false)]
    [InlineData("a:#%g0", false)]
    public static void IsUri_PercentEncodingsOutsideTheHost_AreTwoHexDigitsAnyOctet(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("a:b", true)]
    [InlineData("a:/b", true)]
    [InlineData("a:b/c", true)]
    [InlineData("a:b:c", true)]
    [InlineData("a://h/p/q", true)]
    [InlineData("a:///p", true)]
    [InlineData("a:////p", true)]
    [InlineData("a:/p//q", true)]
    [InlineData("a:/p/", true)]
    [InlineData("a:p@q:r", true)]
    [InlineData("a:p!$&'()*+,;=-._~", true)]
    [InlineData("a:ABCxyz0129", true)]
    [InlineData("a:p q", false)]
    [InlineData("a:p\\q", false)]
    [InlineData("a:p[q", false)]
    [InlineData("a:p]q", false)]
    [InlineData("a:p{q", false)]
    [InlineData("a:p}q", false)]
    [InlineData("a:p<q", false)]
    [InlineData("a:p>q", false)]
    [InlineData("a:p\"q", false)]
    [InlineData("a:p^q", false)]
    [InlineData("a:p|q", false)]
    [InlineData("a:p`q", false)]
    [InlineData("a:p\nq", false)]
    [InlineData("a:p\tq", false)]
    [InlineData("a:p\U000000E9", false)]
    public static void IsUri_Path(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("a:?", true)]
    [InlineData("a:?a=b&c", true)]
    [InlineData("a:?/?:@", true)]
    [InlineData("a:?%41", true)]
    [InlineData("a:??", true)]
    [InlineData("a:?#", true)]
    [InlineData("a:?q#f", true)]
    [InlineData("a:#", true)]
    [InlineData("a:#f", true)]
    [InlineData("a:#?/", true)]
    [InlineData("a:#%41", true)]
    [InlineData("a:p?q?r#f/g?h", true)]
    [InlineData("a:?a b", false)]
    [InlineData("a:?a[b", false)]
    [InlineData("a:?a\"b", false)]
    [InlineData("a:#a#b", false)]
    [InlineData("a:#a b", false)]
    [InlineData("a:#a[", false)]
    [InlineData("a:#a\\", false)]
    [InlineData("a:?q#f#g", false)]
    [InlineData("a:#f?q#g", false)]
    public static void IsUri_QueryAndFragment(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.Uri(text));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("/p", true)]
    [InlineData("p", true)]
    [InlineData("p/q", true)]
    [InlineData("p/q:r", true)]
    [InlineData("./p:q", true)]
    [InlineData("../p:q", true)]
    [InlineData("/p:q", true)]
    [InlineData("/p//q", true)]
    [InlineData("?q", true)]
    [InlineData("#f", true)]
    [InlineData("?q#f", true)]
    [InlineData("//h", true)]
    [InlineData("//h/p?q#f", true)]
    [InlineData("//h:80", true)]
    [InlineData("//u@h", true)]
    [InlineData("///p", true)]
    [InlineData("//", true)]
    [InlineData("//[::1]", true)]
    [InlineData("//[::1]:80/p", true)]
    [InlineData("/%41", true)]
    [InlineData("%41", true)]
    [InlineData("p%41", true)]
    [InlineData("a:b", true)]
    [InlineData("a:/b", true)]
    [InlineData("a://h", true)]
    [InlineData("p@q", true)]
    [InlineData("p!$&'()*+,;=-._~", true)]
    [InlineData("1:2", false)]
    [InlineData(":", false)]
    [InlineData(":p", false)]
    [InlineData("p:q r", false)]
    [InlineData("a%41:b", false)]
    [InlineData("1a:b", false)]
    [InlineData("%4", false)]
    [InlineData("p%4g", false)]
    [InlineData("p q", false)]
    [InlineData("?a b", false)]
    [InlineData("#a#b", false)]
    [InlineData("//u@@h", false)]
    [InlineData("//h:x", false)]
    [InlineData("//h:80:80", false)]
    [InlineData("//[::1", false)]
    [InlineData("//h/%x", false)]
    [InlineData("//h%c3", false)]
    [InlineData("p\\q", false)]
    [InlineData("p\U000000E9", false)]
    public static void IsUriRef_RelativeReferences(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.UriRef(text));
    }

    [Fact]
    public static void IsUriRef_AnyUri_IsAReference()
    {
        string[] uris = ["a:", "http://u@h:80/p?q#f", "a:b", "http://[::1%25eth0]/p", "a:?q#f"];
        foreach (string uri in uris)
        {
            Assert.True(FormatScanners.Uri(uri), uri);
            Assert.True(FormatScanners.UriRef(uri), uri);
        }
    }

    [Fact]
    public static void Uri_UnitsThatAreNoAsciiCharacters_AreRefused_WhereverTheyStand()
    {
        string[] units = ["\0", "\ud800", "\udc00", "\U0001F600", "\U00000085", "\U00002028", "\U000000A0", "\U0000007F", "\U0000001F"];
        foreach (string unit in units)
        {
            Assert.False(FormatScanners.Uri("http://host/p" + unit));
            Assert.False(FormatScanners.Uri("http://host/p?q" + unit));
            Assert.False(FormatScanners.Uri("http://host/p?q#f" + unit));
            Assert.False(FormatScanners.Uri("http://u" + unit + "@host"));
            Assert.False(FormatScanners.Uri("http://ho" + unit + "st"));
            Assert.False(FormatScanners.Uri("http://[::1" + unit + "]"));
            Assert.False(FormatScanners.Uri("ht" + unit + "tp://host"));
            Assert.False(FormatScanners.UriRef("p" + unit));
            Assert.False(FormatScanners.UriRef(unit));
        }
    }
}
