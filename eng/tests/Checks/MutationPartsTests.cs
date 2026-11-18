using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;
using TestRoot = Great.Parts.Cel.Eng.Tests.Support.RepositoryRoot;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// The parts of the run of eng/mutate on the whole library (docs/design/eng-mutate.md, "Parts of the library"): the patterns of
// `mutate` that the check reads, and the configuration files of the parts in eng/stryker-parts/.
public sealed class MutationPartsTests
{
    private const string ProjectDirectory = "src/Great.Parts.Cel";
    private const string MainConfiguration = "stryker-config.json";
    private const string PartsDirectory = "eng/stryker-parts";

    [Theory]
    [InlineData("Values/Cel*.cs", "Values/CelList.cs", true)]
    [InlineData("Values/Cel*.cs", "Values/CelList.Views.cs", true)]
    [InlineData("Values/Cel*.cs", "Values/Cel.cs", true)]
    [InlineData("Values/Cel*.cs", "Values/ConversionRules.cs", false)]
    [InlineData("Values/Cel*.cs", "Values/Nested/CelList.cs", false)]
    [InlineData("Values/Cel*.cs", "Library/Values/CelList.cs", false)]
    [InlineData("*.cs", "CelProgram.cs", true)]
    [InlineData("*.cs", "Values/CelList.cs", false)]
    [InlineData("Library/*.cs", "Library/Other.cs", true)]
    [InlineData("Library/*.cs", "Library/Time/Other.cs", false)]
    [InlineData("Library/Regex/**/*.cs", "Library/Regex/Matcher.cs", true)]
    [InlineData("Library/Regex/**/*.cs", "Library/Regex/Deep/Er/Matcher.cs", true)]
    [InlineData("Library/Regex/**/*.cs", "Library/Regex/Matcher.md", false)]
    [InlineData("Library/Regex/**/*.cs", "Library/RegexOther/Matcher.cs", false)]
    [InlineData("**/*.cs", "Matcher.cs", true)]
    [InlineData("**/*.cs", "A/B/Matcher.cs", true)]
    [InlineData("**", "A/B/Matcher.cs", true)]
    [InlineData("**/Matcher.cs", "A/XMatcher.cs", false)]
    [InlineData("**/Matcher.cs", "A/Matcher.cs", true)]
    [InlineData("Values/?.cs", "Values/A.cs", true)]
    [InlineData("Values/?.cs", "Values/AB.cs", false)]
    [InlineData("Values/*", "Values/A.cs", true)]
    [InlineData("A*B*C", "AxxBxxC", true)]
    [InlineData("A*B*C", "AxxBxxD", false)]
    [InlineData("Matcher.cs", "Matcher.cs", true)]
    [InlineData("Matcher.cs", "matcher.cs", false)]
    [InlineData("Matcher.cs", "Matcher.cs.bak", false)]
    public static void Matches_FollowsTheGlobSyntaxOfTheTool(string pattern, string path, bool expected) =>
        Assert.Equal(expected, MutateScope.Matches(pattern, path));

    [Fact]
    public static void Scope_WithoutPatterns_HasEveryFile()
    {
        MutateScope scope = new([], ProjectDirectory);

        Assert.True(scope.Contains(ProjectDirectory + "/Values/CelList.cs"));
        Assert.True(scope.Contains(ProjectDirectory + "/CelProgram.cs"));
    }

    [Fact]
    public static void Scope_WithIncludeAndExcludePatterns_HasTheIncludedFilesWithoutTheExcludedOnesWhateverTheOrder()
    {
        string[][] orders = [["Values/*.cs", "!Values/Cel*.cs"], ["!Values/Cel*.cs", "Values/*.cs"]];
        foreach (string[] patterns in orders)
        {
            MutateScope scope = new(patterns, ProjectDirectory);

            Assert.True(scope.Contains(ProjectDirectory + "/Values/ConversionRules.cs"));
            Assert.False(scope.Contains(ProjectDirectory + "/Values/CelList.cs"));
            Assert.False(scope.Contains(ProjectDirectory + "/Library/Other.cs"));
        }
    }

    [Fact]
    public static void Scope_WithOnlyExcludePatterns_HasEveryOtherFile()
    {
        MutateScope scope = new(["!Values/**/*.cs"], ProjectDirectory);

        Assert.True(scope.Contains(ProjectDirectory + "/Library/Other.cs"));
        Assert.False(scope.Contains(ProjectDirectory + "/Values/CelList.cs"));
    }

