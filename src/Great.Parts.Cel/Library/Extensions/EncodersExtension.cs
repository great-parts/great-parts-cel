using System;
using System.Collections.Immutable;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set encoders, version 1: base64.decode, base64.encode and json.encode. A call is 1 unit; base64 adds ⌈Lₐ/10⌉ for its
// argument before the function runs and ⌈Lᵣ/10⌉ for its result, which the function charges before it creates the result, and
// json.encode is charged as its text grows. The decoder and the encoder of base64 are in Base64Codec, the encoder of JSON in
// JsonEncoder.
internal static class EncodersExtension
{
    internal const string SetName = "encoders";
    internal const uint Version = 1;
    private const string Source = "extension encoders v1";

    // invalid argument to 'base64.decode': illegal base64 data at input byte i. The function name is the first part, the offset the
    // number of the error.
    private static readonly Func<CelError, string> IllegalDataText = static error =>
        "invalid argument to '" + (string)error.First! + "': illegal base64 data at input byte " + error.Number.ToString(CultureInfo.InvariantCulture);

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, Overloads()) : null;

    private static ImmutableArray<OverloadBinding> Overloads() =>
    [
        new OverloadBinding(
            "base64.decode",
            new CelOverloadDecl("base64_decode_string", false, [CelType.String], CelType.Bytes),
            Decode,
            1,
            Costs.Text,
            Source),
        new OverloadBinding(
            "base64.encode",
            new CelOverloadDecl("base64_encode_bytes", false, [CelType.Bytes], CelType.String),
            Encode,
            1,
            Costs.Text,
            Source),
        new OverloadBinding(
            "json.encode",
            new CelOverloadDecl("json_encode_dyn", false, [CelType.Dyn], CelType.String),
            JsonEncoder.Encode,
            1,
            null,
            Source),
    ];

    // base64_decode_string: the two attempts, with padding and then without it; the second error is the error of the call. The number
    // of bytes is known after a pass that validates and counts, and it is charged before the array is created.
    private static CelValue Decode(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? text))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (!Base64Codec.TryCount(text, out int length, out int errorOffset, out bool padded))
        {
            return site.Error(CelErrorCode.InvalidArgument, IllegalDataText, site.Function, null, errorOffset);
        }

        site.Charge(Costs.Tenths((ulong)length));
        if (length == 0)
        {
            return CelValue.FromBytes(ReadOnlyMemory<byte>.Empty);
        }

        var bytes = new byte[length];
        Base64Codec.Decode(text, padded, bytes);
        return CelValue.FromBytes(bytes);
    }

    // base64_encode_bytes: the standard alphabet with padding and no line breaks; the length of the text is charged before it is made.
    private static CelValue Encode(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetBytes(out ReadOnlyMemory<byte> bytes))
        {
            return site.NoMatchingOverload(arguments);
        }

        site.Charge(Costs.Tenths((ulong)Base64Codec.EncodedLength(bytes.Length)));
        return CelValue.FromString(Base64Codec.Encode(bytes.Span));
    }
}
