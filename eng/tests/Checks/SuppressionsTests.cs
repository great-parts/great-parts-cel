using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks suppressions` on the forms of C# files and on generated code (docs/design/eng-checks-source.md, "suppressions").
// Every test writes the files of its example into a temporary repository and checks the code, the path, the line, the column
// and the message of each finding; FindingAssert also checks that every line has the common form.
public sealed class SuppressionsTests
{
    private const string Unicode = "src/Great.Parts.Cel/Library/Unicode/";
    private const string Snapshots = "tests/Great.Parts.Cel.Generator.Tests/Snapshots/";
    private const string NoReason = "has no reason comment";
    private const string RepeatsId = "that only repeats the rule ID";

    // ---- #pragma warning disable ----

    [Fact]
    public void Pragma_WithoutReason_IsEng101AtTheHash()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("class A", "{", "    #pragma warning disable CA1502", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 3, 5, $"'#pragma warning disable CA1502' {NoReason}"));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void Pragma_ReasonThatOnlyRepeatsTheCode_IsEng102()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502 // CA1502.", "class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng102, "src/A.cs", 1, 1, $"'#pragma warning disable CA1502' has a reason comment {RepeatsId}"));
        Assert.Empty(Scan(root).Places);
    }

    [Theory]
    [InlineData("#pragma warning disable CA1502")]
    [InlineData("#pragma warning disable CA1502 //")]
    [InlineData("#pragma warning disable CA1502 // - ")]
    [InlineData("#pragma warning disable CA1502 /* */")]
    [InlineData("#pragma warning disable CA1502 /* ... */")]
    public static void Pragma_NoTextOrOnlyPunctuation_IsEng101(string directive)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text(directive, "class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 1, 1, $"'#pragma warning disable CA1502' {NoReason}"));
    }

    [Theory]
    [InlineData("#pragma warning disable CA1502 // The planner is a table of cases")]
    [InlineData("#pragma warning disable CA1502 // CA1502: the planner is a table of cases")]
    public static void Pragma_WithReason_IsCountedUnderTheId(string directive)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text(directive, "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("CA1502", "src/A.cs", new Position(1, 1))], scan.Places);
        Assert.Equal("CA1502=1", Counts(scan));
    }

    [Fact]
    public void Pragma_SeveralIds_AreCountedEachAndOneFindingNamesThePlace()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502, IDE0060 // Reason", "#pragma warning disable CA1062, CA1305", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 2, 1, $"'#pragma warning disable CA1062, CA1305' {NoReason}"));
        Assert.Equal("CA1502=1, IDE0060=1", Counts(scan));
    }

    [Fact]
    public void Pragma_ReasonThatRepeatsAllTheIds_IsEng102ButWithAnotherWordItIsAReason()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502, IDE0060 // IDE0060 CA1502", "#pragma warning disable CA1062 // CA1062 only", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng102, "src/A.cs", 1, 1, $"'#pragma warning disable CA1502, IDE0060' has a reason comment {RepeatsId}"));
        Assert.Equal("CA1062=1", Counts(scan));
    }

    [Fact]
    public void Pragma_BareNumber_IsACompilerWarning()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "#pragma warning disable 1591",
                "#pragma warning disable 1572 // 1572",
                "#pragma warning disable 1573 // CS1573",
                "#pragma warning disable 1574 // Documentation is on the base type",
                "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, "src/A.cs", 1, 1, $"'#pragma warning disable 1591' {NoReason}"),
            Error(EngCodes.Eng102, "src/A.cs", 2, 1, $"'#pragma warning disable 1572' has a reason comment {RepeatsId}"),
            Error(EngCodes.Eng102, "src/A.cs", 3, 1, $"'#pragma warning disable 1573' has a reason comment {RepeatsId}"));
        Assert.Equal("CS1574=1", Counts(scan));
    }

    [Fact]
    public void Pragma_WithoutIds_NeedsAReasonAndIsCountedUnderPragma()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable", "#pragma warning disable // Generated by hand", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 1, 1, $"'#pragma warning disable' {NoReason}"));
        Assert.Equal([new SuppressionPlace("pragma", "src/A.cs", new Position(2, 1))], scan.Places);
        Assert.Equal("pragma=1", Counts(scan));
    }

    [Fact]
    public void Pragma_RestoreAndChecksum_AreNotForms()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text("#pragma warning restore CA1502", "#pragma warning enable CA1502", "#pragma checksum \"a.cs\" \"{00000000-0000-0000-0000-000000000000}\" \"00\"", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Empty(scan.Places);
    }

    [Fact]
    public void Pragma_InACommentOrInAStringLiteral_IsNotAForm()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "class A",
                "{",
                "    // #pragma warning disable CA1502",
                "    /* #pragma warning disable CA1502 */",
                "    string text = \"#pragma warning disable CA1502\";",
                "    string raw = \"\"\"",
                "#pragma warning disable CA1062",
                "#nullable disable",
                "[SuppressMessage(\"a\", \"CA1\")] [GeneratedCode(\"t\", \"1\")]",
                "\"\"\";",
                "}"));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void Pragma_UnderConditionalNet11_IsFoundAndCountedOnce()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write(
            "src/A.cs",
            Text("#if NET11_0_OR_GREATER", "#pragma warning disable CA1502", "#pragma warning disable CA1062 // Reason", "#endif", "class A { }"));

        SuppressionScan scan = Scan(root);

        // Four symbol sets read the file, two of them activate the branch: each place is still one place.
        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 2, 1, $"'#pragma warning disable CA1502' {NoReason}"));
        Assert.Equal([new SuppressionPlace("CA1062", "src/A.cs", new Position(3, 1))], scan.Places);
    }

    [Fact]
    public void Pragma_InABranchThatNoSetActivates_IsNotRead()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write("src/A.cs", Text("#if NEVER_DEFINED", "#pragma warning disable CA1502", "#nullable disable", "#endif", "class A { }"));

        FindingAssert.None(Run(root));
    }

    [Fact]
    public void Pragma_WhenTheCodeIsNewerThanCSharp14_IsStillFound()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net11.0");
        root.Write("src/A.cs", Text("union Pet(Cat, Dog);", "#pragma warning disable CA1502", "class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 2, 1, $"'#pragma warning disable CA1502' {NoReason}"));
    }

    // ---- #nullable disable ----

    [Fact]
    public void Nullable_Disable_WithoutReasonIsEng101()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("class A", "{", "    #nullable disable", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 3, 5, $"'#nullable disable' {NoReason}"));
    }

    [Fact]
    public void Nullable_DisableWithATarget_IsAFormToo()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#nullable disable warnings", "#nullable disable annotations // Reason", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 1, 1, $"'#nullable disable warnings' {NoReason}"));
        Assert.Equal([new SuppressionPlace("nullable", "src/A.cs", new Position(2, 1))], scan.Places);
    }

    [Fact]
    public void Nullable_Disable_WithReasonIsCountedUnderNullable()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#nullable disable // The type is filled by a serializer", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("nullable=1", Counts(scan));
    }

    [Fact]
    public void Nullable_EnableRestoreStringLiteralAndTheBangOperator_AreNotForms()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "#nullable enable",
                "#nullable restore",
                "#nullable enable warnings",
                "class A",
                "{",
                "    string? a = null;",
                "    string b = a!;",
                "    int c = a!.Length;",
                "    string d = \"#nullable disable\";",
                "}"));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    // ---- [SuppressMessage] and [UnconditionalSuppressMessage] ----

    [Fact]
    public void SuppressMessage_WithoutJustification_IsEng101AtTheBracket()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text("using System.Diagnostics.CodeAnalysis;", "", "[SuppressMessage(\"Design\", \"CA1024:Use properties where appropriate\")]", "public class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "src/A.cs", 3, 1, $"'[SuppressMessage] CA1024' has no reason in the Justification argument"));
    }

    [Theory]
    [InlineData("Justification = \"\"")]
    [InlineData("Justification = \"  \"")]
    [InlineData("Justification = \"-\"")]
    [InlineData("Justification = null")]
    [InlineData("Justification = \"\" + \" \"")]
    public static void SuppressMessage_EmptyJustification_IsEng101(string argument)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text($"[SuppressMessage(\"Design\", \"CA1024\", {argument})]", "public class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "src/A.cs", 1, 1, "'[SuppressMessage] CA1024' has no reason in the Justification argument"));
    }

    [Theory]
    [InlineData("CA1024")]
    [InlineData("CA1024.")]
    [InlineData("ca1024 - CA1024")]
    public static void SuppressMessage_JustificationThatOnlyRepeatsTheCode_IsEng102(string justification)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text($"[SuppressMessage(\"Design\", \"CA1024:Use properties\", Justification = \"{justification}\")]", "public class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng102, "src/A.cs", 1, 1, $"'[SuppressMessage] CA1024' has a reason in the Justification argument {RepeatsId}"));
    }

    [Fact]
    public void SuppressMessage_WithJustification_IsCountedUnderTheRuleId()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "[SuppressMessage(",
                "    \"Design\",",
                "    \"CA1024:Use properties where appropriate\",",
                "    Justification = \"The method throws for an unsuccessful result.\")]",
                "public class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("CA1024", "src/A.cs", new Position(1, 1))], scan.Places);
    }

    [Fact]
    public void UnconditionalSuppressMessage_WithoutAndWithJustification()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "class A",
                "{",
                "    [UnconditionalSuppressMessage(\"Trimming\", \"IL2026:Members annotated with RequiresUnreferencedCode\")]",
                "    void First() { }",
                "",
                "    [UnconditionalSuppressMessage(\"Trimming\", \"IL2026:Members annotated\", Justification = \"The type is rooted by the generator.\")]",
                "    void Second() { }",
                "}"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, "src/A.cs", 3, 5, "'[UnconditionalSuppressMessage] IL2026' has no reason in the Justification argument"));
        Assert.Equal([new SuppressionPlace("IL2026", "src/A.cs", new Position(6, 5))], scan.Places);
    }

    [Fact]
    public void SuppressMessage_NamedArgumentsQualifiedNamesAndAttributeSuffix_AreRead()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(Category = \"Design\", CheckId = \"CA1062\")]",
                "[SuppressMessageAttribute(\"Design\", checkId: \"CA1707\", Justification = \"Names follow the test convention\" + \" of the repository.\")]",
                "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 1, 1, "'[SuppressMessage] CA1062' has no reason in the Justification argument"));
        Assert.Equal([new SuppressionPlace("CA1707", "src/A.cs", new Position(2, 1))], scan.Places);
    }

    [Fact]
    public void SuppressMessage_SecondAttributeOfAList_IsPlacedAtItsOwnName()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text("[Obsolete, SuppressMessage(\"Design\", \"CA1024\"), SuppressMessage(\"Design\", \"CA1062\", Justification = \"Reason\")]", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 1, 12, "'[SuppressMessage] CA1024' has no reason in the Justification argument"));
        Assert.Equal([new SuppressionPlace("CA1062", "src/A.cs", new Position(1, 49))], scan.Places);
    }

    // The forms of the CheckId argument that are not a rule ID in a string literal: an expression that is not a literal, a literal
    // with nothing before ':' and an attribute without the argument. Each counts under its own key, and the key is not a rule ID
    // (docs/design/eng-checks-source.md, "How the forms are read").
    [Theory]
    [InlineData("[SuppressMessage(\"Design\", Ids.Complexity, Justification = \"Reason\")]", "Ids.Complexity")]
    [InlineData("[SuppressMessage(\"Design\", \"CA\" + \"1502\", Justification = \"Reason\")]", "\"CA\" + \"1502\"")]
    [InlineData("[SuppressMessage(\"Design\", null, Justification = \"Reason\")]", "null")]
    [InlineData("[SuppressMessage(\"Design\", default, Justification = \"Reason\")]", "default")]
    [InlineData("[SuppressMessage(\"Design\", nameof(A), Justification = \"Reason\")]", "nameof(A)")]
    [InlineData("[SuppressMessage(\"Design\", \"\", Justification = \"Reason\")]", "\"\"")]
    [InlineData("[SuppressMessage(\"Design\", \" : Use properties\", Justification = \"Reason\")]", "\" : Use properties\"")]
    [InlineData("[SuppressMessage(\"Design\", (\":Use properties\"), Justification = \"Reason\")]", "\":Use properties\"")]
    [InlineData("[SuppressMessage(\"Design\", Justification = \"Reason\")]", "SuppressMessage")]
    [InlineData("[SuppressMessageAttribute(\"Design\", Justification = \"Reason\")]", "SuppressMessageAttribute")]
    [InlineData("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Design\", Justification = \"Reason\")]", "System.Diagnostics.CodeAnalysis.SuppressMessage")]
    [InlineData("[UnconditionalSuppressMessage(\"Trimming\", Justification = \"Reason\")]", "UnconditionalSuppressMessage")]
    public static void SuppressMessage_CheckIdThatIsNotARuleId_IsCountedUnderItsOwnKey(string attribute, string key)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text(attribute, "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace(key, "src/A.cs", new Position(1, 1))], scan.Places);
    }

    [Fact]
    public void SuppressMessage_CheckIdThatIsNotALiteral_NeedsAJustificationAndItsTextIsNotARuleId()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "[SuppressMessage(\"Design\", Ids.Complexity)]",
                "[SuppressMessage(\"Design\", checkId: Ids.Length, Justification = \"Ids.Length\")]",
                "[SuppressMessage(\"Design\", Ids.Naming, Justification = \"-\")]",
                "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, "src/A.cs", 1, 1, "'[SuppressMessage] Ids.Complexity' has no reason in the Justification argument"),
            Error(EngCodes.Eng101, "src/A.cs", 3, 1, "'[SuppressMessage] Ids.Naming' has no reason in the Justification argument"));
        Assert.Equal([new SuppressionPlace("Ids.Length", "src/A.cs", new Position(2, 1))], scan.Places);
    }

    [Fact]
    public void SuppressMessage_LiteralInParentheses_GivesItsPartBeforeTheColonAsTheRuleId()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("[SuppressMessage(\"Design\", (\"CA1062:Validate\"), Justification = \"Reason\")]", "class A { }"));

        Assert.Equal([new SuppressionPlace("CA1062", "src/A.cs", new Position(1, 1))], Scan(root).Places);
    }

    [Fact]
    public void SuppressMessage_WithoutCheckIdAndWithoutJustification_IsEng101()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("[SuppressMessage(\"Design\")]", "class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 1, 1, "'[SuppressMessage]' has no reason in the Justification argument"));
    }

    [Fact]
    public void SuppressMessage_UnderConditionalNet11_IsFoundAndCountedOnce()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write(
            "src/A.cs",
            Text(
                "#if NET11_0_OR_GREATER",
                "[SuppressMessage(\"Naming\", \"CA1720\")]",
                "[SuppressMessage(\"Naming\", \"CA1721\", Justification = \"Reason\")]",
                "#endif",
                "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, "src/A.cs", 2, 1, "'[SuppressMessage] CA1720' has no reason in the Justification argument"));
        Assert.Equal("CA1721=1", Counts(scan));
    }

    // ---- [GeneratedCode] ----

    [Fact]
    public void GeneratedCode_WithoutComment_IsEng101()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text("using System.CodeDom.Compiler;", "", "class A", "{", "    [GeneratedCode(\"tool\", \"1.0\")]", "    void Run() { }", "}"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 5, 5, $"'[GeneratedCode]' {NoReason}"));
    }

    [Theory]
    [InlineData("[GeneratedCode(\"tool\", \"1.0\")] // Written by the table tool")]
    [InlineData("[GeneratedCode(\"tool\", \"1.0\")] /* Written by the table tool */")]
    [InlineData("[GeneratedCode(\"tool\", \"1.0\")] class A { } // Written by the table tool")]
    [InlineData("[GeneratedCode(\"tool\", // Written by the table tool\n    \"1.0\")] class A { }")]
    public static void GeneratedCode_CommentInTheSameLine_IsAReasonCountedUnderGenerated(string source)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text(source, "class B { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("generated", "src/A.cs", new Position(1, 1))], scan.Places);
    }

    [Theory]
    [InlineData("// Written by the table tool\n[GeneratedCode(\"tool\", \"1.0\")]\nclass A { }\n")]
    [InlineData("[GeneratedCode(\"tool\", \"1.0\")]\n// Written by the table tool\nclass A { }\n")]
    [InlineData("[GeneratedCode(\"tool\", \"1.0\")] // -\nclass A { }\n")]
    public static void GeneratedCode_CommentInAnotherLine_IsNotAReason(string source)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", source);

        Finding finding = Assert.Single(Run(root));

        Assert.Equal(EngCodes.Eng101, finding.Code);
        Assert.Equal($"'[GeneratedCode]' {NoReason}", finding.Message);
    }

    [Theory]
    [InlineData("[System.CodeDom.Compiler.GeneratedCode(\"t\", \"1\")]")]
    [InlineData("[GeneratedCodeAttribute(\"t\", \"1\")]")]
    [InlineData("[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"t\", \"1\")]")]
    public static void GeneratedCode_AnyQualifierAndTheAttributeSuffix_AreFound(string attribute)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text(attribute, "class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 1, 1, $"'[GeneratedCode]' {NoReason}"));
    }

    [Fact]
    public void GeneratedCode_InAGeneratedFileOutsideTheAllowedDirectories_IsNotRead()
    {
        using TempDirectory root = new();
        root.Write("src/Gen.g.cs", Text("// Emitted by the table tool", "[GeneratedCode(\"t\", \"1\")]", "class A { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("generated", "src/Gen.g.cs", new Position(1, 1))], scan.Places);
    }

    // ---- Generated files ----

    [Fact]
    public void GeneratedFile_MarkerWithoutReason_IsEng101AtTheMarker()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("//", "// <auto-generated>", "// </auto-generated>", "class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "src/A.cs", 2, 1, "generated file (auto-generated marker) has no reason comment before the first token"));
    }

    [Fact]
    public void GeneratedFile_MarkerInAnIndentedBlockComment_IsPlacedAtTheComment()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("", "    /* <autogenerated /> */", "class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "src/A.cs", 2, 5, "generated file (auto-generated marker) has no reason comment before the first token"));
    }

    [Theory]
    [InlineData("// <auto-generated/>")]
    [InlineData("// <autogenerated>")]
    [InlineData("// <auto-generated version=\"2\">")]
    [InlineData("// <auto-generated")]
    [InlineData("/// <auto-generated/>")]
    public static void GeneratedFile_OnlyMarkerTags_AreNotAReason(string comment)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text(comment, "class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "src/A.cs", 1, 1, "generated file (auto-generated marker) has no reason comment before the first token"));
    }

    [Fact]
    public void GeneratedFile_MarkerWithReason_IsCountedUnderGenerated()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A.cs",
            Text(
                "// <auto-generated>",
                "//     Emitted by the table tool from the data files.",
                "// </auto-generated>",
                "#pragma warning disable CA1502",
                "class A { }"));

        SuppressionScan scan = Scan(root);

        // The pragma of a generated file is not read: only the comments before the first token are.
        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("generated", "src/A.cs", new Position(1, 1))], scan.Places);
    }

    [Theory]
    [InlineData("src/TemporaryGeneratedFile_5937a670.cs")]
    [InlineData("src/A.designer.cs")]
    [InlineData("src/A.Designer.cs")]
    [InlineData("src/A.generated.cs")]
    [InlineData("src/A.g.cs")]
    [InlineData("src/A.G.CS")]
    [InlineData("src/A.g.i.cs")]
    public static void GeneratedFile_Name_WithoutReasonIsEng101AtTheFirstPosition(string path)
    {
        using TempDirectory root = new();
        root.Write(path, Text("//", "class A { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, path, 1, 1, "generated file (generated name) has no reason comment before the first token"));
    }

    [Fact]
    public void GeneratedFile_Name_WithAReasonCommentIsCounted()
    {
        using TempDirectory root = new();
        root.Write("src/A.g.cs", Text("class A { }"));
        root.Write("src/B.g.cs", Text("", "// Emitted by the table tool", "class B { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, "src/A.g.cs", 1, 1, "generated file (generated name) has no reason comment before the first token"));
        Assert.Equal([new SuppressionPlace("generated", "src/B.g.cs", new Position(1, 1))], scan.Places);
    }

    [Fact]
    public void GeneratedFile_MarkerAndNameInOneFile_AreOnePlace()
    {
        using TempDirectory root = new();
        root.Write("src/A.g.cs", Text("// <auto-generated>", "// </auto-generated>", "class A { }"));
        root.Write("src/B.g.cs", Text("// <auto-generated>", "// Emitted by the table tool", "// </auto-generated>", "class B { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, "src/A.g.cs", 1, 1, "generated file (auto-generated marker) has no reason comment before the first token"));
        Assert.Equal([new SuppressionPlace("generated", "src/B.g.cs", new Position(1, 1))], scan.Places);
    }

    [Fact]
    public void GeneratedFile_InAnAllowedDirectory_IsNotCounted()
    {
        using TempDirectory root = new();
        root.Write(Unicode + "Tables.g.cs", Text("class A { }"));
        root.Write(Unicode + "Marked.cs", Text("// <auto-generated>", "// </auto-generated>", "class B { }"));
        root.Write(Unicode + "Deep/Nested.designer.cs", Text("class C { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Empty(scan.Places);
    }

    [Fact]
    public void GeneratedFile_InTheDirectoryOfSnapshots_IsNotCountedAndInAnotherDirectoryOfTheTestsIs()
    {
        using TempDirectory root = new();
        root.Write(Snapshots + "Smallest.g.cs", Text("// <auto-generated/>", "class A { }"));
        root.Write(Snapshots + "Deep/Nested.g.cs", Text("// <auto-generated/>", "class B { }"));
        root.Write("tests/Great.Parts.Cel.Generator.Tests/Other/Smallest.g.cs", Text("// <auto-generated/>", "class C { }"));
        root.Write("tests/Great.Parts.Cel.Generator.Tests/Smallest.g.cs", Text("// <auto-generated/>", "class D { }"));
        root.Write("tests/Great.Parts.Cel.Generator.Samples/Snapshots/Smallest.g.cs", Text("// <auto-generated/>", "class E { }"));

        FindingAssert.Exactly(
            Run(root),
            Error(EngCodes.Eng101, "tests/Great.Parts.Cel.Generator.Samples/Snapshots/Smallest.g.cs", 1, 1, "generated file (auto-generated marker) has no reason comment before the first token"),
            Error(EngCodes.Eng101, "tests/Great.Parts.Cel.Generator.Tests/Other/Smallest.g.cs", 1, 1, "generated file (auto-generated marker) has no reason comment before the first token"),
            Error(EngCodes.Eng101, "tests/Great.Parts.Cel.Generator.Tests/Smallest.g.cs", 1, 1, "generated file (auto-generated marker) has no reason comment before the first token"));
    }

    [Fact]
    public void FileWithoutASignInAnAllowedDirectory_IsCheckedLikeTheOthers()
    {
        using TempDirectory root = new();
        root.Write(Unicode + "Helper.cs", Text("#pragma warning disable CA1502", "class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, Unicode + "Helper.cs", 1, 1, $"'#pragma warning disable CA1502' {NoReason}"));
    }

    [Fact]
    public void GeneratedFile_MarkerInAnotherCase_IsNotASignAndTheFileIsCheckedLikeTheOthers()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("// <Auto-Generated>", "// </Auto-Generated>", "#pragma warning disable CA1502", "class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 3, 1, $"'#pragma warning disable CA1502' {NoReason}"));
        Assert.Empty(Scan(root).Places);
    }

    // ---- The reason ----

    [Theory]
    [InlineData("", "CA1502", "Missing")]
    [InlineData("  ", "CA1502", "Missing")]
    [InlineData("// - ... --", "CA1502", "Missing")]
    [InlineData("// CA1502", "CA1502", "OnlyRuleId")]
    [InlineData("ca1502:", "CA1502", "OnlyRuleId")]
    [InlineData("CA1502, IDE0060", "CA1502,IDE0060", "OnlyRuleId")]
    [InlineData("CS1591", "CS1591", "OnlyRuleId")]
    [InlineData("1591", "CS1591", "OnlyRuleId")]
    [InlineData("CA1502 table", "CA1502", "Valid")]
    [InlineData("IDE0060", "CA1502", "Valid")]
    [InlineData("// CA1502 is off", "CA1502", "Valid")]
    [InlineData("-", "", "Missing")]
    [InlineData("nullable", "", "Valid")]
    public static void Reason_IsTheTextThatRemainsAfterTheIdsAndPunctuation(string reason, string ids, string expected)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Assert.Equal(Enum.Parse<ReasonState>(expected), SuppressionReason.Judge(reason, ids.Split(',', StringSplitOptions.RemoveEmptyEntries)));
    }

    // ---- The scan and the command ----

    [Fact]
    public void Scan_CountsByKeyAcrossFilesAndKeepsEveryPlace()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502 // Reason", "#nullable disable // Reason", "class A { }"));
        root.Write("src/B.cs", Text("#pragma warning disable CA1502, CA1062 // Reason", "class B { }"));
        root.Write("src/C.g.cs", Text("// Reason", "class C { }"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("CA1062=1, CA1502=2, generated=1, nullable=1", Counts(scan));
        Assert.Equal(
            [
                new SuppressionPlace("CA1502", "src/A.cs", new Position(1, 1)),
                new SuppressionPlace("nullable", "src/A.cs", new Position(2, 1)),
                new SuppressionPlace("CA1502", "src/B.cs", new Position(1, 1)),
                new SuppressionPlace("CA1062", "src/B.cs", new Position(1, 1)),
                new SuppressionPlace("generated", "src/C.g.cs", new Position(1, 1)),
            ],
            scan.Places);
    }

    [Fact]
    public void Scan_IsMadeOncePerContextAndSharedWithTheRatchet()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("#pragma warning disable CA1502 // Reason", "class A { }"));
        CheckContext context = new(root.Path, new Dictionary<string, string>());

        List<Finding> first = [.. new SuppressionsCommand().Run(context)];
        SuppressionScan scan = SuppressionScan.Of(context);

        Assert.Empty(first);
        Assert.Same(scan, SuppressionScan.Of(context));
        Assert.Equal("CA1502=1", Counts(scan));
    }

    [Fact]
    public void Scanners_OfOtherKindsOfFiles_AreJudgedByTheSameRules()
    {
        using TempDirectory root = new();
        CheckContext context = new(root.Path, new Dictionary<string, string>());
        StubScanner scanner = new(
            new SuppressionForm(".editorconfig", new Position(4, 1), "'dotnet_diagnostic.CA1720.severity = none'", "comment line above", ["CA1720"], ["CA1720"], ""),
            new SuppressionForm(".editorconfig", new Position(8, 1), "'dotnet_diagnostic.CA1065.severity = none'", "comment line above", ["CA1065"], ["CA1065"], "# CA1065"),
            new SuppressionForm(".editorconfig", new Position(12, 1), "'dotnet_diagnostic.CA1062.severity = none'", "comment line above", ["CA1062"], ["CA1062"], "# The guard is in the caller"));

        SuppressionScan scan = SuppressionScan.Run(context, [scanner]);

        FindingAssert.Exactly(
            scan.Findings,
            Error(EngCodes.Eng101, ".editorconfig", 4, 1, "'dotnet_diagnostic.CA1720.severity = none' has no comment line above"),
            Error(EngCodes.Eng102, ".editorconfig", 8, 1, "'dotnet_diagnostic.CA1065.severity = none' has a comment line above that only repeats the rule ID"));
        Assert.Equal([new SuppressionPlace("CA1062", ".editorconfig", new Position(12, 1))], scan.Places);
    }

    [Fact]
    public void Command_IsRegisteredAndWritesTheFindingLines()
    {
        using TempDirectory root = new();
        root.Write("Great.Parts.Cel.slnx", "<Solution />");
        root.Write("src/A.cs", Text("class A", "{", "    #pragma warning disable CA1502", "}"));
        root.Write("src/obj/Debug/B.cs", Text("#pragma warning disable CA1502", "class B { }"));
        root.Write("tests/cel-spec/C.cs", Text("#pragma warning disable CA1502", "class C { }"));
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new();

        int exit = Program.Run(["suppressions", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(1, exit);
        Assert.Equal("src/A.cs(3,5): error ENG101: '#pragma warning disable CA1502' has no reason comment\n", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Command_WithEveryReasonPresent_ExitsWithZero()
    {
        using TempDirectory root = new();
        root.Write("Great.Parts.Cel.slnx", "<Solution />");
        root.Write("src/A.cs", Text("#pragma warning disable CA1502 // Reason", "class A { }"));
        using StringWriter output = new();
        using StringWriter error = new();

        int exit = Program.Run(["suppressions", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, output.ToString());
    }

    // ---- Examples that are not run (docs/design/eng-docs.md, "examples") ----

    private const string ExampleReasonPlace = "reason after the word skip in the same line";
    private const string NoExampleReason = "has no " + ExampleReasonPlace;
    private const string ExampleAttributeReasonPlace = "reason in the skip attribute";
    private const string NoExampleAttributeReason = "has no " + ExampleAttributeReasonPlace;

    [Theory]
    [InlineData("```csharp skip")]
    [InlineData("```csharp skip:")]
    [InlineData("```csharp net11.0 skip:   ")]
    [InlineData("~~~csharp skip")]
    public static void ExampleSkip_InMarkdownWithoutReason_IsEng101AtTheFence(string fence)
    {
        using TempDirectory root = new();
        root.Write("README.md", Text("# Title", "", fence, "this is not C#", fence[..3]));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "README.md", 3, 1, $"'csharp skip' {NoExampleReason}"));
        Assert.Empty(Scan(root).Places);
    }

    [Theory]
    [InlineData("```csharp skip: ENG301")]
    [InlineData("```csharp skip: ENG301, ENG302.")]
    [InlineData("```csharp net11.0 skip: eng302")]
    public static void ExampleSkip_InMarkdownWithAReasonThatOnlyRepeatsTheCode_IsEng102(string fence)
    {
        using TempDirectory root = new();
        root.Write("docs/a.md", Text("# Title", "", "  " + fence, "  this is not C#", "  ```"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng102, "docs/a.md", 3, 3, $"'csharp skip' has a {ExampleReasonPlace} {RepeatsId}"));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void ExampleSkip_InMarkdownWithAReason_IsCountedUnderExample()
    {
        using TempDirectory root = new();
        root.Write(
            "README.md",
            Text("# Title", "", "```csharp skip: needs a running server", "this is not C#", "```", "", "```csharp", "Console.WriteLine(1);", "```"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("example", "README.md", new Position(3, 1))], scan.Places);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public static void ExampleSkip_InXmlDocumentationWithoutReason_IsEng101AtTheStartTag(string reason)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("namespace A;", "", "/// <summary>Shows it.</summary>", $"/// <code skip=\"{reason}\">not C#</code>", "public static class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, "src/A.cs", 4, 5, $"'<code skip>' {NoExampleAttributeReason}"));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void ExampleSkip_InXmlDocumentationWithAReasonThatOnlyRepeatsTheCode_IsEng102()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("/// <code skip=\"ENG302.\">not C#</code>", "public static class A { }"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng102, "src/A.cs", 1, 5, $"'<code skip>' has a {ExampleAttributeReasonPlace} {RepeatsId}"));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void ExampleSkip_InXmlDocumentationWithAReason_IsCountedUnderExampleWithTheOnesOfMarkdown()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", Text("/// <code skip=\"needs a running server\">not C#</code>", "public static class A { }"));
        root.Write("docs/a.md", Text("```csharp skip: uses the network", "not C#", "```", "", "```csharp", "Console.WriteLine(1);", "```"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("example=2", Counts(scan));
        Assert.Equal(
            [new SuppressionPlace("example", "docs/a.md", new Position(1, 1)), new SuppressionPlace("example", "src/A.cs", new Position(1, 5))],
            scan.Places.OrderBy(place => place.Path, StringComparer.Ordinal));
    }

    [Fact]
    public void Example_WithoutSkipOrOutsideTheDocumentation_IsNotAForm()
    {
        using TempDirectory root = new();
        root.Write("README.md", Text("```csharp net11.0", "Console.WriteLine(1m);", "```"));
        root.Write("CHANGELOG.md", Text("```csharp skip", "not C#", "```"));
        root.Write("tests/A.cs", Text("/// <code skip=\"\">not C#</code>", "public static class A { }"));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    // ---- .lycheeignore (docs/design/eng-checks-source.md, "suppressions") ----

    private const string NoLycheeReason = "has no comment line above";
    private const string LycheeRepeats = "has a comment line above that only repeats the rule ID";

    [Fact]
    public void LycheeIgnore_PatternWithoutAComment_IsEng101AtItsLine()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# The page is written later", "^https://example\\.org/later", "^https://example\\.org/never"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, ".lycheeignore", 3, 1, $"'^https://example\\.org/never' {NoLycheeReason}"));
        Assert.Equal([new SuppressionPlace("lychee", ".lycheeignore", new Position(2, 1))], scan.Places);
    }

    [Theory]
    [InlineData("# ^https://example\\.org/later")]
    [InlineData("#   ^https://example\\.org/later.")]
    [InlineData("# ^https://example\\.ORG/later")]
    public static void LycheeIgnore_CommentThatOnlyRepeatsThePattern_IsEng102(string comment)
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text(comment, "^https://example\\.org/later"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng102, ".lycheeignore", 2, 1, $"'^https://example\\.org/later' {LycheeRepeats}"));
        Assert.Empty(Scan(root).Places);
    }

    [Theory]
    [InlineData("#")]
    [InlineData("#  - ")]
    public static void LycheeIgnore_CommentWithoutText_IsEng101(string comment)
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text(comment, "^https://example\\.org/later"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".lycheeignore", 2, 1, $"'^https://example\\.org/later' {NoLycheeReason}"));
    }

    [Fact]
    public void LycheeIgnore_PatternWithAReason_IsCountedUnderLychee()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# The page is written later", "^https://example\\.org/later"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal([new SuppressionPlace("lychee", ".lycheeignore", new Position(2, 1))], scan.Places);
    }

    [Fact]
    public void LycheeIgnore_AllCommentLinesDirectlyAbove_AreTheReason()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# ^https://example\\.org/later", "# The page is written later", "^https://example\\.org/later"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("lychee=1", Counts(scan));
    }

    [Fact]
    public void LycheeIgnore_OneCommentCoversOnePattern()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# Both pages are written later", "^https://example\\.org/one", "^https://example\\.org/two"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, ".lycheeignore", 3, 1, $"'^https://example\\.org/two' {NoLycheeReason}"));
        Assert.Equal("lychee=1", Counts(scan));
    }

    [Fact]
    public void LycheeIgnore_EmptyLineBetweenTheCommentAndThePattern_LeavesThePatternWithoutAReason()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# The page is written later", "", "^https://example\\.org/later"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".lycheeignore", 3, 1, $"'^https://example\\.org/later' {NoLycheeReason}"));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void LycheeIgnore_AHashInsideAPatternLine_IsPartOfThePattern()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# Anchors of a page that is written later", "^https://example\\.org/later#.*", "^https://example\\.org/#top"));

        FindingAssert.Exactly(Run(root), Error(EngCodes.Eng101, ".lycheeignore", 3, 1, $"'^https://example\\.org/#top' {NoLycheeReason}"));
    }

    [Fact]
    public void LycheeIgnore_IndentedLines_AreAtTheColumnOfTheirFirstCharacter()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("  # The page is written later", "  ^https://example\\.org/later", "\t^https://example\\.org/never"));

        SuppressionScan scan = Scan(root);

        FindingAssert.Exactly(scan.Findings, Error(EngCodes.Eng101, ".lycheeignore", 3, 2, $"'^https://example\\.org/never' {NoLycheeReason}"));
        Assert.Equal([new SuppressionPlace("lychee", ".lycheeignore", new Position(2, 3))], scan.Places);
    }

    [Fact]
    public void LycheeIgnore_OnlyCommentsAndEmptyLinesOrAnotherDirectory_HasNoForm()
    {
        using TempDirectory root = new();
        root.Write("docs/.lycheeignore", Text("^https://example\\.org/later"));

        FindingAssert.None(Run(root));

        root.Write(".lycheeignore", Text("# Nothing is skipped", string.Empty, "   ", "# Not yet"));

        FindingAssert.None(Run(root));
        Assert.Empty(Scan(root).Places);
    }

    [Fact]
    public void LycheeIgnore_AndLycheeToml_AreCountedTogetherUnderLychee()
    {
        using TempDirectory root = new();
        root.Write(".lycheeignore", Text("# The page is written later", "^https://example\\.org/later"));
        root.Write("lychee.toml", Text("exclude = ['^https://other\\.example/'] # The page is written later"));

        SuppressionScan scan = Scan(root);

        FindingAssert.None(scan.Findings);
        Assert.Equal("lychee=2", Counts(scan));
    }

    // ---- Helpers ----

    private static Finding Error(string code, string path, int line, int column, string message) =>
        FindingAssert.Error(code, path, line, column, message);

    private static List<Finding> Run(TempDirectory root) => [.. new SuppressionsCommand().Run(new CheckContext(root.Path, new Dictionary<string, string>()))];

    private static SuppressionScan Scan(TempDirectory root) => SuppressionScan.Of(new CheckContext(root.Path, new Dictionary<string, string>()));

    private static string Counts(SuppressionScan scan) =>
        string.Join(", ", scan.Counts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"));

    private static string Text(params string[] lines) => string.Join('\n', lines) + "\n";

    private static void WriteTargets(TempDirectory root, string targets) =>
        root.Write("Directory.Build.props", $"<Project><PropertyGroup><TargetFrameworks>{targets}</TargetFrameworks></PropertyGroup></Project>");

    private sealed class StubScanner(params SuppressionForm[] forms) : ISuppressionScanner
    {
        public IEnumerable<SuppressionForm> Scan(CheckContext context) => forms;
    }
}