    [Fact]
    public static void Scope_SpanAfterTheFile_IsLeftOutAndBackslashesAreSeparators()
    {
        MutateScope scope = new(["Library\\Other.cs{10..100}"], ProjectDirectory);

        Assert.True(scope.Contains(ProjectDirectory + "/Library/Other.cs"));
        Assert.False(scope.Contains(ProjectDirectory + "/Library/Another.cs"));
    }

    [Fact]
    public static void Scope_ProjectInTheRootOfTheRepository_TakesThePathAsItIs()
    {
        MutateScope scope = new(["Library/*.cs"], string.Empty);

        Assert.True(scope.Contains("Library/Other.cs"));
        Assert.False(scope.Contains("src/Library/Other.cs"));
    }

    [Fact]
    public static void ConfigurationFile_PatternsOfMutate_AreReadWhetherOrNotUnderTheSection()
    {
        using TempDirectory root = new();
        root.Write("with-section.json", "{ \"stryker-config\": { \"project\": \"a/a.csproj\", \"mutate\": [\"A/*.cs\", \"!A/B.cs\"], \"test-projects\": [\"t/t.csproj\"] } }");
        root.Write("top.json", "{ \"mutate\": [\"C/*.cs\"] }");
        root.Write("none.json", "{ \"stryker-config\": { \"test-projects\": [\"t/t.csproj\"] } }");
        root.Write("other.json", "{ \"stryker-config\": { \"other\": { \"mutate\": [\"X.cs\"] } } }");

        Assert.Equal(["A/*.cs", "!A/B.cs"], MutationConfig.Read(Path.Combine(root.Path, "with-section.json")).Mutate);
        Assert.Equal(["C/*.cs"], MutationConfig.Read(Path.Combine(root.Path, "top.json")).Mutate);
        Assert.Empty(MutationConfig.Read(Path.Combine(root.Path, "none.json")).Mutate);
        Assert.Empty(MutationConfig.Read(Path.Combine(root.Path, "other.json")).Mutate);
    }

    // The files of the parts differ from the main configuration only in "mutate": a change of a common setting is made in all of them.
    [Fact]
    public static void PartConfigurations_AreTheMainConfigurationWithTheirOwnPatternsOfMutate()
    {
        string root = TestRoot.Find();
        JsonNode main = Parse(Path.Combine(root, MainConfiguration));
        string[] files = PartFiles(root);

        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            JsonNode part = Parse(file);
            JsonObject settings = part["stryker-config"]!.AsObject();
            Assert.True(settings.Remove("mutate"), file);
            Assert.True(JsonNode.DeepEquals(main, part), file);
        }
    }

    // Every file of the library is mutated by exactly one part, so the parts together are the run on the whole library.
    [Fact]
    public static void EveryFileOfTheLibrary_IsInExactlyOnePart()
    {
        string root = TestRoot.Find();
        string library = Path.Combine(root, ProjectDirectory);
        (string Name, MutateScope Scope)[] parts = [.. PartFiles(root).Select(file => (Path.GetFileNameWithoutExtension(file), new MutateScope(MutationConfig.Read(file).Mutate, ProjectDirectory)))];
        List<string> problems = [];

        foreach (string file in Directory.EnumerateFiles(library, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
            {
                continue;
            }

            string[] owners = [.. parts.Where(part => part.Scope.Contains(relative)).Select(part => part.Name)];
            if (owners.Length != 1)
            {
                problems.Add($"{relative}: {(owners.Length == 0 ? "no part" : string.Join(", ", owners))}");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public static void EveryPart_HasPatternsOfMutateAndAKebabCaseName()
    {
        foreach (string file in PartFiles(TestRoot.Find()))
        {
            string name = Path.GetFileNameWithoutExtension(file);

            Assert.NotEmpty(MutationConfig.Read(file).Mutate);
            Assert.Matches("^[a-z0-9]+(-[a-z0-9]+)*$", name);
        }
    }

    private static string[] PartFiles(string root) =>
        [.. Directory.EnumerateFiles(Path.Combine(root, PartsDirectory), "*.json").Order(StringComparer.Ordinal)];

    private static JsonNode Parse(string path) =>
        JsonNode.Parse(File.ReadAllText(path), documentOptions: new() { AllowTrailingCommas = true }) ?? throw new InvalidOperationException(path);
}
