namespace Great.Parts.Cel.Library.Extensions.Network;

// The bits of an address of either family: 128 bits as two ulongs, the first 64 bits of an IPv6 address in High and the last 64 in
// Low; an IPv4 address sits in the low 32 bits of Low with High zero. IsIPv4 tells the family, so ::1.2.3.4 and 1.2.3.4 differ. A
// value type with no references, so the functions on it allocate nothing.
internal readonly struct AddressBits(ulong high, ulong low, bool isIPv4)
{
    internal ulong High { get; } = high;

    internal ulong Low { get; } = low;

    internal bool IsIPv4 { get; } = isIPv4;

    // The length of the address in bits: 32 for IPv4, 128 for IPv6.
    internal int BitLength => IsIPv4 ? 32 : 128;

    // The same bits and the same family.
    internal bool SameAs(AddressBits other) => High == other.High && Low == other.Low && IsIPv4 == other.IsIPv4;

    // The 16-bit group of an IPv6 address, 0 to 7 from the first.
    internal ushort Group(int index) =>
        (ushort)((index < 4 ? High >> (48 - (16 * index)) : Low >> (48 - (16 * (index - 4)))) & 0xFFFF);

    // The first byte of the address: of an IPv4 address its first octet, of an IPv6 address its first 8 bits.
    internal int FirstByte => IsIPv4 ? (int)((Low >> 24) & 0xFF) : (int)(High >> 56);

    // 0.0.0.0 or ::.
    internal bool IsUnspecified => High == 0 && Low == 0;

    // IPv4 with the first octet 127; IPv6 ::1.
    internal bool IsLoopback => IsIPv4 ? FirstByte == 127 : High == 0 && Low == 1;

    // IPv4 224 to 239; IPv6 with the first byte 0xff.
    internal bool IsMulticast => IsIPv4 ? (Low >> 28) == 0xE : FirstByte == 0xFF;

    // IPv4 169.254.0.0/16; IPv6 fe80::/10.
    internal bool IsLinkLocalUnicast => IsIPv4 ? (Low >> 16) == 0xA9FE : (High >> 54) == 0x3FA;

    // IPv4 224.0.0.0/24; IPv6 when the first 16 bits with the scope bits cleared are ff02: ff02::, ff12::, ff32:: are, ff01:: and ff0e:: are not.
    internal bool IsLinkLocalMulticast => IsIPv4 ? (Low >> 8) == 0xE00000 : ((High >> 48) & 0xFF0F) == 0xFF02;

    // Not unspecified, not loopback, not multicast, not link-local unicast and not 255.255.255.255: private IPv4 and unique-local
    // IPv6 addresses are global unicast, as in net/netip.
    internal bool IsGlobalUnicast =>
        !IsUnspecified && !IsLoopback && !IsMulticast && !IsLinkLocalUnicast && !(IsIPv4 && Low == uint.MaxValue);

    // The address with the bits after the first length bits cleared.
    internal AddressBits Masked(int length)
    {
        Mask(length, out ulong highMask, out ulong lowMask);
        return new AddressBits(High & highMask, Low & lowMask, IsIPv4);
    }

    // Whether any bit after the first length bits is set.
    internal bool HasHostBits(int length)
    {
        Mask(length, out ulong highMask, out ulong lowMask);
        return ((High & ~highMask) | (Low & ~lowMask)) != 0;
    }

    // Whether the first length bits of the two addresses are equal; the families are the caller's to compare.
    internal bool SamePrefix(AddressBits other, int length)
    {
        Mask(length, out ulong highMask, out ulong lowMask);
        return (((High ^ other.High) & highMask) | ((Low ^ other.Low) & lowMask)) == 0;
    }

    // The masks of the first length bits in the two halves. For IPv4 the mask is in the low 32 bits of Low.
    private void Mask(int length, out ulong highMask, out ulong lowMask)
    {
        if (IsIPv4)
        {
            highMask = 0;
            lowMask = length == 0 ? 0 : (ulong)(uint.MaxValue << (32 - length));
            return;
        }

        if (length <= 64)
        {
            highMask = length == 0 ? 0 : ulong.MaxValue << (64 - length);
            lowMask = 0;
            return;
        }

        highMask = ulong.MaxValue;
        lowMask = ulong.MaxValue << (128 - length);
    }
}
