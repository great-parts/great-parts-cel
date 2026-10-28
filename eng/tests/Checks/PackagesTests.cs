using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks packages` (docs/design/eng-checks-artifacts.md, "packages"). Every test makes a package with System.IO.Compression
// and a list in a temporary directory; `dotnet pack` is replaced by a command that does nothing, so the tests read the files that
// they wrote.
public sealed class PackagesTests
{
    private const string Newline = "\n";
    private const string CelList = """
        # Expected content of the Great.Parts.Cel package.
        dependencies net10.0:
        dependencies net11.0:

        license Apache-2.0

        file NOTICE
        file lib/net10.0/Great.Parts.Cel.dll
        file lib/net11.0/Great.Parts.Cel.dll
        file analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll
        """;
    private const string ProtobufList = """
        # Expected content of the Great.Parts.Cel.Protobuf package.
        # Each dependency group lists exactly Great.Parts.Cel and Google.Protobuf.

        dependencies net10.0: Great.Parts.Cel, Google.Protobuf
        dependencies net11.0: Great.Parts.Cel, Google.Protobuf

        license Apache-2.0

        file NOTICE
        file lib/net10.0/Great.Parts.Cel.Protobuf.dll
        file lib/net11.0/Great.Parts.Cel.Protobuf.dll
        """;
    private const string CelListPath = "eng/packages/Great.Parts.Cel.txt";
    private const string ProtobufListPath = "eng/packages/Great.Parts.Cel.Protobuf.txt";
    private static readonly string[] CelFiles =
        ["NOTICE", "lib/net10.0/Great.Parts.Cel.dll", "lib/net11.0/Great.Parts.Cel.dll", "analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll"];
    private static readonly string[] ProtobufFiles =
        ["NOTICE", "lib/net10.0/Great.Parts.Cel.Protobuf.dll", "lib/net11.0/Great.Parts.Cel.Protobuf.dll"];

    [Fact]
    public void PackagesThatMatchTheirLists_HaveNoFindings()
    {
        using TempDirectory root = Repository(CelList, ProtobufList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])));
        Pack(root, "Great.Parts.Cel.Protobuf", ProtobufFiles, ProtobufGroups());

        FindingAssert.None(Check(root));
    }

    [Fact]
    public void FormatEntriesVersionsAndDirectoryEntries_AreNotCompared()
    {
        using TempDirectory root = Repository(CelList);
        Pack(
            root,
            "Great.Parts.Cel",
            [.. CelFiles, "lib/", "package/services/metadata/core-properties/other.psmdcp", "_rels/other.rels"],
            Groups(("net10.0", []), ("net11.0", [])),
            version: "7.8.9-beta.1");

        FindingAssert.None(Check(root));
    }

    [Fact]
    public void Eng200_LinesThatDoNotParse_AreReportedAtTheLine()
    {
        using TempDirectory root = Repository(
            """
            # a comment

            nonsense line
            dependencies net10.0 Great.Parts.Cel
            dependencies : Great.Parts.Cel
            dependencies net10.0: Great.Parts.Cel,, Google.Protobuf
            dependencies net11.0: Great.Parts.Cel
            dependencies net11.0: Google.Protobuf
            license
            license Apache-2.0
            license MIT
            file
            file NOTICE
            """);
        Pack(root, "Great.Parts.Cel", ["NOTICE"], Groups(("net11.0", ["Great.Parts.Cel"])));

        FindingAssert.Exactly(
            Check(root),
            Error(EngCodes.Eng200, CelListPath, 3, "unknown line 'nonsense line'; a line starts with 'dependencies', 'license' or 'file'"),
            Error(EngCodes.Eng200, CelListPath, 4, "a 'dependencies' line needs a colon after the target: 'dependencies <target>: <id>, <id>, ...'"),
            Error(EngCodes.Eng200, CelListPath, 5, "a 'dependencies' line needs one target before the colon: 'dependencies <target>: <id>, <id>, ...'"),
            Error(EngCodes.Eng200, CelListPath, 6, "the dependencies of 'net10.0' are not a list of package IDs separated by commas"),
            Error(EngCodes.Eng200, CelListPath, 8, "the group 'net11.0' is already in the list"),
            Error(EngCodes.Eng200, CelListPath, 9, "a 'license' line needs an expression: 'license <expression>'"),
            Error(EngCodes.Eng200, CelListPath, 11, "the list has more than one 'license' line"),
            Error(EngCodes.Eng200, CelListPath, 12, "a 'file' line needs a path in the package: 'file <path>'"));
    }

    [Fact]
    public void Eng200_ListWithAProblem_IsNotComparedWithItsPackage()
    {
        using TempDirectory root = Repository("license Apache-2.0\nbad\nfile NOTICE\n");
        Pack(root, "Great.Parts.Cel", ["NOTICE", "lib/extra.dll"], Groups(("net10.0", ["System.Memory"])));

        FindingAssert.Exactly(
            Check(root),
            Error(EngCodes.Eng200, CelListPath, 2, "unknown line 'bad'; a line starts with 'dependencies', 'license' or 'file'"));
    }

    [Fact]
    public void Eng201_ExtraDependencyInAGroup_IsReportedAtTheGroupLine()
    {
        using TempDirectory root = Repository(CelList, ProtobufList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])));
        Pack(
            root,
            "Great.Parts.Cel.Protobuf",
            ProtobufFiles,
            Groups(
                ("net10.0", ["Great.Parts.Cel", "Google.Protobuf", "System.Memory"]),
                ("net11.0", ["Great.Parts.Cel", "Google.Protobuf"])));

        List<Finding> findings = Check(root);

        FindingAssert.Exactly(findings, Error(EngCodes.Eng201, ProtobufListPath, 4, "unexpected dependency 'System.Memory' in group net10.0"));
    }

    [Fact]
    public void Eng201_TheExampleOfTheDesignDocument_IsTheLineOfTheOutput()
    {
        using TempDirectory root = Repository(CelList, ProtobufList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])));
        Pack(
            root,
            "Great.Parts.Cel.Protobuf",
            ProtobufFiles,
            Groups(
                ("net10.0", ["Great.Parts.Cel", "Google.Protobuf", "System.Memory"]),
                ("net11.0", ["Great.Parts.Cel", "Google.Protobuf"])));

        Result result = Run(root);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(
            "eng/packages/Great.Parts.Cel.Protobuf.txt(4,1): error ENG201: unexpected dependency 'System.Memory' in group net10.0" + Newline,
            result.Output);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Eng201_GroupThatTheListDoesNotName_IsReportedAtTheFirstLine()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", []), ("net9.0", ["System.Memory"]), ("net8.0", [])));

        FindingAssert.Exactly(
            Check(root),
            Error(EngCodes.Eng201, CelListPath, 1, "unexpected dependency 'System.Memory' in group net9.0, which the list does not name"),
            Error(EngCodes.Eng201, CelListPath, 1, "unexpected dependency group net8.0, which the list does not name"));
    }

    [Fact]
    public void Eng202_MissingDependencyAndMissingGroup_AreReportedAtTheGroupLine()
    {
        using TempDirectory root = Repository(CelList, ProtobufList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", [])));
        Pack(root, "Great.Parts.Cel.Protobuf", ProtobufFiles, Groups(("net10.0", ["Great.Parts.Cel"]), ("net11.0", ["Google.Protobuf"])));

        FindingAssert.Exactly(
            Check(root),
            Error(EngCodes.Eng202, CelListPath, 3, "the package has no dependency group 'net11.0'"),
            Error(EngCodes.Eng202, ProtobufListPath, 4, "missing dependency 'Google.Protobuf' in group net10.0"),
            Error(EngCodes.Eng202, ProtobufListPath, 5, "missing dependency 'Great.Parts.Cel' in group net11.0"));
    }

    [Fact]
    public void Eng203_EntryThatTheListDoesNotName_IsReportedAtTheFirstLineAndNamesTheEntry()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "Great.Parts.Cel", [.. CelFiles, "lib/net10.0/Extra.dll", "content/readme.txt"], Groups(("net10.0", []), ("net11.0", [])));

        FindingAssert.Exactly(
            Check(root),
            Error(EngCodes.Eng203, CelListPath, 1, "the package has the file 'content/readme.txt', which the list does not name"),
            Error(EngCodes.Eng203, CelListPath, 1, "the package has the file 'lib/net10.0/Extra.dll', which the list does not name"));
    }

    [Fact]
    public void Eng204_ListedFileThatThePackageLacks_IsReportedAtTheFileLine()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "Great.Parts.Cel", ["NOTICE", "lib/net11.0/Great.Parts.Cel.dll"], Groups(("net10.0", []), ("net11.0", [])));

        FindingAssert.Exactly(
            Check(root),
            Error(EngCodes.Eng204, CelListPath, 8, "the file 'lib/net10.0/Great.Parts.Cel.dll' of the list is not in the package"),
            Error(EngCodes.Eng204, CelListPath, 10, "the file 'analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll' of the list is not in the package"));
    }

    [Fact]
    public void Eng205_PackageWithoutAList_IsReportedAtThePathOfThePackage()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])));
        Pack(root, "Great.Parts.Cel.Extra", ["NOTICE"], Groups());

        FindingAssert.Exactly(
            Check(root),
            Error(
                EngCodes.Eng205,
                "artifacts/packages/Great.Parts.Cel.Extra.0.0.0-dev.nupkg",
                1,
                "the package 'Great.Parts.Cel.Extra' has no list eng/packages/Great.Parts.Cel.Extra.txt"));
    }

    [Fact]
    public void Eng205_WithoutTheDirectoryOfLists_EveryPackageHasNoList()
    {
        using TempDirectory root = new();
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", [])));

        FindingAssert.Exactly(
            Check(root),
            Error(
                EngCodes.Eng205,
                "artifacts/packages/Great.Parts.Cel.0.0.0-dev.nupkg",
                1,
                "the package 'Great.Parts.Cel' has no list eng/packages/Great.Parts.Cel.txt"));
    }

    [Fact]
    public void Eng206_ListWithoutAPackage_IsReportedAtTheFirstLineOfTheList()
    {
        using TempDirectory root = Repository(CelList, ProtobufList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])));

        FindingAssert.Exactly(Check(root), Error(EngCodes.Eng206, ProtobufListPath, 1, "the solution has no package 'Great.Parts.Cel.Protobuf' for this list"));
    }

    [Fact]
    public void Eng206_NothingPacked_EveryListHasNoPackage()
    {
        using TempDirectory root = Repository(CelList);

        FindingAssert.Exactly(Check(root), Error(EngCodes.Eng206, CelListPath, 1, "the solution has no package 'Great.Parts.Cel' for this list"));
    }

    [Fact]
    public void Eng207_LicenseThatDiffersFromTheList_IsReportedAtTheLicenseLine()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])), license: "MIT");

        FindingAssert.Exactly(Check(root), Error(EngCodes.Eng207, CelListPath, 5, "the license expression 'MIT' differs from 'Apache-2.0' of the list"));
    }

    [Fact]
    public void Eng207_PackageWithoutALicenseExpression_IsReportedAtTheLicenseLine()
    {
        using TempDirectory withoutLicense = Repository(CelList);
        Pack(withoutLicense, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])), license: null);
        using TempDirectory licenseFile = Repository(CelList);
        Pack(licenseFile, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])), license: "Apache-2.0", licenseType: "file");

        string message = "the package has no license expression; the list expects 'Apache-2.0'";
        FindingAssert.Exactly(Check(withoutLicense), Error(EngCodes.Eng207, CelListPath, 5, message));
        FindingAssert.Exactly(Check(licenseFile), Error(EngCodes.Eng207, CelListPath, 5, message));
    }

    [Fact]
    public void Eng207_ListWithoutALicenseLine_IsReportedAtTheFirstLine()
    {
        using TempDirectory root = Repository("dependencies net10.0:\nfile NOTICE\n");
        Pack(root, "Great.Parts.Cel", ["NOTICE"], Groups(("net10.0", [])));

        FindingAssert.Exactly(Check(root), Error(EngCodes.Eng207, CelListPath, 1, "the list has no 'license' line"));
    }

    [Fact]
    public void EveryLineOfTheOutput_MatchesTheFindingPattern()
    {
        using TempDirectory root = Repository(CelList, ProtobufList);
        Pack(root, "Great.Parts.Cel", [.. CelFiles, "extra.txt"], Groups(("net10.0", ["System.Memory"])), license: "MIT");

        Result result = Run(root);

        string[] lines = result.Output.Split(Newline, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        foreach (string line in lines)
        {
            Assert.Matches(Finding.Pattern, line);
        }
    }

    [Fact]
    public void ListIdAndPackageId_AreComparedWithoutRegardToCase()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "GREAT.PARTS.CEL", CelFiles, Groups(("net10.0", []), ("net11.0", [])));

        FindingAssert.None(Check(root));
    }

    [Fact]
    public void NoBuildOption_IsPassedToThePack()
    {
        using TempDirectory root = Repository(CelList);
        Pack(root, "Great.Parts.Cel", CelFiles, Groups(("net10.0", []), ("net11.0", [])));
        List<(string Root, bool NoBuild)> calls = [];
        PackagesCommand command = new((packRoot, noBuild) => calls.Add((packRoot, noBuild)));

        using StringWriter output = new();
        using StringWriter error = new();
        int withFlag = Program.Run(["packages", "--no-build", "--root", root.Path], [command], root.Path, output, error);
        int withoutFlag = Program.Run(["--root", root.Path, "packages"], [command], root.Path, output, error);

        Assert.Equal([0, 0], [withFlag, withoutFlag]);
        Assert.Equal([(root.Path, true), (root.Path, false)], calls);
    }

    [Fact]
    public void FailedPack_ExitCodeIsTwoAndTheMessageNamesTheCommand()
    {
        using TempDirectory root = new();
        using StringWriter output = new();
        using StringWriter error = new() { NewLine = Newline };

        // The directory has no solution file, so `dotnet pack` fails without building anything.
        int exitCode = Program.Run(["packages", "--root", root.Path], CommandRegistry.All, root.Path, output, error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.StartsWith("eng/checks: 'dotnet pack Great.Parts.Cel.slnx -c Release -o artifacts/packages' failed with the exit code ", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PackageThatIsNotAnArchive_ExitCodeIsTwo()
    {
        using TempDirectory root = Repository(CelList);
        root.Write("artifacts/packages/Broken.0.0.0-dev.nupkg", "not a zip archive");
        using StringWriter output = new();
        using StringWriter error = new() { NewLine = Newline };

        int exitCode = Program.Run(["packages", "--root", root.Path], [new PackagesCommand((_, _) => { })], root.Path, output, error);

        Assert.Equal(2, exitCode);
        Assert.StartsWith("eng/checks: the package artifacts/packages/Broken.0.0.0-dev.nupkg cannot be read: ", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void TheCommand_IsInTheRegistryOfTheApplication()
    {
        Assert.Contains(CommandRegistry.All, command => command is PackagesCommand && command.Name == "packages");
    }

    private static Finding Error(string code, string path, int line, string message) => FindingAssert.Error(code, path, line, 1, message);

    private static (string Target, string[] Dependencies)[] ProtobufGroups() =>
        Groups(("net10.0", ["Great.Parts.Cel", "Google.Protobuf"]), ("net11.0", ["Great.Parts.Cel", "Google.Protobuf"]));

    private static (string Target, string[] Dependencies)[] Groups(params (string Target, string[] Dependencies)[] groups) => groups;

    // A temporary repository root with the lists: the first text is Great.Parts.Cel.txt, the second Great.Parts.Cel.Protobuf.txt.
    private static TempDirectory Repository(params string[] lists)
    {
        TempDirectory root = new();
        string[] paths = [CelListPath, ProtobufListPath];
        for (int index = 0; index < lists.Length; index++)
        {
            root.Write(paths[index], lists[index] + Newline);
        }

        return root;
    }

    private static List<Finding> Check(TempDirectory root)
    {
        CheckContext context = new(root.Path, new Dictionary<string, string>());
        return [.. new PackagesCommand((_, _) => { }).Run(context)];
    }

    private static Result Run(TempDirectory root)
    {
        using StringWriter output = new() { NewLine = Newline };
        using StringWriter error = new() { NewLine = Newline };
        int exitCode = Program.Run(["packages", "--root", root.Path], [new PackagesCommand((_, _) => { })], root.Path, output, error);
        return new Result(exitCode, output.ToString(), error.ToString());
    }

    private sealed record Result(int ExitCode, string Output, string Error);

    // Writes artifacts/packages/<id>.<version>.nupkg: the entries of the package format, the files and a .nuspec. An entry that
    // ends with "/" is an entry of a directory.
    private static void Pack(
        TempDirectory root,
        string id,
        string[] files,
        (string Target, string[] Dependencies)[] groups,
        string? license = "Apache-2.0",
        string licenseType = "expression",
        string version = "0.0.0-dev")
    {
        string path = Path.Combine(root.Path, "artifacts", "packages", $"{id}.{version}.nupkg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        AddEntry(archive, "[Content_Types].xml", "<Types />");
        AddEntry(archive, "_rels/.rels", "<Relationships />");
        AddEntry(archive, "package/services/metadata/core-properties/id.psmdcp", "<coreProperties />");
        AddEntry(archive, $"{id}.nuspec", Nuspec(id, version, groups, license, licenseType));
        foreach (string file in files)
        {
            AddEntry(archive, file, file.EndsWith('/') ? string.Empty : "content");
        }
    }

    private static void AddEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Nuspec(string id, string version, (string Target, string[] Dependencies)[] groups, string? license, string licenseType)
    {
        StringBuilder text = new();
        text.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\">\n<metadata>\n");
        text.Append(CultureInfo.InvariantCulture, $"<id>{id}</id>\n<version>{version}</version>\n");
        if (license is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"<license type=\"{licenseType}\">{license}</license>\n");
        }

        text.Append("<dependencies>\n");
        foreach ((string target, string[] dependencies) in groups)
        {
            text.Append(CultureInfo.InvariantCulture, $"<group targetFramework=\"{target}\">\n");
            foreach (string dependency in dependencies)
            {
                text.Append(CultureInfo.InvariantCulture, $"<dependency id=\"{dependency}\" version=\"{version}\" exclude=\"Build,Analyzers\" />\n");
            }

            text.Append("</group>\n");
        }

        text.Append("</dependencies>\n</metadata>\n</package>\n");
        return text.ToString();
    }
}
