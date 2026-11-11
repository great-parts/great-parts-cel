using System;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Network;

// The implementations of the set network. Each checks the kinds of its arguments and answers EvalNoMatchingOverload for another,
// whatever the static types say. A failed parse is InvalidArgument at the call, whose words are made when the error is read: the
// error holds the function name, the text as written and the code of the failure. A parse allocates nothing on success; the text
// of an address is made once, after the call node has charged its length.
internal static class NetworkFunctions
{
    // ⌈Lᵣ/10⌉ for the text of an address, Lᵣ known from the family and the digits before the text is made.
    internal static readonly CelCostFunction IpTextCost = static arguments =>
        arguments.Length > 0 && TryAddress(arguments[0], out AddressBits address) ? Costs.Tenths((ulong)AddressText.LengthOf(address)) : 0;

    // ⌈Lᵣ/10⌉ for the text of a prefix.
    internal static readonly CelCostFunction CidrTextCost = static arguments =>
        arguments.Length > 0 && TryPrefix(arguments[0], out NetworkPrefix? prefix) ? Costs.Tenths((ulong)AddressText.LengthOf(prefix.Address, prefix.Length)) : 0;

    // The message of a failed parse: the function name is the first part, the text the second, the failure the number and whether
    // the text was read as a prefix the byte.
    private static readonly Func<CelError, string> ParseErrorText = static error =>
        "invalid argument to '" + (string)error.First! + "': " + AddressText.Describe((ParseFailure)error.Number, error.Small == 1, (string)error.Second!);

    // string_to_ip, ip(string) -> net.IP.
    internal static CelValue StringToIp(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        ParseFailure failure = AddressParser.ParseAddress(text, out AddressBits address);
        return failure == ParseFailure.None ? CelValue.FromAbstract(new NetworkAddress(address)) : ParseError(in site, failure, false, text);
    }

    // string_to_cidr, cidr(string) -> net.CIDR.
    internal static CelValue StringToCidr(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        ParseFailure failure = AddressParser.ParsePrefix(text, out AddressBits address, out int length);
        return failure == ParseFailure.None ? CelValue.FromAbstract(new NetworkPrefix(address, (byte)length)) : ParseError(in site, failure, true, text);
    }

    // cidr_ip, net.CIDR.ip() -> net.IP: the address as written, with its host bits.
    internal static CelValue CidrIp(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix) ? CelValue.FromAbstract(new NetworkAddress(prefix.Address)) : site.NoMatchingOverload(arguments);

