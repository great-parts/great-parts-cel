using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Configuration;

public sealed class JsonPositionsTests
{
    // Lines of Utf8JsonReader are counted by "\n" only: "ab", "cd\r" and "ef\rgh".
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 2, 2)]
    [InlineData(1, 0, 3)]
    [InlineData(1, 2, 5)]
    [InlineData(2, 0, 7)]
    [InlineData(2, 3, 10)]
    [InlineData(2, 5, 12)]
    public static void ByteIndex_CountsLinesByLineFeed(long line, long bytePositionInLine, int byteIndex)
    {
        Assert.Equal(byteIndex, JsonPositions.ByteIndex("ab\ncd\r\nef\rgh"u8, line, bytePositionInLine));
    }

    [Fact]
    public static void Locate_CountsUtf16CodeUnitsAndLines()
    {
        // 'a' is 1 byte, U+00E9 2 bytes and U+1F600 4 bytes and 2 UTF-16 code units.
        byte[] json = Encoding.UTF8.GetBytes("a\u00e9\U0001F600\r\nb\rc");

        ImmutableArray<CelSourceLocation> locations = JsonPositions.Locate(json, [0, 1, 3, 7, 9, 10, 11, 12], "doc");

        Assert.Equal(
            "doc 0 1:1|doc 1 1:2|doc 2 1:3|doc 4 1:5|doc 6 2:1|doc 7 2:2|doc 8 3:1|doc 9 3:2",
            string.Join('|', locations.Select(Format)));
    }

    [Fact]
    public static void Locate_IndexInsideACharacter_GetsAPosition()
    {
        // The index 1 falls between the two bytes of U+00E9; each piece decodes to one replacement character.
        CelSourceLocation location = Assert.Single(JsonPositions.Locate("\u00e9"u8, [1], "doc"));

        Assert.Equal("doc 1 1:2", Format(location));
    }

    [Fact]
    public static void Locate_IndexInsideALineBreak_IsOnTheLineThatTheBreakEnds()
    {
        // "\r\n" is one line break, so the index of its "\n" is still on line 1; the text after the index decides it.
        CelSourceLocation location = Assert.Single(JsonPositions.Locate("a\r\nb"u8, [2], "doc"));

        Assert.Equal("doc 2 1:3", Format(location));
    }

    [Fact]
    public static void Locate_WithoutIndexes_GivesNoPositions()
    {
        Assert.Empty(JsonPositions.Locate("{}"u8, [], "doc"));
    }

    private static string Format(CelSourceLocation location) =>
        string.Create(CultureInfo.InvariantCulture, $"{location.SourceName} {location.Offset} {location.Line}:{location.Column}");
}
