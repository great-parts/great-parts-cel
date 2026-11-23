using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Extensions.Formats;

namespace Great.Parts.Cel;

// The extension set of format checks.
public static partial class CelExtensions
{
    /// <summary>
    /// The <c>formats</c> extension set: checks that a string has a text format — <c>isEmail</c>, <c>isHostname</c>,
    /// <c>isHostAndPort</c>, <c>isIpPrefix</c>, <c>isUri</c> and <c>isUriRef</c>. Version 1; cel-go has no such set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each function is a method of <c>string</c> and returns <c>bool</c>: <see langword="true"/> if the whole string has
    /// the format, <see langword="false"/> for any other string, including the empty one; none of them reports an error.
    /// <c>isEmail</c> accepts the "valid e-mail address" of the HTML standard (not RFC 5322); <c>isHostname</c> — a host
    /// name of labels of ASCII letters, digits and hyphens, each at most 63 characters, the rightmost not all digits, at
    /// most 253 characters, with an optional trailing dot; <c>isHostAndPort(portRequired)</c> — a host name, an IPv4
    /// address or a bracketed IPv6 address, then a port from 0 to 65535, which is required when the argument is
    /// <see langword="true"/>; <c>isUri</c> and <c>isUriRef</c> — a URI, which has a scheme, and a URI reference, which
    /// may be relative, as RFC 3986 defines them.
    /// </para>
    /// <para>
    /// <c>isIpPrefix</c> checks an IP address with a prefix length, such as <c>10.0.0.0/8</c> or <c>2001:db8::/32</c>.
    /// Its optional arguments are the IP version (0 for either, 4 or 6; any other value gives <see langword="false"/>)
    /// and, as a <c>bool</c>, whether the check is strict — whether the bits of the address after the prefix must be
    /// zero. The set <c>network</c> has the related functions <c>isCIDR</c> and <c>isMask</c>; the two sets are
    /// independent and can be added to one environment.
    /// </para>
    /// <para>
    /// Every check takes time linear in the length of the string and gives the same result on every platform. The set
    /// works on any data of the environment and cannot be named in an environment description in JSON; add it in code.
    /// The overloads, with their identifiers, are on the page
    /// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/extensions.md">Extension sets</see>.
    /// </para>
    /// </remarks>
    /// <param name="version">The version of the set.</param>
    /// <returns>The extension set.</returns>
    public static CelExtension Formats(uint version) => FormatsExtension.Get(version);
}
