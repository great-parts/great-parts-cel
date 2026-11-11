using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Network.NetworkEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Network;

// The functions of the set network on the edges of their ranges: the predicates at the first and the last address of each range and one
// address outside it on each side, the containment of addresses and prefixes at the lengths 0, 1, 32, 64, 65 and 128, which cross the
// halves of an IPv6 address, the masking of the host bits, the families of the compatible and the mapped forms, and the errors of a
// parse with their words and their places. Each row of the first table is an expression that is true.
public sealed class NetworkFunctionTests
{
    private const string Holds = """
        ip('127.0.0.1').isLoopback()
        ip('127.255.255.255').isLoopback()
        !ip('126.255.255.255').isLoopback()
        !ip('128.0.0.0').isLoopback()
        ip('::1').isLoopback()
        !ip('::2').isLoopback()
        !ip('::').isLoopback()
        !ip('1::1').isLoopback()
        ip('0.0.0.0').isUnspecified()
        !ip('0.0.0.1').isUnspecified()
        ip('::').isUnspecified()
        !ip('::1').isUnspecified()
        !ip('1::').isUnspecified()
        ip('169.254.0.0').isLinkLocalUnicast()
        ip('169.254.255.255').isLinkLocalUnicast()
        !ip('169.253.255.255').isLinkLocalUnicast()
        !ip('169.255.0.0').isLinkLocalUnicast()
        ip('fe80::').isLinkLocalUnicast()
        ip('febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff').isLinkLocalUnicast()
        !ip('fe7f:ffff:ffff:ffff:ffff:ffff:ffff:ffff').isLinkLocalUnicast()
        !ip('fec0::').isLinkLocalUnicast()
        ip('224.0.0.0').isLinkLocalMulticast()
        ip('224.0.0.255').isLinkLocalMulticast()
        !ip('224.0.1.0').isLinkLocalMulticast()
        !ip('223.255.255.255').isLinkLocalMulticast()
        ip('ff02::').isLinkLocalMulticast()
        ip('ff12::').isLinkLocalMulticast()
        ip('ff32::1').isLinkLocalMulticast()
        ip('ff22::').isLinkLocalMulticast()
        !ip('ff01::').isLinkLocalMulticast()
        !ip('ff0e::').isLinkLocalMulticast()
        !ip('ff03::').isLinkLocalMulticast()
        !ip('fe02::').isLinkLocalMulticast()
        ip('1.0.0.0').isGlobalUnicast()
        ip('10.1.1.1').isGlobalUnicast()
        ip('223.255.255.255').isGlobalUnicast()
        ip('240.0.0.1').isGlobalUnicast()
        ip('255.255.255.254').isGlobalUnicast()
        ip('169.253.255.255').isGlobalUnicast()
        ip('169.255.0.0').isGlobalUnicast()
        ip('::2').isGlobalUnicast()
        ip('fc00::1').isGlobalUnicast()
        ip('fec0::1').isGlobalUnicast()
        ip('2001:db8::1').isGlobalUnicast()
        !ip('0.0.0.0').isGlobalUnicast()
        !ip('255.255.255.255').isGlobalUnicast()
        !ip('127.0.0.1').isGlobalUnicast()
        !ip('224.0.0.0').isGlobalUnicast()
        !ip('239.255.255.255').isGlobalUnicast()
        !ip('169.254.0.1').isGlobalUnicast()
        !ip('::').isGlobalUnicast()
        !ip('::1').isGlobalUnicast()
        !ip('ff00::').isGlobalUnicast()
        !ip('fe80::1').isGlobalUnicast()
        ip('1.2.3.4').family() == 4
        ip('::').family() == 6
        ip('::1.2.3.4').family() == 6
        cidr('::/0').ip().family() == 6
        cidr('0.0.0.0/0').ip().family() == 4
        cidr('10.0.0.0/8').containsIP(ip('10.255.255.255'))
        cidr('10.0.0.0/8').containsIP(ip('10.0.0.0'))
        !cidr('10.0.0.0/8').containsIP(ip('11.0.0.0'))
        !cidr('10.0.0.0/8').containsIP(ip('9.255.255.255'))
        cidr('10.9.9.9/8').containsIP(ip('10.1.1.1'))
        cidr('0.0.0.0/0').containsIP(ip('255.255.255.255'))
        !cidr('0.0.0.0/0').containsIP(ip('::'))
        cidr('1.2.3.4/32').containsIP(ip('1.2.3.4'))
        !cidr('1.2.3.4/32').containsIP(ip('1.2.3.5'))
        cidr('128.0.0.0/1').containsIP(ip('255.255.255.255'))
        !cidr('128.0.0.0/1').containsIP(ip('127.255.255.255'))
        cidr('2001:db8::/64').containsIP(ip('2001:db8::ffff:ffff:ffff:ffff'))
        !cidr('2001:db8::/64').containsIP(ip('2001:db8:0:1::'))
        cidr('2001:db8::/65').containsIP(ip('2001:db8::7fff:ffff:ffff:ffff'))
        !cidr('2001:db8::/65').containsIP(ip('2001:db8::8000:0:0:0'))
        cidr('2001:db8::8000:0:0:0/65').containsIP(ip('2001:db8::ffff:0:0:1'))
        cidr('::1/128').containsIP(ip('::1'))
        !cidr('::1/128').containsIP(ip('::2'))
        cidr('::/0').containsIP(ip('ffff::'))
        !cidr('::/0').containsIP(ip('1.2.3.4'))
        cidr('::/0').containsIP(ip('::1.2.3.4'))
        cidr('::/96').containsIP(ip('::1.2.3.4'))
        !cidr('::1.2.3.4/128').containsIP(ip('1.2.3.4'))
        cidr('10.0.0.0/8').containsIP('10.255.255.255')
        !cidr('10.0.0.0/8').containsIP('11.0.0.0')
        !cidr('2001:db8::/32').containsIP('2001:db9::')
        !cidr('10.0.0.0/8').containsIP('::')
        cidr('10.0.0.0/8').containsCIDR(cidr('10.0.0.0/8'))
        cidr('10.0.0.0/8').containsCIDR(cidr('10.255.0.0/16'))
        !cidr('10.0.0.0/8').containsCIDR(cidr('11.0.0.0/16'))
        !cidr('10.0.0.0/8').containsCIDR(cidr('10.0.0.0/7'))
        cidr('10.255.255.255/8').containsCIDR(cidr('10.1.2.3/9'))
        cidr('0.0.0.0/0').containsCIDR(cidr('255.255.255.255/32'))
        !cidr('0.0.0.0/0').containsCIDR(cidr('::/0'))
        !cidr('::/0').containsCIDR(cidr('0.0.0.0/0'))
        cidr('::/0').containsCIDR(cidr('ffff::/16'))
        cidr('2001:db8::/64').containsCIDR(cidr('2001:db8::/65'))
        cidr('2001:db8::/65').containsCIDR(cidr('2001:db8::/128'))
        !cidr('2001:db8::/65').containsCIDR(cidr('2001:db8::8000:0:0:0/66'))
        !cidr('2001:db8::/65').containsCIDR(cidr('2001:db8::/64'))
        cidr('10.0.0.0/8').containsCIDR('10.1.0.0/16')
        !cidr('10.0.0.0/8').containsCIDR('10.0.0.0/7')
        !cidr('10.0.0.0/8').containsCIDR('::/128')
        cidr('0.0.0.0/0').masked() == cidr('0.0.0.0/0')
        cidr('255.255.255.255/0').masked() == cidr('0.0.0.0/0')
        cidr('1.2.3.4/32').masked() == cidr('1.2.3.4/32')
        cidr('1.2.3.5/31').masked() == cidr('1.2.3.4/31')
        cidr('::1/0').masked() == cidr('::/0')
        cidr('::1/128').masked() == cidr('::1/128')
        cidr('2001:db8::1:2:3:4/64').masked() == cidr('2001:db8::/64')
        cidr('2001:db8::8000:0:0:1/65').masked() == cidr('2001:db8::8000:0:0:0/65')
        cidr('2001:db8::7fff:0:0:1/65').masked() == cidr('2001:db8::/65')
        cidr('ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff/127').masked() == cidr('ffff:ffff:ffff:ffff:ffff:ffff:ffff:fffe/127')
        string(cidr('10.1.2.3/8').masked()) == '10.0.0.0/8'
        cidr('10.1.2.3/8').masked().prefixLength() == 8
        !cidr('2001:db8::8000:0:0:0/64').isMask()
        cidr('2001:db8::8000:0:0:0/65').isMask()
        !cidr('::1/127').isMask()
        cidr('::/127').isMask()
        cidr('10.0.0.1/32').prefixLength() == 32
        cidr('::/0').prefixLength() == 0
        cidr('::/128').prefixLength() == 128
        ip.isCanonical('0.0.0.0')
        ip.isCanonical('::1')
        ip.isCanonical('1:0:2:3:4:5:6:7')
        ip.isCanonical('1:2:3:4:5:6:7:0')
        ip.isCanonical('::102:304')
        !ip.isCanonical('::1.2.3.4')
        !ip.isCanonical('1:0:0:2:0:0:3:4')
        ip.isCanonical('0:1:2:3:4:5:6:7')
        !ip.isCanonical('2001:0db8::1')
        !ip.isCanonical('::0')
        isIP('0.0.0.0')
        isIP('::1.2.3.4')
        !isIP('0.0.0.')
        !isIP('::ffff:0.0.0.0')
        !isIP('fe80::%')
        !isIP('10.0.0.0/8')
        isCIDR('::1.2.3.4/96')
        !isCIDR('::ffff:1.2.3.4/128')
        !isCIDR('10.0.0.0/8/8')
        isIP(text) == false
        """;

