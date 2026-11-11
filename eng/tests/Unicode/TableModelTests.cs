using System;
using System.Linq;
using Great.Parts.Cel.Eng.Unicode;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Unicode;

// The tables that the generator makes of the excerpts (docs/design/unicode-tables.md, "What is generated"): the categories, their
// unions and the unassigned, the scripts, the orbits, the fold tables and the names.
public sealed class TableModelTests
{
    private static readonly TableModel Model = Excerpts.Model();

    private static readonly string[] FiveCodePoints = ["0041", "0042", "0043", "0044", "0045"];

    private static readonly string[] CategoryKeys =
    [
        "C", "Cc", "Cf", "Cn", "Co", "Cs", "L", "LC", "Ll", "Lm", "Lo", "Lt", "Lu", "M", "Mc", "Me", "Mn", "N", "Nd", "Nl", "No",
        "P", "Pc", "Pd", "Pe", "Pf", "Pi", "Po", "Ps", "S", "Sc", "Sk", "Sm", "So", "Z", "Zl", "Zp", "Zs",
    ];

    [Fact]
    public static void Categories_AreThe30TwoLetterOnesTheirSevenUnionsAndTheCasedLetters()
    {
        string[] categories = [.. Model.Tables.Select(table => table.Name).Where(name => !name.Contains('(', StringComparison.Ordinal) && name.Length <= 2)];

        Assert.Equal(38, categories.Length);
        Assert.Equal(CategoryKeys, categories);
    }

    [Fact]
    public static void Categories_GiveTheRangesOfTheExcerpt()
    {
        Assert.Equal("41-42 4B-4B 399-399 1E9E-1E9E 212A-212A", Excerpts.Table(Model, "Lu"));
        Assert.Equal("41-42 4B-4B 61-61 6B-6B B5-B5 DF-DF 399-399 3B9-3B9 1E9E-1E9E 1FBE-1FBE 212A-212A 3400-4DBF", Excerpts.Table(Model, "L"));
        Assert.Equal("41-42 4B-4B 61-61 6B-6B B5-B5 DF-DF 399-399 3B9-3B9 1E9E-1E9E 1FBE-1FBE 212A-212A", Excerpts.Table(Model, "LC"));
        Assert.Equal("345-345", Excerpts.Table(Model, "M"));
        Assert.Equal("", Excerpts.Table(Model, "Nd"));
        Assert.Equal("D800-DB7F", Excerpts.Table(Model, "Cs"));
    }

    [Fact]
    public static void TheUnassigned_AreEveryCodePointThatTheFileDoesNotList_AndCIsTheOthersWithIt()
    {
        string unassigned = Excerpts.Table(Model, "Cn");

        Assert.Equal(
            "0-40 43-4A 4C-60 62-6A 6C-B4 B6-DE E0-344 346-398 39A-3B8 3BA-1E9D 1E9F-1FBD 1FBF-2129 212B-33FF 4DC0-D7FF DB80-10FFFF",
            unassigned);
        Assert.Equal(
            "0-40 43-4A 4C-60 62-6A 6C-B4 B6-DE E0-344 346-398 39A-3B8 3BA-1E9D 1E9F-1FBD 1FBF-2129 212B-33FF 4DC0-10FFFF",
            Excerpts.Table(Model, "C"));
    }

    [Fact]
    public static void Scripts_KeepTheirNamesFromTheFile()
    {
        string[] scripts = [.. Model.Tables.Select(table => table.Name).Where(name => name.Length > 2 && !name.Contains('(', StringComparison.Ordinal))];

        Assert.Equal(["Common", "Greek", "Inherited", "Latin", "Old_Italic"], scripts);
        Assert.Equal("10300-1031F", Excerpts.Table(Model, "Old_Italic"));
        Assert.Equal("41-5A 61-7A DF-DF 1E9E-1E9E 212A-212A", Excerpts.Table(Model, "Latin"));
    }

    [Fact]
    public static void Orbits_JoinTheCodePointsOfOneFoldingAndTakeTheNextInAscendingOrderWrappingAround()
    {
        int[] keys = [0x41, 0x4B, 0x61, 0x6B, 0xB5, 0xDF, 0x345, 0x399, 0x3B9, 0x3BC, 0x1E9E, 0x1FBE, 0x212A];
        int[] next = [0x61, 0x6B, 0x41, 0x212A, 0x3BC, 0x1E9E, 0x399, 0x3B9, 0x1FBE, 0xB5, 0xDF, 0x345, 0x4B];

        Assert.Equal(keys, Model.OrbitKeys);
        Assert.Equal(next, Model.OrbitNext);
    }

    [Fact]
    public static void OrbitsOfSixCodePointsAndMore_AreOneCycle()
    {
        string folding = string.Join('\n', FiveCodePoints.Select(code => code + "; C; 0061;"));
        TableModel model = TableModel.Build(Excerpts.Inputs(), Excerpts.UnicodeData, Excerpts.Scripts, folding);

        Assert.Equal([0x41, 0x42, 0x43, 0x44, 0x45, 0x61], model.OrbitKeys);
        Assert.Equal([0x42, 0x43, 0x44, 0x45, 0x61, 0x41], model.OrbitNext);
    }

