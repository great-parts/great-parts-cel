using System;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The format of isIpPrefix: an IP address without a zone, '/', and the length of the prefix in decimal, up to 32 for IPv4 and 128 for
// IPv6, without a sign and without a leading zero unless the length is 0. The version is 0 for either family, 4 or 6 for that one
// only; any other value gives false. The strict form also requires the bits of the address after the length to be zero.
internal static class IpPrefixFormat
{
    private const int MaxLengthDigits = 3;

    internal static bool IsIpPrefix<TCounter>(ReadOnlySpan<char> text, long version, bool strict, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (version is not (0 or 4 or 6))
        {
            return false;
        }

        int slash = FormatChars.IndexOf(text, '/', ref counter);
        if (slash < 0 || !ScanLength(text[(slash + 1)..], out int length, ref counter))
        {
            return false;
        }

        ReadOnlySpan<char> address = text[..slash];
        return FormatChars.IndexOf(address, ':', ref counter) >= 0
            ? version != 4 && IsIPv6(address, length, strict, ref counter)
            : version != 6 && IsIPv4(address, length, strict, ref counter);
    }

    // The length of the prefix: 1 to 3 digits, and no leading zero unless it is exactly 0.
    private static bool ScanLength<TCounter>(ReadOnlySpan<char> digits, out int length, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        length = 0;
        if (digits.Length == 0 || digits.Length > MaxLengthDigits || (digits.Length > 1 && digits[0] == '0'))
        {
            return false;
        }

        foreach (char digit in digits)
        {
            counter.Step();
            if (!FormatChars.IsDigit(digit))
            {
                return false;
            }

            length = (length * 10) + (digit - '0');
        }

        return true;
    }

    private static bool IsIPv4<TCounter>(ReadOnlySpan<char> address, int length, bool strict, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (length > 32 || !IpAddressScanner.ScanIPv4(address, out uint bits, ref counter))
        {
            return false;
        }

        // A shift by 32 is a shift by 0 in C#, so a prefix of 32 bits has no host bits to look at.
        return !strict || length == 32 || (bits & (uint.MaxValue >> length)) == 0;
    }

    private static bool IsIPv6<TCounter>(ReadOnlySpan<char> address, int length, bool strict, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        Span<ushort> groups = stackalloc ushort[8];
        if (length > 128 || !IpAddressScanner.ScanIPv6(address, groups, ref counter))
        {
            return false;
        }

        return !strict || HostBitsAreZero(groups, length);
    }

    // Whether the bits after the first length bits of the eight groups are all zero.
    private static bool HostBitsAreZero(ReadOnlySpan<ushort> groups, int length)
    {
        for (int group = 0; group < groups.Length; group++)
        {
            int firstBit = group * 16;
            if (length >= firstBit + 16)
            {
                continue;
            }

            // The bits of the group that belong to the prefix, 0 to 15; the others are host bits.
            int prefixBits = Math.Max(length - firstBit, 0);
            if ((groups[group] & (0xFFFF >> prefixBits)) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
