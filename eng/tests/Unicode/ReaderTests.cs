using System;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Unicode;
using Xunit;
using GeneratorProgram = Great.Parts.Cel.Eng.Unicode.Program;

namespace Great.Parts.Cel.Eng.Tests.Unicode;

// The forms of the three files of the Unicode Character Database that the generator reads, and of inputs.txt
// (docs/design/unicode-tables.md, "The data"). The excerpts have the form of the real files.
public sealed class ReaderTests
{
    [Fact]
    public static void UnicodeData_GivesTheRangesOfEachCategoryAndJoinsTheTwoLinesOfARange()
    {
        UnicodeDataTable table = UcdReaders.ReadUnicodeData(Excerpts.UnicodeData);

        Assert.Equal("41-42 4B-4B 399-399 1E9E-1E9E 212A-212A", Excerpts.Show(table.Categories["Lu"]));
        Assert.Equal("61-61 6B-6B B5-B5 DF-DF 3B9-3B9 1FBE-1FBE", Excerpts.Show(table.Categories["Ll"]));
        Assert.Equal("3400-4DBF", Excerpts.Show(table.Categories["Lo"]));
        Assert.Equal("D800-DB7F", Excerpts.Show(table.Categories["Cs"]));
        Assert.Equal("345-345", Excerpts.Show(table.Categories["Mn"]));
        Assert.DoesNotContain("Cn", table.Categories.Keys);
        Assert.True(table.Listed.Contains(0x3400));
        Assert.True(table.Listed.Contains(0x4DBF));
        Assert.True(table.Listed.Contains(0x4000));
        Assert.False(table.Listed.Contains(0x4DC0));
        Assert.False(table.Listed.Contains(0x43));
    }

    [Theory]
    [InlineData("0041;LATIN CAPITAL LETTER A;Xx;0;L", "UnicodeData.txt(1): 'Xx' is not a general category")]
    [InlineData("0041;A", "UnicodeData.txt(1): a line has fewer than 3 fields")]
    [InlineData("41;A;Lu", "UnicodeData.txt(1): '41' is not a code point of 4 to 6 hexadecimal digits up to 10FFFF")]
    [InlineData("110000;A;Lu", "UnicodeData.txt(1): '110000' is not a code point of 4 to 6 hexadecimal digits up to 10FFFF")]
    [InlineData("0042;B;Lu\n0041;A;Lu", "UnicodeData.txt(2): the code point 0041 does not come after 0042")]
    [InlineData("3400;<X, First>;Lo", "UnicodeData.txt: the last range is not closed")]
    [InlineData("3400;<X, First>;Lo\n3401;A;Lo", "UnicodeData.txt(2): a range that starts is not closed by the next line")]
    [InlineData("3400;<X, First>;Lo\n3500;<Y, First>;Lo", "UnicodeData.txt(2): a range that starts is not closed before the next one starts")]
    [InlineData("3400;<X, Last>;Lo", "UnicodeData.txt(1): a range that ends has no matching line that starts it")]
    [InlineData("3400;<X, First>;Lo\n4DBF;<Y, Last>;Lo", "UnicodeData.txt(2): a range that ends has no matching line that starts it")]
    public static void UnicodeDataOfAnotherForm_IsRefusedWithTheFileAndTheLine(string text, string message)
    {
        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => UcdReaders.ReadUnicodeData(text));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public static void Scripts_GivesTheRangesOfEachScriptBySkippingCommentsAndKeepingTheUnderscore()
    {
        var scripts = UcdReaders.ReadScripts(Excerpts.Scripts);

        Assert.Equal(["Common", "Greek", "Inherited", "Latin", "Old_Italic"], scripts.Keys);
        Assert.Equal("41-5A 61-7A DF-DF 1E9E-1E9E 212A-212A", Excerpts.Show(scripts["Latin"]));
        Assert.Equal("399-399 3B9-3B9 1FBE-1FBE", Excerpts.Show(scripts["Greek"]));
        Assert.Equal("10300-1031F", Excerpts.Show(scripts["Old_Italic"]));
        Assert.Equal("B5-B5", Excerpts.Show(scripts["Common"]));
    }

    [Theory]
    [InlineData("0041..005A", "Scripts.txt(1): a line has not 2 fields")]
    [InlineData("0041..005A ; ", "Scripts.txt(1): the script has no name")]
    [InlineData("005A..0041 ; Latin", "Scripts.txt(1): the range 005A..0041 goes back")]
    [InlineData("004G ; Latin", "Scripts.txt(1): '004G' is not a code point of 4 to 6 hexadecimal digits up to 10FFFF")]
    [InlineData("0041 ; Latin\n0042 ; Latin ; x", "Scripts.txt(2): a line has not 2 fields")]
    public static void ScriptsOfAnotherForm_IsRefusedWithTheFileAndTheLine(string text, string message)
    {
        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => UcdReaders.ReadScripts(text));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public static void CaseFolding_ReadsTheLinesOfTheStatusesCAndSAndSkipsFAndT()
    {
        var folds = UcdReaders.ReadCaseFolding(Excerpts.CaseFolding);

        Assert.Equal(
            [(0x41, 0x61), (0x4B, 0x6B), (0xB5, 0x3BC), (0x345, 0x3B9), (0x399, 0x3B9), (0x1E9E, 0xDF), (0x1FBE, 0x3B9), (0x212A, 0x6B)],
            folds);
        Assert.DoesNotContain(folds, fold => fold.Code == 0x130);
    }

