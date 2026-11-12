using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>
/// <c>min-sdk</c>: builds the sample application of <c>eng/min-sdk</c> on the .NET SDK 10.0.100 from the packed <c>Great.Parts.Cel</c> and
/// shows that the source generator of the package loads into that compiler (<c>docs/design/eng-test.md</c>, "min-sdk"). The steps run in
/// this process, because they read the output of the build; the build itself is a child process in <c>eng/min-sdk</c>, where the
/// <c>global.json</c> selects the SDK. The command runs from the compiled <c>eng/run</c> in the image with that SDK, which cannot build
/// <c>eng/run</c> for the root <c>global.json</c>.
/// </summary>
internal static class MinSdk
{
    /// <summary>The directory that the command writes under <c>artifacts/</c>; it is deleted before a run.</summary>
    internal const string Output = "artifacts/min-sdk";

    private const string Project = "eng/min-sdk";
    private const string Packages = "artifacts/packages";
    private const string PackagePrefix = "Great.Parts.Cel.";
    private const string Sdk = "10.0.100";
    private const string GeneratorAssembly = "analyzers/dotnet/cs/Great.Parts.Cel.Generator.dll";
    private const string GeneratorType = "Great.Parts.Cel.Generator.CelTypeContextGenerator";

    // The file that the generator creates for the type context of Program.cs, and the line that shows the context was created in it.
    private const string CreatedFile = "SampleContext.g.cs";
    private const string CreatedLine = "partial class SampleContext";

    /// <summary>The steps of the command, in order. A failed step of the first three stops the command, as the next steps need its result.</summary>
    internal static IReadOnlyList<Step> Steps { get; } =
    [
        Step.InProcess("min-sdk: packages", CheckPackages, stopsOnFailure: true),
        Step.InProcess("min-sdk: sdk version", CheckSdk, stopsOnFailure: true),
        Step.InProcess("min-sdk: build", Build, stopsOnFailure: true),
        Step.InProcess("min-sdk: evidence", Evidence),
    ];

    // The package of the library, not of another package of the repository: its name continues with the version, not with a word.
    private static string? CheckPackages()
    {
        bool found = Directory.Exists(Packages) && Directory
            .EnumerateFiles(Packages, PackagePrefix + "*.nupkg")
            .Select(Path.GetFileName)
            .Any(name => name is not null && char.IsAsciiDigit(name[PackagePrefix.Length]));
        return found ? null : $"no package in {Packages}; run the service test first";
    }

    private static string? CheckSdk()
    {
        ProcessResult result = Processes.Run("dotnet", ["--version"], Project, capture: true);
        string version = result.Output.Trim();
        Console.Out.WriteLine(version);
        if (!result.Succeeded)
        {
            return "dotnet --version";
        }

        return version == Sdk ? null : $"sdk version {version}, expected {Sdk}";
    }

    // The console keeps the default verbosity, so the compiler lines are in the output and in console.log; the file logger gets the
    // analyzer report. The build passes when its exit code is 0 and the summary has no warning and no error.
    private static string? Build()
    {
        string output = Path.GetFullPath(Output);
        Directory.CreateDirectory(output);
        ProcessResult result = Processes.Run(
            "dotnet",
            [
                "build",
                "-c",
                "Release",
                "--artifacts-path",
                Path.Combine(output, "build"),
                "-p:ReportAnalyzer=true",
                "-p:EmitCompilerGeneratedFiles=true",
                $"-p:CompilerGeneratedFilesOutputPath={Path.Combine(output, "generated")}",
                $"-flp:LogFile={Path.Combine(output, "build.log")};Verbosity=diagnostic",
            ],
            Project,
            Console.Out);
        File.WriteAllText(Path.Combine(output, "console.log"), result.Output);
        if (!result.Succeeded)
        {
            return "build";
        }

        if (!HasSummaryLine(result.Output, "0 Warning(s)"))
        {
            return "build: warnings";
        }

        return HasSummaryLine(result.Output, "0 Error(s)") ? null : "build: errors";
    }

    // The evidence that the generator loaded: the restore names it as an analyzer of the package, and the analyzer report of the
    // build names its generator type. The lines found are printed with their numbers.
    private static string? Evidence()
    {
        string? assets = Directory.EnumerateFiles(Path.Combine(Output, "build"), "project.assets.json", SearchOption.AllDirectories).FirstOrDefault();
        if (assets is null || !PrintMatches(assets, GeneratorAssembly))
        {
            return "evidence: generator not in project.assets.json";
        }

        if (!PrintMatches(Path.Combine(Output, "build.log"), GeneratorType))
        {
            return "evidence: generator not in the analyzer report";
        }

        return CreatedFileInTheBuild() ? null : "evidence: created file not in the build";
    }

    // The file that went into the assembly: the compiler writes the files that generators create under the directory of
    // CompilerGeneratedFilesOutputPath, in a directory named for the generator. A build that created no file has no such file, and the
    // text of the file shows it is the context of the application and not another file of that name.
    private static bool CreatedFileInTheBuild()
    {
        string directory = Path.Combine(Output, "generated");
        if (!Directory.Exists(directory))
        {
            return false;
        }

        foreach (string file in Directory.EnumerateFiles(directory, CreatedFile, SearchOption.AllDirectories))
        {
            if (PrintMatches(file, CreatedLine))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSummaryLine(string output, string text) =>
        output.ReplaceLineEndings("\n").Split('\n').Any(line => line.TrimStart(' ').StartsWith(text, StringComparison.Ordinal));

    private static bool PrintMatches(string file, string text)
    {
        bool found = false;
        int number = 0;
        foreach (string line in File.ReadLines(file))
        {
            number++;
            if (line.Contains(text, StringComparison.Ordinal))
            {
                Console.Out.WriteLine($"{number}:{line}");
                found = true;
            }
        }

        return found;
    }
}
