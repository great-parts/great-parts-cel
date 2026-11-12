using System;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Tests.Protobuf;

// The unpacking of an Any, beyond the rows that the examples fix: the whole text of each of the five details of the error, an empty
// content, an address that has only a leading slash, the limit of 16 levels of Any in a chain and the address the error names, an
// Any inside a message inside an Any, a time inside an Any that is out of range, an extension in the content, and the cost of an
// unpack, which is charged before the parse, also for an Any that does not unpack, and summed over the elements of a repeated field.
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-8")]
public sealed class AnyUnpackingTests
{
    private const string Source = "all test messages";

    private const string TypeUrl3 = "type.googleapis.com/cel.expr.conformance.proto3.TestAllTypes";

    private const string Nested3 = "cel.expr.conformance.proto3.TestAllTypes.NestedMessage";

    private static readonly CelEnvironment CostEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ProtobufExample.TypesAll)
        .UseContainer(ProtobufExample.Container3)
        .AddVariable<Proto3.TestAllTypes>("x")
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("", "the type URL is empty")]
    [InlineData("TestAllTypes", "the type URL has no '/'")]
    [InlineData("type.googleapis.com/", "the message type '' is not described by the type source '" + Source + "'")]
    [InlineData("type.googleapis.com/acme.Unknown", "the message type 'acme.Unknown' is not described by the type source '" + Source + "'")]
    [InlineData("/acme.Unknown", "the message type 'acme.Unknown' is not described by the type source '" + Source + "'")]
    [InlineData("type.googleapis.com/cel.expr.conformance.proto3", "the message type 'cel.expr.conformance.proto3' is not described by the type source '" + Source + "'")]
    public static void AnyThatCannotBeFound_IsInvalidAnyWithTheDetailOfItsCase(string address, string detail)
    {
        var x = new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = address } };

        CelError error = Reading.ReadError("x.single_any", x);

        Assert.Equal(CelErrorCode.InvalidAny, error.Code);
        Assert.Equal("cannot unpack google.protobuf.Any with type URL '" + address + "': " + detail, error.Message);
    }

    // The content of the named type does not parse: the text names the type and carries the message of the parser.
    [Fact]
    public static void ContentThatDoesNotParse_IsInvalidAnyThatNamesTheType()
    {
        var x = new Proto3.TestAllTypes
        {
            SingleAny = new Any { TypeUrl = "type.googleapis.com/" + Nested3, Value = ByteString.CopyFrom(0xff, 0xff) },
        };

        CelError error = Reading.ReadError("x.single_any", x);

        Assert.Equal(CelErrorCode.InvalidAny, error.Code);
        Assert.StartsWith(
            "cannot unpack google.protobuf.Any with type URL 'type.googleapis.com/" + Nested3 + "': the content is not a valid " + Nested3 + ": ",
            error.Message,
            StringComparison.Ordinal);
    }

    // An Any that is set with a type and no content holds the empty message of that type.
    [Fact]
    public static void AnyWithAnEmptyContent_HoldsTheEmptyMessage()
    {
        var x = new Proto3.TestAllTypes { SingleAny = new Any { TypeUrl = TypeUrl3 } };

        Assert.True(Reading.IsTrue("x.single_any == TestAllTypes{}", x));
        Assert.True(Reading.IsTrue("x.single_any.single_int32 == 0", x));
        Assert.False(Reading.IsTrue("has(x.single_any.single_int32)", x));
    }

    // Only what follows the last slash names the type: an address with a slash at its start and nothing before it works too.
    [Fact]
    public static void AddressWithOnlyALeadingSlash_NamesTheTypeAfterIt()
    {
        var x = new Proto3.TestAllTypes
        {
            SingleAny = new Any { TypeUrl = "/" + Nested3, Value = ProtobufExample.Nested(6).ToByteString() },
        };

        Assert.True(Reading.IsTrue("x.single_any.bb == 6", x));
    }

    // A chain of 16 Any messages, each holding the next, unpacks; the seventeenth is InvalidAny, which names the address of that
    // seventeenth Any and not of the first.
    [Fact]
    public static void ChainOfSixteenAny_Unpacks_AndTheSeventeenthIsRefused()
    {
        var sixteen = new Proto3.TestAllTypes { SingleAny = Chain(16) };
        var seventeen = new Proto3.TestAllTypes { SingleAny = Chain(17) };

        Assert.True(Reading.IsTrue("x.single_any == 7", sixteen));
        CelError error = Reading.ReadError("x.single_any", seventeen);
        Assert.Equal(CelErrorCode.InvalidAny, error.Code);
        Assert.Equal(
            "cannot unpack google.protobuf.Any with type URL 'type.googleapis.com/google.protobuf.Int64Value': Any is nested deeper than 16 levels",
            error.Message);
    }

    // The Any holds a message that has an Any field of its own: the field is unpacked when it is read, as any other.
    [Fact]
    public static void MessageInAnAny_UnpacksItsOwnAnyWhenRead()
    {
        var x = new Proto3.TestAllTypes
        {
            SingleAny = Any.Pack(new Proto3.TestAllTypes { SingleAny = Any.Pack(ProtobufExample.Nested(9)) }),
        };

        Assert.True(Reading.IsTrue("x.single_any.single_any.bb == 9", x));
    }

    // A Timestamp or a Duration inside an Any that is out of range is ConversionRange at the field that holds the Any.
    [Fact]
    public static void TimeInAnAnyOutOfRange_IsConversionRange()
    {
        CelError seconds = Reading.ReadError("x.single_any", Holding(new Timestamp { Seconds = 253_402_300_800 }));
        CelError nanos = Reading.ReadError("x.single_any", Holding(new Timestamp { Seconds = 1, Nanos = 1_000_000_000 }));
        CelError total = Reading.ReadError("x.single_any", Holding(new Duration { Seconds = long.MaxValue }));
        CelError mixed = Reading.ReadError("x.single_any", Holding(new Duration { Seconds = 1, Nanos = -1 }));

        Assert.Equal((CelErrorCode.ConversionRange, "253402300800 is out of the range of timestamp"), (seconds.Code, seconds.Message));
        Assert.Equal((CelErrorCode.ConversionRange, "1000000000 is out of the range of timestamp"), (nanos.Code, nanos.Message));
        Assert.Equal((CelErrorCode.ConversionRange, "9223372036854775807 is out of the range of duration"), (total.Code, total.Message));
        Assert.Equal((CelErrorCode.ConversionRange, "-1 is out of the range of duration"), (mixed.Code, mixed.Message));
    }

    // The content of an Any is parsed with the extensions that the provider registered, so a message of a proto2 type with an
    // extension keeps it.
    [Fact]
    public static void ExtensionInTheContentOfAnAny_IsRead()
    {
        var p = new Proto2.TestAllTypes { SingleAny = Any.Pack(ProtobufExample.WithExtensions()) };

        Assert.True(Reading.IsTrue("proto.getExt(p.single_any, cel.expr.conformance.proto2.int32_ext) == 7", p: p));
        Assert.True(Reading.IsTrue("proto.hasExt(p.single_any, cel.expr.conformance.proto2.int32_ext)", p: p));
    }

    // The units of an unpack are charged before the content is parsed, so a limit that does not cover them stops the evaluation with
    // CostLimitExceeded and one that does gets the error of the Any itself; an Any that is not set adds nothing.
    [Fact]
    public static void UnpackIsChargedBeforeTheParse_AndAnUnsetAnyAddsNothing()
    {
        Any broken = new() { TypeUrl = "type.googleapis.com/acme.Unknown", Value = ByteString.CopyFrom(1) };
        ulong units = Units(broken);

        Assert.Equal(CelErrorCode.CostLimitExceeded, Evaluate("x.single_any", new Proto3.TestAllTypes { SingleAny = broken }, 1 + 1 + units - 1).Code);
        Assert.Equal(CelErrorCode.InvalidAny, Evaluate("x.single_any", new Proto3.TestAllTypes { SingleAny = broken }, 1 + 1 + units).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Evaluate("x.single_any", new Proto3.TestAllTypes(), 1).Code);
        Assert.True(Evaluate("x.single_any", new Proto3.TestAllTypes(), 2).IsSuccess);
    }

    // For a repeated field of Any the units of every element are charged when the field is read, in one amount, though the elements
    // are unpacked one by one when they are read.
    [Fact]
    public static void RepeatedFieldOfAny_ChargesTheSumOfTheUnitsOfItsElements()
    {
        Any first = Any.Pack(ProtobufExample.Nested(1));
        Any second = Any.Pack(new Proto3.TestAllTypes { SingleString = "0123456789" });
        var x = new Proto3.TestAllTypes { RepeatedAny = { first, second } };
        ulong total = 1 + 1 + Units(first) + Units(second);

        Assert.True(Evaluate("x.repeated_any", x, total).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Evaluate("x.repeated_any", x, total - 1).Code);
    }

    // The units of a nested Any are those of the outer one: the inner address and content are bytes of the outer content.
    [Fact]
    public static void NestedAny_IsChargedOnceByTheOuterAddressAndContent()
    {
        Any outer = Chain(3);
        var x = new Proto3.TestAllTypes { SingleAny = outer };
        ulong total = 1 + 1 + Units(outer);

        Assert.True(Evaluate("x.single_any", x, total).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Evaluate("x.single_any", x, total - 1).Code);
    }

    // 1 and one for every ten of the address in UTF-16 units and the content in bytes.
    private static ulong Units(Any any) => 1 + ((ulong)any.TypeUrl.Length + (ulong)any.Value.Length + 9) / 10;

    // A chain of the given number of Any messages, the last holding an Int64Value of 7.
    private static Any Chain(int length)
    {
        Any any = Any.Pack(new Int64Value { Value = 7 });
        for (int level = 1; level < length; level++)
        {
            any = Any.Pack(any);
        }

        return any;
    }

    private static Proto3.TestAllTypes Holding(IMessage message) => new() { SingleAny = Any.Pack(message) };

    // The result of an expression of the variable x with a cost limit; an evaluation that succeeds has no errors, and its Code is None.
    private static (bool IsSuccess, CelErrorCode Code) Evaluate(string expression, Proto3.TestAllTypes x, ulong limit)
    {
        CelEvalResult<CelValue> result = CostEnvironment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("x", x), new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
        return (result.IsSuccess, result.IsSuccess ? default : result.Errors[0].Code);
    }
}
