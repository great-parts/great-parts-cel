using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions.Network;

// The two abstract types of the set, one instance each. The kind of a type of a set is Abstract, its name the full name, and it has no
// parameters in this release.
internal static class NetworkTypes
{
    internal const string IpName = "net.IP";
    internal const string CidrName = "net.CIDR";

    internal static CelType Ip { get; } = CelType.AbstractNamed(IpName, []);

    internal static CelType Cidr { get; } = CelType.AbstractNamed(CidrName, []);
}
