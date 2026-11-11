using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions.Network;

// The table of the extension set network, version 1: the abstract types net.IP and net.CIDR and 21 overloads in the order of
// cel-go, with the identifiers of cel-go. A call is 1 unit; the seven overloads that parse a string add ⌈Lₐ/10⌉ for its length, and
// the two conversions to text add ⌈Lᵣ/10⌉ for the length of the text, both charged before the function runs.
internal static class NetworkExtension
{
    internal const string SetName = "network";
    internal const uint Version = 1;
    private const string Source = "extension network v1";

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version
            ? new CatalogExtension(
                SetName,
                Version,
                Overloads(),
                abstractTypes: [new AbstractTypeDeclaration(NetworkTypes.IpName, NetworkTypes.Ip), new AbstractTypeDeclaration(NetworkTypes.CidrName, NetworkTypes.Cidr)])
            : null;

    // The set that declares the abstract type of a full name, for the hint of a build error; null for any other name.
    internal static string? SetOfType(string typeName) =>
        typeName is NetworkTypes.IpName or NetworkTypes.CidrName ? SetName : null;

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType ip = NetworkTypes.Ip;
        CelType cidr = NetworkTypes.Cidr;
        CelType text = CelType.String;
        CelType number = CelType.Int;
        CelType boolean = CelType.Bool;
        return
        [
            Global("ip", "string_to_ip", [text], ip, NetworkFunctions.StringToIp, Costs.Text),
            Receiver("ip", "cidr_ip", cidr, [], ip, NetworkFunctions.CidrIp),
            Global("cidr", "string_to_cidr", [text], cidr, NetworkFunctions.StringToCidr, Costs.Text),
            Global("string", "ip_to_string", [ip], text, NetworkFunctions.IpToString, NetworkFunctions.IpTextCost),
            Global("string", "cidr_to_string", [cidr], text, NetworkFunctions.CidrToString, NetworkFunctions.CidrTextCost),
            Global("isIP", "is_ip", [text], boolean, NetworkFunctions.IsIp, Costs.Text),
            Global("isCIDR", "is_cidr", [text], boolean, NetworkFunctions.IsCidr, Costs.Text),
            Global("ip.isCanonical", "ip_is_canonical", [text], boolean, NetworkFunctions.IpIsCanonical, Costs.Text),
            Receiver("family", "ip_family", ip, [], number, NetworkFunctions.IpFamily),
            Receiver("isLoopback", "ip_is_loopback", ip, [], boolean, NetworkFunctions.IpIsLoopback),
            Receiver("isGlobalUnicast", "ip_is_global_unicast", ip, [], boolean, NetworkFunctions.IpIsGlobalUnicast),
            Receiver("isLinkLocalMulticast", "ip_is_link_local_multicast", ip, [], boolean, NetworkFunctions.IpIsLinkLocalMulticast),
            Receiver("isLinkLocalUnicast", "ip_is_link_local_unicast", ip, [], boolean, NetworkFunctions.IpIsLinkLocalUnicast),
            Receiver("isUnspecified", "ip_is_unspecified", ip, [], boolean, NetworkFunctions.IpIsUnspecified),
            Receiver("containsIP", "cidr_contains_ip_ip", cidr, [ip], boolean, NetworkFunctions.CidrContainsIpIp),
            Receiver("containsIP", "cidr_contains_ip_string", cidr, [text], boolean, NetworkFunctions.CidrContainsIpString, Costs.Text),
            Receiver("containsCIDR", "cidr_contains_cidr", cidr, [cidr], boolean, NetworkFunctions.CidrContainsCidr),
            Receiver("containsCIDR", "cidr_contains_cidr_string", cidr, [text], boolean, NetworkFunctions.CidrContainsCidrString, Costs.Text),
            Receiver("isMask", "cidr_is_mask", cidr, [], boolean, NetworkFunctions.CidrIsMask),
            Receiver("masked", "cidr_masked", cidr, [], cidr, NetworkFunctions.CidrMasked),
            Receiver("prefixLength", "cidr_prefix_length", cidr, [], number, NetworkFunctions.CidrPrefixLength),
        ];
    }

    // A function called as f(arguments).
    private static OverloadBinding Global(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, 1, argumentCost, Source);

    // A function called on its first parameter, the receiver: receiver.f(arguments).
    private static OverloadBinding Receiver(
        string function,
        string overloadId,
        CelType receiverType,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, true, [receiverType, .. parameterTypes], resultType), implementation, 1, argumentCost, Source);
}
