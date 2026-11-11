using System;
using System.Buffers.Binary;

namespace Great.Parts.Cel.Library.Extensions.Network;

// The strict parser of addresses and prefixes: a port of netip.ParseAddr and netip.ParsePrefix of Go with the refusals of the set — an
// IPv4-mapped IPv6 address and a zone are errors. It reads UTF-16 units, and a unit above U+007F is an error wherever it stands. It
// allocates nothing: the result is a value, a failure is a code. The kind of an address is decided by the first of '.', ':' and '%'.
internal static class AddressParser
{
    // The longest prefix length that has a digit run which cannot overflow the accumulator; a longer run is a bad number.
    private const int MaxBitsDigits = 18;

    // A strict address. IPv4: four fields of 1 to 3 digits, no leading zero, at most 255. IPv6: groups of 1 to 4 hexadecimal digits with
    // at most one '::', and the last 32 bits may be a dotted IPv4. A zone and an IPv4-mapped address are refused.
    internal static ParseFailure ParseAddress(ReadOnlySpan<char> text, out AddressBits address)
    {
        ParseFailure failure = ParseRaw(text, out address, out bool hasZone);
        if (failure != ParseFailure.None)
        {
            return failure;
        }

        if (hasZone)
        {
            return ParseFailure.ZoneNotAllowed;
        }

        return IsMapped(address) ? ParseFailure.MappedNotAllowed : ParseFailure.None;
    }

    // A strict prefix: the text up to the last '/' is an address by the rules above, host bits allowed; the rest is one or more
    // ASCII digits with no sign and no leading zero unless it is exactly 0, at most 32 for IPv4 and 128 for IPv6.
    internal static ParseFailure ParsePrefix(ReadOnlySpan<char> text, out AddressBits address, out int length)
    {
        address = default;
        length = 0;
        int slash = LastSlash(text);
        if (slash < 0)
        {
            return ParseFailure.NoSlash;
        }

        ParseFailure failure = ParseRaw(text[..slash], out address, out bool hasZone);
        if (failure != ParseFailure.None)
        {
            return failure;
        }

        if (hasZone)
        {
            return ParseFailure.ZoneNotAllowed;
        }

        ReadOnlySpan<char> bits = text[(slash + 1)..];
        if (bits.Length == 0 || bits.Length > MaxBitsDigits || (bits.Length > 1 && bits[0] == '0'))
        {
            return ParseFailure.BadBits;
        }

        int value = 0;
        foreach (char digit in bits)
        {
            if (digit is < '0' or > '9')
            {
                return ParseFailure.BadBits;
            }

            // Past 1000 the value is out of range whatever follows; the cap keeps the accumulator small.
            value = Math.Min((value * 10) + (digit - '0'), 1000);
        }

        if (value > address.BitLength)
        {
            return ParseFailure.BitsOutOfRange;
        }

        length = value;
        return IsMapped(address) ? ParseFailure.MappedNotAllowed : ParseFailure.None;
    }

    // The position of the part of a prefix text after its last '/', as AddressText names the bits: the index of the slash.
    internal static int LastSlash(ReadOnlySpan<char> text)
    {
        for (int i = text.Length - 1; i >= 0; i--)
        {
            if (text[i] == '/')
            {
                return i;
            }
        }

        return -1;
    }

    // The first 80 bits zero and the next 16 ones: the IPv4-mapped IPv6 address, in any written form.
    private static bool IsMapped(AddressBits address) => !address.IsIPv4 && address.High == 0 && (address.Low >> 32) == 0xFFFF;

    // netip.ParseAddr: the first '.' makes the text an IPv4 address, the first ':' an IPv6 address, a '%' first is a missing address,
    // and none of the three is not an address. An IPv6 address may carry a zone, which the caller refuses.
    private static ParseFailure ParseRaw(ReadOnlySpan<char> text, out AddressBits address, out bool hasZone)
    {
        address = default;
        hasZone = false;
        foreach (char unit in text)
        {
            switch (unit)
            {
                case '.':
                    ParseFailure failure = ParseIPv4Fields(text, out uint value);
                    address = new AddressBits(0, value, isIPv4: true);
                    return failure;
                case ':':
                    return ParseIPv6(text, out address, out hasZone);
                case '%':
                    return ParseFailure.MissingIPv6Address;
            }
        }

        return ParseFailure.UnableToParse;
    }

