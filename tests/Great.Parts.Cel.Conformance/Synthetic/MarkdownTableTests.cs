using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The reader of the one table of a skip list or of the registry (docs/design/conformance.md, "Skip lists and findings").
public sealed class MarkdownTableTests
{
    private static readonly string[] Columns = ["A", "B", "C"];

    [Fact]
    public void Rows_AreTheLinesThatStartWithABarAfterTheSeparator_WithTrimmedCells()
    {
        string text = "# Title\n\nText | with a bar.\n\n|A|  B  | C |\n| --- | :-- | --: |\n|  1 |2| 3  |\n\nMore text.\n| x | y | z\n";

        MarkdownTable table = MarkdownTable.Read(text, Columns);

        Assert.Empty(table.Problems);
        Assert.Equal(["7: 1 / 2 / 3", "10: x / y / z"], table.Rows.Select(row => $"{row.Line}: {string.Join(" / ", row.Cells)}"));
    }

    [Fact]
    public void EscapedBar_IsPartOfTheCell()
    {
        string text = "| A | B | C |\n|---|---|---|\n| a \\| b | \\| | c\\|\\| |\n";

        MarkdownTable table = MarkdownTable.Read(text, Columns);

        Assert.Empty(table.Problems);
        Assert.Equal(["a | b", "|", "c||"], Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void LinesEndingWithCarriageReturnAndLineFeed_AreRead()
    {
        string text = "| A | B | C |\r\n|---|---|---|\r\n| 1 | 2 | 3 |\r\n";

        MarkdownTable table = MarkdownTable.Read(text, Columns);

        Assert.Empty(table.Problems);
        Assert.Equal(["1", "2", "3"], Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void EmptyCells_AreCells()
    {
        MarkdownTable table = MarkdownTable.Read("| A | B | C |\n|---|---|---|\n| | | |\n", Columns);

        Assert.Empty(table.Problems);
        Assert.Equal(["", "", ""], Assert.Single(table.Rows).Cells);
    }

    [Fact]
    public void RowWithAnotherNumberOfCells_IsAProblemAndNotARow()
    {
        MarkdownTable table = MarkdownTable.Read("| A | B | C |\n|---|---|---|\n| 1 | 2 |\n| 1 | 2 | 3 |\n| 1 | 2 | 3 | 4 |\n|\n", Columns);

        Assert.Equal(
            ["3: the row has 2 cells, not 3 cells", "5: the row has 4 cells, not 3 cells", "6: the row has 1 cell, not 3 cells"],
            table.Problems.Select(problem => $"{problem.Line}: {problem.Message}"));
        Assert.Equal([4], table.Rows.Select(row => row.Line));
    }
}
