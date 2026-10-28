using System;
using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A <c>dependencies</c> line of a package list: the target framework, the package IDs and the line.</summary>
/// <param name="Target">The target framework as the <c>.nuspec</c> writes it, such as <c>net10.0</c>.</param>
/// <param name="Dependencies">The IDs of the packages of the group; empty for a group without dependencies.</param>
/// <param name="Line">The line of the list, starting with 1.</param>
internal sealed record PackageListGroup(string Target, IReadOnlyList<string> Dependencies, int Line);

/// <summary>
/// A list <c>eng/packages/&lt;package id&gt;.txt</c> (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the files, the
/// dependency groups and the license expression that the package must have. A line that does not parse is one <c>ENG200</c>
/// in <see cref="Problems"/>; the lines that do parse are kept.
/// </summary>
internal sealed class PackageList
{
    private const string DependenciesKeyword = "dependencies";
    private const string LicenseKeyword = "license";
    private const string FileKeyword = "file";

    private readonly List<Finding> _problems = [];
    private readonly List<PackageListGroup> _groups = [];
    private readonly Dictionary<string, int> _files = new(StringComparer.Ordinal);

    private PackageList(string path, string id)
    {
        Path = path;
        Id = id;
    }

    /// <summary>The path of the list, relative to the repository root, with <c>/</c>.</summary>
    internal string Path { get; }

    /// <summary>The ID of the package: the name of the file without <c>.txt</c>.</summary>
    internal string Id { get; }

    /// <summary>One <c>ENG200</c> per line that does not parse; empty when every line parses.</summary>
    internal IReadOnlyList<Finding> Problems => _problems;

    /// <summary>The dependency groups in the order of the list.</summary>
    internal IReadOnlyList<PackageListGroup> Groups => _groups;

    /// <summary>The files of the package: the path in the package and the line of the list.</summary>
    internal IReadOnlyDictionary<string, int> Files => _files;

    /// <summary>The license expression, or <see langword="null"/> when the list has no <c>license</c> line.</summary>
    internal string? License { get; private set; }

    /// <summary>The line of the <c>license</c> line, or 1 when the list has none.</summary>
    internal int LicenseLine { get; private set; } = 1;

    /// <summary>Reads a list.</summary>
    /// <param name="path">The path of the list relative to the repository root, with <c>/</c>; the name of the file is the package ID.</param>
    /// <param name="text">The content of the file.</param>
    internal static PackageList Parse(string path, string text)
    {
        string name = path[(path.LastIndexOf('/') + 1)..];
        string id = name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? name[..^".txt".Length] : name;
        PackageList list = new(path, id);
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].Trim();
            if (line.Length > 0 && line[0] != '#')
            {
                list.ParseLine(line, index + 1);
            }
        }

        return list;
    }

    private void ParseLine(string line, int number)
    {
        int space = line.IndexOfAny([' ', '\t']);
        string keyword = space < 0 ? line : line[..space];
        string rest = space < 0 ? string.Empty : line[(space + 1)..].Trim();
        switch (keyword)
        {
            case DependenciesKeyword:
                ParseGroup(rest, number);
                break;
            case LicenseKeyword:
                ParseLicense(rest, number);
                break;
            case FileKeyword:
                ParseFile(rest, number);
                break;
            default:
                Problem(number, $"unknown line '{line}'; a line starts with '{DependenciesKeyword}', '{LicenseKeyword}' or '{FileKeyword}'");
                break;
        }
    }

    private void ParseGroup(string rest, int number)
    {
        int colon = rest.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            Problem(number, $"a '{DependenciesKeyword}' line needs a colon after the target: '{DependenciesKeyword} <target>: <id>, <id>, ...'");
            return;
        }

        string target = rest[..colon].Trim();
        if (target.Length == 0 || target.AsSpan().ContainsAny(' ', '\t'))
        {
            Problem(number, $"a '{DependenciesKeyword}' line needs one target before the colon: '{DependenciesKeyword} <target>: <id>, <id>, ...'");
            return;
        }

        string ids = rest[(colon + 1)..].Trim();
        List<string> dependencies = [];
        if (ids.Length > 0)
        {
            foreach (string part in ids.Split(','))
            {
                string id = part.Trim();
                if (id.Length == 0 || id.AsSpan().ContainsAny(' ', '\t'))
                {
                    Problem(number, $"the dependencies of '{target}' are not a list of package IDs separated by commas");
                    return;
                }

                dependencies.Add(id);
            }
        }

        if (_groups.Exists(group => group.Target == target))
        {
            Problem(number, $"the group '{target}' is already in the list");
            return;
        }

        _groups.Add(new PackageListGroup(target, dependencies, number));
    }

    private void ParseLicense(string rest, int number)
    {
        if (rest.Length == 0)
        {
            Problem(number, $"a '{LicenseKeyword}' line needs an expression: '{LicenseKeyword} <expression>'");
            return;
        }

        if (License is not null)
        {
            Problem(number, $"the list has more than one '{LicenseKeyword}' line");
            return;
        }

        License = rest;
        LicenseLine = number;
    }

    private void ParseFile(string rest, int number)
    {
        if (rest.Length == 0)
        {
            Problem(number, $"a '{FileKeyword}' line needs a path in the package: '{FileKeyword} <path>'");
            return;
        }

        _files.TryAdd(rest, number);
    }

    private void Problem(int number, string message) =>
        _problems.Add(new Finding(Path, number, 1, FindingSeverity.Error, EngCodes.Eng200, message));
}
