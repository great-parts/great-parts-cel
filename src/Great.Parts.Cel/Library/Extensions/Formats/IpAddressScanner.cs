using System;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The scanner of IP addresses that isIpPrefix and isHostAndPort share; it is no function itself. IPv4 is four decimal fields of 1 to 3
// digits, at most 255, without a leading zero unless the field is 0. IPv6 is the text of RFC 4291: eight groups of 1 to 4 hexadecimal
// digits, at most one "::" that stands for at least one group, and the last 32 bits may be an IPv4. A zone is no part of an address:
// the callers that allow one cut it off before the scan, with ScanIPv6AndCutZone. One pass, no recursion; the text of an IPv4 tail is read twice at most, once
// as hexadecimal digits of a group and once as the fields, which is a constant number of units.
internal static class IpAddressScanner
{
    private const int GroupCount = 8;
    private const int MaxGroupDigits = 4;
    private const int MaxFieldDigits = 3;

    // An IPv4 address that fills the whole text; the address is the four bytes, the first one the most significant.
    internal static bool ScanIPv4<TCounter>(ReadOnlySpan<char> text, out uint address, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        address = 0;
        int position = 0;
        for (int field = 0; field < 4; field++)
        {
            if (!ScanField(text, ref position, out uint value, ref counter))
            {
                return false;
            }

            address = (address << 8) | value;
            if (field < 3 && !SkipDot(text, ref position))
            {
                return false;
            }
        }

        return position == text.Length;
    }

    // An IPv6 address that fills the text up to the first '%'. When the text has a '%', the zone is what follows it, whatever it is: its
    // own rule is the caller's.
    internal static bool ScanIPv6AndCutZone<TCounter>(
        ReadOnlySpan<char> text,
        out bool hasZone,
        out ReadOnlySpan<char> zone,
        ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int percent = FormatChars.IndexOf(text, '%', ref counter);
        hasZone = percent >= 0;
        zone = hasZone ? text[(percent + 1)..] : default;
        Span<ushort> groups = stackalloc ushort[GroupCount];
        return ScanIPv6(hasZone ? text[..percent] : text, groups, ref counter);
    }

    // An IPv6 address that fills the whole text, into eight groups of 16 bits, the first one the most significant.
    internal static bool ScanIPv6<TCounter>(ReadOnlySpan<char> text, Span<ushort> groups, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int count = 0;
        int ellipsis = -1;
        int position = 0;
        if (text.Length > 0 && text[0] == ':')
        {
            if (text.Length < 2 || text[1] != ':')
            {
                return false;
            }

            ellipsis = 0;
            position = 2;
        }

        while (position < text.Length)
        {
            if (!ScanGroup(text, ref position, groups, ref count, out bool tail, ref counter))
            {
                return false;
            }

            if (tail || position == text.Length)
            {
                break;
            }

            if (!ScanSeparator(text, ref position, count, ref ellipsis))
            {
                return false;
            }
        }

        return Expand(groups, count, ellipsis);
    }

    // One field of an IPv4 address: 1 to 3 digits, no leading zero unless the field is 0, at most 255.
    private static bool ScanField<TCounter>(ReadOnlySpan<char> text, ref int position, out uint value, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int start = position;
        value = 0;
        while (position < text.Length && FormatChars.IsDigit(text[position]))
        {
            counter.Step();
            if (position - start == MaxFieldDigits)
            {
                return false;
            }

            value = (value * 10) + (uint)(text[position] - '0');
            position++;
        }

        int length = position - start;
        return length != 0 && (length == 1 || text[start] != '0') && value <= 255;
    }

    private static bool SkipDot(ReadOnlySpan<char> text, ref int position)
    {
        if (position >= text.Length || text[position] != '.')
        {
            return false;
        }

        position++;
        return true;
    }

    // One group of hexadecimal digits, or the IPv4 tail when a dot follows the digits; the tail ends the text.
    private static bool ScanGroup<TCounter>(
        ReadOnlySpan<char> text,
        ref int position,
        Span<ushort> groups,
        ref int count,
        out bool tail,
        ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        tail = false;
        int start = position;
        int value = 0;
        while (position < text.Length && FormatChars.IsHex(text[position]))
        {
            counter.Step();
            if (position - start == MaxGroupDigits)
            {
                return false;
            }

            value = (value << 4) | FormatChars.HexValue(text[position]);
            position++;
        }

        if (position == start)
        {
            return false;
        }

        if (position < text.Length && text[position] == '.')
        {
            tail = true;
            position = text.Length;
            return ScanTail(text[start..], groups, ref count, ref counter);
        }

        if (count == GroupCount)
        {
            return false;
        }

        groups[count++] = (ushort)value;
        return true;
    }

    // The IPv4 address that ends the text, as the last two groups.
    private static bool ScanTail<TCounter>(ReadOnlySpan<char> text, Span<ushort> groups, ref int count, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (count > GroupCount - 2 || !ScanIPv4(text, out uint address, ref counter))
        {
            return false;
        }

        groups[count++] = (ushort)(address >> 16);
        groups[count++] = (ushort)address;
        return true;
    }

    // The ':' between two groups, and a second ':' that makes the "::". The text must go on after a single ':'.
    private static bool ScanSeparator(ReadOnlySpan<char> text, ref int position, int count, ref int ellipsis)
    {
        if (text[position] != ':')
        {
            return false;
        }

        position++;
        if (position == text.Length)
        {
            return false;
        }

        if (text[position] != ':')
        {
            return true;
        }

        if (ellipsis >= 0)
        {
            return false;
        }

        ellipsis = count;
        position++;
        return true;
    }

    // Puts the groups in place: without "::" there are eight, with it at most seven (the "::" stands for at least one group), and the
    // groups after it move to the end, the gap being zeros.
    private static bool Expand(Span<ushort> groups, int count, int ellipsis)
    {
        if (ellipsis < 0)
        {
            return count == GroupCount;
        }

        if (count >= GroupCount)
        {
            return false;
        }

        int tail = count - ellipsis;
        groups[ellipsis..count].CopyTo(groups[(GroupCount - tail)..]);
        groups[ellipsis..(GroupCount - tail)].Clear();
        return true;
    }
}