    // The four decimal fields of an IPv4 address, in the order and with the failures of parseIPv4Fields of Go. The text is the
    // whole address: also the dotted tail of an IPv6 address, from the start of its first field.
    private static ParseFailure ParseIPv4Fields(ReadOnlySpan<char> text, out uint value)
    {
        value = 0;
        uint accumulated = 0;
        int field = 0;
        int position = 0;
        int digits = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char unit = text[i];
            if (unit is >= '0' and <= '9')
            {
                if (digits == 1 && field == 0)
                {
                    return ParseFailure.IPv4LeadingZero;
                }

                field = (field * 10) + (unit - '0');
                digits++;
                if (field > 255)
                {
                    return ParseFailure.IPv4FieldTooLarge;
                }
            }
            else if (unit == '.')
            {
                if (i == 0 || i == text.Length - 1 || text[i - 1] == '.')
                {
                    return ParseFailure.IPv4FieldEmpty;
                }

                if (position == 3)
                {
                    return ParseFailure.IPv4TooLong;
                }

                accumulated = (accumulated << 8) | (uint)field;
                position++;
                field = 0;
                digits = 0;
            }
            else
            {
                return ParseFailure.UnexpectedCharacter;
            }
        }

        if (position < 3)
        {
            return ParseFailure.IPv4TooShort;
        }

