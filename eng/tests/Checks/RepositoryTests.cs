using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Tests.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using RepositoryFile = Great.Parts.Cel.Eng.Checks.RepositoryFile;
using RepositoryRoot = Great.Parts.Cel.Eng.Checks.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// The helpers of eng/checks that read the repository: the root, the file set, positions, git, the C# sources with their
// symbol sets, and the signs of generated code (docs/design/eng-checks.md, eng-checks-source.md).
public sealed class RepositoryTests
{
    private const string Unicode = "src/Great.Parts.Cel/Library/Unicode/";
    private const string Snapshots = "tests/Great.Parts.Cel.Generator.Tests/Snapshots/";

    // ---- Root ----

    [Fact]
    public void Root_IsTheNearestParentWithTheSolutionFile()
    {
        using TempDirectory root = new();
        root.Write("Great.Parts.Cel.slnx", "<Solution />");
        root.Write("eng/checks/deep/readme.txt", "x");

        Assert.Equal(root.Path, RepositoryRoot.Find(Path.Combine(root.Path, "eng", "checks", "deep")));
        Assert.Equal(root.Path, RepositoryRoot.Find(root.Path));
    }

    [Fact]
    public void Root_WithoutTheSolutionFile_IsNotFound()
    {
        using TempDirectory root = new();

        Assert.Null(RepositoryRoot.Find(root.Path));
    }

    // ---- File set ----

    [Fact]
    public void FileSet_SkipsGitBinObjArtifactsAndCelSpec()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", "x");
        root.Write("src/bin/B.cs", "x");
        root.Write("src/obj/C.cs", "x");
        root.Write("eng/checks/obj/D.cs", "x");
        root.Write("bin/E.cs", "x");
        root.Write("obj/F.cs", "x");
        root.Write("artifacts/G.txt", "x");
        root.Write("artifacts/test-results/H.xml", "x");
        root.Write("tests/cel-spec/I.textproto", "x");
        root.Write("tests/cel-spec/deep/J.textproto", "x");
        root.Write(".git/config", "x");
        root.Write(".git/objects/K", "x");
        root.Write("tests/other/L.cs", "x");
        root.Write("docs/artifacts/M.md", "x");
        root.Write("N.txt", "x");

        IReadOnlyList<RepositoryFile> files = Context(root).Files;

