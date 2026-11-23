using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Formats;

// The boundaries of isIpPrefix and of the addresses that isHostAndPort reads, which the examples of the set do not cover: lengths of
// the prefix 0, 32 and 128 and the forms that are no length, IPv4 fields with leading zeros, 256 and empty octets, IPv6 with "::" at
// the start, in the middle and at the end, twice, with an IPv4 tail and with nine groups, and the bits after the prefix in the strict
// form. The values come from RFC 4291 and from the rules of the set, not from the scanners.
[Trait("Requirement", "F-28")]
public sealed class IpAddressBoundaryTests
{
    [Theory]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("1.2.3.4/0", true)]
    [InlineData("255.255.255.255/32", true)]
    [InlineData("1.2.3.4/32", true)]
    [InlineData("::/0", true)]
    [InlineData("::/128", true)]
    [InlineData("1:2:3:4:5:6:7:8/128", true)]
    [InlineData("1.2.3.4/33", false)]
    [InlineData("1.2.3.4/100", false)]
    [InlineData("1.2.3.4/1000", false)]
    [InlineData("::/129", false)]
    [InlineData("::/1000", false)]
    [InlineData("1.2.3.4/", false)]
    [InlineData("::/", false)]
    [InlineData("/8", false)]
    [InlineData("/", false)]
    [InlineData("1.2.3.4/+8", false)]
    [InlineData("1.2.3.4/-8", false)]
    [InlineData("1.2.3.4/08", false)]
    [InlineData("1.2.3.4/00", false)]
    [InlineData("::1/064", false)]
    [InlineData("1.2.3.4/a", false)]
    [InlineData("1.2.3.4/8a", false)]
    [InlineData("1.2.3.4/ 8", false)]
    [InlineData("1.2.3.4/8 ", false)]
    [InlineData("1.2.3.4/8/8", false)]
    [InlineData("1.2.3.4/12345678901234567890", false)]
    [InlineData("1.2.3.4//8", false)]
    public static void IsIpPrefix_TheLengthOfThePrefix(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.IpPrefix(text));
    }

    [Theory]
    [InlineData("255.255.255.255/8", true)]
    [InlineData("0.0.0.0/8", true)]
    [InlineData("10.20.30.40/8", true)]
    [InlineData("1.2.3.4/8", true)]
    [InlineData("01.2.3.4/8", false)]
    [InlineData("1.02.3.4/8", false)]
    [InlineData("1.2.3.04/8", false)]
    [InlineData("1.2.3.256/8", false)]
    [InlineData("256.2.3.4/8", false)]
    [InlineData("1.2.3.1000/8", false)]
    [InlineData("1..3.4/8", false)]
    [InlineData(".1.2.3/8", false)]
    [InlineData("1.2.3./8", false)]
    [InlineData("1.2.3/8", false)]
    [InlineData("1.2/8", false)]
    [InlineData("1/8", false)]
    [InlineData("1.2.3.4.5/8", false)]
    [InlineData("1.2.3.4./8", false)]
    [InlineData("1.2.3.4 /8", false)]
    [InlineData("0x1.2.3.4/8", false)]
    [InlineData("1.2.3.-4/8", false)]
    [InlineData("+1.2.3.4/8", false)]
    [InlineData("1.2.3.4a/8", false)]
    [InlineData("a.b.c.d/8", false)]
    [InlineData("1,2,3,4/8", false)]
    public static void IsIpPrefix_IPv4Fields(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.IpPrefix(text));
    }

    [Theory]
    [InlineData("::/64", true)]
    [InlineData("::1/64", true)]
    [InlineData("1::/64", true)]
    [InlineData("1::2/64", true)]
    [InlineData("1:2::3:4/64", true)]
    [InlineData("1:2:3:4:5:6:7:8/64", true)]
    [InlineData("1:2:3:4:5:6:7::/64", true)]
    [InlineData("::2:3:4:5:6:7:8/64", true)]
    [InlineData("1:2:3::6:7:8/64", true)]
    [InlineData("2001:DB8::/32", true)]
    [InlineData("2001:db8:0:0:0:0:0:1/64", true)]
    [InlineData("FFFF:ffff:FfFf::/64", true)]
    [InlineData("::ffff:1.2.3.4/96", true)]
    [InlineData("::1.2.3.4/96", true)]
    [InlineData("1::1.2.3.4/96", true)]
    [InlineData("1:2:3:4:5:6:1.2.3.4/96", true)]
    [InlineData("::0.0.0.0/96", true)]
    [InlineData("1:2:3:4:5:6:7:8:9/64", false)]
    [InlineData("1:2:3:4:5:6:7/64", false)]
    [InlineData("1::2::3/64", false)]
    [InlineData(":::/64", false)]
    [InlineData("::::/64", false)]
    [InlineData("1:::2/64", false)]
    [InlineData("1:2:3:4:5:6:7:8::/64", false)]
    [InlineData("::1:2:3:4:5:6:7:8/64", false)]
    [InlineData("1:2:3:4::5:6:7:8/64", false)]
    [InlineData("12345::1/64", false)]
    [InlineData("1::12345/64", false)]
    [InlineData("1:/64", false)]
    [InlineData(":1/64", false)]
    [InlineData("1:2:/64", false)]
    [InlineData(":1:2:3:4:5:6:7/64", false)]
    [InlineData("g::/64", false)]
    [InlineData("1::g/64", false)]
    [InlineData("1:2:3:4:5:6:7:8:/64", false)]
    [InlineData(" ::1/64", false)]
    [InlineData("::1 /64", false)]
    [InlineData("[::1]/64", false)]
    [InlineData("::1%/64", false)]
    [InlineData("::1%eth0/64", false)]
    [InlineData("::ffff:1.2.3/96", false)]
    [InlineData("::ffff:1.2.3.4.5/96", false)]
    [InlineData("::ffff:01.2.3.4/96", false)]
    [InlineData("::ffff:256.2.3.4/96", false)]
    [InlineData("::1.2.3.4:1/96", false)]
    [InlineData("1.2.3.4::/96", false)]
    [InlineData("1:2:3:4:5:6:7:1.2.3.4/96", false)]
    [InlineData("1:2:3:4:5:1.2.3.4/96", false)]
    [InlineData("1:1.2.3.4/96", false)]
    [InlineData("12345.2.3.4::/64", false)]
    public static void IsIpPrefix_IPv6Groups(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.IpPrefix(text));
    }

    [Theory]
    [InlineData("0.0.0.0/0", true)]
    [InlineData("0.0.0.1/0", false)]
    [InlineData("128.0.0.0/0", false)]
    [InlineData("128.0.0.0/1", true)]
    [InlineData("127.0.0.0/1", false)]
    [InlineData("10.0.0.0/8", true)]
    [InlineData("10.0.0.1/8", false)]
    [InlineData("10.128.0.0/8", false)]
    [InlineData("10.128.0.0/9", true)]
    [InlineData("255.255.254.0/23", true)]
    [InlineData("255.255.255.0/23", false)]
    [InlineData("255.255.255.254/31", true)]
    [InlineData("255.255.255.255/31", false)]
    [InlineData("255.255.255.255/32", true)]
    [InlineData("0.0.0.0/32", true)]
    [InlineData("::/0", true)]
    [InlineData("::1/0", false)]
    [InlineData("1::/0", false)]
    [InlineData("8000::/1", true)]
    [InlineData("c000::/1", false)]
    [InlineData("ff00::/8", true)]
    [InlineData("ff80::/8", false)]
    [InlineData("ffff:ffff:ffff:ffff::/64", true)]
    [InlineData("ffff:ffff:ffff:ffff:8000::/64", false)]
    [InlineData("ffff:ffff:ffff:ffff:8000::/65", true)]
    [InlineData("ffff:ffff:ffff:ffff:ffff:ffff:ffff:fffe/127", true)]
    [InlineData("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/127", false)]
    [InlineData("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/128", true)]
    [InlineData("::/128", true)]
    [InlineData("0:0:0:0:0:ffff:192.1.0.0/112", true)]
    [InlineData("0:0:0:0:0:ffff:192.1.0.128/112", false)]
    [InlineData("::ffff:192.1.0.0/112", true)]
    [InlineData("::ffff:192.1.128.0/112", false)]
    public static void IsIpPrefix_Strict_TheBitsAfterThePrefixAreZero(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.IpPrefix(text, 0, true));
    }

    [Fact]
    public static void IsIpPrefix_NotStrict_IgnoresTheBitsAfterThePrefix()
    {
        Assert.True(FormatScanners.IpPrefix("10.0.0.1/8", 0, false));
        Assert.True(FormatScanners.IpPrefix("255.255.255.255/0", 0, false));
        Assert.True(FormatScanners.IpPrefix("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/0", 0, false));
        Assert.True(FormatScanners.IpPrefix("0:0:0:0:0:ffff:192.1.0.128/112", 0, false));
    }

    [Theory]
    [InlineData(0, "10.0.0.0/8", true)]
    [InlineData(0, "::/8", true)]
    [InlineData(4, "10.0.0.0/8", true)]
    [InlineData(4, "::/8", false)]
    [InlineData(6, "10.0.0.0/8", false)]
    [InlineData(6, "::/8", true)]
    [InlineData(-1, "10.0.0.0/8", false)]
    [InlineData(1, "10.0.0.0/8", false)]
    [InlineData(5, "::/8", false)]
    [InlineData(7, "::/8", false)]
    [InlineData(40, "10.0.0.0/8", false)]
    [InlineData(long.MaxValue, "::/8", false)]
    [InlineData(long.MinValue, "10.0.0.0/8", false)]
    [InlineData(4294967300, "10.0.0.0/8", false)]
    public static void IsIpPrefix_OnlyTheVersions0_4And6_Count(long version, string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.IpPrefix(text, version));
        Assert.Equal(expected, FormatScanners.IpPrefix(text, version, true));
    }

    [Theory]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("127.0.0.1:0", true)]
    [InlineData("1.2.3.4:80", true)]
    [InlineData("256.0.0.0", false)]
    [InlineData("127.0.0", false)]
    [InlineData("127..0.1", false)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("01.2.3.4", false)]
    [InlineData("[0:0:0:0:0:0:0:0]", true)]
    [InlineData("[ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff]", true)]
    [InlineData("[0:0:0:0:0:ffff:192.1.56.10]", true)]
    [InlineData("[::]", true)]
    [InlineData("[::1]:0", true)]
    [InlineData("[::1]:65535", true)]
    [InlineData("[]", false)]
    [InlineData("[", false)]
    [InlineData("]", false)]
    [InlineData("[::1", false)]
    [InlineData("::1", false)]
    [InlineData("::1]", false)]
    [InlineData("[[::1]]", false)]
    [InlineData("[1:2:3:4:5:6:7:8:9]", false)]
    [InlineData("[1::2::3]", false)]
    [InlineData("[127.0.0.1]", false)]
    [InlineData("[example.com]", false)]
    [InlineData("[::1]x", false)]
    [InlineData("[::1]:", false)]
    [InlineData("[::1]:80:80", false)]
    [InlineData("[::1] :80", false)]
    [InlineData("[::1%foo]", true)]
    [InlineData("[::1%foo]:80", true)]
    [InlineData("[::1%]", false)]
    [InlineData("[::1%]:80", false)]
    [InlineData("[::1%% :x\u001f]", true)]
    [InlineData("[::0%00]]", true)]
    [InlineData("[::1%a]b]:80", true)]
    [InlineData("[::1%\U000000E9]", true)]
    [InlineData("[%foo]", false)]
    [InlineData("[:%foo]", false)]
    public static void IsHostAndPort_AddressesInTheHost(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.HostAndPort(text, false));
    }

    [Theory]
    [InlineData("a:0", true)]
    [InlineData("a:1", true)]
    [InlineData("a:65535", true)]
    [InlineData("a:65536", false)]
    [InlineData("a:99999", false)]
    [InlineData("a:100000", false)]
    [InlineData("a:123456789", false)]
    [InlineData("a:00", false)]
    [InlineData("a:080", false)]
    [InlineData("a:+0", false)]
    [InlineData("a:-1", false)]
    [InlineData("a:0xFA", false)]
    [InlineData("a:8a", false)]
    [InlineData("a:", false)]
    [InlineData("a: 1", false)]
    [InlineData("a:1 ", false)]
    [InlineData("a:1:2", false)]
    [InlineData(":80", false)]
    [InlineData(":", false)]
    [InlineData("a::80", false)]
    public static void IsHostAndPort_ThePort(string text, bool expected)
    {
        Assert.Equal(expected, FormatScanners.HostAndPort(text, true));
        Assert.Equal(expected, FormatScanners.HostAndPort(text, false));
    }

    [Fact]
    public static void IsHostAndPort_NamesOfTheHost_FollowTheLimitsOfAHostName()
    {
        string name253 = new string('a', 63) + "." + new string('b', 63) + "." + new string('c', 63) + "." + new string('d', 61);

        Assert.True(FormatScanners.HostAndPort(name253, false));
        Assert.True(FormatScanners.HostAndPort(name253 + ":80", true));
        Assert.False(FormatScanners.HostAndPort(name253 + "d", false));
        Assert.False(FormatScanners.HostAndPort(name253 + "d:80", true));
        Assert.True(FormatScanners.HostAndPort(name253 + ".", false));
        Assert.False(FormatScanners.HostAndPort("a." + new string('1', 3), false));
        Assert.False(FormatScanners.HostAndPort("-a:80", true));
        Assert.False(FormatScanners.HostAndPort("a-:80", true));
        Assert.False(FormatScanners.HostAndPort("a b:80", true));
    }

    [Fact]
    public static void IsHostAndPort_APortIsRequiredOnlyWhenTheArgumentSays()
    {
        Assert.True(FormatScanners.HostAndPort("a", false));
        Assert.False(FormatScanners.HostAndPort("a", true));
        Assert.True(FormatScanners.HostAndPort("1.2.3.4", false));
        Assert.False(FormatScanners.HostAndPort("1.2.3.4", true));
        Assert.True(FormatScanners.HostAndPort("[::1]", false));
        Assert.False(FormatScanners.HostAndPort("[::1]", true));
        Assert.False(FormatScanners.HostAndPort(string.Empty, false));
        Assert.False(FormatScanners.HostAndPort(string.Empty, true));
    }

    [Fact]
    public static void IsHostAndPort_ANulInTheZone_IsRefused()
    {
        Assert.False(FormatScanners.HostAndPort("[::1%\0]", false));
        Assert.False(FormatScanners.HostAndPort("[::1%a\0b]:80", false));
    }
}