    public static TheoryData<string> HoldingCases => HoldingData();

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, new CelBindings().Add("text", "not an address"));

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    // The code and the words of a failed parse, and its place: the open parenthesis of the call.
    [Theory]
    [InlineData("ip('1.2.3')", "invalid argument to 'ip': IP Address \"1.2.3\" parse error during conversion from string: IPv4 address too short")]
    [InlineData("ip('1.2.3.004')", "invalid argument to 'ip': IP Address \"1.2.3.004\" parse error during conversion from string: IPv4 field has octet with leading zero")]
    [InlineData("ip('1::2::3')", "invalid argument to 'ip': IP Address \"1::2::3\" parse error during conversion from string: multiple :: in address")]
    [InlineData("ip('::ffff:1.2.3.4')", "invalid argument to 'ip': IPv4-mapped IPv6 address \"::ffff:1.2.3.4\" is not allowed")]
    [InlineData("ip('fe80::1%en0')", "invalid argument to 'ip': IP Address \"fe80::1%en0\" with zone value is not allowed")]
    [InlineData("ip('')", "invalid argument to 'ip': IP Address \"\" parse error during conversion from string: unable to parse IP")]
    [InlineData("cidr('10.0.0.0/33')", "invalid argument to 'cidr': CIDR \"10.0.0.0/33\" parse error during conversion from string: prefix length out of range")]
    [InlineData("cidr('10.0.0.0')", "invalid argument to 'cidr': CIDR \"10.0.0.0\" parse error during conversion from string: no '/'")]
    [InlineData("cidr('10.0.0.0/08')", "invalid argument to 'cidr': CIDR \"10.0.0.0/08\" parse error during conversion from string: bad bits after slash: \"08\"")]
    [InlineData("cidr('fe80::1%en0/64')", "invalid argument to 'cidr': CIDR \"fe80::1%en0/64\" with zone value is not allowed")]
    [InlineData("cidr('::ffff:1.2.3.4/96')", "invalid argument to 'cidr': IPv4-mapped IPv6 address \"::ffff:1.2.3.4/96\" is not allowed")]
    [InlineData("ip.isCanonical('::ffff:1.2.3.4')", "invalid argument to 'ip.isCanonical': IPv4-mapped IPv6 address \"::ffff:1.2.3.4\" is not allowed")]
    [InlineData("ip.isCanonical('x')", "invalid argument to 'ip.isCanonical': IP Address \"x\" parse error during conversion from string: unable to parse IP")]
    public static void FailedParse_IsInvalidArgumentAtTheCall(string expression, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal((CelErrorCode.InvalidArgument, message, expression.IndexOf('(', StringComparison.Ordinal)), (error.Code, error.Message, error.Location.Offset));
    }

    // The member forms parse the string after the receiver is known; the place is the parenthesis that follows the function name.
    [Theory]
    [InlineData("cidr('10.0.0.0/8').containsIP('10.0.0.256')", "containsIP", "invalid argument to 'containsIP': IP Address \"10.0.0.256\" parse error during conversion from string: IPv4 field has value >255")]
    [InlineData("cidr('10.0.0.0/8').containsIP('::ffff:10.0.0.1')", "containsIP", "invalid argument to 'containsIP': IPv4-mapped IPv6 address \"::ffff:10.0.0.1\" is not allowed")]
    [InlineData("cidr('10.0.0.0/8').containsCIDR('10.0.0.0/33')", "containsCIDR", "invalid argument to 'containsCIDR': CIDR \"10.0.0.0/33\" parse error during conversion from string: prefix length out of range")]
    [InlineData("cidr('10.0.0.0/8').containsCIDR('10.0.0.0')", "containsCIDR", "invalid argument to 'containsCIDR': CIDR \"10.0.0.0\" parse error during conversion from string: no '/'")]
    public static void FailedParseOfAnArgument_IsInvalidArgumentAtTheCall(string expression, string function, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(function);
        CelError error = Assert.Single(Evaluate(expression).Errors);
        int offset = expression.IndexOf(function + "(", StringComparison.Ordinal) + function.Length;

        Assert.Equal((CelErrorCode.InvalidArgument, message, offset), (error.Code, error.Message, error.Location.Offset));
    }

    // The text that fails to parse is the text of the argument: the error holds it by reference, and a variable gives it too.
    [Fact]
    public static void FailedParse_NamesTheStringOfAVariable()
    {
        CelError error = Assert.Single(Evaluate("ip(text)", new CelBindings().Add("text", "10.0.0")).Errors);

        Assert.Equal("invalid argument to 'ip': IP Address \"10.0.0\" parse error during conversion from string: IPv4 address too short", error.Message);
    }

    // The cost of the argument is charged before the parse runs, so a limit below it stops the call, and a limit at it reaches the error.
    [Fact]
    public static void FailedParse_IsReachedAfterItsCostIsCharged()
    {
        Assert.Equal(CelErrorCode.InvalidArgument, Assert.Single(Evaluate("ip('bad')", null, 2).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("ip('bad')", null, 1).Errors).Code);
    }

    // A value of an abstract type that no overload of the function takes is EvalNoMatchingOverload, which names it by its type; the
    // kinds of the other arguments stay in their places, before and after it.
    [Theory]
    [InlineData("x.isLoopback()", "cidr('10.0.0.0/8')", "1", "no such overload: isLoopback(net.CIDR)")]
    [InlineData("y.masked()", "1", "ip('1.2.3.4')", "no such overload: masked(net.IP)")]
    [InlineData("x.containsIP(y)", "cidr('10.0.0.0/8')", "cidr('10.0.0.0/8')", "no such overload: containsIP(net.CIDR, net.CIDR)")]
    [InlineData("x.containsIP(y)", "ip('1.2.3.4')", "cidr('10.0.0.0/8')", "no such overload: containsIP(net.IP, net.CIDR)")]
    [InlineData("x.containsIP(y)", "cidr('10.0.0.0/8')", "5", "no such overload: containsIP(net.CIDR, int)")]
    [InlineData("x.containsIP(y)", "5", "ip('1.2.3.4')", "no such overload: containsIP(int, net.IP)")]
    [InlineData("x.containsCIDR(y)", "cidr('10.0.0.0/8')", "ip('1.2.3.4')", "no such overload: containsCIDR(net.CIDR, net.IP)")]
    public static void ArgumentOfAnotherType_IsNamedByItsType(string expression, string first, string second, string message)
    {
        CelBindings bindings = new CelBindings().Add("x", ValueOf(first)).Add("y", ValueOf(second));

        CelError error = Assert.Single(Evaluate(expression, bindings).Errors);

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
        Assert.Equal(message, error.Message);
    }

    // A value that is no address, a null or a number reaches the function through dyn and is refused by it.
    [Theory]
    [InlineData("x.family()")]
    [InlineData("x.isGlobalUnicast()")]
    [InlineData("y.prefixLength()")]
    [InlineData("y.isMask()")]
    public static void ArgumentThatIsNoValueOfTheSet_IsNoMatchingOverload(string expression)
    {
        foreach (CelValue other in new[] { CelValue.Null, CelValue.FromInt64(5), CelValue.FromString("1.2.3.4") })
        {
            CelBindings bindings = new CelBindings().Add("x", other).Add("y", other);

            Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(Evaluate(expression, bindings).Errors).Code);
        }
    }

    // The three forms of a string argument that is no string: a number through dyn.
    [Theory]
    [InlineData("isIP(x)")]
    [InlineData("isCIDR(x)")]
    [InlineData("ip(x)")]
    [InlineData("cidr(x)")]
    [InlineData("ip.isCanonical(x)")]
    public static void StringArgumentThatIsNoString_IsNoMatchingOverload(string expression)
    {
        CelBindings bindings = new CelBindings().Add("x", 5L);

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(Evaluate(expression, bindings).Errors).Code);
    }

    private static TheoryData<string> HoldingData()
    {
        TheoryData<string> data = [];
        foreach (string row in Holds.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            data.Add(row);
        }

        return data;
    }
}
