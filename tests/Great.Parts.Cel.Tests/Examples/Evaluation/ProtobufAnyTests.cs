using System;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// google.protobuf.Any in a field of a message (the type mapping page, the row of Any and the rules of the table of
// protobuf messages): reading the field unpacks the Any by its type URL, the part after the last '/', among the
// types that the provider describes; the well-known types are among them, so an Any that holds an Int64Value reads
// as an int, one that holds a Struct as a map; an Any that holds an Any is unpacked until the content is not one.
// An address that no type of the provider has, and a content that is not a message of the named type, are
// InvalidAny (3020) at the selection that read the field, "cannot unpack google.protobuf.Any with type URL '<URL>':
// <detail>"; the detail is the free part of the text, so the rows check the text up to it. has() does not unpack.
// cel-go v0.32.0 unpacks in the same way and gives an error at the same places (a probe of the oracle, 2026-11-11).
// Variables of type TestAllTypes: w holds an Any of each kind of content in repeated_any, nest an Any of an Any of a
// message, url an Any whose address has a prefix of its own, bad an Any of an unknown type, badc an Any of a known
// type with a content that does not parse, foreign an Any of a proto2 message.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-8")]
public sealed class ProtobufAnyTests
{
    private const string Prefix = "cannot unpack google.protobuf.Any with type URL ";

    private const string Values = """
        w.repeated_any[0] => 7
        w.repeated_any[1] => 's'
        w.repeated_any[2] => true
        w.repeated_any[3] => 1.5
        w.repeated_any[4] => {'k': 1.0}
        w.repeated_any[5] => [1.0, 'a']
        w.repeated_any[6] => 2.5
        w.repeated_any[7] => duration('5s')
        w.repeated_any[8] => timestamp('1970-01-01T00:01:00Z')
        w.repeated_any[9] => b'\x01'
        w.repeated_any[10] => 8u
        nest.single_any == TestAllTypes{single_int32: 4} => true
        nest.single_any.single_int32 => 4
        type(nest.single_any) == TestAllTypes => true
        url.single_any.bb => 5
        url.single_any == TestAllTypes.NestedMessage{bb: 5} => true
        has(bad.single_any) => true
        has(badc.single_any) => true
        size([bad, badc]) => 2
        """;

