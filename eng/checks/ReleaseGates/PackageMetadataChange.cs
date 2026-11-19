using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Tells whether the change of <c>Directory.Build.props</c> or of a project <c>src/&lt;directory&gt;/&lt;name&gt;.csproj</c> between two commits is
/// only a change of the metadata of the packages, which the differential run does not depend on (<c>docs/design/eng-release.md</c>,
/// "The metadata of the packages in the paths of ENG904"). The two versions of the file are read as XML and compared after the comments,
/// the metadata properties of a <c>PropertyGroup</c> and the <c>None</c> items with <c>Pack="true"</c> of an <c>ItemGroup</c> directly under
/// <c>Project</c> are removed, and the groups without elements are removed too.
/// </summary>
internal static class PackageMetadataChange
{
    // The properties that reach only the package or the symbol package, never the assembly. Description, Authors, Copyright, Version,
    // Company, Product and NeutralLanguage are not here: they are written to the attributes of the assembly.
    private static readonly HashSet<string> MetadataProperties = new(StringComparer.Ordinal)
    {
        "PackageReadmeFile",
        "PackageIcon",
        "PackageTags",
        "PackageProjectUrl",
        "PackageReleaseNotes",
        "PackageLicenseExpression",
        "PackageRequireLicenseAcceptance",
        "EmbedUntrackedSources",
    };

    /// <summary>Whether the path is a file that holds the metadata of the packages and can be compared.</summary>
    /// <param name="path">The path relative to the repository root, with <c>/</c>.</param>
    internal static bool Holds(string path)
    {
        if (path == "Directory.Build.props")
        {
            return true;
        }

        string[] parts = path.Split('/');
        return parts.Length == 3 && parts[0] == "src" && parts[2].EndsWith(".csproj", StringComparison.Ordinal);
    }

    /// <summary>Whether nothing but the metadata of the packages differs in the file between the two commits.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="before">The commit of the long run.</param>
    /// <param name="after">The checked commit.</param>
    /// <param name="path">The path of the file, for which <see cref="Holds"/> is true.</param>
    internal static bool IsOnlyMetadata(string root, string before, string after, string path)
    {
        XElement? first = Read(root, before, path);
        XElement? second = Read(root, after, path);
        return first is not null && second is not null && XNode.DeepEquals(first, second);
    }

    // The project element without what the metadata may change; no value for a file that does not exist at the commit or is not XML.
    private static XElement? Read(string root, string commit, string path)
    {
        GitResult shown = Git.Run(root, "show", $"{commit}:{path}");
        if (!shown.Succeeded)
        {
            return null;
        }

        try
        {
            XElement? project = XDocument.Parse(shown.Output.TrimStart('﻿')).Root;
            if (project is null)
            {
                return null;
            }

            List<XComment> comments = [.. project.DescendantNodes().OfType<XComment>()];
            foreach (XComment comment in comments)
            {
                comment.Remove();
            }

            List<XElement> groups = [.. project.Elements()];
            foreach (XElement group in groups)
            {
                List<XElement> metadata = [.. group.Elements().Where(element => IsMetadata(group, element))];
                foreach (XElement element in metadata)
                {
                    element.Remove();
                }

                if (!group.HasElements)
                {
                    group.Remove();
                }
            }

            return project;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static bool IsMetadata(XElement group, XElement element) => group.Name.LocalName switch
    {
        "PropertyGroup" => MetadataProperties.Contains(element.Name.LocalName),
        "ItemGroup" => element.Name.LocalName == "None" && string.Equals((string?)element.Attribute("Pack"), "true", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };
}