    [Theory]
    [InlineData("Mn", "399-399 3B9-3B9 1FBE-1FBE")]
    [InlineData("M", "399-399 3B9-3B9 1FBE-1FBE")]
    [InlineData("Lu", "61-61 6B-6B DF-DF 345-345 3B9-3B9 1FBE-1FBE")]
    [InlineData("Greek", "345-345")]
    [InlineData("Inherited", "399-399 3B9-3B9 1FBE-1FBE")]
    [InlineData("Common", "3BC-3BC")]
    [InlineData("Cn", "B5-B5")]
    [InlineData("C", "B5-B5")]
    public static void FoldTable_IsTheCodePointsOutsideTheTableWhoseOrbitHasAMemberInIt(string table, string expected)
    {
        Assert.Equal(expected, Excerpts.Table(Model, table + " (fold)"));
    }

    [Theory]
    [InlineData("Latin")]
    [InlineData("Nd")]
    [InlineData("Old_Italic")]
    [InlineData("LC")]
    public static void ATableClosedUnderFolding_HasNoFoldTable_AndNeitherHasTheCasedLetters(string table)
    {
        Assert.DoesNotContain(Model.Tables, definition => definition.Name == table + " (fold)");
    }

    [Fact]
    public static void FoldTables_FollowThePrimaryTablesInTheirOrder()
    {
        string[] names = [.. Model.Tables.Select(table => table.Name)];
        int firstFold = names.ToList().FindIndex(name => name.EndsWith("(fold)", StringComparison.Ordinal));

        Assert.True(firstFold > 0);
        Assert.All(names[..firstFold], name => Assert.DoesNotContain("(fold)", name, StringComparison.Ordinal));
        string[] folds = names[firstFold..];
        Assert.All(folds, name => Assert.EndsWith(" (fold)", name, StringComparison.Ordinal));
        Assert.Equal(folds.Order(StringComparer.Ordinal), folds);
    }

    [Fact]
    public static void Names_AreTheCanonicalNamesOfGoWithTheirTables()
    {
        TableNameEntry Entry(string name) => Model.Names.Single(entry => entry.Name == name);
        int Number(string name) => Model.NumberOf(name);

        Assert.Equal(new TableNameEntry("Lu", Number("Lu"), Number("Lu (fold)"), false), Entry("Lu"));
        Assert.Equal(new TableNameEntry("Letter", Number("L"), Number("L (fold)"), false), Entry("Letter"));
        Assert.Equal(new TableNameEntry("Olditalic", Number("Old_Italic"), -1, false), Entry("Olditalic"));
        Assert.Equal(new TableNameEntry("Greek", Number("Greek"), Number("Greek (fold)"), false), Entry("Greek"));
        Assert.Equal(new TableNameEntry("Lc", Number("LC"), -1, false), Entry("Lc"));
        Assert.Equal(new TableNameEntry("Casedletter", Number("LC"), -1, false), Entry("Casedletter"));
        Assert.Equal(new TableNameEntry("Unassigned", Number("Cn"), Number("Cn (fold)"), false), Entry("Unassigned"));
        Assert.Equal(new TableNameEntry("Assigned", Number("Cn"), Number("Cn"), true), Entry("Assigned"));
        Assert.Equal(new TableNameEntry("Digit", Number("Nd"), -1, false), Entry("Digit"));
        Assert.Equal(new TableNameEntry("Cntrl", Number("Cc"), -1, false), Entry("Cntrl"));
    }

    [Fact]
    public static void Names_HaveNoAnyNoAsciiAndNoNonCanonicalForm()
    {
        string[] names = [.. Model.Names.Select(entry => entry.Name)];

        Assert.DoesNotContain("Any", names);
        Assert.DoesNotContain("Ascii", names);
        Assert.DoesNotContain("LC", names);
        Assert.DoesNotContain("Old_Italic", names);
        Assert.All(names, name => Assert.Equal(GoNames.Canonical(name), name));
        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Theory]
    [InlineData("Old_Italic", "Olditalic")]
    [InlineData("SignWriting", "Signwriting")]
    [InlineData("Zanabazar_Square", "Zanabazarsquare")]
    [InlineData("LC", "Lc")]
    [InlineData("Lu", "Lu")]
    [InlineData("L", "L")]
    [InlineData("cntrl", "Cntrl")]
    [InlineData("decimal number", "Decimalnumber")]
    [InlineData("Letter-Number", "Letternumber")]
    [InlineData("_a-B z", "Abz")]
    [InlineData("", "")]
    public static void CanonicalName_UpperCasesTheFirstLetterLowerCasesTheRestAndDropsUnderscoresSpacesAndHyphens(string name, string canonical)
    {
        Assert.Equal(canonical, GoNames.Canonical(name));
    }

    [Fact]
    public static void CategoryAliases_AreTheFortyTwoOfGo()
    {
        Assert.Equal(42, GoNames.CategoryAliases.Length);
        Assert.Equal(GoNames.CategoryAliases.Select(alias => alias.Alias).Distinct().Count(), GoNames.CategoryAliases.Length);
    }

    [Fact]
    public static void TwoScriptsWithOneCanonicalName_AreRefused()
    {
        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => GoNames.Entries(CategoryKeys, ["Old_Italic", "OldItalic"]));

        Assert.Equal("GoNames: the canonical name Olditalic stands for Old_Italic and for OldItalic", exception.Message);
    }

    [Fact]
    public static void AnAliasOfACategoryThatIsNotThere_IsRefused()
    {
        UcdFormatException exception = Assert.Throws<UcdFormatException>(() => GoNames.Entries(["Lu"], []));

        Assert.Equal("GoNames: the alias Cased_Letter names the category LC, which is not a category", exception.Message);
    }
}
