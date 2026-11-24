using System;
using System.Linq;
using Great.Parts.Cel.Policy.Yaml;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Yaml;

// The syntax of YAML that the reader accepts, the values of the styles of scalars, and the errors it reports, by the rules of
// go.yaml.in/yaml/v3 v3.0.4 that cel-go uses.
[Trait("Requirement", "F-30")]
public sealed class YamlSyntaxTests
{
    [Fact]
    public void BlockSequenceIndentedLessThanItsKey_IsTheValueOfTheKey()
    {
        YamlMapping root = Root<YamlMapping>("a:\n- 1\n- 2");

        YamlSequence sequence = Assert.IsType<YamlSequence>(Assert.Single(root.Pairs).Value);
        Assert.False(sequence.IsFlow);
        Assert.Equal("1", Value(sequence.Items[0]));
        Assert.Equal("2", Value(sequence.Items[1]));
    }

    [Fact]
    public void NestedBlocks_KeepTheirStructure()
    {
        YamlMapping root = Root<YamlMapping>("a:\n  b:\n    - x\n    - y: z\n  c: 1");

        YamlMapping inner = Assert.IsType<YamlMapping>(Assert.Single(root.Pairs).Value);
        Assert.Equal(2, inner.Pairs.Count);
        YamlSequence sequence = Assert.IsType<YamlSequence>(inner.Pairs[0].Value);
        Assert.Equal("x", Value(sequence.Items[0]));
        YamlMapping item = Assert.IsType<YamlMapping>(sequence.Items[1]);
        Assert.Equal("z", Value(Assert.Single(item.Pairs).Value));
        Assert.Equal("1", Value(inner.Pairs[1].Value));
    }

    [Fact]
    public void Comments_AreSkipped()
    {
        YamlMapping root = Root<YamlMapping>("# head\na: 1 # tail\n# between\n\nb: 2\n# foot");

        Assert.Equal(["a", "b"], root.Pairs.Select(pair => Value(pair.Key)).ToArray());
        Assert.Equal(["1", "2"], root.Pairs.Select(pair => Value(pair.Value)).ToArray());
    }

    [Fact]
    public void RepeatedKeys_AreKept()
    {
        YamlMapping root = Root<YamlMapping>("a: 1\na: 2");

        Assert.Equal(2, root.Pairs.Count);
    }

    [Fact]
    public void ExplicitDocumentWithEnd_IsTheDocument()
    {
        YamlMapping root = Root<YamlMapping>("---\na: 1\n...\n");

        Assert.Equal("1", Value(Assert.Single(root.Pairs).Value));
    }

