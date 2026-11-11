using Great.Parts.Cel.Library.Extensions.Network;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Network;

// The strict parser on the edges of its rules: the four fields of IPv4 with their leading zeros and ranges, the groups of IPv6 and the
// places of "::", the dotted tail, the three refusals of the set (a zone, an IPv4-mapped address, a unit above U+007F) and the length of
// a prefix. An accepted text gives the bits and the family; a refused one gives the reason in the order that Go reports it.
public sealed class AddressParserTests
{
    [Theory]
    [InlineData("0.0.0.0", 0UL, 0UL, true)]
    [InlineData("255.255.255.255", 0UL, 0xFFFFFFFFUL, true)]
    [InlineData("1.2.3.4", 0UL, 0x01020304UL, true)]
    [InlineData("192.168.0.255", 0UL, 0xC0A800FFUL, true)]
    [InlineData("1.2.3.0", 0UL, 0x01020300UL, true)]
    [InlineData("::", 0UL, 0UL, false)]
    [InlineData("::1", 0UL, 1UL, false)]
    [InlineData("::0001", 0UL, 1UL, false)]
    [InlineData("1::", 0x0001000000000000UL, 0UL, false)]
    [InlineData("1::8", 0x0001000000000000UL, 8UL, false)]
    [InlineData("1:2:3:4:5:6:7:8", 0x0001000200030004UL, 0x0005000600070008UL, false)]
    [InlineData("1:2:3:4:5:6:7::", 0x0001000200030004UL, 0x0005000600070000UL, false)]
    [InlineData("1:2:3:4:5:6::8", 0x0001000200030004UL, 0x0005000600000008UL, false)]
    [InlineData("::2:3:4:5:6:7:8", 0x0000000200030004UL, 0x0005000600070008UL, false)]
    [InlineData("2001:DB8::1", 0x20010DB800000000UL, 1UL, false)]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", 0x20010DB800000000UL, 1UL, false)]
    [InlineData("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", ulong.MaxValue, ulong.MaxValue, false)]
    [InlineData("::1.2.3.4", 0UL, 0x01020304UL, false)]
    [InlineData("::fffe:1.2.3.4", 0UL, 0x0000FFFE01020304UL, false)]
    [InlineData("1:2:3:4:5:6:1.2.3.4", 0x0001000200030004UL, 0x0005000601020304UL, false)]
    public static void Address_IsAcceptedWithItsBitsAndFamily(string text, ulong high, ulong low, bool isIPv4)
    {
        ParseFailure failure = AddressParser.ParseAddress(text, out AddressBits address);

        Assert.Equal(nameof(ParseFailure.None), failure.ToString());
        Assert.Equal((high, low, isIPv4), (address.High, address.Low, address.IsIPv4));
    }

    [Theory]
    [InlineData("1.2.3", "IPv4TooShort")]
    [InlineData("1.2", "IPv4TooShort")]
    [InlineData("1.2.3.4.5", "IPv4TooLong")]
    [InlineData("01.2.3.4", "IPv4LeadingZero")]
    [InlineData("00.2.3.4", "IPv4LeadingZero")]
    [InlineData("1.2.3.04", "IPv4LeadingZero")]
    [InlineData("1.2.3.256", "IPv4FieldTooLarge")]
    [InlineData("1.2.3.1000", "IPv4FieldTooLarge")]
    [InlineData("1.2.3.", "IPv4FieldEmpty")]
    [InlineData(".1.2.3", "IPv4FieldEmpty")]
    [InlineData("1..2.3", "IPv4FieldEmpty")]
    [InlineData(" 1.2.3.4", "UnexpectedCharacter")]
    [InlineData("1.2.3.4 ", "UnexpectedCharacter")]
    [InlineData("1.2.3.-4", "UnexpectedCharacter")]
    [InlineData("0x1.2.3.4", "UnexpectedCharacter")]
    [InlineData("1.2.3.4%eth0", "UnexpectedCharacter")]
    [InlineData("1.2.3.4/8", "UnexpectedCharacter")]
    [InlineData("1.2.3.4:5", "UnexpectedCharacter")]
    [InlineData("1:2.3.4", "EmbeddedIPv4Place")]
    [InlineData("1.2.3.٤", "UnexpectedCharacter")]
    [InlineData("１.2.3.4", "UnexpectedCharacter")]
    [InlineData("", "UnableToParse")]
    [InlineData("12", "UnableToParse")]
    [InlineData("localhost", "UnableToParse")]
    [InlineData("%en0", "MissingIPv6Address")]
    [InlineData("1::2::3", "MultipleEllipsis")]
    [InlineData("1::2::3%en0", "MultipleEllipsis")]
    [InlineData("12345::1", "IPv6FieldTooLong")]
    [InlineData("1:2:3:4:5:6:7:12345", "IPv6FieldTooLong")]
    [InlineData("g::1", "IPv6FieldEmpty")]
    [InlineData(":1", "IPv6FieldEmpty")]
    [InlineData(":::", "IPv6FieldEmpty")]
    [InlineData("1:::2", "IPv6FieldEmpty")]
    [InlineData("1::x", "IPv6FieldEmpty")]
    [InlineData("[::1]", "IPv6FieldEmpty")]
    [InlineData("1:", "ColonNeedsMore")]
    [InlineData("1:2:3:4:5:6:7:8:", "ColonNeedsMore")]
    [InlineData("1:2", "AddressTooShort")]
    [InlineData("1:2:3:4:5:6:7", "AddressTooShort")]
    [InlineData("1:2:3:4:5:6:7:8:9", "TrailingGarbage")]
    [InlineData("1:2:3:4:5:6:7:8::", "EllipsisExpandsNothing")]
    [InlineData("::1:2:3:4:5:6:7:8", "EllipsisExpandsNothing")]
    [InlineData("1:2:3:4:5:6::1.2.3.4", "EllipsisExpandsNothing")]
    [InlineData("1:2:3:4:5:1.2.3.4", "EmbeddedIPv4Place")]
    [InlineData("1:2:3:4:5:6:7:1.2.3.4", "EmbeddedIPv4Place")]
    [InlineData("1:2:3:4:5:6:7::1.2.3.4", "EmbeddedIPv4NoRoom")]
    [InlineData("::1.2.3", "IPv4TooShort")]
    [InlineData("::1.2.3.4.5", "IPv4TooLong")]
    [InlineData("::01.2.3.4", "IPv4LeadingZero")]
    [InlineData("1:2:3:4:5:6:1.2.3.4:5", "UnexpectedCharacter")]
    [InlineData("fe80::1%", "ZoneEmpty")]
    [InlineData("fe80::1%en0", "ZoneNotAllowed")]
    [InlineData("1:2:3:4:5:6:7:8%z", "ZoneNotAllowed")]
    [InlineData("::1.2.3.4%en0", "ZoneNotAllowed")]
    [InlineData("::ffff:1.2.3.4", "MappedNotAllowed")]
    [InlineData("::ffff:c0a8:1", "MappedNotAllowed")]
    [InlineData("::FFFF:0:0", "MappedNotAllowed")]
    [InlineData("0:0:0:0:0:ffff:1.2.3.4", "MappedNotAllowed")]
    // The family is decided by the first of '.', ':' and '%', whatever follows it: 1.2.3.4:5 is an IPv4 text with a stray character, and
    // 1:2.3.4 an IPv6 text whose dotted tail is in the wrong place.
    public static void Address_IsRefusedWithItsReason(string text, string reason)
    {
        ParseFailure failure = AddressParser.ParseAddress(text, out _);

        Assert.Equal(reason, failure.ToString());
    }

