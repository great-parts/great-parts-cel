using System.Text;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The strings of the profile of the extension sets (docs/design/eng-difftest.md, "Profile of the extension sets"): the pool of the
/// functions of <c>strings</c>, the texts of <c>ip</c> and <c>cidr</c>, valid and not, the texts of <c>base64.decode</c> and
/// the strings that <c>json.encode</c> escapes. They hold Unicode scalar values only, and they may be longer than the strings
/// of the standard library.
/// </summary>
internal static class ExtensionStrings
{
    // A string argument of a function of the set strings is a string of this pool one time in two.
    internal static readonly string[] Pool =
    [
        string.Empty, "a", "abc", "aBc", "ABC", "hello world", "a,b,c", "aaa", "abab", "ab", "b", "\u00e9", "h\u00e9llo", "\U0001f600", "a\U0001f600b", "\U0001f600\U0001f600",
        "\ue000", "\U00010000", "a\ue000\U00010000", " ", "  a b  ", "\u00a0x\u3000", "\t\n x \r\n", "\u0085\u2028y\u2029", "\u00df", "\u0130", "K", "\"", "a\"b",
        "a\\b", "\u0007\b\f\n\r\t\v", "x\u0000y", "'", "%", "a%b",
    ];

    // The texts of ip, isIP and the other string arguments of the set network, valid and not (docs/design/extension-network.md, "Parsing").
    private static readonly string[] IpValid =
    [
        "0.0.0.0", "1.2.3.4", "127.0.0.1", "255.255.255.255", "10.0.0.1", "192.168.1.5", "169.254.1.1", "224.0.0.1", "224.0.0.251",
        "239.255.255.255", "100.64.0.1", "8.8.8.8", "::", "::1", "2001:db8::1", "2001:0db8:0:0:0:0:0:1", "2001:DB8::1", "1:0:0:2:0:0:0:3",
        "1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7::", "::1.2.3.4", "1:2:3:4:5:6:1.2.3.4", "fe80::1", "ff02::1", "ff12::1", "ff32::1", "ff01::1",
        "ff0e::1", "fc00::1", "1:0:0:0:0:0:0:1", "::0", "0:0:0:0:0:0:0:0", "1:2:3:4:5:6:7:0",
    ];

    private static readonly string[] IpInvalid =
    [
        string.Empty, "1.2.3", "01.2.3.4", "1.2.3.256", "1.2.3.4.", "0x1.2.3.4", " 1.2.3.4", "1.2.3.4 ", "1.2.3.4/8", "1..2.3", "1.2.3.-4",
        "1.2.3.4.5", "256.0.0.0", "+1.2.3.4", "1.2.3.00", "1.2.3.\u0664", "1::2::3", "12345::1", ":1", "1:", "[::1]", "::ffff:1.2.3.4",
        "::ffff:c0a8:1", "::ffff:0:0", "fe80::1%eth0", "fe80::1%", "1:2:3:4:5:6:7:8::", "1:2:3:4:5:6:7", "g::1", "::1.2.3", "::1.2.3.256", "::1:",
        "1:2:3:4:5:6:7:8:9", "abc", "localhost", "\U0001f600",
    ];

    private static readonly string[] CidrValid =
    [
        "10.0.0.0/8", "10.0.0.1/8", "192.168.1.5/24", "0.0.0.0/0", "255.255.255.255/32", "1.2.3.4/0", "10.0.0.0/32", "::1/128", "::/0",
        "2001:db8::/32", "2001:db8::1/64", "fe80::/10", "ff02::/16", "1:2:3:4:5:6:7:8/127", "::1.2.3.4/96",
    ];

    private static readonly string[] CidrInvalid =
    [
        string.Empty, "10.0.0.0/33", "10.0.0.0/08", "10.0.0.0/+8", "10.0.0.0/", "10.0.0.0", "::ffff:1.2.3.4/96", "::1/129", "/8", "10.0.0.0/8/8",
        "10.0.0.0/-1", "10.0.0.0/ 8", "1.2.3/8", "10.0.0.0/8 ", "::1%eth0/64", "10.0.0.0/\u0668", "10.0.0.0/256", "::/00", "abc/8",
    ];

    // The prefix lengths that the generated prefixes take: the boundaries of both families.
    private static readonly string[] Lengths = ["0", "1", "7", "8", "24", "31", "32", "33", "63", "64", "127", "128", "129"];

    private static readonly string[] Base64Valid =
    [
        string.Empty, "aGVsbG8=", "aGVsbG8", "AA==", "AA", "AAA=", "AAA", "/+8=", "Zm9v", "Zm9vYg==", "Zm9vYg", "Zm9vYmE=", "Zm9vYmE",
        "aGVs\r\nbG8=", "aGVs\nbG8", "\r\naGVsbG8=\r\n", "aGVsbG9=", "////", "++++",
    ];

    private static readonly string[] Base64Invalid =
    [
        "a", "aGVsbG8==", "aGVsbG8===", "====", "aGV sbG8=", "aGVsbG8-", "aGVsbG8_", "\u00e9", "=aGVsbG8", "aGVsbG8=aGVs", "AA=", "A===", "AAA==",
        "aGVsbG8=\t", "a-_b", "\u00a0",
    ];

    // Strings that json.encode escapes, and the quote and the backslash.
    private static readonly string[] JsonTexts =
    [
        "<", ">", "&", "a<b>&c", "\u0001", "\u001f", "\u007f", "\u2028", "\u2029", "\"\\", "\t\n\r\b\f", "\u00e9\U0001f600", "\ue000", "\U00010000", "/", string.Empty,
    ];

    /// <summary>A text for <c>ip</c>: an address, or, one in four, a text that is none.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string IpText(PCG pcg) => pcg.OneIn(4) ? pcg.Pick(IpInvalid) : pcg.Pick(IpValid);

    /// <summary>A text for <c>cidr</c>: a prefix, an address with a length that the generator makes, or, one in four, a text that is none.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string CidrText(PCG pcg) => pcg.Below(8) switch
    {
        0 or 1 => pcg.Pick(CidrInvalid),
        2 => pcg.Pick(IpValid) + "/" + pcg.Pick(Lengths),
        _ => pcg.Pick(CidrValid),
    };

    /// <summary>A text for <c>isIP</c>, <c>isCIDR</c> and the string argument of the methods: an address or a prefix, valid or not.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string AddressOrPrefix(PCG pcg) => pcg.OneIn(2) ? IpText(pcg) : CidrText(pcg);

    /// <summary>A text for <c>base64.decode</c>: padded, unpadded, with line breaks, and not valid.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string Base64Text(PCG pcg) => pcg.OneIn(3) ? pcg.Pick(Base64Invalid) : pcg.Pick(Base64Valid);

    /// <summary>A string that <c>json.encode</c> escapes.</summary>
    /// <param name="pcg">The random source.</param>
    internal static string JsonText(PCG pcg) => pcg.Pick(JsonTexts);

    /// <summary>The length in code points of a string.</summary>
    /// <param name="text">The string.</param>
    internal static int Length(string text)
    {
        int length = 0;
        foreach (Rune _ in text.EnumerateRunes())
        {
            length++;
        }

        return length;
    }
}
