using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks suppressions` on the forms of the configuration files: .editorconfig and .globalconfig, MSBuild files and
// lychee.toml (docs/design/eng-checks-source.md, "suppressions"). Every test writes the files of its example into a temporary
// repository and checks the code, the path, the line, the column and the message of each finding.
public sealed class SuppressionsConfigTests
{
    private const string CommentAbove = "has no comment line above";
    private const string RepeatsIdAbove = "has a comment line above that only repeats the rule ID";
    private const string XmlAbove = "has no reason comment in the line above";
    private const string SameLine = "has no comment in the same line";

    // ---- .editorconfig and .globalconfig: the level of a rule ----

    [Theory]
    [InlineData("none")]
    [InlineData("silent")]
    [InlineData("suggestion")]
    [InlineData("NONE")]
    public static void RuleLevel_Lowered_WithoutComment_IsEng101(string level)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", $"dotnet_diagnostic.CA1065.severity = {level}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 2, 1, $"'dotnet_diagnostic.CA1065.severity = {level}' {CommentAbove}"));
        Assert.Empty(Scan(root).Places);
    }

    [Theory]
    [InlineData("# CA1065")]
    [InlineData("; CA1065.")]
    [InlineData("# ca1065: ")]
    public static void RuleLevel_CommentThatOnlyRepeatsTheCode_IsEng102(string comment)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", comment, "dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng102, ".editorconfig", 3, 1, $"'dotnet_diagnostic.CA1065.severity = none' {RepeatsIdAbove}"));
    }

    [Theory]
    [InlineData("#")]
    [InlineData("; --")]
    public static void RuleLevel_CommentWithoutText_IsEng101(string comment)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text(comment, "dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 2, 1, $"'dotnet_diagnostic.CA1065.severity = none' {CommentAbove}"));
    }

    [Theory]
    [InlineData("# The skeleton throws NotImplementedException")]
    [InlineData("; The skeleton throws NotImplementedException")]
    [InlineData("   # CA1065: the skeleton throws NotImplementedException")]
    public static void RuleLevel_WithComment_IsCountedUnderTheId(string comment)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", comment, "dotnet_diagnostic.CA1065.severity = none"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("CA1065", ".editorconfig", new Position(3, 1))], scan.Places);
    }

    [Fact]
    public void RuleLevel_CommentLinesDirectlyAbove_AllCountAsTheReason()
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", "# CA1065", "# The skeleton throws NotImplementedException", "dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.None(Run(root));
        Assert.Equal("CA1065=1", Counts(Scan(root)));
    }

    [Fact]
    public void RuleLevel_BlankLineBetweenTheCommentAndTheEntry_IsNoReason()
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("# The skeleton throws NotImplementedException", string.Empty, "dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 3, 1, $"'dotnet_diagnostic.CA1065.severity = none' {CommentAbove}"));
    }

    [Fact]
    public void RuleLevel_CommentAboveTheSectionHeader_IsNoReason()
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("# The skeleton throws NotImplementedException", "[*.cs]", "dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 3, 1, $"'dotnet_diagnostic.CA1065.severity = none' {CommentAbove}"));
    }

    [Fact]
    public void RuleLevel_CommentOfAnEntryDoesNotCoverTheNextEntry()
    {
        using TempDirectory root = new();
        root.Write(
            ".editorconfig",
            Text(
                "# The guard is in the caller",
                "dotnet_diagnostic.CA1062.severity = none",
                "dotnet_diagnostic.CA1305.severity = suggestion"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 3, 1, $"'dotnet_diagnostic.CA1305.severity = suggestion' {CommentAbove}"));
        Assert.Equal("CA1062=1", Counts(Scan(root)));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("warning")]
    [InlineData("default")]
    public static void RuleLevel_NotLowered_IsNotAForm(string level)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", $"dotnet_diagnostic.CA1062.severity = {level}", $"dotnet_analyzer_diagnostic.severity = {level}"));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void RuleLevel_ColumnIsTheFirstCharacterOfTheKey()
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", "  \tdotnet_diagnostic.CA1065.severity=none"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 2, 4, $"'dotnet_diagnostic.CA1065.severity = none' {CommentAbove}"));
    }

    [Fact]
    public void RuleLevel_KeyAndValueAreReadIgnoringCase()
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", "DOTNET_DIAGNOSTIC.ca1065.SEVERITY : Suggestion"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 2, 1, $"'DOTNET_DIAGNOSTIC.ca1065.SEVERITY = Suggestion' {CommentAbove}"));
    }

    [Theory]
    [InlineData(".editorconfig")]
    [InlineData(".globalconfig")]
    [InlineData("src/.editorconfig")]
    [InlineData("eng/analyzers.globalconfig")]
    public static void RuleLevel_IsReadInEditorConfigAndGlobalConfigFiles(string path)
    {
        using TempDirectory root = new();
        root.Write(path, Text("dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, path, 1, 1, $"'dotnet_diagnostic.CA1065.severity = none' {CommentAbove}"));
    }

    [Fact]
    public void RuleLevel_InOtherFiles_IsNotAForm()
    {
        using TempDirectory root = new();
        root.Write("docs/config.txt", Text("dotnet_diagnostic.CA1065.severity = none"));
        root.Write("src/obj/.editorconfig", Text("dotnet_diagnostic.CA1065.severity = none"));

        FindingAssert.None(Run(root));
    }

    // ---- .editorconfig and .globalconfig: the level of the analyzers ----

    [Theory]
    [InlineData("dotnet_analyzer_diagnostic.severity = none")]
    [InlineData("dotnet_analyzer_diagnostic.severity = silent")]
    [InlineData("dotnet_analyzer_diagnostic.category-Style.severity = suggestion")]
    [InlineData("dotnet_analyzer_diagnostic.category-Security.severity = none")]
    public static void AnalyzerLevel_Lowered_WithoutComment_IsEng101(string entry)
    {
        using TempDirectory root = new();
        root.Write(".globalconfig", Text("is_global = true", entry));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".globalconfig", 2, 1, $"'{entry}' {CommentAbove}"));
    }

    [Fact]
    public void AnalyzerLevel_WithComment_IsCountedUnderAnalysisMode()
    {
        using TempDirectory root = new();
        root.Write(
            ".editorconfig",
            Text(
                "# Style rules are advice for the examples",
                "dotnet_analyzer_diagnostic.category-Style.severity = suggestion",
                "; Every rule is off in the generated sources",
                "dotnet_analyzer_diagnostic.severity = none"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("AnalysisMode=2", Counts(scan));
    }

    [Theory]
    [InlineData("dotnet_analyzer_diagnostic.severity = error")]
    [InlineData("dotnet_analyzer_diagnostic.category-Style.severity = warning")]
    [InlineData("dotnet_analyzer_diagnostic.category-.severity = none")]
    [InlineData("dotnet_analyzer_diagnostic.category-Style = none")]
    public static void AnalyzerLevel_NotLoweredOrNotComplete_IsNotAForm(string entry)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text(entry));

        FindingAssert.None(Run(root));
    }

    // ---- .editorconfig and .globalconfig: generated_code ----

    [Theory]
    [InlineData("generated_code = true")]
    [InlineData("generated_code = TRUE")]
    public static void GeneratedCode_True_WithoutComment_IsEng101(string entry)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[src/Great.Parts.Cel/Library/Unicode/**.cs]", entry));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".editorconfig", 2, 1, $"'{entry}' {CommentAbove}"));
    }

    [Fact]
    public void GeneratedCode_True_WithComment_IsCountedUnderGenerated()
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", "[src/Tables/**.cs]", "# The tables are written by a script", "generated_code = true"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("generated", ".editorconfig", new Position(4, 1))], scan.Places);
    }

    [Theory]
    [InlineData("generated_code = false")]
    [InlineData("generated_code = False")]
    [InlineData("generated_code = auto")]
    public static void GeneratedCode_NotTrue_IsNotAForm(string entry)
    {
        using TempDirectory root = new();
        root.Write(".editorconfig", Text("[*.cs]", entry));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    // ---- MSBuild: NoWarn and WarningsNotAsErrors ----

    [Theory]
    [InlineData("a.csproj")]
    [InlineData("Directory.Build.props")]
    [InlineData("eng/build.targets")]
    public static void NoWarn_WithoutComment_IsEng101InEveryKindOfFile(string path)
    {
        using TempDirectory root = new();
        root.Write(path, Project("<NoWarn>$(NoWarn);CS1591</NoWarn>"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, path, 3, 5, $"'<NoWarn>$(NoWarn);CS1591</NoWarn>' {XmlAbove}"));
        Assert.Empty(Scan(root).Places);
    }

    [Theory]
    [InlineData("<!-- CS1591 -->")]
    [InlineData("<!-- 1591. -->")]
    [InlineData("<!-- CS1591, CA1707 -->")]
    public static void NoWarn_CommentThatOnlyRepeatsTheIds_IsEng102(string comment)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project(comment, "<NoWarn>CS1591;CA1707</NoWarn>"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng102, "a.csproj", 4, 5, "'<NoWarn>CS1591;CA1707</NoWarn>' has a reason comment in the line above that only repeats the rule ID"));
    }

    [Fact]
    public void NoWarn_WithComment_IsCountedEachIdOnce()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project("<!-- Tests have no XML documentation -->", "<NoWarn>$(NoWarn);CS1591;1573 ; CA1707,CS1591</NoWarn>"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("CA1707=1, CS1573=1, CS1591=1", Counts(scan));
        Assert.All(scan.Places, place => Assert.Equal(new Position(4, 5), place.Position));
    }

    [Theory]
    [InlineData("<NoWarn>$(NoWarn)</NoWarn>")]
    [InlineData("<NoWarn />")]
    [InlineData("<NoWarn>  </NoWarn>")]
    [InlineData("<NoWarn>$(OtherWarnings);@(Ids)</NoWarn>")]
    [InlineData("<WarningsNotAsErrors>$(WarningsNotAsErrors)</WarningsNotAsErrors>")]
    public static void NoWarn_WithoutAnId_IsNotAForm(string element)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project(element));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void WarningsNotAsErrors_WithoutComment_IsEng101AndWithCommentCountedPerId()
    {
        using TempDirectory root = new();
        root.Write(
            "a.csproj",
            Project(
                "<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901</WarningsNotAsErrors>",
                "<!-- The advisory feed is not ours to fix -->",
                "<WarningsNotAsErrors>NU1902;NU1903</WarningsNotAsErrors>"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901</WarningsNotAsErrors>' {XmlAbove}"));
        Assert.Equal("NU1902=1, NU1903=1", Counts(scan));
    }

    [Fact]
    public void WarningsAsErrors_IsNotAForm()
    {
        using TempDirectory root = new();
        root.Write("Directory.Build.props", Project("<WarningsAsErrors>$(WarningsAsErrors);CA1509</WarningsAsErrors>"));

        FindingAssert.None(Run(root));
    }

    // ---- MSBuild: where the reason is ----

    [Fact]
    public void Reason_MultiLineCommentThatEndsInTheLineAbove_Counts()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", "<Project>\n  <PropertyGroup>\n    <!--\n      Tests have no XML documentation.\n    -->\n    <NoWarn>CS1591</NoWarn>\n  </PropertyGroup>\n</Project>\n");

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("CS1591", "a.csproj", new Position(6, 5))], scan.Places);
    }

    [Fact]
    public void Reason_SeveralCommentsDirectlyAbove_AllCount()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project("<!-- CS1591 -->", "<!-- Tests have no XML documentation -->", "<NoWarn>CS1591</NoWarn>"));

        FindingAssert.None(Run(root));
    }

    [Theory]
    [InlineData("<!-- Tests have no XML documentation --><NoWarn>CS1591</NoWarn>")]
    [InlineData("<NoWarn>CS1591</NoWarn> <!-- Tests have no XML documentation -->")]
    public static void Reason_CommentInTheSameLine_IsNotInTheLineAbove(string line)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project(line));

        Assert.Single(Run(root));
    }

    [Fact]
    public void Reason_CommentSeparatedByABlankLineOrAnElement_IsNoReason()
    {
        using TempDirectory root = new();
        root.Write(
            "a.csproj",
            Project(
                "<!-- Tests have no XML documentation -->",
                "<Description>Tests</Description>",
                "<NoWarn>CS1591</NoWarn>"));
        root.Write("b.csproj", "<Project>\n  <PropertyGroup>\n    <!-- Tests have no XML documentation -->\n\n    <NoWarn>CS1591</NoWarn>\n  </PropertyGroup>\n</Project>\n");

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "a.csproj", 5, 5, $"'<NoWarn>CS1591</NoWarn>' {XmlAbove}"),
            Error(EngCodes.Eng101, "b.csproj", 5, 5, $"'<NoWarn>CS1591</NoWarn>' {XmlAbove}"));
    }

    [Fact]
    public void Reason_CommentOfAnElementDoesNotCoverTheNextElement()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project("<!-- Tests have no XML documentation -->", "<NoWarn>CS1591</NoWarn>", "<NoWarn>CA1707</NoWarn>"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "a.csproj", 5, 5, $"'<NoWarn>CA1707</NoWarn>' {XmlAbove}"));
    }

    // ---- MSBuild: the properties of the table ----

    [Theory]
    [InlineData("TreatWarningsAsErrors", "false")]
    [InlineData("TreatWarningsAsErrors", "FALSE")]
    [InlineData("Nullable", "disable")]
    [InlineData("Nullable", "annotations")]
    [InlineData("Nullable", "warnings")]
    [InlineData("Nullable", "$(NullableMode)")]
    [InlineData("Nullable", "")]
    [InlineData("AnalysisMode", "Default")]
    [InlineData("AnalysisMode", "Recommended")]
    [InlineData("AnalysisMode", "$(Mode)")]
    [InlineData("AnalysisModeSecurity", "None")]
    [InlineData("AnalysisLevel", "10.0")]
    [InlineData("AnalysisLevel", "latest-recommended")]
    [InlineData("AnalysisLevel", "10.0-all")]
    [InlineData("AnalysisLevel", "none")]
    [InlineData("AnalysisLevel", "$(Level)")]
    [InlineData("AnalysisLevelStyle", "9.0-all")]
    [InlineData("WarningLevel", "0")]
    [InlineData("WarningLevel", "9999")]
    [InlineData("WarningLevel", "")]
    [InlineData("WarningLevel", "$(AnalysisLevel)")]
    [InlineData("CodeAnalysisRuleSet", "strict.ruleset")]
    [InlineData("CodeAnalysisRuleSet", "")]
    public static void Property_WithAnUnsafeValue_WithoutComment_IsEng101(string name, string value)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project($"<{name}>{value}</{name}>"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<{name}>{value}</{name}>' {XmlAbove}"));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void Property_ValueIsTrimmedAndShownWithoutTheWhiteSpaceAround()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", "<Project>\n  <PropertyGroup>\n    <TreatWarningsAsErrors>\n      false\n    </TreatWarningsAsErrors>\n  </PropertyGroup>\n</Project>\n");

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<TreatWarningsAsErrors>false</TreatWarningsAsErrors>' {XmlAbove}"));
    }

    [Theory]
    [InlineData("TreatWarningsAsErrors", "true")]
    [InlineData("TreatWarningsAsErrors", "$(Strict)")]
    [InlineData("TreatWarningsAsErrors", "")]
    [InlineData("Nullable", "enable")]
    [InlineData("Nullable", " ENABLE ")]
    [InlineData("AnalysisMode", "All")]
    [InlineData("AnalysisMode", " all ")]
    [InlineData("AnalysisModeSecurity", "All")]
    [InlineData("AnalysisLevel", "latest")]
    [InlineData("AnalysisLevel", "latest-all")]
    [InlineData("AnalysisLevel", "preview")]
    [InlineData("AnalysisLevel", "PREVIEW-ALL")]
    [InlineData("AnalysisLevelSecurity", "latest-all")]
    [InlineData("LangVersion", "latest")]
    [InlineData("OutputType", "Exe")]
    public static void Property_WithASafeValueOrNotInTheTable_IsNotAForm(string name, string value)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project($"<{name}>{value}</{name}>"));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    [Theory]
    [InlineData("RunAnalyzers")]
    [InlineData("RunAnalyzersDuringBuild")]
    [InlineData("RunAnalyzersDuringLiveAnalysis")]
    [InlineData("EnableNETAnalyzers")]
    [InlineData("EnforceCodeStyleInBuild")]
    [InlineData("GenerateDocumentationFile")]
    [InlineData("IsAotCompatible")]
    [InlineData("EnableTrimAnalyzer")]
    [InlineData("EnableAotAnalyzer")]
    [InlineData("EnableSingleFileAnalyzer")]
    [InlineData("CodeAnalysisTreatWarningsAsErrors")]
    [InlineData("ILLinkTreatWarningsAsErrors")]
    [InlineData("IlcTreatWarningsAsErrors")]
    public static void PropertyThatShouldBeTrue_OtherThanTrue_IsAFormUnderItsName(string name)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project($"<{name}>false</{name}>", $"<{name}>$(Value)</{name}>", $"<{name}>true</{name}>", $"<{name}> TRUE </{name}>"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<{name}>false</{name}>' {XmlAbove}"),
            Error(EngCodes.Eng101, "a.csproj", 4, 5, $"'<{name}>$(Value)</{name}>' {XmlAbove}"));
    }

    [Theory]
    [InlineData("RunAnalyzers")]
    [InlineData("IsAotCompatible")]
    [InlineData("IlcTreatWarningsAsErrors")]
    public static void PropertyThatShouldBeTrue_WithComment_IsCountedUnderItsName(string name)
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project("<!-- The tool project is not packed -->", $"<{name}>false</{name}>"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace(name, "a.csproj", new Position(4, 5))], scan.Places);
    }

    [Theory]
    [InlineData("SuppressTrimAnalysisWarnings")]
    [InlineData("SuppressAotAnalysisWarnings")]
    public static void PropertyThatShouldBeFalse_OtherThanFalse_IsAFormUnderItsName(string name)
    {
        using TempDirectory root = new();
        root.Write(
            "a.csproj",
            Project($"<{name}>true</{name}>", $"<{name}>$(Value)</{name}>", $"<{name}>false</{name}>", "<!-- Known warnings of the sample -->", $"<{name}>TRUE</{name}>"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<{name}>true</{name}>' {XmlAbove}"),
            Error(EngCodes.Eng101, "a.csproj", 4, 5, $"'<{name}>$(Value)</{name}>' {XmlAbove}"));
        Assert.Equal([new SuppressionPlace(name, "a.csproj", new Position(7, 5))], scan.Places);
    }

    [Fact]
    public void Property_WithACategory_IsCountedWithoutTheCategory()
    {
        using TempDirectory root = new();
        root.Write(
            "a.csproj",
            Project(
                "<!-- Security rules are noise in the samples -->",
                "<AnalysisModeSecurity>Default</AnalysisModeSecurity>",
                "<!-- The samples pin the rule set of the old SDK -->",
                "<AnalysisLevelStyle>9.0-all</AnalysisLevelStyle>",
                "<!-- The samples are not compiled strictly -->",
                "<AnalysisMode>Recommended</AnalysisMode>"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("AnalysisLevel=1, AnalysisMode=2", Counts(scan));
    }

    [Fact]
    public void Property_InAnyGroup_WithOrWithoutCondition_IsAForm()
    {
        using TempDirectory root = new();
        root.Write(
            "a.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
            + "  <PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\">\n"
            + "    <Nullable Condition=\"'$(X)' == ''\">disable</Nullable>\n"
            + "  </PropertyGroup>\n"
            + "  <Target Name=\"T\">\n"
            + "    <PropertyGroup>\n"
            + "      <TreatWarningsAsErrors>false</TreatWarningsAsErrors>\n"
            + "    </PropertyGroup>\n"
            + "  </Target>\n"
            + "</Project>\n");

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<Nullable>disable</Nullable>' {XmlAbove}"),
            Error(EngCodes.Eng101, "a.csproj", 7, 7, $"'<TreatWarningsAsErrors>false</TreatWarningsAsErrors>' {XmlAbove}"));
    }

    [Fact]
    public void Property_InAnOldStyleFileWithANamespace_IsAForm()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\n  <PropertyGroup>\n    <Nullable>disable</Nullable>\n  </PropertyGroup>\n</Project>\n");

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<Nullable>disable</Nullable>' {XmlAbove}"));
    }

    [Fact]
    public void Property_OutsideAPropertyGroup_IsNotAForm()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", "<Project>\n  <ItemGroup>\n    <Nullable>disable</Nullable>\n    <NoWarn>CS1591</NoWarn>\n  </ItemGroup>\n</Project>\n");

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void File_WithoutAPropertyOfTheTable_HasNoForm()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", Project("<TargetFramework>net10.0</TargetFramework>", "<OutputType>Exe</OutputType>"));
        root.Write("Directory.Build.targets", "<Project />\n");

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void File_ThatIsNotWellFormedXml_HasNoForm()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", "<Project>\n  <PropertyGroup>\n    <NoWarn>CS1591\n");
        root.Write("b.props", "<!DOCTYPE Project [<!ENTITY x \"y\">]>\n<Project><PropertyGroup><NoWarn>CS1591</NoWarn></PropertyGroup></Project>\n");
        root.Write("c.csproj", string.Empty);

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void Property_AfterAByteOrderMark_KeepsItsPosition()
    {
        using TempDirectory root = new();
        root.Write("a.csproj", "﻿" + Project("<Nullable>disable</Nullable>"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "a.csproj", 3, 5, $"'<Nullable>disable</Nullable>' {XmlAbove}"));
    }

    // ---- lychee.toml ----

    [Fact]
    public void Lychee_ExcludedPatterns_NeedACommentInTheLineOfEach()
    {
        using TempDirectory root = new();
        root.Write(
            "lychee.toml",
            Text(
                "exclude = [",
                "  '^https://example\\.com/', # The site refuses bots",
                "  '^https://other\\.example/',",
                "]"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "lychee.toml", 3, 3, $"'exclude '^https://other\\.example/'' {SameLine}"));
        Assert.Equal([new SuppressionPlace("lychee", "lychee.toml", new Position(2, 3))], scan.Places);
    }

    [Fact]
    public void Lychee_CommentAboveOrBelow_IsNotInTheSameLine()
    {
        using TempDirectory root = new();
        root.Write(
            "lychee.toml",
            Text(
                "# The site refuses bots",
                "exclude_path = [\"^docs/old/\"]",
                "# The site refuses bots"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "lychee.toml", 2, 17, $"'exclude_path \"^docs/old/\"' {SameLine}"));
    }

    [Fact]
    public void Lychee_AHashInsideAStringIsNotAComment()
    {
        using TempDirectory root = new();
        root.Write("lychee.toml", Text("exclude = ['^https://example\\.com/#top', \"a#b\"]"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "lychee.toml", 1, 12, $"'exclude '^https://example\\.com/#top'' {SameLine}"),
            Error(EngCodes.Eng101, "lychee.toml", 1, 42, $"'exclude \"a#b\"' {SameLine}"));
    }

    [Fact]
    public void Lychee_OneCommentInALineCoversEveryExclusionOfTheLine()
    {
        using TempDirectory root = new();
        root.Write("lychee.toml", Text("exclude = ['a', 'b'] # Both hosts are private"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("lychee=2", Counts(scan));
    }

    [Theory]
    [InlineData("exclude_all_private")]
    [InlineData("exclude_private")]
    [InlineData("exclude_link_local")]
    [InlineData("exclude_loopback")]
    public static void Lychee_FlagSetToTrue_IsAnExclusionAtItsKey(string flag)
    {
        using TempDirectory root = new();
        root.Write("lychee.toml", Text("verbose = \"info\"", $"{flag} = true"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "lychee.toml", 2, 1, $"'{flag} = true' {SameLine}"));
    }

    [Fact]
    public void Lychee_FlagWithComment_IsCounted()
    {
        using TempDirectory root = new();
        root.Write("lychee.toml", Text("exclude_loopback = true # Local servers are not part of the docs"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("lychee", "lychee.toml", new Position(1, 1))], scan.Places);
    }

    [Fact]
    public void Lychee_OtherSettings_AreNotExclusions()
    {
        using TempDirectory root = new();
        root.Write(
            "lychee.toml",
            Text(
                "exclude_loopback = false",
                "exclude_private = \"true\"",
                "include = ['^https://example\\.com/']",
                "include_fragments = \"full\"",
                "include_mail = true",
                "exclude_extra = ['a']",
                "[nested]",
                "exclude = [['inner'], { exclude = 'x' }]",
                "max_retries = 3"));

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void Lychee_FileOutsideTheRoot_IsNotRead()
    {
        using TempDirectory root = new();
        root.Write("docs/lychee.toml", Text("exclude = ['a']"));
        root.Write(".lychee.toml", Text("exclude = ['a']"));

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void Lychee_KeysInATableAndDottedKeys_AreFoundByTheirLastPart()
    {
        using TempDirectory root = new();
        root.Write("lychee.toml", Text("[checks]", "\"exclude\" = ['a']", "checks.exclude_path = ['b'] # Generated pages"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "lychee.toml", 2, 14, $"'exclude 'a'' {SameLine}"));
    }

    [Fact]
    public void Lychee_MultiLineStringAndEscapes_DoNotBreakTheReader()
    {
        using TempDirectory root = new();
        root.Write(
            "lychee.toml",
            Text(
                "header = \"\"\"",
                "exclude = ['not a setting'] # nor a comment",
                "\"\"\"",
                "exclude = [\"a\\\"b\", 'c'] # Escaped quote",
                "exclude_path = ['d'"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "lychee.toml", 5, 17, $"'exclude_path 'd'' {SameLine}"));
        Assert.Equal("lychee=2", Counts(scan));
    }

    // ---- All the scanners ----

    [Fact]
    public void Command_ReadsEveryKindOfFileAndCountsTheCommentedPlaces()
    {
        using TempDirectory root = new();
        root.Write("Great.Parts.Cel.slnx", "<Solution />");
        root.Write("src/A.cs", Text("#pragma warning disable CA1502 // Reason", "class A { }"));
        root.Write(".editorconfig", Text("[*.cs]", "# The skeleton", "dotnet_diagnostic.CA1065.severity = none", "dotnet_diagnostic.CA1720.severity = none"));
        root.Write("src/a.csproj", Project("<!-- Tests have no XML documentation -->", "<NoWarn>CS1591</NoWarn>", "<Nullable>disable</Nullable>"));
        root.Write("lychee.toml", Text("exclude = ['a'] # Reason", "exclude_loopback = true"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, ".editorconfig", 4, 1, $"'dotnet_diagnostic.CA1720.severity = none' {CommentAbove}"),
            Error(EngCodes.Eng101, "src/a.csproj", 5, 5, $"'<Nullable>disable</Nullable>' {XmlAbove}"),
            Error(EngCodes.Eng101, "lychee.toml", 2, 1, $"'exclude_loopback = true' {SameLine}"));
        Assert.Equal("CA1065=1, CA1502=1, CS1591=1, lychee=1", Counts(scan));
    }

    // ---- Helpers ----

    private static Finding Error(string code, string path, int line, int column, string message) =>
        FindingAssert.Error(code, path, line, column, message);

    private static List<Finding> Run(TempDirectory root) => [.. new SuppressionsCommand().Run(new CheckContext(root.Path, new Dictionary<string, string>()))];

    private static SuppressionScan Scan(TempDirectory root) => SuppressionScan.Of(new CheckContext(root.Path, new Dictionary<string, string>()));

    private static string Counts(SuppressionScan scan) =>
        string.Join(", ", scan.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    // A project file whose property group holds the lines, from line 3, with the column 5.
    private static string Project(params string[] lines) =>
        "<Project>\n  <PropertyGroup>\n" + string.Concat(lines.Select(line => "    " + line + "\n")) + "  </PropertyGroup>\n</Project>\n";
}
