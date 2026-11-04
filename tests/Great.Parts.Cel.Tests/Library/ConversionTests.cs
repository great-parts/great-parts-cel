using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The implementations of type, dyn and the conversions, called directly with a call site: the ranges of the
// numeric conversions at their bounds, the errors with their texts, and the identities.
public sealed class ConversionTests
{
    private static readonly LineTable Lines = new("x");

    private static readonly CallSite IntSite = new("int", 0, Lines);
    private static readonly CallSite UIntSite = new("uint", 0, Lines);
    private static readonly CallSite DoubleSite = new("double", 0, Lines);
    private static readonly CallSite StringSite = new("string", 0, Lines);
    private static readonly CallSite BytesSite = new("bytes", 0, Lines);
    private static readonly CallSite BoolSite = new("bool", 0, Lines);

    // The open range (-2^63, 2^63): the double just inside each bound converts, the bound itself does not.
    [Theory]
    [InlineData(-9223372036854774784.0, -9223372036854774784L)]
    [InlineData(9223372036854774784.0, 9223372036854774784L)]
    [InlineData(-1.9, -1L)]
    [InlineData(1.9, 1L)]
    [InlineData(-0.0, 0L)]
    public static void DoubleToInt_InsideTheOpenRange_TruncatesTowardZero(double value, long result)
    {
        Assert.True(Conversions.DoubleToInt64([CelValue.FromDouble(value)], IntSite).TryGetInt64(out long converted));
        Assert.Equal(result, converted);
    }

    [Theory]
    [InlineData(-9223372036854775808.0, "-9.223372036854776e+18 is out of the range of int")]
    [InlineData(9223372036854775808.0, "9.223372036854776e+18 is out of the range of int")]
    [InlineData(1e300, "1e+300 is out of the range of int")]
    [InlineData(double.NaN, "NaN is out of the range of int")]
    [InlineData(double.PositiveInfinity, "+Inf is out of the range of int")]
    [InlineData(double.NegativeInfinity, "-Inf is out of the range of int")]
    public static void DoubleToInt_AtOrBeyondTheBounds_IsConversionRange(double value, string message)
    {
        AssertError(CelErrorCode.ConversionRange, message, Conversions.DoubleToInt64([CelValue.FromDouble(value)], IntSite));
    }

    [Theory]
    [InlineData(-0.0, 0UL)]
    [InlineData(0.5, 0UL)]
    [InlineData(18446744073709549568.0, 18446744073709549568UL)]
    public static void DoubleToUInt_InsideTheRange_TruncatesTowardZero(double value, ulong result)
    {
        Assert.True(Conversions.DoubleToUInt64([CelValue.FromDouble(value)], UIntSite).TryGetUInt64(out ulong converted));
        Assert.Equal(result, converted);
    }

    [Theory]
    [InlineData(-0.5, "-0.5 is out of the range of uint")]
    [InlineData(-1.0, "-1 is out of the range of uint")]
    [InlineData(18446744073709551616.0, "1.8446744073709552e+19 is out of the range of uint")]
    [InlineData(double.NaN, "NaN is out of the range of uint")]
    [InlineData(double.PositiveInfinity, "+Inf is out of the range of uint")]
    public static void DoubleToUInt_NegativeOrFromTwoToThe64_IsConversionRange(double value, string message)
    {
        AssertError(CelErrorCode.ConversionRange, message, Conversions.DoubleToUInt64([CelValue.FromDouble(value)], UIntSite));
    }

    [Fact]
    public static void IntegerConversions_AtTheBounds()
    {
        Assert.True(Conversions.UInt64ToInt64([CelValue.FromUInt64(long.MaxValue)], IntSite).TryGetInt64(out long fromUInt));
        Assert.Equal(long.MaxValue, fromUInt);
        Assert.True(Conversions.Int64ToUInt64([CelValue.FromInt64(0)], UIntSite).TryGetUInt64(out ulong fromInt));
        Assert.Equal(0UL, fromInt);
        AssertError(
            CelErrorCode.ConversionRange,
            "9223372036854775808 is out of the range of int",
            Conversions.UInt64ToInt64([CelValue.FromUInt64((ulong)long.MaxValue + 1)], IntSite));
        AssertError(CelErrorCode.ConversionRange, "-1 is out of the range of uint", Conversions.Int64ToUInt64([CelValue.FromInt64(-1)], UIntSite));
        AssertError(
            CelErrorCode.ConversionRange,
            "-9223372036854775808 is out of the range of uint",
            Conversions.Int64ToUInt64([CelValue.FromInt64(long.MinValue)], UIntSite));
    }

    [Fact]
    public static void IntegersToDouble_AreTheNearestDouble()
    {
        Assert.True(Conversions.Int64ToDouble([CelValue.FromInt64(9007199254740993)], DoubleSite).TryGetDouble(out double fromInt));
        Assert.Equal(9007199254740992.0, fromInt);
        Assert.True(Conversions.UInt64ToDouble([CelValue.FromUInt64(ulong.MaxValue)], DoubleSite).TryGetDouble(out double fromUInt));
        Assert.Equal(18446744073709551616.0, fromUInt);
    }

