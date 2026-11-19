using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
#if NET11_0_OR_GREATER
using Great.Parts.Cel.Tests.Examples.Library;
#endif
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The text of the kinds of value that the examples do not show: an optional, an error value, the non-finite doubles, the exponent
// form, a map with keys of several kinds, bytes and strings with controls and lone surrogates, a decimal, and the depth of 32 and
// 34 levels.
[Trait("Requirement", "F-13")]
public sealed class ValueTextKindsTests
{
    [Fact]
    public void Optional_IsOptionalOfOrNone()
    {
        Assert.Equal("optional.of(1)", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1))).ToString());
        Assert.Equal("optional.of(null)", CelValue.FromOptional(new CelOptional<CelValue>(CelValue.Null)).ToString());
        Assert.Equal("optional.none()", CelValue.FromOptional(default).ToString());
    }

    [Fact]
    public void ErrorValue_IsTheCodeAndTheMessage()
    {
        CelError error = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow().Compile<bool>("1 +").Errors[0];

        string text = CelValue.FromError(error).ToString();

        Assert.Equal("error(" + error.Code + ": " + error.Message + ")", text);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, "double(\"Infinity\")")]
    [InlineData(double.NegativeInfinity, "double(\"-Infinity\")")]
    [InlineData(double.NaN, "double(\"NaN\")")]
    [InlineData(-0.0, "-0.0")]
    [InlineData(0.1, "0.1")]
    [InlineData(100.0, "100.0")]
    [InlineData(1e21, "1e+21")]
    [InlineData(-2.5e-7, "-2.5e-07")]
    public static void Double_IsWrittenLikeStringOfDouble(double value, string text)
    {
        Assert.Equal(text, CelValue.FromDouble(value).ToString());
    }

    [Fact]
    public void Map_HasTheKeysOfSeveralKinds()
    {
        CelValue map = CelValue.FromMap(CelMap.Create(
        [
            new KeyValuePair<CelValue, CelValue>(CelValue.FromInt64(1), CelValue.FromString("a")),
            new KeyValuePair<CelValue, CelValue>(CelValue.FromString("k"), CelValue.FromBoolean(true)),
            new KeyValuePair<CelValue, CelValue>(CelValue.FromUInt64(2), CelValue.Null),
            new KeyValuePair<CelValue, CelValue>(CelValue.FromBoolean(false), CelValue.FromDouble(1.5)),
        ]));

        string text = map.ToString();

        Assert.StartsWith("{", text, StringComparison.Ordinal);
        Assert.EndsWith("}", text, StringComparison.Ordinal);
        Assert.Contains("1: \"a\"", text, StringComparison.Ordinal);
        Assert.Contains("\"k\": true", text, StringComparison.Ordinal);
        Assert.Contains("2u: null", text, StringComparison.Ordinal);
        Assert.Contains("false: 1.5", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Bytes_AreWrittenWithEscapes()
    {
        byte[] bytes = [0x00, 0x41, 0x7F, 0x80, 0x22, 0x5C, 0x0A];

        Assert.Equal("b\"\\x00A\\x7F\\x80\\\"\\\\\\n\"", CelValue.FromBytes(bytes).ToString());
    }

    [Fact]
    public void String_WithALoneSurrogate_IsWrittenWithAnEscapeAndDoesNotThrow()
    {
        Assert.Equal("\"a\\uD800b\"", CelValue.FromString("a\uD800b").ToString());
        Assert.Equal("\"😀\"", CelValue.FromString("😀").ToString());
    }

    [Fact]
    public void DefaultValue_IsNull() => Assert.Equal("null", default(CelValue).ToString());

    [Fact]
    public void ValueAt32Levels_IsWrittenInFull()
    {
        string text = Nested(32).ToString();

        Assert.Equal(new string('[', 32) + "1" + new string(']', 32), text);
    }

    [Fact]
    public void ValueAt34Levels_IsCutAt32()
    {
        string text = Nested(34).ToString();

        Assert.Equal(new string('[', 32) + "…" + new string(']', 32), text);
    }

    [Fact]
    public void MapValue_AtTheDepthLimit_IsCut()
    {
        // The map is level 0, so the list that holds the 1 is level 1, and the list at level 32 is cut: 31 brackets are written.
        CelValue value = CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromInt64(1), Nested(40))]));

        string text = value.ToString();

        Assert.StartsWith("{1: ", text, StringComparison.Ordinal);
        Assert.Contains("…", text, StringComparison.Ordinal);
        Assert.Equal(31, CountOf(text, '['));
    }

#if NET11_0_OR_GREATER
    [Fact]
    public void Decimal_IsWrittenWithTheSuffix()
    {
        Assert.Equal("12.34m", CelValue.FromDecimal128(DecimalExample.Make(1234, -2)).ToString());
        Assert.Equal("1E+2m", CelValue.FromDecimal128(DecimalExample.Make(1, 2)).ToString());
    }
#endif

    private static CelValue Nested(int levels)
    {
        CelValue value = CelValue.FromInt64(1);
        for (int level = 0; level < levels; level++)
        {
            value = CelValue.FromList(CelList.Create(value));
        }

        return value;
    }

    private static int CountOf(string text, char c)
    {
        int count = 0;
        foreach (char unit in text)
        {
            if (unit == c)
            {
                count++;
            }
        }

        return count;
    }
}