        Assert.Equal(["N.txt", "docs/artifacts/M.md", "src/A.cs", "tests/other/L.cs"], files.Select(file => file.Path));
        Assert.All(files, file => Assert.Equal(Path.Combine(root.Path, file.Path.Replace('/', Path.DirectorySeparatorChar)), file.FullPath));
    }

    [Fact]
    public void FileSet_IsSortedOrdinalWithSlashes()
    {
        using TempDirectory root = new();
        root.Write("b.txt", "x");
        root.Write("B.txt", "x");
        root.Write("a/z.txt", "x");
        root.Write("a.txt", "x");

        Assert.Equal(["B.txt", "a.txt", "a/z.txt", "b.txt"], Context(root).Files.Select(file => file.Path));
    }

    // ---- Positions ----

    [Fact]
    public void Position_ColumnCountsUtf16CodeUnits()
    {
        // U+1F600 is two UTF-16 code units: the character after it is in column 4, not 3.
        const string Text = "a\U0001F600b";
        SourceLines lines = new(Text);

        Assert.Equal(new Position(1, 1), lines.Locate(0));
        Assert.Equal(new Position(1, 2), lines.Locate(1));
        Assert.Equal(new Position(1, 4), lines.Locate(Text.IndexOf('b', StringComparison.Ordinal)));
        Assert.Equal(new Position(1, 5), lines.Locate(Text.Length));
    }

    [Fact]
    public void Position_LineBreaksAreLineFeedCarriageReturnLineFeedAndCarriageReturn()
    {
        const string Text = "ab\ncd\r\nef\rgh";
        SourceLines lines = new(Text);

        Assert.Equal(new Position(1, 3), lines.Locate(2));
        Assert.Equal(new Position(2, 1), lines.Locate(3));
        Assert.Equal(new Position(2, 3), lines.Locate(5));
        Assert.Equal(new Position(3, 1), lines.Locate(7));
        Assert.Equal(new Position(4, 1), lines.Locate(10));
        Assert.Equal(new Position(4, 3), lines.Locate(Text.Length));
    }

    [Fact]
    public void Position_OffsetOutsideTheText_IsRejected()
    {
        SourceLines lines = new("ab");

        Assert.Throws<ArgumentOutOfRangeException>(() => lines.Locate(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => lines.Locate(3));
    }

    [Fact]
    public void ReadText_ReadsUtf8AndDropsTheByteOrderMark()
    {
        using TempDirectory root = new();
        string path = Path.Combine(root.Path, "bom.txt");
        File.WriteAllBytes(path, [.. new byte[] { 0xEF, 0xBB, 0xBF }, .. System.Text.Encoding.UTF8.GetBytes("é\U0001F600x")]);
        root.Write("plain.txt", "éx");

        IReadOnlyList<RepositoryFile> files = Context(root).Files;

        Assert.Equal("é\U0001F600x", files.Single(file => file.Path == "bom.txt").ReadText());
        Assert.Equal("éx", files.Single(file => file.Path == "plain.txt").ReadText());
    }

    // ---- git ----

    [Fact]
    public void Git_RunsInTheDirectoryAndReadsOutput()
    {
        using TempDirectory root = new();

        GitResult init = Git.Run(root.Path, "init", "--quiet");
        GitResult inside = Git.Run(root.Path, "rev-parse", "--is-inside-work-tree");
        GitResult version = Git.Run(root.Path, "--version");

        Assert.True(init.Succeeded);
        Assert.Equal("true", inside.Output.Trim());
        Assert.StartsWith("git version", version.Output, StringComparison.Ordinal);
        Assert.Equal(0, version.ExitCode);
    }

    [Fact]
    public void Git_FailedCommand_IsAResultWithTheExitCodeAndTheError()
    {
        using TempDirectory root = new();

        GitResult result = Git.Run(root.Path, "no-such-subcommand");

        Assert.False(result.Succeeded);
        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEqual(Git.NotStarted, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    // ---- Symbol sets ----

    [Fact]
    public void SymbolSets_NamedInProjectFilesAndProps_AreEveryTargetInEveryConfiguration()
    {
        using TempDirectory root = new();
        root.Write("src/A/A.csproj", "<Project><PropertyGroup><TargetFrameworks>net10.0; net11.0</TargetFrameworks></PropertyGroup></Project>");
        root.Write("Directory.Build.props", "<Project><PropertyGroup Condition=\"'$(X)' == 'y'\"><TargetFramework>netstandard2.0</TargetFramework></PropertyGroup></Project>");
        root.Write("src/B/B.csproj", "<Project><PropertyGroup><TargetFramework>$(Other)</TargetFramework></PropertyGroup></Project>");

        IReadOnlyList<SymbolSet> sets = Context(root).Sources.Sets;

        Assert.Equal(
            ["net10.0/Debug", "net10.0/Release", "net11.0/Debug", "net11.0/Release", "netstandard2.0/Debug", "netstandard2.0/Release"],
            sets.Select(set => set.Name));
    }

    [Fact]
    public void SymbolSets_WithoutAnyTarget_AreOnePerConfigurationWithoutSdkSymbols()
    {
        using TempDirectory root = new();
        root.Write("A.cs", "class A { }");

        IReadOnlyList<SymbolSet> sets = Context(root).Sources.Sets;

        Assert.Equal(["Debug", "Release"], sets.Select(set => set.Configuration));
        Assert.Equal(["DEBUG", "TRACE"], sets[0].Symbols);
        Assert.Equal(["RELEASE", "TRACE"], sets[1].Symbols);
    }

    [Fact]
    public void SymbolSets_Net11_HasTheSdkSymbolsUpToNet11OrGreater()
    {
        IReadOnlyList<string> symbols = SdkSymbols.For("net11.0");

        Assert.Contains("NET", symbols);
        Assert.Contains("NETCOREAPP", symbols);
        Assert.Contains("NET11_0", symbols);
        Assert.Contains("NET11_0_OR_GREATER", symbols);
        Assert.Contains("NET10_0_OR_GREATER", symbols);
        Assert.Contains("NET5_0_OR_GREATER", symbols);
        Assert.Contains("NETCOREAPP3_1_OR_GREATER", symbols);
        Assert.DoesNotContain("NET10_0", symbols);
        Assert.DoesNotContain("NET12_0_OR_GREATER", symbols);
        Assert.DoesNotContain("NETSTANDARD", symbols);
    }

    [Fact]
    public void SymbolSets_Net10_HasNoNet11Symbol()
    {
        IReadOnlyList<string> symbols = SdkSymbols.For("net10.0");

        Assert.Contains("NET10_0", symbols);
        Assert.Contains("NET10_0_OR_GREATER", symbols);
        Assert.DoesNotContain("NET11_0", symbols);
        Assert.DoesNotContain("NET11_0_OR_GREATER", symbols);
    }

    [Fact]
    public void SymbolSets_NetStandard_NetFrameworkAndUnknownTargets()
    {
        IReadOnlyList<string> standard = SdkSymbols.For("netstandard2.0");
        IReadOnlyList<string> framework = SdkSymbols.For("net472");

        Assert.Contains("NETSTANDARD", standard);
        Assert.Contains("NETSTANDARD2_0", standard);
        Assert.Contains("NETSTANDARD2_0_OR_GREATER", standard);
        Assert.Contains("NETSTANDARD1_6_OR_GREATER", standard);
        Assert.DoesNotContain("NETSTANDARD2_1_OR_GREATER", standard);
        Assert.DoesNotContain("NET", standard);
        Assert.Contains("NETFRAMEWORK", framework);
        Assert.Contains("NET472", framework);
        Assert.Contains("NET462_OR_GREATER", framework);
        Assert.DoesNotContain("NET48_OR_GREATER", framework);
        Assert.Empty(SdkSymbols.For("monikerthatdoesnotexist"));
        Assert.Contains("NET10_0", SdkSymbols.For("net10.0-android"));
    }

    [Fact]
    public void SymbolSets_ConfigurationTraceAndDefineConstants_AreInEverySet()
    {
        using TempDirectory root = new();
        root.Write(
            "src/A/A.csproj",
            "<Project><PropertyGroup><TargetFramework>net11.0</TargetFramework><DefineConstants>$(DefineConstants);FEATURE_X;Y,Z;1bad;with-dash</DefineConstants></PropertyGroup></Project>");

        IReadOnlyList<SymbolSet> sets = Context(root).Sources.Sets;
        SymbolSet debug = sets.Single(set => set.Configuration == "Debug");
        SymbolSet release = sets.Single(set => set.Configuration == "Release");

        Assert.Contains("DEBUG", debug.Symbols);
        Assert.DoesNotContain("RELEASE", debug.Symbols);
        Assert.Contains("RELEASE", release.Symbols);
        Assert.DoesNotContain("DEBUG", release.Symbols);
        Assert.All(sets, set => Assert.Equal(["FEATURE_X", "Y", "Z"], set.Symbols.Skip(set.Symbols.Count - 3)));
        Assert.All(sets, set => Assert.Contains("TRACE", set.Symbols));
        Assert.All(sets, set => Assert.DoesNotContain(set.Symbols, symbol => symbol.Contains('$', StringComparison.Ordinal) || symbol == "1bad"));
    }

    [Fact]
    public void SymbolSets_ProjectFileThatIsNotXml_IsSkipped()
    {
        using TempDirectory root = new();
        root.Write("src/A/A.csproj", "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework>");
        root.Write("src/B/B.csproj", "<Project><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>");

        Assert.Equal(["net11.0/Debug", "net11.0/Release"], Context(root).Sources.Sets.Select(set => set.Name));
    }

    // ---- C# sources ----

    [Fact]
    public void Sources_ConditionalNet11Branch_IsActiveForNet11AndNotForNet10()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write(
            "src/A.cs",
            """
            #if NET11_0_OR_GREATER
            #pragma warning disable ONLY11
            #endif
            class A { }
            """);

        CSharpFile file = Context(root).Sources.Files.Single();

        Assert.Equal(
            ["net11.0/Debug", "net11.0/Release"],
            file.Parses.Where(parse => PragmaTexts(parse.Tree).Contains("#pragma warning disable ONLY11")).Select(parse => parse.Set.Name));
    }

    [Fact]
    public void Sources_PlaceSeenByManyParses_IsOnePlace()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write(
            "src/A.cs",
            """
            #if NET10_0_OR_GREATER
            #pragma warning disable BOTH
            #endif
            #if NET11_0_OR_GREATER
            #pragma warning disable ONLY11
            #endif
            #pragma warning disable PLAIN
            class A { }
            """);

        CSharpFile file = Context(root).Sources.Files.Single();
        List<SourcePlace<string>> places = [.. file.Find(tree => Pragmas(tree))];

        // BOTH is active in all four sets, ONLY11 in two, PLAIN everywhere: each is one place.
        Assert.Equal(
            [("#pragma warning disable BOTH", 2, 1), ("#pragma warning disable ONLY11", 5, 1), ("#pragma warning disable PLAIN", 7, 1)],
            places.Select(place => (place.Value, place.Position.Line, place.Position.Column)));
        Assert.All(places, place => Assert.Equal("src/A.cs", place.Path));
    }

    [Fact]
    public void Sources_BothBranchesOfAnIfElse_AreFoundOnceEach()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net11.0");
        root.Write(
            "src/A.cs",
            """
            #if DEBUG
            #pragma warning disable IN_DEBUG
            #else
            #pragma warning disable IN_RELEASE
            #endif
            class A { }
            """);

        CSharpFile file = Context(root).Sources.Files.Single();

        Assert.Equal(
            [("#pragma warning disable IN_DEBUG", 2), ("#pragma warning disable IN_RELEASE", 4)],
            file.Find(tree => Pragmas(tree)).Select(place => (place.Value, place.Position.Line)));
    }

    [Fact]
    public void Sources_BranchThatNoSetActivates_IsNotRead()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write(
            "src/A.cs",
            """
            #if NET99_0_OR_GREATER
            #pragma warning disable NEVER
            [System.Diagnostics.CodeAnalysis.SuppressMessage("a", "b")]
            class Hidden { }
            #endif
            #if DEBUG && RELEASE
            #pragma warning disable NEVER_TOO
            #endif
            class A { }
            """);

        CSharpFile file = Context(root).Sources.Files.Single();

        Assert.Empty(file.Find(tree => Pragmas(tree)));
        Assert.Empty(file.Find(tree => SyntaxQueries.Attributes(tree.GetRoot(), "SuppressMessage").Select(attribute => (attribute.Span, attribute.ToString()))));
    }

    [Fact]
    public void Sources_FileWithoutConditions_SharesOneTreeAcrossTheSets()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net10.0;net11.0");
        root.Write("src/A.cs", "class A { }");
        root.Write("src/B.cs", "#if NET11_0_OR_GREATER\nclass B { }\n#endif\n");

        CSharpSources sources = Context(root).Sources;
        CSharpFile plain = sources.Files.Single(file => file.Path == "src/A.cs");
        CSharpFile conditional = sources.Files.Single(file => file.Path == "src/B.cs");

        Assert.Equal(4, plain.Parses.Count);
        Assert.Single(plain.Parses.Select(parse => parse.Tree).Distinct());
        Assert.Equal(4, conditional.Parses.Select(parse => parse.Tree).Distinct().Count());
    }

    [Fact]
    public void Sources_DirectiveAndAttributeNextToUnknownSyntax_AreFound()
    {
        // Syntax of a newer language version than C# 14: the parse has errors, and the check goes on.
        using TempDirectory root = new();
        WriteTargets(root, "net11.0");
        root.Write(
            "src/A.cs",
            """
            using System;
            namespace N;
            public class A
            {
            #pragma warning disable BEFORE
                [System.CodeDom.Compiler.GeneratedCode("t", "1")]
                public extension int Ex for Pet(Cat, Dog) => ;
            #pragma warning disable MIDDLE
                ) ) ] @@
            #pragma warning disable SKIPPED
                ) $$ !!
                [GeneratedCode("t", "2")]
                int after;
            }
            """);

        CSharpFile file = Context(root).Sources.Files.Single();
        SyntaxTree tree = file.Parses[0].Tree;

        Assert.Contains(tree.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal(
            ["#pragma warning disable BEFORE", "#pragma warning disable MIDDLE", "#pragma warning disable SKIPPED"],
            file.Find(item => Pragmas(item)).Select(place => place.Value));
        Assert.Equal(
            [("System.CodeDom.Compiler.GeneratedCode(\"t\", \"1\")", 6), ("GeneratedCode(\"t\", \"2\")", 12)],
            file.Find(item => GeneratedCode.FindAttributes(item.GetRoot()).Select(attribute => (attribute.Span, attribute.ToString())))
                .Select(place => (place.Value, place.Position.Line)));
    }

    [Fact]
    public void Sources_DirectiveBetweenSkippedTokens_IsFound()
    {
        // The parser skips the stray tokens and keeps them, with the directive between them, as trivia of the next token.
        const string Text = """
            using System;
            ) )
            #pragma warning disable SKIPPED
            ) )
            class A { }
            """;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Text, cancellationToken: TestContext.Current.CancellationToken);
        SyntaxNode root = tree.GetRoot(TestContext.Current.CancellationToken);

        Assert.Contains(tree.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal(["#pragma warning disable SKIPPED"], SyntaxQueries.Directives(root).Select(directive => directive.ToString().Trim()));
    }

    [Fact]
    public void Sources_UnknownSyntaxFollowedByAnAttribute_KeepsTheAttributeList()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net11.0");
        root.Write(
            "src/A.cs",
            """
            union Pet(Cat, Dog);
            [SuppressMessage("Category", "CA1502", Justification = "reason")]
            public class A { }
            """);

        CSharpFile file = Context(root).Sources.Files.Single();

        Assert.Contains(file.Parses[0].Tree.GetDiagnostics(TestContext.Current.CancellationToken), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Single(file.Find(item => SyntaxQueries.Attributes(item.GetRoot(), "SuppressMessage").Select(attribute => (attribute.Span, attribute.ToString()))));
    }

    [Fact]
    public void Sources_TextInsideAStringOrAComment_IsNeitherADirectiveNorAnAttribute()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net11.0");
        root.Write(
            "src/A.cs",
            "class A\n{\n    // [GeneratedCode(\"t\", \"1\")]\n    string text = \"#pragma warning disable X [GeneratedCode]\";\n    string raw = \"\"\"\n#pragma warning disable Y\n\"\"\";\n}\n");

        CSharpFile file = Context(root).Sources.Files.Single();

        Assert.Empty(file.Find(tree => Pragmas(tree)));
        Assert.Empty(file.Find(tree => GeneratedCode.FindAttributes(tree.GetRoot()).Select(attribute => (attribute.Span, attribute.ToString()))));
    }

    [Fact]
    public void Sources_OnlyCSharpFilesAreParsed()
    {
        using TempDirectory root = new();
        WriteTargets(root, "net11.0");
        root.Write("src/A.cs", "class A { }");
        root.Write("src/B.csx", "class B { }");
        root.Write("src/C.txt", "class C { }");

        Assert.Equal(["src/A.cs"], Context(root).Sources.Files.Select(file => file.Path));
    }

    [Fact]
    public void Scans_AreComputedOncePerContext()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", "class A { }");
        CheckContext context = Context(root);

        Assert.Same(context.Files, context.Files);
        Assert.Same(context.Sources, context.Sources);
    }

    // ---- Generated code ----

    [Theory]
    [InlineData("// <auto-generated>\nclass A { }\n")]
    [InlineData("// <autogenerated />\nclass A { }\n")]
    [InlineData("/* <auto-generated */\nclass A { }\n")]
    [InlineData("/// <auto-generated/>\nclass A { }\n")]
    [InlineData("// text before <auto-generated text after\nclass A { }\n")]
    public static void Generated_Marker_IsASign(string text)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", text);

        CSharpSources sources = Context(root).Sources;

        Assert.Empty(sources.Files);
        GeneratedFile file = Assert.Single(sources.GeneratedFiles);
        Assert.Equal("src/A.cs", file.Path);
        Assert.True(file.HasMarker);
        Assert.False(file.HasGeneratedName);
        Assert.False(file.Allowed);
        Assert.Equal(new Position(1, 1), file.Position);
    }

    [Theory]
    [InlineData("// <Auto-Generated>\nclass A { }\n")]
    [InlineData("// <AUTO-GENERATED>\nclass A { }\n")]
    [InlineData("// <autoGenerated>\nclass A { }\n")]
    [InlineData("// auto-generated\nclass A { }\n")]
    [InlineData("// <auto generated>\nclass A { }\n")]
    public static void Generated_MarkerInAnotherCase_IsNotASign(string text)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", text);

        CSharpSources sources = Context(root).Sources;

        Assert.Empty(sources.GeneratedFiles);
        Assert.Equal(["src/A.cs"], sources.Files.Select(file => file.Path));
    }

    [Theory]
    [InlineData("class A { } // <auto-generated>\n")]
    [InlineData("namespace N;\n// <auto-generated>\nclass A { }\n")]
    [InlineData("class A\n{\n    string text = \"<auto-generated\";\n}\n")]
    public static void Generated_MarkerAfterTheFirstToken_IsNotASign(string text)
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", text);

        CSharpSources sources = Context(root).Sources;

        Assert.Empty(sources.GeneratedFiles);
        Assert.Single(sources.Files);
    }

    [Fact]
    public void Generated_MarkerPlace_IsTheCommentWithTheMarker()
    {
        using TempDirectory root = new();
        root.Write("src/A.cs", "// Copyright text\n//\n  /* <auto-generated> */\n// tail\nclass A { }\n");

        GeneratedFile file = Assert.Single(Context(root).Sources.GeneratedFiles);

        Assert.Equal(new Position(3, 3), file.Position);
        Assert.Equal(
            [(1, 1, "// Copyright text"), (2, 1, "//"), (3, 3, "/* <auto-generated> */"), (4, 1, "// tail")],
            file.Comments.Select(comment => (comment.Position.Line, comment.Position.Column, comment.Text)));
    }

    [Theory]
    [InlineData("TemporaryGeneratedFile_036C0B5B-1481-4323-8D20-8F5ADCB23D92.cs")]
    [InlineData("temporarygeneratedfile_x.cs")]
    [InlineData("Form1.designer.cs")]
    [InlineData("Form1.Designer.cs")]
    [InlineData("FORM1.DESIGNER.CS")]
    [InlineData("Model.generated.cs")]
    [InlineData("Model.Generated.cs")]
    [InlineData("Model.g.cs")]
    [InlineData("Model.G.cs")]
    [InlineData("Model.g.i.cs")]
    [InlineData("Model.G.I.cs")]
    public static void Generated_Name_IsASignWithoutCase(string name)
    {
        using TempDirectory root = new();
        root.Write("src/Lib/" + name, "class A { }\n");

        CSharpSources sources = Context(root).Sources;

        Assert.True(GeneratedCode.HasGeneratedName(name));
        Assert.Empty(sources.Files);
        GeneratedFile file = Assert.Single(sources.GeneratedFiles);
        Assert.True(file.HasGeneratedName);
        Assert.False(file.HasMarker);
        Assert.Equal(new Position(1, 1), file.Position);
        Assert.Empty(file.Comments);
    }

    [Theory]
    [InlineData("Model.cs")]
    [InlineData("Modelgenerated.cs")]
    [InlineData("Model.gen.cs")]
    [InlineData("Model.designers.cs")]
    [InlineData("Model.g.j.cs")]
    [InlineData("Model.g.cs.txt")]
    [InlineData("MyTemporaryGeneratedFile_1.cs")]
    [InlineData("g.cs")]
    public static void Generated_OtherName_IsNotASign(string name)
    {
        Assert.False(GeneratedCode.HasGeneratedName(name));
    }

    [Fact]
    public void Generated_NameAndMarkerInOneFile_AreOnePlaceAtTheMarker()
    {
        using TempDirectory root = new();
        root.Write("src/Lib/A.g.cs", "\n// <auto-generated>\nclass A { }\n");

        GeneratedFile file = Assert.Single(Context(root).Sources.GeneratedFiles);

        Assert.True(file.HasMarker);
        Assert.True(file.HasGeneratedName);
        Assert.Equal(new Position(2, 1), file.Position);
    }

    [Fact]
    public void Generated_AllowedDirectory_IsAConstantOfTheCommand()
    {
        Assert.Equal([Unicode, Snapshots], GeneratedCode.AllowedDirectories);
        Assert.True(GeneratedCode.IsInAllowedDirectory(Unicode + "Tables.g.cs"));
        Assert.True(GeneratedCode.IsInAllowedDirectory(Unicode + "deep/Tables.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("src/Great.Parts.Cel/Library/UnicodeOther/Tables.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("src/Great.Parts.Cel/Library/Tables.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("tests/" + Unicode + "Tables.g.cs"));
    }

    [Fact]
    public void Generated_DirectoryOfSnapshots_IsAllowedAndNoOtherDirectoryOfTheTestsIs()
    {
        Assert.True(GeneratedCode.IsInAllowedDirectory(Snapshots + "Smallest.g.cs"));
        Assert.True(GeneratedCode.IsInAllowedDirectory(Snapshots + "deep/Smallest.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("tests/Great.Parts.Cel.Generator.Tests/SnapshotsOther/Smallest.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("tests/Great.Parts.Cel.Generator.Tests/Smallest.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("tests/Great.Parts.Cel.Generator.Samples/Snapshots/Smallest.g.cs"));
        Assert.False(GeneratedCode.IsInAllowedDirectory("tests/Great.Parts.Cel.Tests/Snapshots/Smallest.g.cs"));
    }

    [Fact]
    public void Generated_FileInAnAllowedDirectory_IsMarkedAllowedAndOutsideItIsNot()
    {
        using TempDirectory root = new();
        root.Write(Unicode + "Tables.g.cs", "class Tables { }\n");
        root.Write(Unicode + "Marked.cs", "// <auto-generated>\nclass Marked { }\n");
        root.Write(Unicode + "Plain.cs", "class Plain { }\n");
        root.Write("src/Great.Parts.Cel/Library/Other/Tables.g.cs", "class Other { }\n");

        CSharpSources sources = Context(root).Sources;

        Assert.Equal([Unicode + "Plain.cs"], sources.Files.Select(file => file.Path));
        Assert.Equal(
            [("src/Great.Parts.Cel/Library/Other/Tables.g.cs", false), (Unicode + "Marked.cs", true), (Unicode + "Tables.g.cs", true)],
            sources.GeneratedFiles.Select(file => (file.Path, file.Allowed)));
    }

    [Fact]
    public void Generated_GeneratedCodeAttribute_IsFoundByNameWithAnyQualifier()
    {
        const string Text = """
            using System.CodeDom.Compiler;
            [assembly: GeneratedCode("t", "0")]
            [GeneratedCode("t", "1")]
            class A
            {
                [GeneratedCodeAttribute("t", "2")]
                void M() { }

                [System.CodeDom.Compiler.GeneratedCode("t", "3")]
                int f;

                [Other, global::System.CodeDom.Compiler.GeneratedCodeAttribute("t", "4")]
                string p { get; }

                [NotGeneratedCode]
                int g;

                [GeneratedCodes]
                int h;
            }
            """;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(Text, cancellationToken: TestContext.Current.CancellationToken);

        string[] found = [.. GeneratedCode.FindAttributes(tree.GetRoot(TestContext.Current.CancellationToken)).Select(attribute => attribute.Name.ToString())];

        Assert.Equal(
            ["GeneratedCode", "GeneratedCode", "GeneratedCodeAttribute", "System.CodeDom.Compiler.GeneratedCode", "global::System.CodeDom.Compiler.GeneratedCodeAttribute"],
            found);
    }

    // ---- helpers ----

    private static CheckContext Context(TempDirectory root) => new(root.Path, new Dictionary<string, string>());

    private static void WriteTargets(TempDirectory root, string targets) =>
        root.Write("Directory.Build.props", $"<Project><PropertyGroup><TargetFrameworks>{targets}</TargetFrameworks></PropertyGroup></Project>");

    private static string[] PragmaTexts(SyntaxTree tree) =>
        [.. SyntaxQueries.Directives(tree.GetRoot()).OfType<PragmaWarningDirectiveTriviaSyntax>().Select(directive => directive.ToString().Trim())];

    private static IEnumerable<(TextSpan Span, string Value)> Pragmas(SyntaxTree tree) =>
        SyntaxQueries.Directives(tree.GetRoot())
            .OfType<PragmaWarningDirectiveTriviaSyntax>()
            .Select(directive => (directive.Span, directive.ToString().Trim()));
}
