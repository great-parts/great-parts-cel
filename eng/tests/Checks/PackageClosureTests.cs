using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks package-closure` (docs/design/eng-checks-artifacts.md, "package-closure"). Every test makes packages (a .nupkg with a
// .nuspec that names the id and the version) and lists in a temporary directory, and replaces `dotnet restore` by a function that
// gives the text of a project.assets.json, so no test needs the network.
public sealed class PackageClosureTests
{
    private const string Newline = "\n";
    private const string Version = "1.2.3-beta.4";
    private const string CelPath = "eng/packages/Great.Parts.Cel.txt";
    private const string InjectionPath = "eng/packages/Great.Parts.Cel.DependencyInjection.txt";
    private const string Cel = "Great.Parts.Cel";
    private const string Injection = "Great.Parts.Cel.DependencyInjection";
    private const string AspNetCore = "Great.Parts.Cel.AspNetCore";
    private const string Abstractions = "Microsoft.Extensions.DependencyInjection.Abstractions";
    private const string Options = "Microsoft.Extensions.Options";
    private const string Primitives = "Microsoft.Extensions.Primitives";
    private const string CelList = """
        # Expected content of the Great.Parts.Cel package.
        dependencies net10.0:
        dependencies net11.0:

        closure net10.0:
        closure net11.0:
        """;
    private const string InjectionList = """
        # Expected content of the Great.Parts.Cel.DependencyInjection package.
        dependencies net10.0: Great.Parts.Cel, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Options
        dependencies net11.0: Great.Parts.Cel

        closure net10.0: Great.Parts.Cel, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Options, Microsoft.Extensions.Primitives
        closure net11.0: Great.Parts.Cel
        """;
    private const string AspNetCoreList = """
        # Expected content of the Great.Parts.Cel.AspNetCore package.
        dependencies net10.0: Great.Parts.Cel.DependencyInjection
        dependencies net11.0: Great.Parts.Cel.DependencyInjection

        framework net10.0: Microsoft.AspNetCore.App
        framework net11.0: Microsoft.AspNetCore.App

        closure net10.0: Great.Parts.Cel, Great.Parts.Cel.DependencyInjection
        closure net11.0: Great.Parts.Cel, Great.Parts.Cel.DependencyInjection
        """;

    [Fact]
    public void RestoresThatMatchTheClosureLines_HaveNoFindings()
    {
        using TempDirectory root = Repository(Cel, Injection, AspNetCore);
        List<ClosureRequest> requests = [];

        List<Finding> findings = Check(root, request => Restore(requests, request));

        FindingAssert.None(findings);
        Assert.Equal(
            [
                $"{AspNetCore}/net10.0/web", $"{AspNetCore}/net11.0/web",
                $"{Injection}/net10.0/console", $"{Injection}/net11.0/console",
                $"{Cel}/net10.0/console", $"{Cel}/net11.0/console",
            ],
            requests.Select(request => $"{request.Id}/{request.Target}/{(request.Web ? "web" : "console")}").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Request_NamesTheVersionOfThePackageTheLocalSourceAndAnEmptyDirectory()
    {
        using TempDirectory root = Repository(Cel);
        List<ClosureRequest> requests = [];

        _ = Check(root, request => Restore(requests, request));

        ClosureRequest first = requests[0];
        Assert.Equal(Cel, first.Id);
        Assert.Equal(Version, first.Version);
        Assert.Equal(Path.Combine(root.Path, "artifacts/packages"), first.Source);
        Assert.Equal(root.Path, first.Root);
        Assert.NotEqual(requests[0].Directory, requests[1].Directory);
        Assert.False(Directory.Exists(first.Directory), "the scratch directory is deleted when the command ends");
    }

    [Fact]
    public void Eng209_PackageThatTheRestoreAddsAndTheLineDoesNotName_IsReportedAtTheClosureLine()
    {
        using TempDirectory root = Repository(Cel, Injection);

        List<Finding> findings = Check(
            root,
            request => request is { Id: Injection, Target: "net10.0" }
                ? AssetsOf($"{Cel}/{Version}", $"{Abstractions}/10.0.0", $"{Options}/10.0.0", $"{Primitives}/10.0.0", "Microsoft.Extensions.Logging.Abstractions/10.0.0")
                : Assets(request));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(
                EngCodes.Eng209,
                InjectionPath,
                5,
                1,
                $"the restore of {Injection} in a net10.0 application adds package 'Microsoft.Extensions.Logging.Abstractions', which is not in the closure list"));
    }

    [Fact]
    public void Eng209_TheLineOfTheOutput_HasTheFormOfTheDesignDocument()
    {
        using TempDirectory root = Repository(Cel);

        Result result = Run(root, request => AssetsOf($"{request.Id}/{Version}", "Some.Package/1.0.0"));

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(
            $"{CelPath}(5,1): error ENG209: the restore of {Cel} in a net10.0 application adds package 'Some.Package', which is not in the closure list" + Newline
            + $"{CelPath}(6,1): error ENG209: the restore of {Cel} in a net11.0 application adds package 'Some.Package', which is not in the closure list" + Newline,
            result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Eng210_PackageOfTheClosureLineThatTheRestoreDoesNotAdd_IsReportedAtTheClosureLine()
    {
        using TempDirectory root = Repository(Injection);

        List<Finding> findings = Check(
            root,
            request => request.Target == "net10.0" ? AssetsOf($"{Injection}/{Version}", $"{Cel}/{Version}", $"{Abstractions}/10.0.0", $"{Primitives}/10.0.0") : Assets(request));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(EngCodes.Eng210, InjectionPath, 5, 1, $"the restore of {Injection} in a net10.0 application does not add package '{Options}' of the closure list"));
    }

    [Fact]
    public void Versions_TheCaseOfIds_ThePackageItselfAndProjects_AreNotCompared()
    {
        using TempDirectory root = Repository(Injection);

        List<Finding> findings = Check(
            root,
            request => request.Target == "net10.0"
                ? AssetsOf(
                    $"{Injection.ToUpperInvariant()}/9.9.9",
                    $"{Cel.ToUpperInvariant()}/0.0.1",
                    "microsoft.extensions.dependencyinjection.abstractions/10.0.0",
                    $"{Options}/10.0.5",
                    $"{Primitives}/10.0.5",
                    "Some.Project/1.0.0|project")
                : Assets(request));

        FindingAssert.None(findings);
    }

    [Fact]
    public void EmptyClosureLine_MeansThatTheRestoreAddsNothingBesidesThePackage()
    {
        using TempDirectory root = Repository(Cel);

        List<Finding> findings = Check(root, _ => AssetsOf($"{Cel}/{Version}", $"{Abstractions}/10.0.0"));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(EngCodes.Eng209, CelPath, 5, 1, $"the restore of {Cel} in a net10.0 application adds package '{Abstractions}', which is not in the closure list"),
            FindingAssert.Error(EngCodes.Eng209, CelPath, 6, 1, $"the restore of {Cel} in a net11.0 application adds package '{Abstractions}', which is not in the closure list"));
    }

    [Fact]
    public void Eng200_ClosureLineThatDoesNotParse_IsReportedAndTheListIsNotRestored()
    {
        using TempDirectory root = Repository(Cel);
        root.Write(CelPath, "closure net10.0: Some.Package,, Other.Package\n");
        List<ClosureRequest> requests = [];

        List<Finding> findings = Check(root, request => Restore(requests, request));

        FindingAssert.Exactly(findings, FindingAssert.Error(EngCodes.Eng200, CelPath, 1, 1, "the packages of the closure of 'net10.0' are not a list of package IDs separated by commas"));
        Assert.Empty(requests);
    }

    [Fact]
    public void Eng200_TargetOfTheDependenciesWithoutAClosureLine_IsReportedAtTheFirstLine()
    {
        using TempDirectory root = Repository(Cel);
        root.Write(CelPath, "# a comment\ndependencies net10.0:\ndependencies net11.0:\nclosure net10.0:\n");
        List<ClosureRequest> requests = [];

        List<Finding> findings = Check(root, request => Restore(requests, request));

        FindingAssert.Exactly(
            findings,
            FindingAssert.Error(EngCodes.Eng200, CelPath, 1, 1, "the list has a 'dependencies' line for 'net11.0' and no 'closure' line for it: 'closure net11.0: <id>, <id>, ...'"));
        Assert.Empty(requests);
    }

    [Fact]
    public void PackageWithoutAList_IsNotRestored()
    {
        using TempDirectory root = Repository(Cel);
        Pack(root, "Great.Parts.Cel.Extra");
        List<ClosureRequest> requests = [];

        List<Finding> findings = Check(root, request => Restore(requests, request));

        FindingAssert.None(findings);
        Assert.DoesNotContain(requests, request => request.Id == "Great.Parts.Cel.Extra");
    }

    [Fact]
    public void ListWithoutAPackage_IsNotRestored()
    {
        using TempDirectory root = Repository(Cel);
        root.Write(InjectionPath, InjectionList + Newline);
        List<ClosureRequest> requests = [];

        List<Finding> findings = Check(root, request => Restore(requests, request));

        FindingAssert.None(findings);
        Assert.DoesNotContain(requests, request => request.Id == Injection);
    }

    [Fact]
    public void NoPackage_IsExitCodeTwoAndTheMessageNamesTheCommandThatPacks()
    {
        using TempDirectory root = new();
        root.Write(CelPath, CelList + Newline);

        Result result = Run(root, Assets);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Equal("eng/checks: artifacts/packages has no package; the command 'packages' packs them" + Newline, result.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"libraries":[]}""")]
    public static void AssetsThatAreNotAnAssetsFile_AreExitCodeTwo(string assets)
    {
        using TempDirectory root = Repository(Cel);

        Result result = Run(root, _ => assets);

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("eng/checks: the assets file ", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageIds_AreThePackagesOfTheLibrariesWithoutVersions()
    {
        const string Json = """
            {
              "version": 3,
              "libraries": {
                "A.B/1.0.0": { "type": "package", "sha512": "x" },
                "Some.Project/1.0.0": { "type": "project", "path": "../x.csproj" },
                "C/2.0.0-beta.1": { "type": "package" }
              },
              "projectFileDependencyGroups": { "net10.0": [ "A.B >= 1.0.0" ] }
            }
            """;

        Assert.Equal(["A.B", "C"], ClosureRestore.PackageIds(Json));
    }

    [Fact]
    public void TheCommand_IsInTheRegistryOfTheApplication()
    {
        Assert.Contains(CommandRegistry.All, command => command is PackageClosureCommand && command.Name == "package-closure");
    }

    // A temporary repository root with a package and the list of each id (one of the constants above).
    private static TempDirectory Repository(params string[] ids)
    {
        TempDirectory root = new();
        foreach (string id in ids)
        {
            Pack(root, id);
            root.Write($"eng/packages/{id}.txt", ListOf(id) + Newline);
        }

        return root;
    }

    private static string ListOf(string id) => id switch
    {
        Cel => CelList,
        Injection => InjectionList,
        _ => AspNetCoreList,
    };

    // artifacts/packages/<id>.<version>.nupkg with a .nuspec that has the id and the version.
    private static void Pack(TempDirectory root, string id)
    {
        string path = Path.Combine(root.Path, "artifacts", "packages", $"{id}.{Version}.nupkg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        ZipArchiveEntry entry = archive.CreateEntry($"{id}.nuspec");
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(
            $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">\n<metadata>\n<id>{id}</id>\n<version>{Version}</version>\n</metadata>\n</package>\n");
    }

    // The restore of a package as the lists above expect it: the package itself and the packages of its closure line of the target.
    private static string Assets(ClosureRequest request)
    {
        string[] closure = (request.Id, request.Target) switch
        {
            (Cel, _) => [],
            (Injection, "net10.0") => [Cel, Abstractions, Options, Primitives],
            (Injection, _) => [Cel],
            _ => [Cel, Injection],
        };
        return AssetsOf([$"{request.Id}/{request.Version}", .. closure.Select(id => $"{id}/{request.Version}")]);
    }

    private static string Restore(List<ClosureRequest> requests, ClosureRequest request)
    {
        requests.Add(request);
        Assert.True(Directory.Exists(request.Directory), "the directory of the application exists when the restore starts");
        Assert.Empty(Directory.EnumerateFileSystemEntries(request.Directory));
        return Assets(request);
    }

    // The text of a project.assets.json with the libraries '<id>/<version>' of type package; a name that ends with '|project' is a project.
    private static string AssetsOf(params string[] libraries)
    {
        IEnumerable<string> entries = libraries.Select(library =>
        {
            string[] parts = library.Split('|');
            return $"\"{parts[0]}\": {{ \"type\": \"{(parts.Length > 1 ? parts[1] : "package")}\" }}";
        });
        return "{ \"version\": 3, \"libraries\": { " + string.Join(", ", entries) + " } }";
    }

    private static List<Finding> Check(TempDirectory root, Func<ClosureRequest, string> restore)
    {
        CheckContext context = new(root.Path, new Dictionary<string, string>());
        return [.. new PackageClosureCommand(restore).Run(context)];
    }

    private static Result Run(TempDirectory root, Func<ClosureRequest, string> restore)
    {
        using StringWriter output = new() { NewLine = Newline };
        using StringWriter error = new() { NewLine = Newline };
        int exitCode = Program.Run(["package-closure", "--root", root.Path], [new PackageClosureCommand(restore)], root.Path, output, error);
        return new Result(exitCode, output.ToString(), error.ToString());
    }

    private sealed record Result(int ExitCode, string Output, string Error);
}
