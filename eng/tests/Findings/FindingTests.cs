using System;
using Great.Parts.Cel.Eng.Findings;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Findings;

public sealed class FindingTests
{
    [Fact]
    public void ToString_Error_IsTheCommonForm()
    {
        Finding finding = new("src/Lib/A.cs", 7, 32, FindingSeverity.Error, "ARCH001", "namespace cycle Lib.A -> Lib.B -> Lib.A");

        Assert.Equal("src/Lib/A.cs(7,32): error ARCH001: namespace cycle Lib.A -> Lib.B -> Lib.A", finding.ToString());
    }

    [Fact]
    public void ToString_Warning_IsTheCommonForm()
    {
        Finding finding = new(".editorconfig", 1, 1, FindingSeverity.Warning, "ENG102", "reason repeats the rule ID");

        Assert.Equal(".editorconfig(1,1): warning ENG102: reason repeats the rule ID", finding.ToString());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public static void Constructor_LineOrColumnBelowOne_IsRejected(int line, int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Finding("a.cs", line, column, FindingSeverity.Error, "ENG601", "m"));
    }

    [Fact]
    public void Constructor_FirstPlace_IsAccepted()
    {
        Finding finding = new("a.cs", 1, 1, FindingSeverity.Error, "ENG601", "m");

        Assert.Equal((1, 1), (finding.Line, finding.Column));
    }

    [Fact]
    public void ForCommit_NamesTheCommitAtTheFirstPlace()
    {
        Finding finding = Finding.ForCommit("0123abc", FindingSeverity.Error, "ENG701", "no Signed-off-by trailer");

        Assert.Equal("commit/0123abc(1,1): error ENG701: no Signed-off-by trailer", finding.ToString());
        Assert.Matches(Finding.Pattern, finding.ToString());
    }

    [Fact]
    public void ForTag_NamesTheTagAtTheFirstPlace()
    {
        Finding finding = Finding.ForTag("v1.0.0", FindingSeverity.Warning, "ENG200", "tag message");

        Assert.Equal("tag/v1.0.0(1,1): warning ENG200: tag message", finding.ToString());
        Assert.Matches(Finding.Pattern, finding.ToString());
    }

    [Theory]
    [InlineData("a.cs(1,1): error ENG601: message")]
    [InlineData("src/Lib/A.cs(12,345): warning ARCH008: message with: colon and (parentheses)")]
    [InlineData("commit/abc(1,1): error ENG701: m")]
    public static void Pattern_FindingLine_Matches(string line)
    {
        Assert.Matches(Finding.Pattern, line);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.cs(1,1): error ENG601:")]
    [InlineData("a.cs(1,1): error ENG601: ")]
    [InlineData("a.cs(1,1): error ENG60: message")]
    [InlineData("a.cs(1,1): error ENG6010: message")]
    [InlineData("a.cs(1,1): error CS0103: message")]
    [InlineData("a.cs(1,1): info ENG601: message")]
    [InlineData("a.cs(1): error ENG601: message")]
    [InlineData("(1,1): error ENG601: message")]
    [InlineData("a.cs(a,1): error ENG601: message")]
    [InlineData("== test")]
    public static void Pattern_OtherLine_DoesNotMatch(string line)
    {
        Assert.DoesNotMatch(Finding.Pattern, line);
    }

    [Theory]
    [InlineData("src/Lib/A.cs", 7, 32, FindingSeverity.Error, "ARCH001", "cycle: A -> B")]
    [InlineData("a b.cs", 1, 1, FindingSeverity.Warning, "ENG102", "message (with) parentheses: and colons")]
    [InlineData("tag/v1.0.0", 1, 1, FindingSeverity.Error, "ENG200", "m")]
    public static void TryParse_FormattedFinding_RoundTrips(string path, int line, int column, FindingSeverity severity, string code, string message)
    {
        Finding expected = new(path, line, column, severity, code, message);

        bool parsed = Finding.TryParse(expected.ToString(), out Finding actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("== test")]
    [InlineData("test: passed")]
    [InlineData("a.cs(0,1): error ENG601: message")]
    [InlineData("a.cs(1,0): error ENG601: message")]
    [InlineData("a.cs(99999999999,1): error ENG601: message")]
    [InlineData("a.cs(1,1): error ENG601: message\n")]
    [InlineData("a.cs(1,1): error ENG601: first\nsecond")]
    public static void TryParse_OtherLine_ReturnsFalse(string line)
    {
        bool parsed = Finding.TryParse(line, out Finding finding);

        Assert.False(parsed);
        Assert.Equal(default, finding);
    }
}