    [Fact]
    public static void TextToNumbers_GiveInvalidArgumentOrConversionRange()
    {
        AssertError(CelErrorCode.InvalidArgument, "invalid argument to 'int': cannot parse 12a as int", Conversions.StringToInt64([Text("12a")], IntSite));
        AssertError(CelErrorCode.ConversionRange, "-9223372036854775809 is out of the range of int", Conversions.StringToInt64([Text("-9223372036854775809")], IntSite));
        AssertError(CelErrorCode.InvalidArgument, "invalid argument to 'uint': cannot parse -0 as uint", Conversions.StringToUInt64([Text("-0")], UIntSite));
        AssertError(CelErrorCode.ConversionRange, "99999999999999999999 is out of the range of uint", Conversions.StringToUInt64([Text("99999999999999999999")], UIntSite));
        AssertError(CelErrorCode.InvalidArgument, "invalid argument to 'double': cannot parse 1e as double", Conversions.StringToDouble([Text("1e")], DoubleSite));
        AssertError(CelErrorCode.ConversionRange, "-2e308 is out of the range of double", Conversions.StringToDouble([Text("-2e308")], DoubleSite));
        AssertError(CelErrorCode.InvalidArgument, "invalid argument to 'bool': cannot parse  as bool", Conversions.StringToBool([Text(string.Empty)], BoolSite));
    }

    [Theory]
    [InlineData(new byte[] { 0x80 }, 0)]
    [InlineData(new byte[] { 0x61, 0xC3 }, 1)]
    [InlineData(new byte[] { 0x61, 0x62, 0xE2, 0x82 }, 2)]
    [InlineData(new byte[] { 0xC0, 0x80 }, 0)]
    [InlineData(new byte[] { 0x7A, 0xED, 0xA0, 0x80 }, 1)]
    [InlineData(new byte[] { 0xF4, 0x90, 0x80, 0x80 }, 0)]
    public static void BytesToString_OfInvalidUtf8_NamesTheFirstInvalidByte(byte[] bytes, int offset)
    {
        AssertError(
            CelErrorCode.InvalidArgument,
            "invalid argument to 'string': invalid UTF-8 at byte " + offset,
            Conversions.BytesToString([CelValue.FromBytes(bytes)], StringSite));
    }

    [Fact]
    public static void TextAndBytes_ConvertThroughUtf8()
    {
        Assert.Equal("aé😀", StringOf(Conversions.BytesToString([CelValue.FromBytes(new byte[] { 0x61, 0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80 })], StringSite)));
        Assert.Equal(new byte[] { 0x61, 0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80 }, BytesOf(Conversions.StringToBytes([Text("aé😀")], BytesSite)));

        // A lone surrogate becomes U+FFFD.
        Assert.Equal(new byte[] { 0x78, 0xEF, 0xBF, 0xBD }, BytesOf(Conversions.StringToBytes([Text("x\uD800")], BytesSite)));
    }

    [Fact]
    public static void ToText_WritesWithoutCulture()
    {
        Assert.Equal("-9223372036854775808", StringOf(Conversions.Int64ToString([CelValue.FromInt64(long.MinValue)], StringSite)));
        Assert.Equal("18446744073709551615", StringOf(Conversions.UInt64ToString([CelValue.FromUInt64(ulong.MaxValue)], StringSite)));
        Assert.Equal("-1.5e-07", StringOf(Conversions.DoubleToString([CelValue.FromDouble(-1.5e-7)], StringSite)));
        Assert.Equal("false", StringOf(Conversions.BoolToString([CelValue.FromBoolean(false)], StringSite)));
    }

    [Fact]
    public static void TypeAndIdentities_ReturnTheirValues()
    {
        CelValue list = CelValue.FromList(CelList.Create(CelValue.FromInt64(1)));
        Assert.True(Conversions.Type([list], new CallSite("type", 0, Lines)).TryGetType(out CelType? type));
        Assert.Equal(CelType.ListOf(CelType.Dyn), type);
        Assert.True(Conversions.Type([CelValue.FromType(CelType.Int)], new CallSite("type", 0, Lines)).TryGetType(out CelType? typeOfType));
        Assert.Equal(CelType.TypeOf(CelType.Dyn), typeOfType);
        Assert.True(Conversions.ToDyn([list], new CallSite("dyn", 0, Lines)).TryGetList(out CelList? same));
        Assert.True(list.TryGetList(out CelList? original));
        Assert.Same(original, same);

        string text = "abc";
        Assert.Same(text, StringOf(Conversions.StringIdentity([Text(text)], StringSite)));
    }

    // An identity checks the kind as every implementation does: a static type is no guarantee of the value.
    [Fact]
    public static void Identity_OfAnotherKind_IsEvalNoMatchingOverload()
    {
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: int(string)", Conversions.IntIdentity([Text("a")], IntSite));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: uint(int)", Conversions.UIntIdentity([CelValue.FromInt64(1)], UIntSite));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: double(int)", Conversions.DoubleIdentity([CelValue.FromInt64(1)], DoubleSite));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: string(uint)", Conversions.StringIdentity([CelValue.FromUInt64(1)], StringSite));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: bytes(string)", Conversions.BytesIdentity([Text("a")], BytesSite));
        AssertError(CelErrorCode.EvalNoMatchingOverload, "no such overload: bool(null_type)", Conversions.BoolIdentity([CelValue.Null], BoolSite));
    }

    private static CelValue Text(string value) => CelValue.FromString(value);

    private static string StringOf(CelValue value)
    {
        Assert.True(value.TryGetString(out string? text), value.Kind.ToString());
        return text;
    }

    private static byte[] BytesOf(CelValue value)
    {
        Assert.True(value.TryGetBytes(out ReadOnlyMemory<byte> bytes), value.Kind.ToString());
        return bytes.ToArray();
    }

    private static void AssertError(CelErrorCode code, string message, CelValue result)
    {
        Assert.True(result.TryGetError(out CelError? error), result.Kind.ToString());
        Assert.Equal((code, message), (error.Code, error.Message));
    }
}
