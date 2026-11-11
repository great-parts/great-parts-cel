using System;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Library.Extensions.Network;

// The text forms of addresses and prefixes, and the words of the parse errors. An address is written as netip.Addr.String() of Go
// does: IPv4 as four decimal fields; IPv6 in lower case with no leading zeros in a group, the first of the longest runs of two or
// more zero groups replaced by '::', and no replacement for a single zero group. A prefix is the address as written, '/' and the
// length. The length of a text is known before the text is made, so a caller charges it first.
internal static class AddressText
{
    // The longest text of an address: eight groups of four digits and seven colons.
    internal const int MaxAddressLength = 39;

    private static readonly SpanAction<char, TextState> WriteState = static (span, state) => state.WriteTo(span);

    // The text of an address of IPv4 or IPv6.
    internal static string Of(AddressBits address) => string.Create(LengthOf(address), new TextState(address, -1), WriteState);

    // The text of a prefix: the address and the length.
    internal static string Of(AddressBits address, int length) => string.Create(LengthOf(address, length), new TextState(address, length), WriteState);

    // The number of characters of the text of an address.
    internal static int LengthOf(AddressBits address)
    {
        if (address.IsIPv4)
        {
            return 3 + Digits((int)((address.Low >> 24) & 0xFF)) + Digits((int)((address.Low >> 16) & 0xFF))
                + Digits((int)((address.Low >> 8) & 0xFF)) + Digits((int)(address.Low & 0xFF));
        }

        FindZeroRun(address, out int zeroStart, out int zeroEnd);
        int length = 0;
        for (int i = 0; i < 8; i++)
        {
            if (i == zeroStart)
            {
                length += 2;
                i = zeroEnd;
                if (i >= 8)
                {
                    break;
                }
            }
            else if (i > 0)
            {
                length++;
            }

            length += HexDigits(address.Group(i));
        }

        return length;
    }

    // The number of characters of the text of a prefix of the given length.
    internal static int LengthOf(AddressBits address, int length) => LengthOf(address) + 1 + Digits(length);

    // Writes the text of an address into a span of at least LengthOf(address) characters; returns the number written.
    internal static int Write(AddressBits address, Span<char> destination)
    {
        int written = 0;
        if (address.IsIPv4)
        {
            written += WriteDecimal((int)((address.Low >> 24) & 0xFF), destination[written..]);
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                destination[written++] = '.';
                written += WriteDecimal((int)((address.Low >> shift) & 0xFF), destination[written..]);
            }

            return written;
        }

        FindZeroRun(address, out int zeroStart, out int zeroEnd);
        for (int i = 0; i < 8; i++)
        {
            if (i == zeroStart)
            {
                destination[written++] = ':';
                destination[written++] = ':';
                i = zeroEnd;
                if (i >= 8)
                {
                    break;
                }
            }
            else if (i > 0)
            {
                destination[written++] = ':';
            }

            written += WriteHex(address.Group(i), destination[written..]);
        }