    [Theory]
    [InlineData("0041; X; 0061;", "CaseFolding.txt(1): 'X' is not a status of the file")]
    [InlineData("0041; C", "CaseFolding.txt(1): a line has fewer than 3 fields")]
    [InlineData("0041; C; 0061 0062;", "CaseFolding.txt(1): a line of status C has more than one code point")]
    [InlineData("0041; C; 0061;\n0041; S; 0061;", "CaseFolding.txt(2): the code point 0041 has two lines of the statuses C and S")]
    [InlineData("0041; C; 006;", "CaseFolding.txt(1): '006' is not a code point of 4 to 6 hexadecimal digits up to 10FFFF")]
    public static void CaseFoldingOfAnotherForm_IsRefusedWithTheFileAndTheLine(string text, string message)
    {
        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => UcdReaders.ReadCaseFolding(text));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public static void CaseFoldingLinesOfStatusFAndTWithManyCodePoints_AreNotRead()
    {
        var folds = UcdReaders.ReadCaseFolding("00DF; F; 0073 0073; # LATIN SMALL LETTER SHARP S\n0049; T; 0131; # LATIN CAPITAL LETTER I\n");

        Assert.Empty(folds);
    }

    [Fact]
    public static void Inputs_ReadsTheVersionAndTheThreeFilesInTheOrderOfTheGenerator()
    {
        UcdInputs inputs = UcdInputs.Parse(Excerpts.InputsText());

        Assert.Equal("17.0.0", inputs.Version);
        Assert.Equal(Excerpts.FileNames, inputs.Files.Select(file => file.Name));
        Assert.All(inputs.Files, file => Assert.Equal(64, file.Sha256.Length));
        Assert.Equal(new Uri("https://www.unicode.org/Public/17.0.0/ucd/Scripts.txt"), inputs.Files[1].Address);
    }

    [Fact]
    public void InputsOfTheRepository_NameTheThreeFilesOfVersion17()
    {
        string root = Support.RepositoryRoot.Find();

        UcdInputs inputs = UcdInputs.Parse(File.ReadAllText(Path.Combine(root, GeneratorProgram.InputsPath)));

        Assert.Equal("17.0.0", inputs.Version);
        Assert.Equal(Excerpts.FileNames, inputs.Files.Select(file => file.Name));
        Assert.All(inputs.Files, file => Assert.Equal($"https://www.unicode.org/Public/17.0.0/ucd/{file.Name}", file.Address.AbsoluteUri));
    }

    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("", "inputs.txt: there is no 'version' line")]
    [InlineData("version 17.0.0\n", "inputs.txt: there is no 'file' line for UnicodeData.txt")]
    [InlineData("version 17.0.0\nversion 16.0.0\n", "inputs.txt(2): expected 'version <version>' once or 'file <name> <sha256> <address>'")]
    [InlineData("version 17.0.0\nfile UnicodeData.txt abc https://x.example/a\n", "inputs.txt(2): the SHA-256 of UnicodeData.txt is not 64 lower case hexadecimal digits")]
    [InlineData("version 17.0.0\nfile UnicodeData.txt 0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF https://x.example/a\n", "inputs.txt(2): the SHA-256 of UnicodeData.txt is not 64 lower case hexadecimal digits")]
    [InlineData("version 17.0.0\nfile UnicodeData.txt " + Sha + " http://x.example/a\n", "inputs.txt(2): the address of UnicodeData.txt is not an absolute https address")]
    [InlineData("version 17.0.0\nfile UnicodeData.txt " + Sha + " https://x.example/a\nfile UnicodeData.txt " + Sha + " https://x.example/b\n", "inputs.txt(3): the file UnicodeData.txt is listed twice")]
    [InlineData("version 17.0.0\nsomething else\n", "inputs.txt(2): expected 'version <version>' once or 'file <name> <sha256> <address>'")]
    public static void InputsOfAnotherForm_AreRefusedWithTheFileAndTheLine(string text, string message)
    {
        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => UcdInputs.Parse(text));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public static void InputsWithAFileOtherThanTheThree_AreRefused()
    {
        string text = Excerpts.InputsText() + "file Extra.txt " + Sha + " https://x.example/e\n";

        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => UcdInputs.Parse(text));

        Assert.Equal("inputs.txt: a file other than UnicodeData.txt, Scripts.txt, CaseFolding.txt is listed", exception.Message);
    }
}
