using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks what the restore of each package adds to an application against the <c>closure</c> lines of the lists
/// <c>eng/packages/&lt;package id&gt;.txt</c> (<c>docs/design/eng-checks-artifacts.md</c>, "package-closure"). For every package of
/// <c>artifacts/packages</c> that has a list, and for every target of its <c>closure</c> lines, it restores a console application
/// with one reference to the package from <c>artifacts/packages</c> and nuget.org, and compares the packages of the restore, without
/// the package itself and without versions, with the line. A package with a <c>browser</c> line is restored once more into a
/// <c>browser-wasm</c> application of <c>net11.0</c>, and the assembly that this restore takes must be the one in
/// <c>runtimes/browser-wasm/lib/net11.0/</c> (<c>ENG212</c>). It does not pack: <c>packages</c> does, in the same invocation or before.
/// </summary>
internal sealed class PackageClosureCommand : ICheckCommand
{
    private const string WebFramework = "Microsoft.AspNetCore.App";
    private const string BrowserTarget = "net11.0";
    private const string BrowserRuntime = "browser-wasm";
    private const string BrowserFolder = "runtimes/browser-wasm/lib/net11.0/";

    private readonly Func<ClosureRequest, string> _restore;

    /// <summary>Creates the command that restores with <c>dotnet restore</c>.</summary>
    public PackageClosureCommand()
        : this(ClosureRestore.Run)
    {
    }

    /// <summary>Creates the command with another restore; the tests give the text of <c>project.assets.json</c> themselves.</summary>
    /// <param name="restore">Restores the application of the request and returns the text of its <c>obj/project.assets.json</c>.</param>
    internal PackageClosureCommand(Func<ClosureRequest, string> restore) => _restore = restore;

    /// <inheritdoc/>
    public string Name => "package-closure";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        List<PackageContent> packages = [.. PackagesCommand.ReadPackages(context.Root)];
        if (packages.Count == 0)
        {
            throw new CheckInputException($"{PackagesCommand.PackagesDirectory} has no package; the command 'packages' packs them");
        }

        List<PackageList> lists = [.. PackagesCommand.ReadLists(context.Root)];
        List<Finding> findings = [.. PackagesCommand.NewProblems(context, lists)];
        using ScratchDirectory scratch = new("gpc-closure-");
        string source = Path.Combine(context.Root, PackagesCommand.PackagesDirectory);
        string cache = Path.Combine(scratch.Path, "packages");
        foreach (PackageContent package in packages)
        {
            // A package without a list is ENG205 of 'packages'; a list with a line that did not parse is not compared, as in 'packages'.
            PackageList? list = lists.Find(candidate => string.Equals(candidate.Id, package.Id, StringComparison.OrdinalIgnoreCase));
            if (list is null || list.Problems.Count > 0)
            {
                continue;
            }

            foreach (PackageListLine closure in list.Closures)
            {
                string directory = Path.Combine(scratch.Path, $"{package.Id}-{closure.Target}");
                Directory.CreateDirectory(directory);
                bool web = list.Frameworks.Any(line => line.Target == closure.Target && line.Names.Contains(WebFramework, StringComparer.OrdinalIgnoreCase));
                string assets = _restore(new ClosureRequest(package.Id, package.Version, closure.Target, web, directory, source, cache, context.Root));
                findings.AddRange(Compare(list, closure, package.Id, ClosureRestore.PackageIds(assets)));
            }

            findings.AddRange(CheckBrowserAssemblies(list, package, new ClosureRequest(package.Id, package.Version, BrowserTarget, false, string.Empty, source, cache, context.Root, BrowserRuntime), scratch.Path));
        }

        return findings;
    }

    // A package with a browser line whose list names assemblies in runtimes/browser-wasm/lib/net11.0/ is restored once more, into an
    // application of net11.0 with the runtime identifier browser-wasm: the runtime assets of the package in that target must be those
    // assemblies, not the ones of lib/net11.0/ (ENG212). The packages of this restore are not compared: the closure lines did that.
    private IEnumerable<Finding> CheckBrowserAssemblies(PackageList list, PackageContent package, ClosureRequest template, string scratch)
    {
        string[] expected = [.. list.Files.Keys.Where(file => file.StartsWith(BrowserFolder, StringComparison.Ordinal) && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)];
        if (list.BrowserLine == 0 || expected.Length == 0)
        {
            return [];
        }

        string directory = Path.Combine(scratch, $"{package.Id}-{BrowserTarget}-{BrowserRuntime}");
        Directory.CreateDirectory(directory);
        string assets = _restore(template with { Directory = directory });
        List<string> actual = [.. ClosureRestore.RuntimeAssets(assets, $"{BrowserTarget}/{BrowserRuntime}", package.Id)];
        int line = list.Closures.FirstOrDefault(closure => closure.Target == BrowserTarget)?.Line ?? list.BrowserLine;
        string where = $"the restore of {package.Id} in a {BrowserTarget} application with the runtime identifier {BrowserRuntime}";
        return expected
            .Where(file => !actual.Contains(file))
            .Select(file => new Finding(
                list.Path,
                line,
                1,
                FindingSeverity.Error,
                EngCodes.Eng212,
                actual.Count == 0
                    ? $"{where} takes no assembly of the package, not '{file}'"
                    : $"{where} takes {string.Join(", ", actual.Select(path => $"'{path}'"))} of the package, not '{file}'"));
    }

    // The packages of the restore without the package itself against the closure line: an extra one is ENG209, a missing one ENG210.
    private static IEnumerable<Finding> Compare(PackageList list, PackageListLine closure, string id, IReadOnlyList<string> restored)
    {
        HashSet<string> actual = new(restored.Where(name => !string.Equals(name, id, StringComparison.OrdinalIgnoreCase)), StringComparer.OrdinalIgnoreCase);
        HashSet<string> named = new(closure.Names, StringComparer.OrdinalIgnoreCase);
        string where = $"the restore of {id} in a {closure.Target} application";
        foreach (string name in actual.Where(name => !named.Contains(name)).Order(StringComparer.Ordinal))
        {
            yield return new Finding(list.Path, closure.Line, 1, FindingSeverity.Error, EngCodes.Eng209, $"{where} adds package '{name}', which is not in the closure list");
        }

        foreach (string name in closure.Names.Where(name => !actual.Contains(name)))
        {
            yield return new Finding(list.Path, closure.Line, 1, FindingSeverity.Error, EngCodes.Eng210, $"{where} does not add package '{name}' of the closure list");
        }
    }
}
