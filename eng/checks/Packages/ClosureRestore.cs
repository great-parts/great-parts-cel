using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One restore of <c>package-closure</c>: a package in an application of one target framework.</summary>
/// <param name="Id">The ID of the package.</param>
/// <param name="Version">The version of the package; the restore takes exactly this one.</param>
/// <param name="Target">The target framework of the application, such as <c>net10.0</c>.</param>
/// <param name="Web">Whether the application is a project of <c>Microsoft.NET.Sdk.Web</c>: the package references the shared framework of ASP.NET Core.</param>
/// <param name="Directory">The full path of the directory of the application, which exists and is empty.</param>
/// <param name="Source">The full path of the directory of the packages of the commit, the local source of the restore.</param>
/// <param name="Cache">The full path of the packages folder of the restore: nothing of an earlier run is in it.</param>
/// <param name="Root">The full path of the repository root, whose <c>global.json</c> is copied next to the application.</param>
internal sealed record ClosureRequest(string Id, string Version, string Target, bool Web, string Directory, string Source, string Cache, string Root);

/// <summary>
/// The restore of <c>package-closure</c> (<c>docs/design/eng-checks-artifacts.md</c>, "package-closure"): a console application of one
/// target framework with one <c>PackageReference</c>, restored with <c>dotnet restore</c> from the directory of the packages of the
/// commit and nuget.org. The result is the <c>obj/project.assets.json</c> of the application.
/// </summary>
internal static class ClosureRestore
{
    private const string NuGetOrg = "https://api.nuget.org/v3/index.json";
    private const string ProjectFile = "Closure.csproj";
    private const string AssetsFile = "obj/project.assets.json";

    /// <summary>Writes the application, restores it and returns the text of <c>obj/project.assets.json</c>.</summary>
    /// <param name="request">The package and the target framework.</param>
    /// <exception cref="CheckInputException">The restore did not start, failed, or wrote no assets file.</exception>
    internal static string Run(ClosureRequest request)
    {
        // The application is outside the repository, so no Directory.Build.props or Directory.Packages.props of it applies; the
        // global.json of the repository selects the same SDK as the build.
        string sdk = request.Web ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk";
        string project = $"""
            <Project Sdk="{sdk}">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{request.Target}</TargetFramework>
                <NuGetAudit>false</NuGetAudit>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{request.Id}" Version="[{request.Version}]" />
              </ItemGroup>
            </Project>
            """;
        File.WriteAllText(Path.Combine(request.Directory, ProjectFile), project, new UTF8Encoding(false));

        string output = Restore(request);
        string assets = Path.Combine(request.Directory, AssetsFile);
        return File.Exists(assets)
            ? File.ReadAllText(assets)
            : throw new CheckInputException($"the restore of {request.Id} for {request.Target} wrote no {AssetsFile}: {FirstError(output)}");
    }

    /// <summary>
    /// The IDs of the packages of type <c>package</c> in the <c>libraries</c> of an assets file, without versions and in the order of the file.
    /// </summary>
    /// <param name="assets">The text of <c>project.assets.json</c>.</param>
    /// <exception cref="CheckInputException">The text is not an assets file.</exception>
    internal static IReadOnlyList<string> PackageIds(string assets)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(assets);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("libraries", out JsonElement libraries) || libraries.ValueKind != JsonValueKind.Object)
            {
                throw new CheckInputException("the assets file has no 'libraries' object");
            }

            List<string> ids = [];
            foreach (JsonProperty library in libraries.EnumerateObject())
            {
                if (library.Value.ValueKind == JsonValueKind.Object
                    && library.Value.TryGetProperty("type", out JsonElement type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() == "package")
                {
                    // The name is '<id>/<version>'.
                    int slash = library.Name.IndexOf('/', StringComparison.Ordinal);
                    ids.Add(slash < 0 ? library.Name : library.Name[..slash]);
                }
            }

            return ids;
        }
        catch (JsonException exception)
        {
            throw new CheckInputException($"the assets file cannot be read: {exception.Message}", exception);
        }
    }

    private static string Restore(ClosureRequest request)
    {
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = request.Directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "restore", ProjectFile, "--source", request.Source, "--source", NuGetOrg, "--packages", request.Cache })
        {
            start.ArgumentList.Add(argument);
        }

        string global = Path.Combine(request.Root, "global.json");
        if (File.Exists(global))
        {
            File.Copy(global, Path.Combine(request.Directory, "global.json"), overwrite: true);
        }

        string command = string.Join(' ', start.ArgumentList.Prepend(start.FileName));
        try
        {
            using Process process = Process.Start(start) ?? throw new CheckInputException($"'{command}' did not start");
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            string all = output + "\n" + error.GetAwaiter().GetResult();
            return process.ExitCode == 0
                ? all
                : throw new CheckInputException($"the restore of {request.Id} for {request.Target} failed with the exit code {process.ExitCode}: {FirstError(all)}");
        }
        catch (Win32Exception exception)
        {
            throw new CheckInputException($"'{command}' did not start: {exception.Message}", exception);
        }
    }

    private static string FirstError(string output) => output
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .FirstOrDefault(line => line.Contains(" error ", StringComparison.Ordinal)) ?? "no error line in the output";
}