    // ip_to_string, string(net.IP) -> string.
    internal static CelValue IpToString(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromString(AddressText.Of(address)) : site.NoMatchingOverload(arguments);

    // cidr_to_string, string(net.CIDR) -> string: the address as written, '/' and the length.
    internal static CelValue CidrToString(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix) ? CelValue.FromString(AddressText.Of(prefix.Address, prefix.Length)) : site.NoMatchingOverload(arguments);

    // is_ip, isIP(string) -> bool: true when the strict parse succeeds; never an error.
    internal static CelValue IsIp(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetString(out string? text)
            ? CelValue.FromBoolean(AddressParser.ParseAddress(text, out _) == ParseFailure.None)
            : site.NoMatchingOverload(arguments);

    // is_cidr, isCIDR(string) -> bool: true when the strict parse succeeds, host bits allowed; never an error.
    internal static CelValue IsCidr(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetString(out string? text)
            ? CelValue.FromBoolean(AddressParser.ParsePrefix(text, out _, out _) == ParseFailure.None)
            : site.NoMatchingOverload(arguments);

    // ip_is_canonical, ip.isCanonical(string) -> bool: the strict parse, an error when it fails, then the text equal to its own text form.
    internal static CelValue IpIsCanonical(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        ParseFailure failure = AddressParser.ParseAddress(text, out AddressBits address);
        if (failure != ParseFailure.None)
        {
            return ParseError(in site, failure, false, text);
        }

        Span<char> written = stackalloc char[AddressText.MaxAddressLength];
        int length = AddressText.Write(address, written);
        return CelValue.FromBoolean(text.AsSpan().SequenceEqual(written[..length]));
    }

    // ip_family, net.IP.family() -> int: 4 or 6.
    internal static CelValue IpFamily(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromInt64(address.IsIPv4 ? 4 : 6) : site.NoMatchingOverload(arguments);

    // ip_is_loopback, net.IP.isLoopback() -> bool.
    internal static CelValue IpIsLoopback(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromBoolean(address.IsLoopback) : site.NoMatchingOverload(arguments);

    // ip_is_unspecified, net.IP.isUnspecified() -> bool.
    internal static CelValue IpIsUnspecified(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromBoolean(address.IsUnspecified) : site.NoMatchingOverload(arguments);

    // ip_is_link_local_unicast, net.IP.isLinkLocalUnicast() -> bool.
    internal static CelValue IpIsLinkLocalUnicast(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromBoolean(address.IsLinkLocalUnicast) : site.NoMatchingOverload(arguments);

    // ip_is_link_local_multicast, net.IP.isLinkLocalMulticast() -> bool.
    internal static CelValue IpIsLinkLocalMulticast(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromBoolean(address.IsLinkLocalMulticast) : site.NoMatchingOverload(arguments);

    // ip_is_global_unicast, net.IP.isGlobalUnicast() -> bool.
    internal static CelValue IpIsGlobalUnicast(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryAddress(arguments[0], out AddressBits address) ? CelValue.FromBoolean(address.IsGlobalUnicast) : site.NoMatchingOverload(arguments);

    // cidr_prefix_length, net.CIDR.prefixLength() -> int.
    internal static CelValue CidrPrefixLength(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix) ? CelValue.FromInt64(prefix.Length) : site.NoMatchingOverload(arguments);

    // cidr_is_mask, net.CIDR.isMask() -> bool: true when the address has no host bits.
    internal static CelValue CidrIsMask(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix) ? CelValue.FromBoolean(!prefix.Address.HasHostBits(prefix.Length)) : site.NoMatchingOverload(arguments);

    // cidr_masked, net.CIDR.masked() -> net.CIDR: the host bits of the address cleared, the same length.
    internal static CelValue CidrMasked(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix)
            ? CelValue.FromAbstract(new NetworkPrefix(prefix.Address.Masked(prefix.Length), prefix.Length))
            : site.NoMatchingOverload(arguments);

    // cidr_contains_ip_ip, net.CIDR.containsIP(net.IP) -> bool.
    internal static CelValue CidrContainsIpIp(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix) && TryAddress(arguments[1], out AddressBits address)
            ? CelValue.FromBoolean(Contains(prefix, address))
            : site.NoMatchingOverload(arguments);

    // cidr_contains_ip_string, net.CIDR.containsIP(string) -> bool: the strict parse of the string, an error when it fails.
    internal static CelValue CidrContainsIpString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryPrefix(arguments[0], out NetworkPrefix? prefix) || !arguments[1].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        ParseFailure failure = AddressParser.ParseAddress(text, out AddressBits address);
        return failure == ParseFailure.None ? CelValue.FromBoolean(Contains(prefix, address)) : ParseError(in site, failure, false, text);
    }

    // cidr_contains_cidr, net.CIDR.containsCIDR(net.CIDR) -> bool.
    internal static CelValue CidrContainsCidr(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        TryPrefix(arguments[0], out NetworkPrefix? prefix) && TryPrefix(arguments[1], out NetworkPrefix? other)
            ? CelValue.FromBoolean(Contains(prefix, other.Address, other.Length))
            : site.NoMatchingOverload(arguments);

    // cidr_contains_cidr_string, net.CIDR.containsCIDR(string) -> bool: the strict parse of the string as a prefix, an error when it fails.
    internal static CelValue CidrContainsCidrString(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!TryPrefix(arguments[0], out NetworkPrefix? prefix) || !arguments[1].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        ParseFailure failure = AddressParser.ParsePrefix(text, out AddressBits address, out int length);
        return failure == ParseFailure.None
            ? CelValue.FromBoolean(Contains(prefix, address, length))
            : ParseError(in site, failure, true, text);
    }

    // An address of the family of the prefix whose first Length bits equal those of the prefix; another family is false.
    private static bool Contains(NetworkPrefix prefix, AddressBits address) =>
        prefix.Address.IsIPv4 == address.IsIPv4 && prefix.Address.SamePrefix(address, prefix.Length);

    // A prefix of the same family, not shorter, whose first Length bits equal those of the prefix; the host bits of both are ignored.
    private static bool Contains(NetworkPrefix prefix, AddressBits otherAddress, int otherLength) =>
        prefix.Address.IsIPv4 == otherAddress.IsIPv4 && otherLength >= prefix.Length && prefix.Address.SamePrefix(otherAddress, prefix.Length);

    private static bool TryAddress(CelValue value, out AddressBits address)
    {
        if (value.TryGetAbstract(out AbstractValue? content) && content is NetworkAddress ip)
        {
            address = ip.Address;
            return true;
        }

        address = default;
        return false;
    }

    private static bool TryPrefix(CelValue value, [NotNullWhen(true)] out NetworkPrefix? prefix)
    {
        if (value.TryGetAbstract(out AbstractValue? content) && content is NetworkPrefix found)
        {
            prefix = found;
            return true;
        }

        prefix = null;
        return false;
    }

    // InvalidArgument at the call; one object, whose message is made when it is read.
    private static CelValue ParseError(in CallSite site, ParseFailure failure, bool isPrefix, string text) =>
        site.Error(CelErrorCode.InvalidArgument, ParseErrorText, site.Function, text, (long)failure, isPrefix ? (byte)1 : (byte)0);
}