        value = (accumulated << 8) | (uint)field;
        return ParseFailure.None;
    }

    // netip's parseIPv6: the text before the first '%', the groups and the '::' in the order and with the failures of Go.
    private static ParseFailure ParseIPv6(ReadOnlySpan<char> input, out AddressBits address, out bool hasZone)
    {
        address = default;
        ParseFailure failure = SplitZone(input, out ReadOnlySpan<char> rest, out hasZone);
        if (failure != ParseFailure.None)
        {
            return failure;
        }

        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        failure = ReadGroups(rest, bytes, out int filled, out int ellipsis);
        if (failure != ParseFailure.None)
        {
            return failure;
        }

        failure = ExpandEllipsis(bytes, filled, ellipsis);
        if (failure != ParseFailure.None)
        {
            return failure;
        }

        address = new AddressBits(BinaryPrimitives.ReadUInt64BigEndian(bytes), BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]), isIPv4: false);
        return ParseFailure.None;
    }

    // The text before the first '%'; an empty zone is a failure of its own, as in Go.
    private static ParseFailure SplitZone(ReadOnlySpan<char> input, out ReadOnlySpan<char> rest, out bool hasZone)
    {
        rest = input;
        hasZone = false;
        for (int at = 0; at < input.Length; at++)
        {
            if (input[at] != '%')
            {
                continue;
            }

            if (at == input.Length - 1)
            {
                return ParseFailure.ZoneEmpty;
            }

            rest = input[..at];
            hasZone = true;
            break;
        }

        return ParseFailure.None;
    }

    // The groups of the address in order, each of 1 to 4 hexadecimal digits, with the position of the '::' (-1 for none) and the
    // number of bytes the groups fill. The dotted IPv4 at the end takes the place of the last two groups.
    private static ParseFailure ReadGroups(ReadOnlySpan<char> rest, Span<byte> bytes, out int filled, out int ellipsis)
    {
        filled = 0;
        ellipsis = -1;
        if (rest.Length >= 2 && rest[0] == ':' && rest[1] == ':')
        {
            ellipsis = 0;
            rest = rest[2..];
            if (rest.Length == 0)
            {
                return ParseFailure.None;
            }
        }

        while (filled < 16)
        {
            ParseFailure failure = ReadGroup(rest, out int length, out uint group);
            if (failure != ParseFailure.None)
            {
                return failure;
            }

            // A dot after the digits: the dotted IPv4 that stands for the last two groups ends the text.
            if (length < rest.Length && rest[length] == '.')
            {
                return ReadEmbeddedIPv4(rest, bytes, ref filled, ellipsis);
            }

            bytes[filled] = (byte)(group >> 8);
            bytes[filled + 1] = (byte)group;
            filled += 2;
            rest = rest[length..];
            if (rest.Length == 0)
            {
                return ParseFailure.None;
            }

            failure = ReadSeparator(ref rest, ref ellipsis, filled, out bool ended);
            if (failure != ParseFailure.None || ended)
            {
                return failure;
            }
        }

        return rest.Length != 0 ? ParseFailure.TrailingGarbage : ParseFailure.None;
    }

    // The hexadecimal digits at the start of the text: their number, at least 1 and at most 4, and their value.
    private static ParseFailure ReadGroup(ReadOnlySpan<char> rest, out int length, out uint group)
    {
        group = 0;
        for (length = 0; length < rest.Length; length++)
        {
            int digit = HexDigit(rest[length]);
            if (digit < 0)
            {
                break;
            }

            group = (group << 4) + (uint)digit;
            if (length > 3)
            {
                return ParseFailure.IPv6FieldTooLong;
            }
        }

        return length == 0 ? ParseFailure.IPv6FieldEmpty : ParseFailure.None;
    }

    // The dotted IPv4 from the start of the current field, in the place of the last two groups: at the end of the address, or after a
    // '::' that makes room for it.
    private static ParseFailure ReadEmbeddedIPv4(ReadOnlySpan<char> rest, Span<byte> bytes, ref int filled, int ellipsis)
    {
        if (ellipsis < 0 && filled != 12)
        {
            return ParseFailure.EmbeddedIPv4Place;
        }

        if (filled + 4 > 16)
        {
            return ParseFailure.EmbeddedIPv4NoRoom;
        }

        ParseFailure failure = ParseIPv4Fields(rest, out uint value);
        if (failure == ParseFailure.None)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes[filled..], value);
            filled += 4;
        }

        return failure;
    }

    // What follows a group: a ':' and more characters, and possibly a second ':', which is the '::' and may end the text.
    private static ParseFailure ReadSeparator(ref ReadOnlySpan<char> rest, ref int ellipsis, int filled, out bool ended)
    {
        ended = false;
        if (rest[0] != ':')
        {
            return ParseFailure.WantColon;
        }

        if (rest.Length == 1)
        {
            return ParseFailure.ColonNeedsMore;
        }

        rest = rest[1..];
        if (rest[0] == ':')
        {
            if (ellipsis >= 0)
            {
                return ParseFailure.MultipleEllipsis;
            }

            ellipsis = filled;
            rest = rest[1..];
            ended = rest.Length == 0;
        }

        return ParseFailure.None;
    }

    // The '::' stands for the missing zero bytes: the groups after it move to the end. Without a '::' all 16 bytes have to be
    // there, and with one it has to stand for at least one group.
    private static ParseFailure ExpandEllipsis(Span<byte> bytes, int filled, int ellipsis)
    {
        if (filled >= 16)
        {
            return ellipsis >= 0 ? ParseFailure.EllipsisExpandsNothing : ParseFailure.None;
        }

        if (ellipsis < 0)
        {
            return ParseFailure.AddressTooShort;
        }

        int shift = 16 - filled;
        for (int j = filled - 1; j >= ellipsis; j--)
        {
            bytes[j + shift] = bytes[j];
        }

        bytes.Slice(ellipsis, shift).Clear();
        return ParseFailure.None;
    }

    // The value of a hexadecimal digit of either case; -1 for any other unit.
    private static int HexDigit(char unit) => unit switch
    {
        >= '0' and <= '9' => unit - '0',
        >= 'a' and <= 'f' => unit - 'a' + 10,
        >= 'A' and <= 'F' => unit - 'A' + 10,
        _ => -1,
    };
}
