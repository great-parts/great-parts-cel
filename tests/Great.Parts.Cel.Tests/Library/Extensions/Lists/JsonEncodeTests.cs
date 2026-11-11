using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Lists.ListsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Lists;

// json.encode of the set encoders on what the examples leave: the extremes of the double forms, the strings that only bindings can
// hold, the order of the keys of a map by code points and its units charged as the text grows, values of the application and of JSON
// documents, the details of the errors, and nesting that no recursion survives.
public sealed class JsonEncodeTests
{
    private const string Backslash = "\\";

    private static readonly LineTable Lines = new("0123456789");

    // The elements of an application list that fails when it is read: the second.
    private static readonly CelValue Failing = CelValue.FromList(CelList.CreateView(
        new[] { 1, 2, 3 },
        static number => number == 2 ? throw new InvalidOperationException("boom") : CelValue.FromInt64(number)));

    [Theory]
    [InlineData("json.encode(5e-324)", "5e-324")]
    [InlineData("json.encode(1.7976931348623157e308)", "1.7976931348623157e+308")]
    [InlineData("json.encode(-1e-10)", "-1e-10")]
    [InlineData("json.encode(1.2345e-10)", "1.2345e-10")]
    [InlineData("json.encode(9.999999e-7)", "9.999999e-7")]
    [InlineData("json.encode(0.1 + 0.2)", "0.30000000000000004")]
    [InlineData("json.encode(123456789012345680000.0)", "123456789012345680000")]
    // The double is exactly 123456789012345685803008: of the 17-digit texts that read back to it, ...69e+23 is the nearer.
    [InlineData("json.encode(1.2345678901234568e23)", "1.2345678901234569e+23")]
    [InlineData("json.encode(-1e21)", "-1e+21")]
    [InlineData("json.encode(1.5e300)", "1.5e+300")]
    [InlineData("json.encode(100000.5)", "100000.5")]
    [InlineData("json.encode(0.000123)", "0.000123")]
    [InlineData("json.encode(1e15)", "1000000000000000")]
    [InlineData("json.encode(-123.456)", "-123.456")]
    public static void Double_IsWrittenAsGoWritesIt(string expression, string expected)
    {
        Assert.Equal(expected, Json(expression));
    }

    // The characters that an expression cannot write: a lone surrogate is written as the escape of U+FFFD, a control character as the
    // escape of its code in lower case, and the delete character and a pair of surrogates as they are.
    [Fact]
    public static void String_EscapesWhatGoEscapes()
    {
        string lone = "a" + (char)0xD83D + "b";
        string control = "a" + (char)7 + (char)0x1F + "b";
        string kept = "a" + (char)0x7F + "😀b";

        Assert.Equal("\"a" + Backslash + "ufffdb\"", Json("json.encode(x)", new CelBindings().Add("x", lone)));
        Assert.Equal("\"a" + Backslash + "u0007" + Backslash + "u001fb\"", Json("json.encode(x)", new CelBindings().Add("x", control)));
        Assert.Equal("\"" + kept + "\"", Json("json.encode(x)", new CelBindings().Add("x", kept)));
        Assert.Equal("\"" + Backslash + "ufffd\"", Json("json.encode(x)", new CelBindings().Add("x", ((char)0xDE00).ToString())));
    }

    // The keys of a map are ordered by code points, so a character beyond the plane 0 comes after one of U+FF21, which the order of
    // UTF-16 units would put the other way; a key is escaped as a string is, but ordered by its characters, not by its text.
    [Fact]
    public static void Map_OrdersItsKeysByCodePoints()
    {
        Assert.Equal("{\"b\":3,\"Ａ\":2,\"😀\":1}", Json("json.encode({'😀': 1, 'Ａ': 2, 'b': 3})"));
        Assert.Equal("{\"" + Backslash + "u003c\":1,\"[\":2}", Json("json.encode({'[': 2, '<': 1})"));
    }

    // A map of the application with thirty keys is ordered above the twelve at which the order changes its way.
    [Fact]
    public static void MapOfManyKeys_IsOrderedByItsKeys()
    {
        var dictionary = new Dictionary<string, long>();
        var expected = new List<string>();
        for (int i = 29; i >= 0; i--)
        {
            dictionary.Add("k" + i.ToString("00", CultureInfo.InvariantCulture), i);
        }

        for (int i = 0; i < 30; i++)
        {
            expected.Add("\"k" + i.ToString("00", CultureInfo.InvariantCulture) + "\":" + i.ToString(CultureInfo.InvariantCulture));
        }

        Assert.Equal("{" + string.Join(",", expected) + "}", Json("json.encode(x)", new CelBindings().Add("x", dictionary)));
    }