        return written;
    }

    // The words of a parse error: the detail that follows "invalid argument to 'f': ". An address that is no IP reads
    // IP Address "…" parse error during conversion from string: <reason>; a prefix CIDR "…" … likewise. The refusals of the set have
    // their own words. The text is the argument as it was written; the message is made when the error is read.
    internal static string Describe(ParseFailure failure, bool isPrefix, string text)
    {
        string subject = isPrefix ? "CIDR" : "IP Address";
        var detail = new StringBuilder();
        switch (failure)
        {
            case ParseFailure.MappedNotAllowed:
                return detail.Append("IPv4-mapped IPv6 address \"").Append(text).Append("\" is not allowed").ToString();
            case ParseFailure.ZoneNotAllowed:
                return detail.Append(subject).Append(" \"").Append(text).Append("\" with zone value is not allowed").ToString();
            default:
                detail.Append(subject).Append(" \"").Append(text).Append("\" parse error during conversion from string: ").Append(Reason(failure));
                if (failure == ParseFailure.BadBits)
                {
                    int slash = AddressParser.LastSlash(text);
                    detail.Append(": \"").Append(text, slash + 1, text.Length - slash - 1).Append('"');
                }

                return detail.ToString();
        }
    }

    // The reason of Go for a failure, with the three failures of the set that Go has no words for.
    private static string Reason(ParseFailure failure) => failure switch
    {
        ParseFailure.UnableToParse => "unable to parse IP",
        ParseFailure.MissingIPv6Address => "missing IPv6 address",
        ParseFailure.IPv4FieldEmpty => "IPv4 field must have at least one digit",
        ParseFailure.IPv4FieldTooLarge => "IPv4 field has value >255",
        ParseFailure.IPv4LeadingZero => "IPv4 field has octet with leading zero",
        ParseFailure.IPv4TooLong => "IPv4 address too long",
        ParseFailure.IPv4TooShort => "IPv4 address too short",
        ParseFailure.UnexpectedCharacter => "unexpected character",
        ParseFailure.IPv6FieldEmpty => "each colon-separated field must have at least one digit",
        ParseFailure.IPv6FieldTooLong => "each colon-separated field must have at most 4 hex digits",
        ParseFailure.EmbeddedIPv4Place => "embedded IPv4 address must replace the final 2 fields of the address",
        ParseFailure.EmbeddedIPv4NoRoom => "too many hex fields to fit an embedded IPv4 at the end of the address",
        ParseFailure.WantColon => "unexpected character, want colon",
        ParseFailure.ColonNeedsMore => "colon must be followed by more characters",
        ParseFailure.MultipleEllipsis => "multiple :: in address",
        ParseFailure.TrailingGarbage => "trailing garbage after address",
        ParseFailure.AddressTooShort => "address string too short",
        ParseFailure.EllipsisExpandsNothing => "the :: must expand to at least one field of zeros",
        ParseFailure.ZoneEmpty => "zone must be a non-empty string",
        ParseFailure.NoSlash => "no '/'",
        ParseFailure.BadBits => "bad bits after slash",
        ParseFailure.BitsOutOfRange => "prefix length out of range",
        _ => "invalid address",
    };

    // The first of the longest runs of two or more zero groups of an IPv6 address: groups [start, end); start is 255 for none, so
    // that no index equals it.
    private static void FindZeroRun(AddressBits address, out int start, out int end)
    {
        start = 255;
        end = 255;
        for (int i = 0; i < 8; i++)
        {
            int j = i;
            while (j < 8 && address.Group(j) == 0)
            {
                j++;
            }

            int run = j - i;
            if (run >= 2 && run > end - start)
            {
                start = i;
                end = j;
            }
        }
    }

    private static int Digits(int value) => value < 10 ? 1 : value < 100 ? 2 : 3;

    private static int HexDigits(ushort value) => value < 0x10 ? 1 : value < 0x100 ? 2 : value < 0x1000 ? 3 : 4;

    private static int WriteDecimal(int value, Span<char> destination)
    {
        int length = Digits(value);
        value.TryFormat(destination, out _, default, CultureInfo.InvariantCulture);
        return length;
    }

    private static int WriteHex(ushort value, Span<char> destination)
    {
        int length = HexDigits(value);
        for (int i = length - 1; i >= 0; i--)
        {
            int digit = value & 0xF;
            destination[i] = (char)(digit < 10 ? '0' + digit : 'a' + digit - 10);
            value >>= 4;
        }

        return length;
    }

    // An address and the length of the prefix it belongs to, -1 for none: what string.Create needs to write the text.
    private readonly struct TextState(AddressBits address, int length)
    {
        internal void WriteTo(Span<char> destination)
        {
            int written = Write(address, destination);
            if (length >= 0)
            {
                destination[written++] = '/';
                _ = length.TryFormat(destination[written..], out _, default, CultureInfo.InvariantCulture);
            }
        }
    }
}