    [Fact]
    public void ExplicitEmptyDocument_IsANullScalar()
    {
        YamlScalar root = Root<YamlScalar>("---\n");

        Assert.Equal(YamlNode.NullTag, root.Tag);
        Assert.Equal(string.Empty, root.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("# only a comment\n")]
    public static void StreamWithoutADocument_HasNoRoot(string text)
    {
        YamlReadResult result = YamlReader.Read(text);

        Assert.False(result.IsError);
        Assert.Null(result.Root);
    }

    [Fact]
    public void VersionDirective_MustBeOneDotOne()
    {
        Assert.Equal("1", Value(Assert.Single(Root<YamlMapping>("%YAML 1.1\n---\na: 1").Pairs).Value));
        Assert.Equal("found incompatible YAML document", Error("%YAML 1.2\n---\na: 1").ErrorMessage);
        Assert.Equal("found duplicate %YAML directive", Error("%YAML 1.1\n%YAML 1.1\n---\na: 1").ErrorMessage);
    }

    [Fact]
    public void ByteOrderMarkAtTheStart_IsNotPartOfTheDocument()
    {
        YamlMapping root = Root<YamlMapping>((char)0xFEFF + "a: 1");

        Assert.Equal("a", Value(Assert.Single(root.Pairs).Key));
        Assert.Equal(1, root.Pairs[0].Key.Start);
    }

    [Fact]
    public void NextLineAndSeparators_AreLineBreaksOfAPlainScalar()
    {
        YamlScalar value = ValueOfKeyA("a: b" + (char)0x85 + "  c");

        Assert.Equal("b c", value.Value);
    }

    [Theory]
    [InlineData("a: |\n  x\n  y\n", "x\ny\n")]
    [InlineData("a: |-\n  x\n  y\n", "x\ny")]
    [InlineData("a: |+\n  x\n\n", "x\n\n")]
    [InlineData("a: >\n  x\n  y\n\n  z\n", "x y\nz\n")]
    [InlineData("a: >-\n  x\n   y\n  z", "x\n y\nz")]
    [InlineData("a: |2\n    x\n", "  x\n")]
    [InlineData("a: |1-\n  x", " x")]
    [InlineData("a: |  # comment\n  x\n", "x\n")]
    [InlineData("a: |\n\n  x\n", "\nx\n")]
    [InlineData("a: |\r\n  x\r\n  y\r\n", "x\ny\n")]
    public static void BlockScalar_HasTheValueOfItsStyleAndIndicators(string text, string expected)
    {
        ArgumentNullException.ThrowIfNull(text);
        YamlScalar value = ValueOfKeyA(text);

        Assert.Equal(expected, value.Value);
        Assert.Equal(text[3] == '|' ? YamlScalarStyle.Literal : YamlScalarStyle.Folded, value.Style);
    }

    [Theory]
    [InlineData("a: 'it''s'", "it's")]
    [InlineData("a: 'b\n  c'", "b c")]
    [InlineData("a: 'b\n\n  c'", "b\nc")]
    [InlineData("a: \"b\n  c\"", "b c")]
    [InlineData("a: \"x\\\n   y\"", "xy")]
    [InlineData("a: \"\\x41\\t\\\\\\\"\"", "A\t\\\"")]
    [InlineData("a: \"\\0\\a\\b\\e\\ \\N\\_\\L\\P\"", "\0\a\b\u001b \u0085\u00a0\u2028\u2029")]
    [InlineData("a: b\n\n  c", "b\nc")]
    [InlineData("a: b   \n  c", "b c")]
    [InlineData("a: b  c", "b  c")]
    [InlineData("a: b#c", "b#c")]
    [InlineData("a: b # c", "b")]
    [InlineData("a: [x, 'y']", null)]
    public static void FlowScalar_HasTheValueOfItsStyle(string text, string? expected)
    {
        if (expected is null)
        {
            Assert.IsType<YamlSequence>(Assert.Single(Root<YamlMapping>(text).Pairs).Value);
            return;
        }

        Assert.Equal(expected, ValueOfKeyA(text).Value);
    }

    [Fact]
    public void LongEscapes_GiveTheCharacterOfTheCodePoint()
    {
        YamlScalar value = ValueOfKeyA("a: \"\\U" + "0001F600\\u" + "00e9\"");

        Assert.Equal(char.ConvertFromUtf32(0x1F600) + (char)0xE9, value.Value);
    }

    [Theory]
    [InlineData("a: b: c", "mapping values are not allowed in this context")]
    [InlineData("a: [1, 2", "did not find expected ',' or ']'")]
    [InlineData("a: {b: 1", "did not find expected ',' or '}'")]
    [InlineData("- a\nb: 1", "did not find expected '-' indicator")]
    [InlineData("a: 1\n- b", "did not find expected key")]
    [InlineData("a: *", "did not find expected alphabetic or numeric character")]
    [InlineData("a: &", "did not find expected alphabetic or numeric character")]
    [InlineData("%FOO bar\n---\na: 1", "found unknown directive name")]
    [InlineData("a: !e!foo 1", "found undefined tag handle")]
    [InlineData("%TAG !e! x\n%TAG !e! y\n---\na: 1", "found duplicate %TAG directive")]
    [InlineData("a: \"\\q\"", "found unknown escape character")]
    [InlineData("a: \"\\xZZ\"", "did not find expected hexdecimal number")]
    [InlineData("a: |0\n x", "found an indentation indicator equal to 0")]
    [InlineData("a: |x\n x", "did not find expected comment or line break")]
    [InlineData("a: 'x\n---\n'", "found unexpected document indicator")]
    [InlineData("a: 'x", "found unexpected end of stream")]
    [InlineData("a: 1\nb", "could not find expected ':'")]
    [InlineData("? a\n? b\nc: d\n- e", "did not find expected key")]
    [InlineData("a:\n  - b\n c: d", "did not find expected key")]
    [InlineData("]", "did not find expected node content")]
    [InlineData("a: @b", "found character that cannot start any token")]
    public static void NotYaml_IsAnErrorWithTheTextOfTheLibrary(string text, string message)
    {
        Assert.Equal(message, Error(text).ErrorMessage);
    }

    [Fact]
    public void LongEscapeOfASurrogate_IsAnError()
    {
        YamlReadResult result = Error("a: \"\\U" + "0000D800\"");

        Assert.Equal("found invalid Unicode character escape code", result.ErrorMessage);
    }

    [Fact]
    public void ControlCharacter_IsAnErrorAtTheCharacter()
    {
        const string Text = "a: b\nc: d";
        string withControl = Text.Insert(3, ((char)1).ToString());

        YamlReadResult result = Error(withControl);

        Assert.Equal("control characters are not allowed", result.ErrorMessage);
        Assert.Equal(3, result.ErrorOffset);
    }

    [Fact]
    public void UnpairedSurrogate_IsAnError()
    {
        YamlReadResult result = Error("a: " + (char)0xD800);

        Assert.Equal("control characters are not allowed", result.ErrorMessage);
    }

    [Fact]
    public void PairedSurrogates_AreOneCharacterInTheValue()
    {
        YamlScalar value = ValueOfKeyA("a: " + char.ConvertFromUtf32(0x1F600) + "x");

        Assert.Equal(char.ConvertFromUtf32(0x1F600) + "x", value.Value);
    }

    [Fact]
    public void TabAfterTheColon_IsWhitespace()
    {
        Assert.Equal("1", ValueOfKeyA("a:\t1").Value);
    }

    [Fact]
    public void TabInsideAFlowCollection_IsWhitespace()
    {
        YamlSequence sequence = Assert.IsType<YamlSequence>(Assert.Single(Root<YamlMapping>("a: [\t1,\t2\t]").Pairs).Value);

        Assert.Equal(2, sequence.Items.Count);
    }

    [Fact]
    public void FlowSequenceEntryWithAKey_IsAMappingOfOnePair()
    {
        YamlSequence root = Root<YamlSequence>("[a: 1, b]");

        YamlMapping first = Assert.IsType<YamlMapping>(root.Items[0]);
        Assert.Equal("a", Value(Assert.Single(first.Pairs).Key));
        Assert.Equal("b", Value(root.Items[1]));
    }

    [Fact]
    public void KeyWithoutAValue_IsANullValue()
    {
        YamlMapping root = Root<YamlMapping>("a:\nb:");

        Assert.All(root.Pairs, pair => Assert.Equal(YamlNode.NullTag, pair.Value.Tag));
    }

    [Fact]
    public void AnchorAndTagBeforeANode_StartTheNodeAtTheFirstOfThem()
    {
        const string Text = "a: &x !!str b";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal(3, value.Start);
        Assert.Equal(YamlNode.StrTag, value.Tag);
        Assert.Equal("b", value.Value);
    }

    [Fact]
    public void RecursiveAnchor_IsACycleOfNodes()
    {
        YamlSequence root = Root<YamlSequence>("&x [*x]");

        Assert.Same(root, Assert.Single(root.Items));
    }

    [Fact]
    public void Offsets_AreOffsetsInTheText()
    {
        const string Text = "key: value\nother: [1, 2]";

        YamlMapping root = Root<YamlMapping>(Text);

        Assert.All(root.Pairs, pair => Assert.True(pair.Key.Start >= 0 && pair.Value.End <= Text.Length));
        Assert.Equal("key", Text[root.Pairs[0].Key.Start..root.Pairs[0].Key.End]);
        Assert.Equal("value", Text[root.Pairs[0].Value.Start..root.Pairs[0].Value.End]);
        Assert.Equal(Text.IndexOf("other", StringComparison.Ordinal), root.Pairs[1].Key.Start);
    }
}
