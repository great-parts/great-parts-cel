using System;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The formats of isUri and isUriRef, by the grammar of RFC 3986: URI (the scheme is required) and URI-reference (a URI or a relative
// reference). Each part has its own set of characters, and a percent-encoding is '%' and two hexadecimal digits; a raw unit above
// U+007F is in no set. The text that a host name or the zone of an IPv6 literal writes in percent-encodings has to be UTF-8, as the RFC
// asks of a registered name; elsewhere the octets are not interpreted. One pass over each part, no recursion, no backtracking: the
// authority is cut at its end, then each of its parts is read once.
internal static class UriFormat
{
    // The sets of characters of the parts that are read in one loop.
    private enum Part
    {
        // The first segment of a relative reference whose path does not start with '/': pchar without ':'.
        FirstSegment,

        // A path: pchar and '/'.
        Path,

        // A query or a fragment: pchar, '/' and '?'.
        QueryOrFragment,
    }

    internal static bool IsUri<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int colon = SchemeEnd(text, ref counter);
        return colon >= 0 && ScanHierarchy(text[(colon + 1)..], false, ref counter);
    }

    internal static bool IsUriReference<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int colon = SchemeEnd(text, ref counter);
        return colon >= 0 ? ScanHierarchy(text[(colon + 1)..], false, ref counter) : ScanHierarchy(text, true, ref counter);
    }

    // The index of the ':' that ends a scheme at the start of the text (a letter, then letters, digits, '+', '-' and '.'), or -1 when the
    // text does not start with a scheme.
    private static int SchemeEnd<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (text.Length == 0 || !FormatChars.IsLetter(text[0]))
        {
            return -1;
        }

        int position = 1;
        while (position < text.Length && (FormatChars.IsAlphanumeric(text[position]) || text[position] is '+' or '-' or '.'))
        {
            counter.Step();
            position++;
        }

        return position < text.Length && text[position] == ':' ? position : -1;
    }

    // What follows the scheme, or a relative reference as a whole: "//" and an authority and a path that is empty or starts with '/', or
    // a path alone; then an optional query and an optional fragment. The first segment of a relative path without '/' at the start has
    // no ':', or it would read as a scheme.
    private static bool ScanHierarchy<TCounter>(ReadOnlySpan<char> text, bool relative, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int position = 0;
        bool authority = text.Length >= 2 && text[0] == '/' && text[1] == '/';
        if (authority)
        {
            int end = AuthorityEnd(text, ref counter);
            if (!ScanAuthority(text[2..end], ref counter))
            {
                return false;
            }

            position = end;
        }
        else if (relative && (text.Length == 0 || text[0] != '/') && !ScanFirstSegment(text, ref position, ref counter))
        {
            return false;
        }

        return ScanChars(text, ref position, Part.Path, ref counter) && ScanQueryAndFragment(text[position..], ref counter);
    }

    // The first segment of a relative path: pchar without ':'. It ends at a unit that is no pchar, which the path or the query reads next,
    // except a ':', which makes the reference invalid.
    private static bool ScanFirstSegment<TCounter>(ReadOnlySpan<char> text, ref int position, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        return ScanChars(text, ref position, Part.FirstSegment, ref counter) && (position == text.Length || text[position] != ':');
    }

    // The index of the first '/', '?' or '#' after the "//", or the length of the text: where the authority ends.
    private static int AuthorityEnd<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int position = 2;
        while (position < text.Length && text[position] is not ('/' or '?' or '#'))
        {
            counter.Step();
            position++;
        }

        return position;
    }

    // Reads the units of the set from the position, and each percent-encoding of two hexadecimal digits; stops at the first other unit and
    // gives true, or gives false at a '%' that is no percent-encoding.
    private static bool ScanChars<TCounter>(ReadOnlySpan<char> text, ref int position, Part part, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        while (position < text.Length)
        {
            counter.Step();
            char c = text[position];
            if (c == '%')
            {
                if (!IsPercentEncoding(text, position))
                {
                    return false;
                }

                position += 3;
            }
            else if (IsAllowed(c, part))
            {
                position++;
            }
            else
            {
                return true;
            }
        }

        return true;
    }

    private static bool IsAllowed(char c, Part part) => part switch
    {
        Part.FirstSegment => IsSegmentChar(c) && c != ':',
        Part.Path => IsSegmentChar(c) || c == '/',
        _ => IsSegmentChar(c) || c is '/' or '?',
    };

    // pchar of RFC 3986 without the percent-encoding: unreserved, sub-delims, ':' and '@'.
    private static bool IsSegmentChar(char c) => FormatChars.IsUnreserved(c) || FormatChars.IsSubDelimiter(c) || c is ':' or '@';

    private static bool IsPercentEncoding(ReadOnlySpan<char> text, int position) =>
        position + 2 < text.Length && FormatChars.IsHex(text[position + 1]) && FormatChars.IsHex(text[position + 2]);

    // The part of the text that begins at the end of the path: nothing, or '?' and a query, '#' and a fragment, or both in that order.
    private static bool ScanQueryAndFragment<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int position = 0;
        if (position < text.Length && text[position] == '?')
        {
            position++;
            if (!ScanChars(text, ref position, Part.QueryOrFragment, ref counter))
            {
                return false;
            }
        }

        if (position < text.Length && text[position] == '#')
        {
            position++;
            if (!ScanChars(text, ref position, Part.QueryOrFragment, ref counter))
            {
                return false;
            }
        }

        return position == text.Length;
    }

    // The authority: optional user information up to the first '@', then the host and the port.
    private static bool ScanAuthority<TCounter>(ReadOnlySpan<char> authority, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int at = FormatChars.IndexOf(authority, '@', ref counter);
        if (at < 0)
        {
            return ScanHostAndPort(authority, ref counter);
        }

        return ScanUserInfo(authority[..at], ref counter) && ScanHostAndPort(authority[(at + 1)..], ref counter);
    }

    // userinfo: unreserved, percent-encodings, sub-delims and ':'.
    private static bool ScanUserInfo<TCounter>(ReadOnlySpan<char> userInfo, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int position = 0;
        while (position < userInfo.Length)
        {
            counter.Step();
            char c = userInfo[position];
            if (c == '%')
            {
                if (!IsPercentEncoding(userInfo, position))
                {
                    return false;
                }

                position += 3;
            }
            else if (FormatChars.IsUnreserved(c) || FormatChars.IsSubDelimiter(c) || c == ':')
            {
                position++;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    // The host, which is an IP literal in brackets or a registered name (possibly empty), then optionally ':' and a port of any number
    // of digits, possibly none.
    private static bool ScanHostAndPort<TCounter>(ReadOnlySpan<char> text, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        ReadOnlySpan<char> rest;
        if (text.Length > 0 && text[0] == '[')
        {
            int close = FormatChars.IndexOf(text, ']', ref counter);
            if (close < 0 || !ScanIpLiteral(text[1..close], ref counter))
            {
                return false;
            }

            rest = text[(close + 1)..];
        }
        else
        {
            int colon = FormatChars.IndexOf(text, ':', ref counter);
            ReadOnlySpan<char> host = colon < 0 ? text : text[..colon];
            if (!ScanUtf8Text(host, true, ref counter))
            {
                return false;
            }

            rest = text[host.Length..];
        }

        return rest.Length == 0 || (rest[0] == ':' && FormatChars.AllDigits(rest[1..], ref counter));
    }

    // IPvFuture, or an IPv6 address with an optional zone written as "%25" and the identifier of the zone (RFC 6874).
    private static bool ScanIpLiteral<TCounter>(ReadOnlySpan<char> literal, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (literal.Length > 0 && literal[0] is 'v' or 'V')
        {
            return ScanIpFuture(literal, ref counter);
        }

        return IpAddressScanner.ScanIPv6AndCutZone(literal, out bool hasZone, out ReadOnlySpan<char> zone, ref counter)
            && (!hasZone || ScanZone(zone, ref counter));
    }

    // "v", one or more hexadecimal digits, '.', then one or more of unreserved, sub-delims and ':'.
    private static bool ScanIpFuture<TCounter>(ReadOnlySpan<char> literal, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        int position = 1;
        while (position < literal.Length && FormatChars.IsHex(literal[position]))
        {
            counter.Step();
            position++;
        }

        if (position == 1 || position >= literal.Length - 1 || literal[position] != '.')
        {
            return false;
        }

        for (position++; position < literal.Length; position++)
        {
            counter.Step();
            char c = literal[position];
            if (!(FormatChars.IsUnreserved(c) || FormatChars.IsSubDelimiter(c) || c == ':'))
            {
                return false;
            }
        }

        return true;
    }

    // "25" and an identifier of one or more units, each unreserved or a percent-encoding; the octets of the encodings are UTF-8.
    private static bool ScanZone<TCounter>(ReadOnlySpan<char> zone, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        return zone.Length > 2 && zone[0] == '2' && zone[1] == '5' && ScanUtf8Text(zone[2..], false, ref counter);
    }

    // Unreserved units, optionally sub-delims, and percent-encodings whose octets are UTF-8: a sequence of octets cannot be left
    // unfinished by a unit that is no percent-encoding, nor at the end.
    private static bool ScanUtf8Text<TCounter>(ReadOnlySpan<char> text, bool subDelimiters, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        var sequence = default(Utf8Sequence);
        int position = 0;
        while (position < text.Length)
        {
            counter.Step();
            char c = text[position];
            if (c == '%')
            {
                if (!IsPercentEncoding(text, position) || !sequence.Accept(HexOctet(text, position)))
                {
                    return false;
                }

                position += 3;
            }
            else if (!sequence.Pending && (FormatChars.IsUnreserved(c) || (subDelimiters && FormatChars.IsSubDelimiter(c))))
            {
                position++;
            }
            else
            {
                return false;
            }
        }

        return !sequence.Pending;
    }

    private static byte HexOctet(ReadOnlySpan<char> text, int position) =>
        (byte)((FormatChars.HexValue(text[position + 1]) << 4) | FormatChars.HexValue(text[position + 2]));
}

// The state of a check that a sequence of octets is well-formed UTF-8: the lead octet says how many continuation octets follow, and the
// range of the first one narrows for E0, ED, F0 and F4 so that overlong forms, surrogates and values above U+10FFFF are refused.
internal struct Utf8Sequence
{
    private int _remaining;
    private byte _lower;
    private byte _upper;

    // Whether a sequence has started and is not finished.
    internal readonly bool Pending => _remaining > 0;

    // Takes the next octet; false when it cannot continue or start a well-formed sequence.
    internal bool Accept(byte octet)
    {
        if (_remaining > 0)
        {
            if (octet < _lower || octet > _upper)
            {
                return false;
            }

            _remaining--;
            _lower = 0x80;
            _upper = 0xBF;
            return true;
        }

        return octet < 0x80 || Start(octet);
    }

    private bool Start(byte octet)
    {
        _lower = 0x80;
        _upper = 0xBF;
        switch (octet)
        {
            case >= 0xC2 and <= 0xDF:
                _remaining = 1;
                return true;
            case 0xE0:
                _lower = 0xA0;
                _remaining = 2;
                return true;
            case 0xED:
                _upper = 0x9F;
                _remaining = 2;
                return true;
            case >= 0xE1 and <= 0xEF:
                _remaining = 2;
                return true;
            case 0xF0:
                _lower = 0x90;
                _remaining = 3;
                return true;
            case 0xF4:
                _upper = 0x8F;
                _remaining = 3;
                return true;
            case >= 0xF1 and <= 0xF3:
                _remaining = 3;
                return true;
            default:
                return false;
        }
    }
}
