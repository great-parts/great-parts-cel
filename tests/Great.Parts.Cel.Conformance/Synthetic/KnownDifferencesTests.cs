using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The registry of known differences, read only for its signatures (docs/design/conformance.md, "Skip lists and findings").
public sealed class KnownDifferencesTests
{
    private const string Path = "tests/Great.Parts.Cel.Conformance/Regression/known-differences.md";

    [Fact]
    public void Signatures_AreTheFirstColumnOfTheRows()
    {
        string text = """
            # Known differences from cel-go

            Divergences.

            | Signature | Class | Reference |
            |---|---|---|
            | duration_to_milliseconds(duration) value-mismatch | cel-go-bug | Minimal case: duration('1.5s').getMilliseconds(). |
            | matches(string, string) we-error | by-design | Minimal case: 'a'.matches('(') \| 'b'. |
            """;

        KnownDifferences registry = KnownDifferences.Read(Path, text);

        Assert.Empty(registry.Findings);
        Assert.Equal(["duration_to_milliseconds(duration) value-mismatch", "matches(string, string) we-error"], registry.Signatures.Order(System.StringComparer.Ordinal));
    }

    [Fact]
    public void FileWithoutTheTable_IsEng800AtLineOneColumnOne()
    {
        KnownDifferences registry = KnownDifferences.Read(Path, "# Known differences from cel-go\n");

        FindingAssert.Single(registry.Findings, EngCodes.Eng800, Path, 1, 1, "the file has no table with the columns 'Signature | Class | Reference'");
        Assert.Empty(registry.Signatures);
    }

    [Fact]
    public void WrongColumns_AreEng800AtTheHeader()
    {
        KnownDifferences registry = KnownDifferences.Read(Path, "# Known differences\n\n| Signature | Reference |\n|---|---|\n| a(int) we-error | x |\n");

        FindingAssert.Single(registry.Findings, EngCodes.Eng800, Path, 3, 1, "the columns of the table are 'Signature | Reference', not 'Signature | Class | Reference'");
        Assert.Empty(registry.Signatures);
    }
}