    [Theory]
    [InlineData("10.0.0.1/8", 0UL, 0x0A000001UL, true, 8)]
    [InlineData("10.0.0.0/0", 0UL, 0x0A000000UL, true, 0)]
    [InlineData("10.0.0.0/32", 0UL, 0x0A000000UL, true, 32)]
    [InlineData("0.0.0.0/0", 0UL, 0UL, true, 0)]
    [InlineData("::/0", 0UL, 0UL, false, 0)]
    [InlineData("::/128", 0UL, 0UL, false, 128)]
    [InlineData("2001:db8::/32", 0x20010DB800000000UL, 0UL, false, 32)]
    [InlineData("2001:db8::1/128", 0x20010DB800000000UL, 1UL, false, 128)]
    [InlineData("::1.2.3.4/96", 0UL, 0x01020304UL, false, 96)]
    public static void Prefix_IsAcceptedWithItsAddressAsWrittenAndItsLength(string text, ulong high, ulong low, bool isIPv4, int length)
    {
        ParseFailure failure = AddressParser.ParsePrefix(text, out AddressBits address, out int parsed);

        Assert.Equal(nameof(ParseFailure.None), failure.ToString());
        Assert.Equal((high, low, isIPv4, length), (address.High, address.Low, address.IsIPv4, parsed));
    }

    [Theory]
    [InlineData("10.0.0.0", "NoSlash")]
    [InlineData("10.0.0.0/", "BadBits")]
    [InlineData("10.0.0.0/08", "BadBits")]
    [InlineData("10.0.0.0/00", "BadBits")]
    [InlineData("10.0.0.0/+8", "BadBits")]
    [InlineData("10.0.0.0/-1", "BadBits")]
    [InlineData("10.0.0.0/8 ", "BadBits")]
    [InlineData("10.0.0.0/0x8", "BadBits")]
    [InlineData("10.0.0.0/٨", "BadBits")]
    [InlineData("10.0.0.0/99999999999999999999", "BadBits")]
    [InlineData("10.0.0.0/33", "BitsOutOfRange")]
    [InlineData("10.0.0.0/129", "BitsOutOfRange")]
    [InlineData("10.0.0.0/999", "BitsOutOfRange")]
    [InlineData("::/129", "BitsOutOfRange")]
    [InlineData("::1/1000", "BitsOutOfRange")]
    [InlineData("/8", "UnableToParse")]
    [InlineData("1.2.3/8", "IPv4TooShort")]
    [InlineData("1.2.3.4/8/8", "UnexpectedCharacter")]
    [InlineData("10.0.0.256/8", "IPv4FieldTooLarge")]
    [InlineData("10.0.0.256/33", "IPv4FieldTooLarge")]
    [InlineData("fe80::1%en0/64", "ZoneNotAllowed")]
    [InlineData("::ffff:1.2.3.4/96", "MappedNotAllowed")]
    [InlineData("::ffff:1.2.3.4/129", "BitsOutOfRange")]
    public static void Prefix_IsRefusedWithItsReason(string text, string reason)
    {
        ParseFailure failure = AddressParser.ParsePrefix(text, out _, out _);

        Assert.Equal(reason, failure.ToString());
    }
}
