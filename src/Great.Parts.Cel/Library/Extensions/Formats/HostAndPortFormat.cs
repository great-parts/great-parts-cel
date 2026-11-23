using System;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The format of isHostAndPort: a host, then optionally ':' and a port. The host is a host name, an IPv4 address, or an IPv6 address in
// square brackets with an optional zone ('%' and one or more units other than NUL). The port is 0 to 65535 in decimal, with no sign and
// no leading zero unless it is 0, and is required when the argument says so. The whole text is the host and the port: no spaces.
internal static class HostAndPortFormat
{
    private const int MaxPortDigits = 5;
    private const int MaxPort = 65535;

    internal static bool IsHostAndPort<TCounter>(ReadOnlySpan<char> text, bool portRequired, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (!Split(text, out ReadOnlySpan<char> host, out ReadOnlySpan<char> port, out bool hasPort, ref counter))
        {
            return false;
        }

        if (hasPort ? !IsPort(port, ref counter) : portRequired)
        {
            return false;
        }

        return host[0] == '[' ? IsBracketedIPv6(host[1..^1], ref counter) : IsIPv4OrHostname(host, ref counter);
    }

    // Cuts the text into the host and the port. A host in brackets ends at the last ']', since a zone may hold any unit but NUL, and
    // what follows is empty or ':' and the port; any other host ends at the first ':'.
    private static bool Split<TCounter>(
        ReadOnlySpan<char> text,
        out ReadOnlySpan<char> host,
        out ReadOnlySpan<char> port,
        out bool hasPort,
        ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        host = default;
        port = default;
        hasPort = false;
        if (text.Length == 0)
        {
            return false;
        }

        int hostEnd;
        if (text[0] == '[')
        {
            int close = FormatChars.LastIndexOf(text, ']', ref counter);
            if (close < 0 || (close + 1 < text.Length && text[close + 1] != ':'))
            {
                return false;
            }

            hostEnd = close + 1;
        }
        else
        {
            int colon = FormatChars.IndexOf(text, ':', ref counter);
            hostEnd = colon < 0 ? text.Length : colon;
        }

        host = text[..hostEnd];
        hasPort = hostEnd < text.Length;
        port = hasPort ? text[(hostEnd + 1)..] : default;
        return host.Length > 0;
    }

    private static bool IsPort<TCounter>(ReadOnlySpan<char> port, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        if (port.Length == 0 || port.Length > MaxPortDigits || (port.Length > 1 && port[0] == '0'))
        {
            return false;
        }

        int value = 0;
        foreach (char digit in port)
        {
            counter.Step();
            if (!FormatChars.IsDigit(digit))
            {
                return false;
            }

            value = (value * 10) + (digit - '0');
        }

        return value <= MaxPort;
    }

    // The text between the brackets: an IPv6 address, then optionally '%' and a zone of one or more units, none of them NUL.
    private static bool IsBracketedIPv6<TCounter>(ReadOnlySpan<char> inner, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        return IpAddressScanner.ScanIPv6AndCutZone(inner, out bool hasZone, out ReadOnlySpan<char> zone, ref counter)
            && (!hasZone || IsZone(zone, ref counter));
    }

    private static bool IsZone<TCounter>(ReadOnlySpan<char> zone, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        return zone.Length > 0 && FormatChars.IndexOf(zone, '\0', ref counter) < 0;
    }

    private static bool IsIPv4OrHostname<TCounter>(ReadOnlySpan<char> host, ref TCounter counter)
        where TCounter : struct, IStepCounter
    {
        return IpAddressScanner.ScanIPv4(host, out _, ref counter) || HostnameFormat.IsHostname(host, ref counter);
    }
}
