using System;
using Great.Parts.Cel.Library.Extensions.Network;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions.Network;

// The text forms of addresses and prefixes: IPv4 as four decimal fields, IPv6 in lower case with the first of the longest runs of two
// or more zero groups written as "::" and a single zero group left alone; the length of the text, which the cost is charged from before
// the text is made, equal to the length of the text that is made; and the words of every reason of a failed parse.
public sealed class AddressTextTests
{
    [Theory]
    [InlineData("0.0.0.0", "0.0.0.0")]
    [InlineData("255.255.255.255", "255.255.255.255")]
    [InlineData("10.20.30.40", "10.20.30.40")]
    [InlineData("1.22.3.100", "1.22.3.100")]
    [InlineData("::", "::")]
    [InlineData("::1", "::1")]
    [InlineData("1::", "1::")]
    [InlineData("0:0:0:0:0:0:0:1", "::1")]
    [InlineData("1:0:0:0:0:0:0:0", "1::")]
    [InlineData("2001:0db8:0:0:0:0:0:1", "2001:db8::1")]
    [InlineData("ABCD:EF01:2345:6789:ABCD:EF01:2345:6789", "abcd:ef01:2345:6789:abcd:ef01:2345:6789")]
    [InlineData("1:0:0:2:0:0:0:3", "1:0:0:2::3")]
    [InlineData("1:0:0:2:0:0:3:4", "1::2:0:0:3:4")]
    [InlineData("1:2:3:4:5:6:7:0", "1:2:3:4:5:6:7:0")]
    [InlineData("0:1:2:3:4:5:6:7", "0:1:2:3:4:5:6:7")]
    [InlineData("1:0:2:3:4:5:6:7", "1:0:2:3:4:5:6:7")]
    [InlineData("1:2:0:0:5:6:7:8", "1:2::5:6:7:8")]
    [InlineData("0:0:1:0:0:0:0:8", "0:0:1::8")]
    [InlineData("::1.2.3.4", "::102:304")]
    [InlineData("1:2:3:4:5:6:1.2.3.4", "1:2:3:4:5:6:102:304")]
    [InlineData("1111:2222:3333:4444:5555:6666:7777:8888", "1111:2222:3333:4444:5555:6666:7777:8888")]
    public static void Address_IsWrittenAsNetipWritesIt(string text, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Assert.Equal(nameof(ParseFailure.None), AddressParser.ParseAddress(text, out AddressBits address).ToString());

        string written = AddressText.Of(address);

        Assert.Equal(expected, written);
        Assert.Equal(expected.Length, AddressText.LengthOf(address));
    }

    [Fact]
    public static void LongestAddress_FillsTheBufferOfTheFunctionThatComparesTexts()
    {
        _ = AddressParser.ParseAddress("1111:2222:3333:4444:5555:6666:7777:8888", out AddressBits address);

        Assert.Equal(AddressText.MaxAddressLength, AddressText.LengthOf(address));
    }

    // The prefix keeps the address as it was written, host bits included, and the length is written in decimal.
    [Theory]
    [InlineData("10.0.0.1/8", "10.0.0.1/8")]
    [InlineData("10.0.0.0/0", "10.0.0.0/0")]
    [InlineData("255.255.255.255/32", "255.255.255.255/32")]
    [InlineData("::/0", "::/0")]
    [InlineData("::/128", "::/128")]
    [InlineData("2001:0DB8:0:0:0:0:0:1/64", "2001:db8::1/64")]
    [InlineData("1:2:3:4:5:6:7:8/100", "1:2:3:4:5:6:7:8/100")]
    public static void Prefix_IsTheAddressAsWrittenAndTheLength(string text, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Assert.Equal(nameof(ParseFailure.None), AddressParser.ParsePrefix(text, out AddressBits address, out int length).ToString());

        string written = AddressText.Of(address, length);

        Assert.Equal(expected, written);
        Assert.Equal(expected.Length, AddressText.LengthOf(address, length));
    }

    [Theory]
    [InlineData("IPv4TooShort", false, "1.2.3", "IP Address \"1.2.3\" parse error during conversion from string: IPv4 address too short")]
    [InlineData("UnableToParse", false, "x", "IP Address \"x\" parse error during conversion from string: unable to parse IP")]
    [InlineData("MultipleEllipsis", false, "1::2::3", "IP Address \"1::2::3\" parse error during conversion from string: multiple :: in address")]
    [InlineData("NoSlash", true, "10.0.0.0", "CIDR \"10.0.0.0\" parse error during conversion from string: no '/'")]
    [InlineData("BitsOutOfRange", true, "10.0.0.0/33", "CIDR \"10.0.0.0/33\" parse error during conversion from string: prefix length out of range")]
    [InlineData("BadBits", true, "10.0.0.0/+8", "CIDR \"10.0.0.0/+8\" parse error during conversion from string: bad bits after slash: \"+8\"")]
    [InlineData("BadBits", true, "10.0.0.0/", "CIDR \"10.0.0.0/\" parse error during conversion from string: bad bits after slash: \"\"")]
    [InlineData("MappedNotAllowed", false, "::ffff:1.2.3.4", "IPv4-mapped IPv6 address \"::ffff:1.2.3.4\" is not allowed")]
    [InlineData("MappedNotAllowed", true, "::ffff:1.2.3.4/96", "IPv4-mapped IPv6 address \"::ffff:1.2.3.4/96\" is not allowed")]
    [InlineData("ZoneNotAllowed", false, "fe80::1%en0", "IP Address \"fe80::1%en0\" with zone value is not allowed")]
    [InlineData("ZoneNotAllowed", true, "fe80::1%en0/64", "CIDR \"fe80::1%en0/64\" with zone value is not allowed")]
    public static void Failure_HasItsWords(string reason, bool isPrefix, string text, string expected)
    {
        ParseFailure failure = Enum.Parse<ParseFailure>(reason);

        Assert.Equal(expected, AddressText.Describe(failure, isPrefix, text));
    }

    // Every reason has words of its own: none falls to the general text, which would hide a reason added to the enumeration alone.
    [Fact]
    public static void EveryReason_HasWordsOfItsOwn()
    {
        foreach (ParseFailure failure in Enum.GetValues<ParseFailure>())
        {
            if (failure == ParseFailure.None)
            {
                continue;
            }

            Assert.DoesNotContain("invalid address", AddressText.Describe(failure, false, "x"), StringComparison.Ordinal);
            Assert.DoesNotContain("invalid address", AddressText.Describe(failure, true, "x"), StringComparison.Ordinal);
        }
    }
}
