using Great.Parts.Cel.Policy.Yaml;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Yaml;

// The tag of a plain scalar by the table of resolve.go of go.yaml.in/yaml/v3 v3.0.4: one row of the table, or one edge of a row, in
// each line. A scalar that is quoted, a block scalar, and a scalar with a tag of its own do not go through the table.
[Trait("Requirement", "F-30")]
public sealed class YamlTagTests
{
    [Theory]
    [InlineData("true", "bool")]
    [InlineData("True", "bool")]
    [InlineData("TRUE", "bool")]
    [InlineData("false", "bool")]
    [InlineData("False", "bool")]
    [InlineData("FALSE", "bool")]
    [InlineData("tRue", "str")]
    [InlineData("yes", "str")]
    [InlineData("No", "str")]
    [InlineData("on", "str")]
    [InlineData("Off", "str")]
    [InlineData("y", "str")]
    [InlineData("n", "str")]
    [InlineData("", "null")]
    [InlineData("~", "null")]
    [InlineData("null", "null")]
    [InlineData("Null", "null")]
    [InlineData("NULL", "null")]
    [InlineData("nULL", "str")]
    [InlineData("<<", "merge")]
    public static void Word_HasTheTagOfItsSpelling(string value, string tag)
    {
        Assert.Equal(tag, Short(ValueOfKeyA("a: " + value)));
    }

    [Theory]
    [InlineData("0", "int")]
    [InlineData("-1", "int")]
    [InlineData("+5", "int")]
    [InlineData("42", "int")]
    [InlineData("0x1f", "int")]
    [InlineData("0X1F", "int")]
    [InlineData("-0x1F", "int")]
    [InlineData("0o17", "int")]
    [InlineData("0b101", "int")]
    [InlineData("-0b101", "int")]
    [InlineData("017", "int")]
    [InlineData("1_000", "int")]
    [InlineData("9223372036854775807", "int")]
    [InlineData("-9223372036854775808", "int")]
    [InlineData("18446744073709551615", "int")]
    [InlineData("+9223372036854775807", "int")]
    [InlineData("+9223372036854775808", "float")]
    [InlineData("18446744073709551616", "float")]
    [InlineData("-9223372036854775809", "float")]
    [InlineData("0x", "str")]
    [InlineData("0xg", "str")]
    [InlineData("0b2", "str")]
    [InlineData("09", "float")]
    [InlineData("1a", "str")]
    [InlineData("+", "str")]
    [InlineData("1-2", "str")]
    public static void Integer_HasTheTagOfItsForm(string value, string tag)
    {
        Assert.Equal(tag, Short(ValueOfKeyA("a: " + value)));
    }

    [Theory]
    [InlineData("1.5", "float")]
    [InlineData("1.", "float")]
    [InlineData("-.5", "float")]
    [InlineData("+1.5e3", "float")]
    [InlineData("1e3", "float")]
    [InlineData("1E-3", "float")]
    [InlineData(".5", "float")]
    [InlineData(".5e3", "float")]
    [InlineData(".5_5", "float")]
    [InlineData("1_0.5", "float")]
    [InlineData(".nan", "float")]
    [InlineData(".NaN", "float")]
    [InlineData(".NAN", "float")]
    [InlineData(".inf", "float")]
    [InlineData("+.Inf", "float")]
    [InlineData("-.INF", "float")]
    [InlineData(".iNf", "str")]
    [InlineData(".", "str")]
    [InlineData(".e1", "str")]
    [InlineData(".5e", "str")]
    [InlineData("._5", "str")]
    [InlineData("1.2.3", "str")]
    [InlineData("1e999", "str")]
    [InlineData(".5e999", "str")]
    [InlineData("1e", "str")]
    public static void Float_HasTheTagOfItsForm(string value, string tag)
    {
        Assert.Equal(tag, Short(ValueOfKeyA("a: " + value)));
    }

    [Theory]
    [InlineData("2024-01-01", "timestamp")]
    [InlineData("2024-1-2", "timestamp")]
    [InlineData("2024-01-01T10:00:00Z", "timestamp")]
    [InlineData("2024-01-01t10:00:00Z", "timestamp")]
    [InlineData("2024-01-01T10:00:00.5+02:30", "timestamp")]
    [InlineData("2024-1-1T1:2:3-00:00", "timestamp")]
    [InlineData("2024-01-01 10:00:00", "timestamp")]
    [InlineData("2024-01-01 10:00:00.123456789", "timestamp")]
    [InlineData("2024-02-29", "timestamp")]
    [InlineData("2023-02-29", "str")]
    [InlineData("2024-02-30", "str")]
    [InlineData("2024-13-01", "str")]
    [InlineData("2024-00-01", "str")]
    [InlineData("2024-01-00", "str")]
    [InlineData("2024-01-01T10:00:00", "str")]
    [InlineData("2024-01-01T10:00:00z", "str")]
    [InlineData("2024-01-01 10:00:00Z", "str")]
    [InlineData("2024-01-01T25:00:00Z", "str")]
    [InlineData("2024-01-01T10:60:00Z", "str")]
    [InlineData("2024-01-01T10:00:60Z", "str")]
    [InlineData("2024-01-01T10:00:00+0200", "str")]
    [InlineData("2024-01-01x", "str")]
    [InlineData("20240-01-01", "str")]
    [InlineData("202-01-01", "str")]
    public static void Timestamp_HasTheTagOfTheFormsOfTimeParse(string value, string tag)
    {
        Assert.Equal(tag, Short(ValueOfKeyA("a: " + value)));
    }

    [Theory]
    [InlineData("a: \"1\"")]
    [InlineData("a: 'true'")]
    [InlineData("a: \"null\"")]
    [InlineData("a: \"2024-01-01\"")]
    [InlineData("a: |\n  1\n")]
    [InlineData("a: >\n  true\n")]
    public static void QuotedAndBlockScalars_AreStrings(string text)
    {
        Assert.Equal("str", Short(ValueOfKeyA(text)));
    }

    [Theory]
    [InlineData("a: !!int \"3\"", "int")]
    [InlineData("a: !!str 3", "str")]
    [InlineData("a: !!float 3", "float")]
    [InlineData("a: !!bool x", "bool")]
    [InlineData("a: !<tag:yaml.org,2002:float> 3", "float")]
    [InlineData("a: ! 3", "int")]
    [InlineData("a: ! \"3\"", "str")]
    [InlineData("a: !txt 3", "!txt")]
    [InlineData("a: !foo 3", "!foo")]
    public static void ExplicitTag_IsTheTagOfTheNodeWhateverItsText(string text, string tag)
    {
        Assert.Equal(tag, Short(ValueOfKeyA(text)));
    }

    [Theory]
    [InlineData("a: !!map {}", "map")]
    [InlineData("a: !!seq []", "seq")]
    [InlineData("a: !txt []", "!txt")]
    [InlineData("a: []", "seq")]
    [InlineData("a: {}", "map")]
    public static void Collection_HasItsTagOrTheTagOfTheDocument(string text, string tag)
    {
        YamlMapping root = Root<YamlMapping>(text);

        Assert.Equal(tag, Short(root.Pairs[0].Value));
    }

    [Fact]
    public void ExplicitNonSpecificTag_IsNotAnExplicitTag()
    {
        YamlScalar value = ValueOfKeyA("a: ! 3");

        Assert.False(value.HasExplicitTag);
    }
}