    // The bytes are written in chunks, and the text is that of Convert whatever the length.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(95)]
    [InlineData(96)]
    [InlineData(97)]
    [InlineData(200)]
    public static void Bytes_AreWrittenAsBase64OfAnyLength(int length)
    {
        byte[] bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 11) + 5);
        }

        string text = Json("json.encode(x)", new CelBindings().Add("x", CelValue.FromBytes(bytes)));

        Assert.Equal("\"" + Convert.ToBase64String(bytes) + "\"", text);
    }

    // A JSON document is read as the value it holds: the numbers are doubles, the properties are ordered by their names.
    [Fact]
    public static void JsonDocument_IsWrittenWithItsKeysInOrder()
    {
        using JsonDocument document = JsonDocument.Parse("{\"b\":[1,\"x\",true],\"a\":null,\"c\":{\"z\":2.5,\"y\":{}}}");

        string text = Json("json.encode(x)", new CelBindings().Add("x", document.RootElement));

        Assert.Equal("{\"a\":null,\"b\":[1,\"x\",true],\"c\":{\"y\":{},\"z\":2.5}}", text);
    }

    [Fact]
    public static void JsonObjectWithARepeatedName_IsTheErrorOfItsKeys()
    {
        using JsonDocument document = JsonDocument.Parse("{\"a\":1,\"a\":2}");

        CelEvalResult<CelValue> result = Evaluate("json.encode(x)", new CelBindings().Add("x", document.RootElement));

        Assert.Equal(CelErrorCode.DuplicateMapKey, Assert.Single(result.Errors).Code);
    }

    // "expression | message": every error is InvalidArgument at the call.
    [Theory]
    [InlineData("json.encode({1: 'a'})", "unsupported type conversion from 'int' to string")]
    [InlineData("json.encode({1u: 'a'})", "unsupported type conversion from 'uint' to string")]
    [InlineData("json.encode({true: 1})", "unsupported type conversion from 'bool' to string")]
    [InlineData("json.encode(double('NaN'))", "a double that is NaN or infinite cannot be encoded")]
    [InlineData("json.encode([1, 2, -double('inf')])", "a double that is NaN or infinite cannot be encoded")]
    [InlineData("json.encode(int)", "unsupported type conversion")]
    [InlineData("json.encode([1, string])", "unsupported type conversion")]
    public static void ValueWithNoJson_IsRefusedWithItsDetail(string expression, string detail)
    {
        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal(CelErrorCode.InvalidArgument, error.Code);
        Assert.Equal("invalid argument to 'json.encode': " + detail, error.Message);
    }

    [Fact]
    public static void ElementThatFails_IsTheErrorOfTheCall()
    {
        CelEvalResult<CelValue> result = Evaluate("json.encode(x)", new CelBindings().Add("x", Failing));

        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(result.Errors).Code);
    }

    // The writer descends by recursion and asks for the stack before each level: a list nested two hundred thousand levels, which
    // only bindings can build, is InsufficientStack at the call, not a crash.
    [Fact]
    public static void DeepNesting_IsInsufficientStack()
    {
        CelValue deep = ListOf(CelValue.FromInt64(1));
        for (int i = 0; i < 200_000; i++)
        {
            deep = ListOf(deep);
        }

        CelEvalResult<CelValue> result = Evaluate("json.encode(x)", new CelBindings().Add("x", deep));

        Assert.Equal(CelErrorCode.InsufficientStack, Assert.Single(result.Errors).Code);
    }

    // A map is written in the order of its entries and then in the order of its keys, and the units are those of the text that results,
    // once: 27 characters of {"a":[2,{"c":false}],"b":1} are 3 units, and a limit of 2 stops the writer before the result is made.
    [Fact]
    public static void Map_ChargesTheLengthOfItsTextOnce()
    {
        CelValue inner = CelValue.FromMap(CelMap.Create([new(CelValue.FromString("c"), CelValue.FromBoolean(false))]));
        CelValue map = CelValue.FromMap(CelMap.Create(
        [
            new(CelValue.FromString("b"), CelValue.FromInt64(1)),
            new(CelValue.FromString("a"), ListOf(CelValue.FromInt64(2), inner)),
        ]));
        var meter = new RecordingMeter();

        CelValue result = JsonEncoder.Encode([map], new CallSite("json.encode", 4, Lines, meter));

        Assert.True(result.TryGetString(out string? text));
        Assert.Equal("{\"a\":[2,{\"c\":false}],\"b\":1}", text);
        Assert.Equal(3UL, meter.Total);
        Assert.Throws<CostLimitException>(() => JsonEncoder.Encode([map], new CallSite("json.encode", 4, Lines, new RecordingMeter(2))));
    }

    private static string Json(string expression, CelBindings? bindings = null)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, bindings);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetString(out string? text));
        return text;
    }
}
