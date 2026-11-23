using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The implementations of the set formats, version 1. Each function reads the receiver as a string and gives a bool: the scanner says
// whether the whole text has the format, and no function of the set reports an error for a string of another format. An argument of
// another kind is EvalNoMatchingOverload, whatever the static types say, as in the other sets.
internal static class FormatsFunctions
{
    // A scanner that needs nothing but the text; the lambdas that name it do not capture, so each is one instance for the process.
    private delegate bool TextScanner(ReadOnlySpan<char> text);

    // string_is_email, s.isEmail().
    internal static CelValue IsEmail(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        ScanText(arguments, site, static text => { NoSteps counter = default; return EmailFormat.IsEmail(text, ref counter); });

    // string_is_hostname, s.isHostname().
    internal static CelValue IsHostname(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        ScanText(arguments, site, static text => { NoSteps counter = default; return HostnameFormat.IsHostname(text, ref counter); });

    // string_is_host_and_port_bool, s.isHostAndPort(portRequired).
    internal static CelValue IsHostAndPort(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        NoSteps counter = default;
        return arguments[0].TryGetString(out string? text) && arguments[1].TryGetBoolean(out bool portRequired)
            ? CelValue.FromBoolean(HostAndPortFormat.IsHostAndPort(text, portRequired, ref counter))
            : site.NoMatchingOverload(arguments);
    }

    // string_is_ip_prefix, s.isIpPrefix(): either family, not strict.
    internal static CelValue IsIpPrefix(ReadOnlySpan<CelValue> arguments, in CallSite site) => IpPrefix(arguments, site, 0, false);

    // string_is_ip_prefix_int, s.isIpPrefix(version).
    internal static CelValue IsIpPrefixVersion(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[1].TryGetInt64(out long version) ? IpPrefix(arguments, site, version, false) : site.NoMatchingOverload(arguments);

    // string_is_ip_prefix_bool, s.isIpPrefix(strict): either family.
    internal static CelValue IsIpPrefixStrict(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[1].TryGetBoolean(out bool strict) ? IpPrefix(arguments, site, 0, strict) : site.NoMatchingOverload(arguments);

    // string_is_ip_prefix_int_bool, s.isIpPrefix(version, strict).
    internal static CelValue IsIpPrefixVersionStrict(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[1].TryGetInt64(out long version) && arguments[2].TryGetBoolean(out bool strict)
            ? IpPrefix(arguments, site, version, strict)
            : site.NoMatchingOverload(arguments);

    // string_is_uri, s.isUri().
    internal static CelValue IsUri(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        ScanText(arguments, site, static text => { NoSteps counter = default; return UriFormat.IsUri(text, ref counter); });

    // string_is_uri_ref, s.isUriRef().
    internal static CelValue IsUriRef(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        ScanText(arguments, site, static text => { NoSteps counter = default; return UriFormat.IsUriReference(text, ref counter); });

    private static CelValue ScanText(ReadOnlySpan<CelValue> arguments, in CallSite site, TextScanner scanner) =>
        arguments[0].TryGetString(out string? text) ? CelValue.FromBoolean(scanner(text)) : site.NoMatchingOverload(arguments);

    private static CelValue IpPrefix(ReadOnlySpan<CelValue> arguments, in CallSite site, long version, bool strict)
    {
        NoSteps counter = default;
        return arguments[0].TryGetString(out string? text)
            ? CelValue.FromBoolean(IpPrefixFormat.IsIpPrefix(text, version, strict, ref counter))
            : site.NoMatchingOverload(arguments);
    }
}
