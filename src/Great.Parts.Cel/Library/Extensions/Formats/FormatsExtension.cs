using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions.Formats;

// The definition of the set formats, version 1. Like decimal and calendar it is not a CatalogExtension: cel-go has no such set, so its
// name is not among the names that a description of an environment accepts.
internal sealed class FormatsDefinition(string name, uint version, string source, ImmutableArray<OverloadBinding> overloads)
    : ExtensionDefinition(name, version, source, overloads);

// The table of the set formats, version 1: nine overloads, all methods of string that give bool. A call is 1 unit and adds ⌈Lₐ/10⌉ for the
// length of the receiver, whatever the format. The functions have descriptions of their own, since cel-go has no such functions.
internal static class FormatsExtension
{
    internal const string SetName = "formats";
    internal const uint Version = 1;
    private const string Source = "extension formats v1";

    private static readonly FrozenDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        ["isEmail"] = "Tells whether a string is a valid e-mail address by the definition of the HTML standard.",
        ["isHostname"] = "Tells whether a string is a valid host name: labels of letters, digits and hyphens of at most 63 characters, at most 253 characters in all.",
        ["isHostAndPort"] = "Tells whether a string is a host name, an IPv4 address or an IPv6 address in brackets, with a port that is required when the argument is true.",
        ["isIpPrefix"] = "Tells whether a string is an IP address with a prefix length, such as 10.0.0.0/8; the optional arguments are the version, 0, 4 or 6, and whether the bits after the prefix must be zero.",
        ["isUri"] = "Tells whether a string is a URI with a scheme, as RFC 3986 defines it.",
        ["isUriRef"] = "Tells whether a string is a URI or a relative reference, as RFC 3986 defines them.",
    }.ToFrozenDictionary(System.StringComparer.Ordinal);

    private static readonly CelExtension VersionOne = new(
        SetName,
        Version,
        new FormatsDefinition(SetName, Version, Source, Overloads()),
        [Version]);

    // The set of the version: always the same instance, so that adding it twice is one set. A version the set does not have is a set
    // without a definition, which the build reports as UnknownExtensionVersion with the versions that exist.
    internal static CelExtension Get(uint version) =>
        version == Version ? VersionOne : new CelExtension(SetName, version, null, [Version]);

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType text = CelType.String;
        CelType number = CelType.Int;
        CelType boolean = CelType.Bool;
        ImmutableArray<OverloadBinding> table =
        [
            Receiver("isEmail", "string_is_email", [], FormatsFunctions.IsEmail),
            Receiver("isHostname", "string_is_hostname", [], FormatsFunctions.IsHostname),
            Receiver("isHostAndPort", "string_is_host_and_port_bool", [boolean], FormatsFunctions.IsHostAndPort),
            Receiver("isIpPrefix", "string_is_ip_prefix", [], FormatsFunctions.IsIpPrefix),
            Receiver("isIpPrefix", "string_is_ip_prefix_int", [number], FormatsFunctions.IsIpPrefixVersion),
            Receiver("isIpPrefix", "string_is_ip_prefix_bool", [boolean], FormatsFunctions.IsIpPrefixStrict),
            Receiver("isIpPrefix", "string_is_ip_prefix_int_bool", [number, boolean], FormatsFunctions.IsIpPrefixVersionStrict),
            Receiver("isUri", "string_is_uri", [], FormatsFunctions.IsUri),
            Receiver("isUriRef", "string_is_uri_ref", [], FormatsFunctions.IsUriRef),
        ];

        return FunctionDescriptions.Describe(table, Descriptions);

        // A method of string: the receiver is the first parameter, and the call costs the unit of the call and the length of the receiver.
        OverloadBinding Receiver(string function, string overloadId, ImmutableArray<CelType> parameterTypes, OverloadImplementation implementation) =>
            new(
                function,
                new CelOverloadDecl(overloadId, true, [text, .. parameterTypes], boolean),
                implementation,
                1,
                Costs.Text,
                Source);
    }
}
