using System;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

public sealed class LineTableTests
{
    // The expected "line:column" of every offset of the text, from 0 to the end of the text.
    [Theory]
    [InlineData("", "1:1")]
    [InlineData("ab", "1:1 1:2 1:3")]
    [InlineData("a\nb", "1:1 1:2 2:1 2:2")]
    [InlineData("a\rb", "1:1 1:2 2:1 2:2")]
    [InlineData("a\r\nb", "1:1 1:2 1:3 2:1 2:2")]
    [InlineData("\r\r\n\n", "1:1 2:1 2:2 3:1 4:1")]
    [InlineData("\n\rx\r", "1:1 2:1 3:1 3:2 4:1")]
    [InlineData("ab\ncd\r\nef\rgh\n", "1:1 1:2 1:3 2:1 2:2 2:3 2:4 3:1 3:2 3:3 4:1 4:2 4:3 5:1")]
    public static void EveryOffset_GivesItsLineAndColumnFromOne(string text, string positions)
    {
        LineTable lines = new(text);

        string actual = string.Join(
            ' ',
            Enumerable.Range(0, text.AsSpan().Length + 1)
                .Select(offset => lines.GetLocation(offset))
                .Select(location => string.Create(CultureInfo.InvariantCulture, $"{location.Line}:{location.Column}")));

        Assert.Equal(positions, actual);
    }

    [Fact]
    public void Location_KeepsTheOffsetAndHasNoSourceName()
    {
        CelSourceLocation location = new LineTable("ab\ncd").GetLocation(4);

        Assert.Equal(((string?)null, 4, 2, 2), (location.SourceName, location.Offset, location.Line, location.Column));
    }
}
