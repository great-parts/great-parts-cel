using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A dependency group of a <c>.nuspec</c>.</summary>
/// <param name="Target">The <c>targetFramework</c> attribute as the <c>.nuspec</c> writes it; empty for dependencies outside a group.</param>
/// <param name="Dependencies">The IDs of the dependencies.</param>
internal sealed record PackageGroup(string Target, IReadOnlyList<string> Dependencies);

/// <summary>
/// What the check reads of a <c>.nupkg</c> (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the entries of the archive and,
/// from the <c>.nuspec</c>, the package ID, the dependency groups and the license expression. Versions are not read.
/// </summary>
internal sealed class PackageContent
{
    private PackageContent(string path, string id, IReadOnlyList<string> entries, IReadOnlyList<PackageGroup> groups, string? license)
    {
        Path = path;
        Id = id;
        Entries = entries;
        Groups = groups;
        License = license;
    }

    /// <summary>The path of the <c>.nupkg</c>, relative to the repository root, with <c>/</c>.</summary>
    internal string Path { get; }

    /// <summary>The ID of the package from the <c>.nuspec</c>.</summary>
    internal string Id { get; }

    /// <summary>
    /// The files of the package with <c>/</c> in the paths, as the archive has them. The entries of the package format —
    /// <c>[Content_Types].xml</c>, <c>_rels/</c>, <c>package/</c> and the <c>.nuspec</c> — and directory entries are not in it.
    /// </summary>
    internal IReadOnlyList<string> Entries { get; }

    /// <summary>The dependency groups of the <c>.nuspec</c> in its order.</summary>
    internal IReadOnlyList<PackageGroup> Groups { get; }

    /// <summary>The text of <c>&lt;license type="expression"&gt;</c>, or <see langword="null"/> when the <c>.nuspec</c> has none.</summary>
    internal string? License { get; }

    /// <summary>Reads a package.</summary>
    /// <param name="fullPath">The full path of the <c>.nupkg</c>.</param>
    /// <param name="path">The path of the <c>.nupkg</c> relative to the repository root, with <c>/</c>.</param>
    /// <exception cref="CheckInputException">The file is not an archive of a package with a readable <c>.nuspec</c>.</exception>
    internal static PackageContent Read(string fullPath, string path)
    {
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(fullPath);
            ZipArchiveEntry nuspec = archive.Entries.FirstOrDefault(entry => IsNuspec(entry.FullName))
                ?? throw new CheckInputException($"the package {path} has no .nuspec");
            XDocument document;
            using (Stream stream = nuspec.Open())
            {
                document = XDocument.Load(stream);
            }

            XElement metadata = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName == "metadata")
                ?? throw new CheckInputException($"the .nuspec of the package {path} has no metadata");
            string id = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "id")?.Value.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                throw new CheckInputException($"the .nuspec of the package {path} has no package id");
            }

            string[] entries =
            [
                .. archive.Entries
                    .Select(entry => entry.FullName)
                    .Where(name => !name.EndsWith('/') && !IsFormatEntry(name))
                    .Order(StringComparer.Ordinal),
            ];
            return new PackageContent(path, id, entries, ReadGroups(metadata), ReadLicense(metadata));
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException)
        {
            throw new CheckInputException($"the package {path} cannot be read: {exception.Message}", exception);
        }
    }

    private static bool IsNuspec(string name) => !name.Contains('/', StringComparison.Ordinal) && name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase);

    private static bool IsFormatEntry(string name) =>
        IsNuspec(name)
        || name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("_rels/", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("package/", StringComparison.OrdinalIgnoreCase);

    // The namespace of a .nuspec depends on the version of NuGet that wrote it, so elements are matched by their local names.
    private static List<PackageGroup> ReadGroups(XElement metadata)
    {
        List<PackageGroup> groups = [];
        XElement? dependencies = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "dependencies");
        if (dependencies is null)
        {
            return groups;
        }

        List<string> ungrouped = [.. IdsOf(dependencies)];
        if (ungrouped.Count > 0)
        {
            groups.Add(new PackageGroup(string.Empty, ungrouped));
        }

        foreach (XElement group in dependencies.Elements().Where(element => element.Name.LocalName == "group"))
        {
            string target = group.Attribute("targetFramework")?.Value.Trim() ?? string.Empty;
            groups.Add(new PackageGroup(target, [.. IdsOf(group)]));
        }

        return groups;
    }

    private static IEnumerable<string> IdsOf(XElement parent) => parent.Elements()
        .Where(element => element.Name.LocalName == "dependency")
        .Select(element => element.Attribute("id")?.Value.Trim() ?? string.Empty);

    private static string? ReadLicense(XElement metadata)
    {
        XElement? license = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "license");
        return license?.Attribute("type")?.Value == "expression" ? license.Value.Trim() : null;
    }
}
