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
/// <param name="Dependencies">The IDs of the dependencies, or the names of the shared frameworks of a <c>frameworkReferences</c> group.</param>
internal sealed record PackageGroup(string Target, IReadOnlyList<string> Dependencies);

/// <summary>
/// What the check reads of a <c>.nupkg</c> (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the entries of the archive and,
/// from the <c>.nuspec</c>, the package ID and version, the dependency groups, the groups of references to a shared framework and the
/// license expression. The versions of the dependencies are not read.
/// </summary>
internal sealed class PackageContent
{
    private PackageContent(string path, string id, IReadOnlyList<string> entries, IReadOnlyList<PackageGroup> groups, string? license, XElement metadata)
    {
        Path = path;
        Id = id;
        Entries = entries;
        Groups = groups;
        FrameworkGroups = ReadFrameworkGroups(metadata);
        Version = TextOf(metadata, "version");
        License = license;
        Authors = TextOf(metadata, "authors");
        Description = TextOf(metadata, "description");
        Readme = TextOf(metadata, "readme").Replace('\\', '/');
        RepositoryUrl = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "repository")?.Attribute("url")?.Value.Trim() ?? string.Empty;
    }

    /// <summary>The text of <c>&lt;authors&gt;</c>; empty when the <c>.nuspec</c> has none.</summary>
    internal string Authors { get; }

    /// <summary>The text of <c>&lt;description&gt;</c>; empty when the <c>.nuspec</c> has none.</summary>
    internal string Description { get; }

    /// <summary>The text of <c>&lt;readme&gt;</c>, the path of the README in the package, with <c>/</c>; empty when the <c>.nuspec</c> has none.</summary>
    internal string Readme { get; }

    /// <summary>The <c>url</c> of <c>&lt;repository&gt;</c>; empty when the <c>.nuspec</c> has none.</summary>
    internal string RepositoryUrl { get; }

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

    /// <summary>
    /// The groups of <c>frameworkReferences</c> of the <c>.nuspec</c> in its order; the names of the shared frameworks are in
    /// <see cref="PackageGroup.Dependencies"/>.
    /// </summary>
    internal IReadOnlyList<PackageGroup> FrameworkGroups { get; }

    /// <summary>The text of <c>&lt;version&gt;</c>, the version of the package; empty when the <c>.nuspec</c> has none.</summary>
    internal string Version { get; }

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
            return new PackageContent(path, id, entries, ReadGroups(metadata), ReadLicense(metadata), metadata);
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
    private static List<PackageGroup> ReadGroups(XElement metadata) => ReadGroups(metadata, "dependencies", "dependency", "id");

    // frameworkReferences has groups of frameworkReference elements, which name a shared framework.
    private static List<PackageGroup> ReadFrameworkGroups(XElement metadata) => ReadGroups(metadata, "frameworkReferences", "frameworkReference", "name");

    // A container has groups of items with a target framework; an item outside any group has no target, and the names of such items form
    // one group without a target.
    private static List<PackageGroup> ReadGroups(XElement metadata, string container, string item, string attribute)
    {
        List<PackageGroup> groups = [];
        XElement? parent = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == container);
        if (parent is null)
        {
            return groups;
        }

        List<string> ungrouped = [.. NamesOf(parent, item, attribute)];
        if (ungrouped.Count > 0)
        {
            groups.Add(new PackageGroup(string.Empty, ungrouped));
        }

        foreach (XElement group in parent.Elements().Where(element => element.Name.LocalName == "group"))
        {
            string target = group.Attribute("targetFramework")?.Value.Trim() ?? string.Empty;
            groups.Add(new PackageGroup(target, [.. NamesOf(group, item, attribute)]));
        }

        return groups;
    }

    private static IEnumerable<string> NamesOf(XElement parent, string item, string attribute) => parent.Elements()
        .Where(element => element.Name.LocalName == item)
        .Select(element => element.Attribute(attribute)?.Value.Trim() ?? string.Empty);

    private static string TextOf(XElement metadata, string name) =>
        metadata.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value.Trim() ?? string.Empty;

    private static string? ReadLicense(XElement metadata)
    {
        XElement? license = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "license");
        return license?.Attribute("type")?.Value == "expression" ? license.Value.Trim() : null;
    }
}
