using System.Linq;
using System.Text;
using Great.Parts.Cel.Policy.Yaml;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests.Yaml;

// The segments of the value of a scalar: where the characters of the value stand in the document, for every style of scalar and for both
// line endings. A segment starts after each place where YAML changed the text: an escape sequence, a folded line break with the
// indentation after it, a line of a block.
[Trait("Requirement", "F-31")]
public sealed class YamlSegmentTests
{
    private static readonly string[] LineEndings = ["\n", "\r\n"];

    private static readonly string[] SpaceSeparators = [" "];

    [Fact]
    public void LiteralBlock_HasASegmentAtTheStartOfEachLineAfterItsIndentation()
    {
        const string Text = "a: |\n  x\n  y\n";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("x\ny\n", value.Value);
        Assert.Equal([(0, 7), (2, 11)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void FoldedBlock_HasASegmentAfterEachFold()
    {
        const string Text = "a: >\n  x\n  y\n";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("x y\n", value.Value);
        Assert.Equal([(0, 7), (2, 11)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void LineEndingCarriageReturnLineFeed_ShiftsTheDocumentOffsetByOnePerLine()
    {
        const string Text = "a: b\r\n  c";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("b c", value.Value);
        Assert.Equal([(0, 3), (2, 8)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void SingleQuotedScalarOnTwoLines_HasASegmentOnTheSecondLine()
    {
        const string Text = "a: 'b\n  c'";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("b c", value.Value);
        Assert.Equal([(0, 4), (2, 8)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void DoubledSingleQuote_HasASegmentAfterIt()
    {
        const string Text = "a: 'it''s'";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("it's", value.Value);
        Assert.Equal([(0, 4), (3, 8)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void EscapedLineBreak_HasASegmentAtTheFirstCharacterOfTheNextLine()
    {
        const string Text = "a: \"x\\\n   y\"";

        YamlScalar value = ValueOfKeyA(Text);

        Assert.Equal("xy", value.Value);
        Assert.Equal([(0, 4), (1, 10)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    [Fact]
    public void EmptyScalar_HasOneSegment()
    {
        YamlScalar value = ValueOfKeyA("a: \"\"");

        Assert.Equal(string.Empty, value.Value);
        Assert.Equal((0, 4), (value.Segments.Single().ValueOffset, value.Segments.Single().DocumentOffset));
    }

    [Fact]
    public void EscapeAtTheEndOfTheValue_AddsNoSegmentAtTheEnd()
    {
        YamlScalar value = ValueOfKeyA("a: \"x\\t\"");

        Assert.Equal("x\t", value.Value);
        Assert.Equal([(0, 4)], value.Segments.Select(s => (s.ValueOffset, s.DocumentOffset)).ToArray());
    }

    // A plain scalar, and a scalar in single or double quotes, written on several lines with escape sequences and doubled quotes: each
    // letter and digit of the value stands in the document at the place that the segments give.
    [Fact]
    public void RandomScalars_HaveEveryLetterAtThePlaceOfItsSegment()
    {
        var random = new TestRandom(97);
        for (int i = 0; i < 400; i++)
        {
            string eol = LineEndings[random.Next(LineEndings.Length)];
            AssertLettersAtSegments(PlainDocument(random, eol));
            AssertLettersAtSegments(SingleQuotedDocument(random, eol));
            AssertLettersAtSegments(DoubleQuotedDocument(random, eol));
            AssertLettersAtSegments(BlockDocument(random, eol, '|'));
            AssertLettersAtSegments(BlockDocument(random, eol, '>'));
        }
    }

    private static void AssertLettersAtSegments(string text)
    {
        YamlScalar value = ValueOfKeyA(text);
        Assert.Equal(0, value.Segments[0].ValueOffset);
        for (int s = 1; s < value.Segments.Length; s++)
        {
            Assert.True(value.Segments[s].ValueOffset > value.Segments[s - 1].ValueOffset, "The segments do not grow: " + text);
        }

        for (int i = 0; i < value.Value.Length; i++)
        {
            char c = value.Value[i];
            if (!char.IsAsciiLetterOrDigit(c))
            {
                continue;
            }

            YamlSegment segment = value.Segments.Last(s => s.ValueOffset <= i);
            int offset = segment.DocumentOffset + i - segment.ValueOffset;
            Assert.True(offset < text.Length && text[offset] == c, "The character " + c + " of the value is not at its segment in: " + text);
        }
    }

    private static string Word(TestRandom random)
    {
        const string Letters = "abcdefghijklmnopqrstuvwxyz0123456789";
        var word = new StringBuilder();
        int length = random.Next(1, 7);
        for (int i = 0; i < length; i++)
        {
            word.Append(Letters[random.Next(Letters.Length)]);
        }

        return word.ToString();
    }

    private static string Join(TestRandom random, string[] separators)
    {
        var text = new StringBuilder(Word(random));
        int count = random.Next(1, 8);
        for (int i = 0; i < count; i++)
        {
            text.Append(separators[random.Next(separators.Length)]).Append(Word(random));
        }

        return text.ToString();
    }

    private static string PlainDocument(TestRandom random, string eol) =>
        "a: " + Join(random, [" ", " ", eol + "  ", eol + eol + "    "]);

    private static string SingleQuotedDocument(TestRandom random, string eol) =>
        "a: '" + Join(random, [" ", eol + "  ", "''", eol + eol + "  "]) + "'";

    private static string DoubleQuotedDocument(TestRandom random, string eol) =>
        "a: \"" + Join(random, [" ", eol + "  ", "\\t", "\\x3b", "\\\"", "\\\\", "\\" + eol + "   ", eol + eol + "  "]) + "\"";

    private static string BlockDocument(TestRandom random, string eol, char style)
    {
        var text = new StringBuilder("a: ").Append(style).Append(eol);
        int lines = random.Next(1, 6);
        for (int i = 0; i < lines; i++)
        {
            text.Append("  ").Append(Join(random, SpaceSeparators)).Append(eol);
            if (random.Next(4) == 0)
            {
                text.Append(eol);
            }
        }

        return text.ToString();
    }
}
