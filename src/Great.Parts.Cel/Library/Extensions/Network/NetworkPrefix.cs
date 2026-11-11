using System;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Network;

// A value of net.CIDR: one object of at most 48 bytes with the address as it was written, host bits included, and the length of the
// prefix. Two values are equal when they have the same family, the same address as written and the same length, so
// 10.0.0.1/8 and 10.0.0.0/8 differ.
internal sealed class NetworkPrefix(AddressBits address, byte length) : AbstractValue
{
    internal AddressBits Address { get; } = address;

    // The number of leading bits that make the network: 0 to 32 for IPv4, 0 to 128 for IPv6.
    internal byte Length { get; } = length;

    internal override CelType Type => NetworkTypes.Cidr;

    internal override bool ContentEquals(AbstractValue other) =>
        other is NetworkPrefix prefix && Length == prefix.Length && Address.SameAs(prefix.Address);

    internal override int ContentHash() => HashCode.Combine(Address.High, Address.Low, Address.IsIPv4, Length);
}
