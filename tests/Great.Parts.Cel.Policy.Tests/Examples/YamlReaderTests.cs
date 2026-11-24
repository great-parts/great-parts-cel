using System.Linq;
using Great.Parts.Cel.Policy.Yaml;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Examples;

// The reader of YAML: the nodes, the tags that the library of cel-go gives to them, the places, the first error and the first document
// only.
[Trait("Requirement", "F-30")]
public sealed class YamlReaderTests
{
    private static readonly string[] BlockMappingTags = ["int", "str", "str", "str"];

    [Fact]
    public void BlockMapping_HasThePairsTheTagsAndThePlacesOfTheValues()
    {
        const string Text = "a: 1\nb: \"x\"\nc: 'y'\nd: z";

        YamlMapping mapping = Root<YamlMapping>(Text);

        Assert.Equal(4, mapping.Pairs.Count);
        Assert.Equal(BlockMappingTags, mapping.Pairs.Select(pair => Short(pair.Value)).ToArray());
        Assert.Equal(["1:4", "2:4", "3:4", "4:4"], mapping.Pairs.Select(pair => Place(Text, pair.Value.Start)).ToArray());
        Assert.Equal(YamlScalarStyle.DoubleQuoted, Assert.IsType<YamlScalar>(mapping.Pairs[1].Value).Style);
        Assert.Equal(YamlScalarStyle.SingleQuoted, Assert.IsType<YamlScalar>(mapping.Pairs[2].Value).Style);
        Assert.False(mapping.IsFlow);
    }

    [Fact]
    public void FlowCollections_AreASequenceAndAMapping()
    {
        YamlMapping root = Root<YamlMapping>("a: [1, {b: c}]");

        YamlSequence sequence = Assert.IsType<YamlSequence>(Assert.Single(root.Pairs).Value);
        Assert.True(sequence.IsFlow);
        Assert.Equal(2, sequence.Items.Count);
        Assert.Equal("1", Value(sequence.Items[0]));
        YamlMapping inner = Assert.IsType<YamlMapping>(sequence.Items[1]);
        Assert.True(inner.IsFlow);
        Assert.Equal("b", Value(Assert.Single(inner.Pairs).Key));
        Assert.Equal("c", Value(Assert.Single(inner.Pairs).Value));
    }

    [Fact]
    public void Alias_IsTheNodeOfTheAnchorItself()
    {
        YamlMapping root = Root<YamlMapping>("a: &x {b: 1}\nc: *x");

        Assert.Same(root.Pairs[0].Value, root.Pairs[1].Value);
    }

    [Fact]
    public void UnknownAlias_IsAnErrorAtTheAlias()
    {
        const string Text = "a: *nope";

        YamlReadResult result = Error(Text);

        Assert.Equal("unknown anchor 'nope' referenced", result.ErrorMessage);
        Assert.Equal("1:4", Place(Text, result.ErrorOffset));
    }

    [Theory]
    [InlineData("a: yes", "str")]
    [InlineData("a: on", "str")]
    [InlineData("a: ~", "null")]
    [InlineData("a: 0x1F", "int")]
    [InlineData("a: 1_000", "int")]
    [InlineData("a: .inf", "float")]
    [InlineData("a: 2024-01-01", "timestamp")]
    [InlineData("a: 2024-01-01T10:00:00Z", "timestamp")]
    public static void PlainScalar_HasTheTagOfTheTableOfTheLibrary(string text, string tag)
    {
        Assert.Equal(tag, Short(ValueOfKeyA(text)));
    }

    [Fact]
    public void LocalTag_IsKeptAsWritten()
    {
        YamlScalar value = ValueOfKeyA("a: !txt \"x\"");

        Assert.Equal("!txt", value.Tag);
        Assert.Equal("x", value.Value);
        Assert.True(value.HasExplicitTag);
    }

    [Fact]
    public void TagHandleOfADirective_IsExpanded()
    {
        YamlScalar value = ValueOfKeyA("%TAG !e! tag:example.com,2000:\n---\na: !e!foo 1");

        Assert.Equal("tag:example.com,2000:foo", value.Tag);
        Assert.Equal("1", value.Value);
    }

    [Fact]
    public void ExplicitKey_IsAKeyOfTheMapping()
    {
        YamlMapping root = Root<YamlMapping>("? a\n: 1");

        YamlPair pair = Assert.Single(root.Pairs);
        Assert.Equal("a", Value(pair.Key));
        Assert.Equal("1", Value(pair.Value));
    }

    [Fact]
    public void SecondDocument_IsNotRead()
    {
        YamlMapping root = Root<YamlMapping>("a: 1\n---\nb: 2");

        YamlPair pair = Assert.Single(root.Pairs);
        Assert.Equal("a", Value(pair.Key));
        Assert.Equal("1", Value(pair.Value));
    }

    [Fact]
    public void TabInTheIndentation_IsAnErrorAtTheTab()
    {
        const string Text = "a:\n\tb: 1";

        YamlReadResult result = Error(Text);

        Assert.Equal("2:1", Place(Text, result.ErrorOffset));
    }

    [Fact]
    public void UnclosedQuote_IsAnErrorAtTheEndOfTheDocument()
    {
        const string Text = "a: \"x";

        YamlReadResult result = Error(Text);

        Assert.Equal(Text.Length, result.ErrorOffset);
    }

    [Fact]
    public void HundredThousandBrackets_AreAnErrorOfTheDepthWithoutOverflowingTheStack()
    {
        YamlReadResult result = Error(new string('[', 100_000));

        Assert.Contains("max depth", result.ErrorMessage, System.StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesOfADoubleQuotedScalar_GiveTheValueAndASegmentAfterEachOne()
    {
        // a: "x<tab>y<e acute>" with the escape sequences \t, \xe9 and \n written as YAML escapes, then z.
        const string Text = "a: \"x\\ty\\xe9\\nz\"";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("x\ty" + (char)0xE9 + "\nz", value.Value);
        Assert.Equal(YamlScalarStyle.DoubleQuoted, value.Style);
        Assert.Equal([(0, 4), (2, 7), (4, 12), (5, 14)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void PlainScalarOnTwoLines_IsFoldedToASpaceAndHasASegmentOnTheSecondLine()
    {
        const string Text = "a: b\n  c";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("b c", value.Value);
        Assert.Equal(["1:4", "2:3"], value.Segments.Select(s => Place(Text, s.DocumentOffset)).ToArray());
        Assert.Equal([0, 2], value.Segments.Select(s => s.ValueOffset).ToArray());
    }
}