    // The error is at the selection of the field, the "." after the variable, and one error ends the evaluation.
    private const string UnpackErrors = $$"""
        Evaluate | bad^.single_any | InvalidAny | {{Prefix}}'type.googleapis.com/acme.Unknown': *
        Evaluate | bad^.single_any.bb | InvalidAny | {{Prefix}}'type.googleapis.com/acme.Unknown': *
        Evaluate | badc^.single_any | InvalidAny | {{Prefix}}'type.googleapis.com/cel.expr.conformance.proto3.TestAllTypes.NestedMessage': *
        Evaluate | foreign^.single_any | InvalidAny | {{Prefix}}'type.googleapis.com/cel.expr.conformance.proto2.TestAllTypes': *
        Evaluate | size([1, bad^.single_any]) | InvalidAny | {{Prefix}}'type.googleapis.com/acme.Unknown': *
        """;

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("w", Wellknown())
        .Add("nest", new Proto3.TestAllTypes { SingleAny = Any.Pack(Any.Pack(new Proto3.TestAllTypes { SingleInt32 = 4 })) })
        .Add("url", new Proto3.TestAllTypes
        {
            SingleAny = new Any
            {
                TypeUrl = "example.com/some/path/" + ProtobufExample.NestedName3,
                Value = ProtobufExample.Nested(5).ToByteString(),
            },
        })
        .Add("bad", new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = "type.googleapis.com/acme.Unknown", Value = ByteString.CopyFrom(1) } })
        .Add("badc", new Proto3.TestAllTypes
        {
            SingleAny = new Any { TypeUrl = "type.googleapis.com/" + ProtobufExample.NestedName3, Value = ByteString.CopyFrom(0xff, 0xff) },
        })
        .Add("foreign", new Proto3.TestAllTypes { SingleAny = Any.Pack(new Proto2.TestAllTypes { SingleInt32 = 5 }) });

    private static readonly string[] Names = ["w", "nest", "url", "bad", "badc", "foreign"];

    private readonly CelEnvironment environment = Declare(ProtobufExample.Builder3(), Names);

    [Fact]
    public void KindsOfContent_AreUnpackedToTheirValues()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Values, Bindings));
    }

    [Fact]
    public void AddressThatNoTypeHas_AndContentThatDoesNotParse_AreInvalidAny()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, UnpackErrors, Bindings));
    }

    // The Any is unpacked among the types of the provider, not among the types that the process knows: a proto2 message that
    // the provider of the proto3 messages does not describe is refused, and a provider that has both describes it.
    [Fact]
    public void AnyOfATypeOfTheProvider_IsUnpackedWhenTheProviderDescribesIt()
    {
        CelProtobufTypes both = new CelProtobufTypesBuilder("both")
            .AddFile(Proto3.TestAllTypesReflection.Descriptor)
            .AddFile(Proto2.TestAllTypesReflection.Descriptor)
            .Build();
        CelEnvironment widened = Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(both).UseContainer(ProtobufExample.Container3), Names);

        Assert.Empty(ExtensionRows.CheckValues(widened, "foreign.single_any.single_int32 => 5\nforeign.single_any == dyn(cel.expr.conformance.proto2.TestAllTypes{single_int32: 5}) => true", Bindings));
    }

    // The message of the error: the text of the code with the address, as the page of the error codes says; the detail
    // after the colon is in the text and is not empty.
    [Fact]
    public void InvalidAny_HasTheCodeTheStageAndTheAddress()
    {
        CelError error = Assert.Single(environment.Compile<CelValue>("bad.single_any").GetProgramOrThrow()
            .Evaluate(Bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.InvalidAny, CelErrorStage.Evaluate, 3), (error.Code, error.Stage, error.Location.Offset));
        string detail = error.Message[(Prefix + "'type.googleapis.com/acme.Unknown': ").Length..];
        Assert.StartsWith(Prefix + "'type.googleapis.com/acme.Unknown': ", error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(detail);
    }

    // An element of a repeated field and a value of a map field is unpacked when it is read, so an Any that cannot be
    // unpacked is an error where its element is read and not where the field is.
    [Fact]
    public void ElementOfARepeatedFieldAndValueOfAMap_AreUnpackedWhenRead()
    {
        var bindings = new CelBindings().Add(
            "m",
            new Proto3.TestAllTypes
            {
                RepeatedAny = { Any.Pack(ProtobufExample.Nested(1)), new Any { TypeUrl = "type.googleapis.com/acme.Unknown" } },
                MapStringAny = { ["a"] = Any.Pack(ProtobufExample.Nested(2)), ["b"] = new Any { TypeUrl = "type.googleapis.com/acme.Unknown" } },
            });
        CelEnvironment own = Declare(ProtobufExample.Builder3(), "m");

        Assert.Empty(ExtensionRows.CheckValues(own, "size(m.repeated_any) => 2\nm.repeated_any[0].bb => 1\nm.map_string_any['a'].bb => 2\nsize(m.map_string_any) => 2", bindings));
        Assert.All(
            UnpackedWhenRead,
            expression =>
            {
                CelError error = Assert.Single(own.Compile<CelValue>(expression).GetProgramOrThrow()
                    .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);
                Assert.Equal((CelErrorCode.InvalidAny, CelErrorStage.Evaluate), (error.Code, error.Stage));
            });
    }

    private static readonly string[] UnpackedWhenRead = ["m.repeated_any[1]", "m.map_string_any['b']"];

    private static CelEnvironment Declare(CelEnvironmentBuilder builder, params string[] names)
    {
        foreach (string name in names)
        {
            builder.AddVariable<Proto3.TestAllTypes>(name);
        }

        return builder.Build().GetEnvironmentOrThrow();
    }

    private static Proto3.TestAllTypes Wellknown() => new()
    {
        RepeatedAny =
        {
            Any.Pack(new Int64Value { Value = 7 }),
            Any.Pack(new StringValue { Value = "s" }),
            Any.Pack(new BoolValue { Value = true }),
            Any.Pack(new DoubleValue { Value = 1.5 }),
            Any.Pack(new Struct { Fields = { ["k"] = Value.ForNumber(1) } }),
            Any.Pack(new ListValue { Values = { Value.ForNumber(1), Value.ForString("a") } }),
            Any.Pack(Value.ForNumber(2.5)),
            Any.Pack(new Duration { Seconds = 5 }),
            Any.Pack(new Timestamp { Seconds = 60 }),
            Any.Pack(new BytesValue { Value = ByteString.CopyFrom(1) }),
            Any.Pack(new UInt64Value { Value = 8 }),
        },
    };
}
