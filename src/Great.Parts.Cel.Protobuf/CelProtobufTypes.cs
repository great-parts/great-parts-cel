using System;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// A type provider for protobuf messages: describes the messages and enumerations of <c>.proto</c> files
/// so that expressions can read message fields, test their presence, compare messages and create
/// messages with object literals. Build one with <see cref="CelProtobufTypesBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// CEL type names are the full protobuf names, such as <c>acme.billing.Invoice</c>. The well-known types
/// of <c>google/protobuf</c> are always included. A message enters an expression as the instance of its
/// generated class, without copying; a variable declared with that class has the message type.
/// </para>
/// <para>
/// Values follow the protobuf data conversion of the CEL language definition: 32-bit and 64-bit integers are <c>int</c>
/// or <c>uint</c>, enumerations are <c>int</c>, repeated fields are lists and map fields are maps, read as views
/// without copying; <c>google.protobuf.Timestamp</c> and <c>google.protobuf.Duration</c> are <c>timestamp</c> and
/// <c>duration</c>; wrapper fields are their primitive values or <c>null</c>; <c>google.protobuf.Struct</c>,
/// <c>Value</c> and <c>ListValue</c> are JSON-like maps, values and lists; a <c>google.protobuf.Any</c> is unpacked to
/// the message its type URL names, which must be described by this provider. An unset message field reads as an empty
/// message. The full mapping is in
/// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>; how to add the
/// package, the known types, literals and <c>Any</c> are on the
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/protobuf.md">protobuf page</see>.
/// </para>
/// <para>
/// <c>has(m.f)</c> follows the field selection rules of the language definition: in proto2, and for proto3
/// fields with explicit presence, oneof members and message fields, whether the field is set; for other
/// proto3 scalar fields, whether the value differs from the default; for repeated and map fields, whether
/// they are not empty. Equality compares the descriptors and the set fields by CEL equality, so a NaN is
/// not equal to itself, unpacks <c>google.protobuf.Any</c> first and compares the bytes of the packed message when it cannot be
/// unpacked; it does not use the generated <c>Equals</c>, and unknown fields are not compared.
/// </para>
/// <para>
/// Fields are read and set through the field accessors of <c>Google.Protobuf</c>, which work under
/// Native AOT; extensions are registered typed for the same reason. The provider is immutable and can be
/// shared between threads and environments; messages must not be changed while an evaluation reads them.
/// </para>
/// </remarks>
public sealed class CelProtobufTypes : CelTypeProvider
{
    internal CelProtobufTypes(string sourceName, ReadOnlySpan<CelObjectType> objectTypes, ReadOnlySpan<CelEnumType> enumTypes)
        : base(sourceName, objectTypes, enumTypes)
    {
    }
}
