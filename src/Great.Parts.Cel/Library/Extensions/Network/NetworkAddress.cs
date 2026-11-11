using System;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Network;

// A value of net.IP: one object of 40 bytes with the 128 bits as two ulongs and the flag of IPv4. Two values are equal when they
// have the same family and the same bits.
internal sealed class NetworkAddress(AddressBits address) : AbstractValue
{
    internal AddressBits Address { get; } = address;

    internal override CelType Type => NetworkTypes.Ip;

    internal override bool ContentEquals(AbstractValue other) => other is NetworkAddress ip && Address.SameAs(ip.Address);

    internal override int ContentHash() => HashCode.Combine(Address.High, Address.Low, Address.IsIPv4);
}
