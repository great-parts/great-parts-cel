using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks the packages of the solution against the lists <c>eng/packages/&lt;package id&gt;.txt</c>
/// (<c>docs/design/eng-checks-artifacts.md</c>, "packages"). It packs the solution into <c>artifacts/packages</c>, reads every
/// <c>.nupkg</c> there with <see cref="PackageContent"/> and every list with <see cref="PackageList"/>, and pairs them by the
/// package ID. With <c>--no-build</c> it packs the Release build that already exists. A package of an older run in
/// <c>artifacts/packages</c> is a package without a list: <c>eng/run</c> deletes the directory before a run.
/// </summary>
internal sealed class PackagesCommand : ICheckCommand
{
    /// <summary>The flag of the command line: pack the Release build that already exists.</summary>
    internal const string NoBuildOption = "--no-build";

    /// <summary>The directory of the packages, relative to the repository root.</summary>
    internal const string PackagesDirectory = "artifacts/packages";

    /// <summary>The directory of the lists, relative to the repository root.</summary>
    internal const string ListsDirectory = "eng/packages";

    private readonly Action<string, bool> _pack;

    /// <summary>Creates the command that packs with <c>dotnet pack</c>.</summary>
    public PackagesCommand()
        : this(DotnetPack.Run)
    {
    }

    /// <summary>Creates the command with another way to pack; the tests make the packages themselves.</summary>
    /// <param name="pack">Packs the solution; gets the repository root and whether the build is skipped.</param>
    internal PackagesCommand(Action<string, bool> pack) => _pack = pack;

    /// <inheritdoc/>
    public string Name => "packages";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        _pack(context.Root, context.Options.ContainsKey(NoBuildOption));

        List<PackageContent> packages = [.. ReadPackages(context.Root)];
        List<PackageList> lists = [.. ReadLists(context.Root)];
        List<Finding> findings = [.. lists.SelectMany(list => list.Problems)];
        foreach (PackageContent package in packages)
        {
            PackageList? list = lists.Find(candidate => string.Equals(candidate.Id, package.Id, StringComparison.OrdinalIgnoreCase));
            if (list is null)
            {
                findings.Add(new Finding(
                    package.Path,
                    1,
                    1,
                    FindingSeverity.Error,
                    EngCodes.Eng205,
                    $"the package '{package.Id}' has no list {ListsDirectory}/{package.Id}.txt"));
            }
            else if (list.Problems.Count == 0)
            {
                // A list with a line that does not parse is not compared: the missing line would show as differences of the package.
                findings.AddRange(PackageComparison.Compare(list, package));
            }
        }

        foreach (PackageList list in lists.Where(list => !packages.Exists(package => string.Equals(package.Id, list.Id, StringComparison.OrdinalIgnoreCase))))
        {
            findings.Add(new Finding(
                list.Path,
                1,
                1,
                FindingSeverity.Error,
                EngCodes.Eng206,
                $"the solution has no package '{list.Id}' for this list"));
        }

        return findings;
    }

    private static IEnumerable<PackageContent> ReadPackages(string root)
    {
        string directory = Path.Combine(root, PackagesDirectory);
        if (!Directory.Exists(directory))
        {
            // Nothing was packed: every list is then a list without a package.
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.nupkg").Order(StringComparer.Ordinal))
        {
            yield return PackageContent.Read(file, $"{PackagesDirectory}/{Path.GetFileName(file)}");
        }
    }

    private static IEnumerable<PackageList> ReadLists(string root)
    {
        string directory = Path.Combine(root, ListsDirectory);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(directory, "*.txt").Order(StringComparer.Ordinal))
        {
            yield return PackageList.Parse($"{ListsDirectory}/{Path.GetFileName(file)}", File.ReadAllText(file));
        }
    }
}
