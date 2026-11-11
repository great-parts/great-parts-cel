namespace Great.Parts.Cel.Library.Extensions.Network;

// Why a text is no address and no prefix: the reasons of netip.ParseAddr and netip.ParsePrefix of Go, and the three refusals of the
// set. The reason is a code, so a failed parse allocates no text; AddressText gives the words when an error is read.
internal enum ParseFailure : byte
{
    None = 0,

    // No '.', ':' or '%' in the text.
    UnableToParse,

    // A '%' before any '.' or ':'.
    MissingIPv6Address,

    // IPv4.
    IPv4FieldEmpty,
    IPv4FieldTooLarge,
    IPv4LeadingZero,
    IPv4TooLong,
    IPv4TooShort,
    UnexpectedCharacter,

    // IPv6.
    IPv6FieldEmpty,
    IPv6FieldTooLong,
    EmbeddedIPv4Place,
    EmbeddedIPv4NoRoom,
    WantColon,
    ColonNeedsMore,
    MultipleEllipsis,
    TrailingGarbage,
    AddressTooShort,
    EllipsisExpandsNothing,
    ZoneEmpty,

    // The refusals of the set.
    ZoneNotAllowed,
    MappedNotAllowed,

    // A prefix.
    NoSlash,
    BadBits,
    BitsOutOfRange,
}
