using System;
using Great.Parts.Cel.Library.Extensions.Formats;
using UriFormat = Great.Parts.Cel.Library.Extensions.Formats.UriFormat;

namespace Great.Parts.Cel.Tests.Library.Extensions.Formats;

// The scanners of the set formats as the tests call them: with no counter for a result, and with the counter of steps for the check of
// the linear time.
internal static class FormatScanners
{
    internal static bool Email(string text)
    {
        NoSteps counter = default;
        return EmailFormat.IsEmail(text, ref counter);
    }

    internal static bool Hostname(string text)
    {
        NoSteps counter = default;
        return HostnameFormat.IsHostname(text, ref counter);
    }

    internal static bool HostAndPort(string text, bool portRequired)
    {
        NoSteps counter = default;
        return HostAndPortFormat.IsHostAndPort(text, portRequired, ref counter);
    }

    internal static bool IpPrefix(string text, long version = 0, bool strict = false)
    {
        NoSteps counter = default;
        return IpPrefixFormat.IsIpPrefix(text, version, strict, ref counter);
    }

    internal static bool Uri(string text)
    {
        NoSteps counter = default;
        return UriFormat.IsUri(text, ref counter);
    }

    internal static bool UriRef(string text)
    {
        NoSteps counter = default;
        return UriFormat.IsUriReference(text, ref counter);
    }

    // The result and the number of steps of a function of the set, named as in an expression; the arguments of the function are in the name.
    internal static (bool Result, long Steps) Run(string function, string text)
    {
        var counter = default(StepCount);
        bool result = function switch
        {
            "isEmail" => EmailFormat.IsEmail(text, ref counter),
            "isHostname" => HostnameFormat.IsHostname(text, ref counter),
            "isHostAndPort(false)" => HostAndPortFormat.IsHostAndPort(text, false, ref counter),
            "isHostAndPort(true)" => HostAndPortFormat.IsHostAndPort(text, true, ref counter),
            "isIpPrefix()" => IpPrefixFormat.IsIpPrefix(text, 0, false, ref counter),
            "isIpPrefix(4, true)" => IpPrefixFormat.IsIpPrefix(text, 4, true, ref counter),
            "isIpPrefix(6, true)" => IpPrefixFormat.IsIpPrefix(text, 6, true, ref counter),
            "isUri" => UriFormat.IsUri(text, ref counter),
            "isUriRef" => UriFormat.IsUriReference(text, ref counter),
            _ => throw new ArgumentException("Unknown function " + function, nameof(function)),
        };

        return (result, counter.Steps);
    }
}
