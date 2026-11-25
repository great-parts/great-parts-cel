using System;
using System.Collections.Generic;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A <c>dependencies</c>, <c>framework</c> or <c>closure</c> line of a package list: the target framework, the names and the line.</summary>
/// <param name="Target">The target framework as the <c>.nuspec</c> writes it, such as <c>net10.0</c>.</param>
/// <param name="Names">The IDs of the packages, or the names of the shared frameworks; empty for a line without names.</param>
/// <param name="Line">The line of the list, starting with 1.</param>
internal sealed record PackageListLine(string Target, IReadOnlyList<string> Names, int Line);

/// <summary>
/// A list <c>eng/packages/&lt;package id&gt;.txt</c> (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the files, the
/// dependency groups, the shared frameworks, the closure of the restore and the license expression that the package must have. A
/// line that does not parse is one <c>ENG200</c> in <see cref="Problems"/>; the lines that do parse are kept.
/// </summary>
internal sealed class PackageList
{
    private const string DependenciesKeyword = "dependencies";
    private const string FrameworkKeyword = "framework";
    private const string ClosureKeyword = "closure";
    private const string LicenseKeyword = "license";
    private const string FileKeyword = "file";
    private const string BrowserKeyword = "browser";

    private readonly List<Finding> _problems = [];
    private readonly List<PackageListLine> _groups = [];
    private readonly List<PackageListLine> _frameworks = [];
    private readonly List<PackageListLine> _closures = [];
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

    /// <summary>The dependency groups (the <c>dependencies</c> lines) in the order of the list.</summary>
    internal IReadOnlyList<PackageListLine> Groups => _groups;

    /// <summary>The shared frameworks of the <c>frameworkReferences</c> groups (the <c>framework</c> lines) in the order of the list.</summary>
    internal IReadOnlyList<PackageListLine> Frameworks => _frameworks;

    /// <summary>The packages that the restore adds to an application (the <c>closure</c> lines) in the order of the list.</summary>
    internal IReadOnlyList<PackageListLine> Closures => _closures;

    /// <summary>The files of the package: the path in the package and the line of the list.</summary>
    internal IReadOnlyDictionary<string, int> Files => _files;

    /// <summary>The license expression, or <see langword="null"/> when the list has no <c>license</c> line.</summary>
    internal string? License { get; private set; }

    /// <summary>The line of the <c>license</c> line, or 1 when the list has none.</summary>
    internal int LicenseLine { get; private set; } = 1;

    /// <summary>
    /// The line of the <c>browser</c> line, which marks a package that runs in WebAssembly applications, or 0 when the list has none.
    /// </summary>
    internal int BrowserLine { get; private set; }

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

        list.CheckClosures();
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
                ParseTargetLine(DependenciesKeyword, "the dependencies", "package IDs", _groups, rest, number, duplicate: target => $"the group '{target}' is already in the list");
                break;
            case FrameworkKeyword:
                ParseTargetLine(FrameworkKeyword, "the frameworks", "names of shared frameworks", _frameworks, rest, number, duplicate: target => $"the list has more than one '{FrameworkKeyword}' line for '{target}'");
                break;
            case ClosureKeyword:
                ParseTargetLine(ClosureKeyword, "the packages of the closure", "package IDs", _closures, rest, number, duplicate: target => $"the list has more than one '{ClosureKeyword}' line for '{target}'");
                break;
            case LicenseKeyword:
                ParseLicense(rest, number);
                break;
            case FileKeyword:
                ParseFile(rest, number);
                break;
            case BrowserKeyword:
                ParseBrowser(rest, number);
                break;
            default:
                Problem(number, $"unknown line '{line}'; a line starts with '{DependenciesKeyword}', '{FrameworkKeyword}', '{ClosureKeyword}', '{LicenseKeyword}', '{FileKeyword}' or '{BrowserKeyword}'");
                break;
        }
    }

    // A line '<keyword> <target>: <name>, <name>, ...'. The same target on two lines of one keyword is a problem of the second line.
    private void ParseTargetLine(
        string keyword,
        string subject,
        string what,
        List<PackageListLine> lines,
        string rest,
        int number,
        Func<string, string> duplicate)
    {
        int colon = rest.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            Problem(number, $"a '{keyword}' line needs a colon after the target: '{keyword} <target>: <name>, <name>, ...'");
            return;
        }

        string target = rest[..colon].Trim();
        if (target.Length == 0 || target.AsSpan().ContainsAny(' ', '\t'))
        {
            Problem(number, $"a '{keyword}' line needs one target before the colon: '{keyword} <target>: <name>, <name>, ...'");
            return;
        }

        string names = rest[(colon + 1)..].Trim();
        List<string> items = [];
        if (names.Length > 0)
        {
            foreach (string part in names.Split(','))
            {
                string item = part.Trim();
                if (item.Length == 0 || item.AsSpan().ContainsAny(' ', '\t'))
                {
                    Problem(number, $"{subject} of '{target}' are not a list of {what} separated by commas");
                    return;
                }

                items.Add(item);
            }
        }

        if (lines.Exists(line => line.Target == target))
        {
            Problem(number, duplicate(target));
            return;
        }

        lines.Add(new PackageListLine(target, items, number));
    }

    // A target of the dependency groups without a closure line is a list that does not say what the restore adds: one problem on the
    // first line of the list.
    private void CheckClosures()
    {
        foreach (PackageListLine group in _groups)
        {
            if (!_closures.Exists(closure => closure.Target == group.Target))
            {
                Problem(1, $"the list has a '{DependenciesKeyword}' line for '{group.Target}' and no '{ClosureKeyword}' line for it: '{ClosureKeyword} {group.Target}: <id>, <id>, ...'");
            }
        }
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

    private void ParseBrowser(string rest, int number)
    {
        if (rest.Length > 0)
        {
            Problem(number, $"a '{BrowserKeyword}' line has no text after the keyword");
            return;
        }

        if (BrowserLine > 0)
        {
            Problem(number, $"the list has more than one '{BrowserKeyword}' line");
            return;
        }

        BrowserLine = number;
    }

    private void Problem(int number, string message) =>
        _problems.Add(new Finding(Path, number, 1, FindingSeverity.Error, EngCodes.Eng200, message));
}
