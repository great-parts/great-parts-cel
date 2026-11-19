using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Compares a package with its list (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the files, the dependency groups
/// and the license expression. The place of a finding is a line of the list; where the list has no line for it, the first line.
/// </summary>
internal static class PackageComparison
{
    /// <summary>Compares the package with its list; every difference is one finding.</summary>
    /// <param name="list">The list of the package.</param>
    /// <param name="package">The package.</param>
    internal static IEnumerable<Finding> Compare(PackageList list, PackageContent package)
    {
        List<Finding> findings = [];
        CompareFiles(list, package, findings);
        CompareGroups(list.Groups, package.Groups, new GroupKind("dependency", "dependency group", ReportsEmptyGroup: true), list, findings);
        CompareGroups(list.Frameworks, package.FrameworkGroups, new GroupKind("framework reference", "framework reference group", ReportsEmptyGroup: false), list, findings);
        CompareLicense(list, package, findings);
        return findings;
    }

    // The words of the messages for the dependency groups and for the groups of references to a shared framework; an empty group of
    // dependencies that the list does not name is a finding, an empty group of references is not (it references nothing).
    private sealed record GroupKind(string Noun, string GroupNoun, bool ReportsEmptyGroup);

    private static void CompareFiles(PackageList list, PackageContent package, List<Finding> findings)
    {
        foreach (string entry in package.Entries.Where(entry => !list.Files.ContainsKey(entry)))
        {
            findings.Add(Error(list, 1, EngCodes.Eng203, $"the package has the file '{entry}', which the list does not name"));
        }

        HashSet<string> entries = new(package.Entries, StringComparer.Ordinal);
        foreach ((string file, int line) in list.Files.Where(file => !entries.Contains(file.Key)))
        {
            findings.Add(Error(list, line, EngCodes.Eng204, $"the file '{file}' of the list is not in the package"));
        }
    }

    private static void CompareGroups(
        IReadOnlyList<PackageListLine> expectedLines,
        IReadOnlyList<PackageGroup> packageGroups,
        GroupKind kind,
        PackageList list,
        List<Finding> findings)
    {
        foreach (PackageListLine expected in expectedLines)
        {
            PackageGroup[] groups = [.. packageGroups.Where(group => group.Target == expected.Target)];
            if (groups.Length == 0)
            {
                findings.Add(Error(list, expected.Line, EngCodes.Eng202, $"the package has no {kind.GroupNoun} '{expected.Target}'"));
                continue;
            }

            HashSet<string> actual = new(groups.SelectMany(group => group.Dependencies), StringComparer.OrdinalIgnoreCase);
            foreach (string name in expected.Names.Where(name => !actual.Contains(name)))
            {
                findings.Add(Error(list, expected.Line, EngCodes.Eng202, $"missing {kind.Noun} '{name}' in group {expected.Target}"));
            }

            HashSet<string> named = new(expected.Names, StringComparer.OrdinalIgnoreCase);
            foreach (string name in actual.Where(name => !named.Contains(name)).Order(StringComparer.Ordinal))
            {
                findings.Add(Error(list, expected.Line, EngCodes.Eng201, $"unexpected {kind.Noun} '{name}' in group {expected.Target}"));
            }
        }

        // A group that the list does not name has no line of its own: its entries, or the group itself, are put on the first line.
        foreach (PackageGroup group in packageGroups.Where(group => !expectedLines.Any(expected => expected.Target == group.Target)))
        {
            string target = group.Target.Length == 0 ? "without a target" : group.Target;
            if (group.Dependencies.Count == 0 && kind.ReportsEmptyGroup)
            {
                findings.Add(Error(list, 1, EngCodes.Eng201, $"unexpected {kind.GroupNoun} {target}, which the list does not name"));
            }

            foreach (string name in group.Dependencies)
            {
                findings.Add(Error(list, 1, EngCodes.Eng201, $"unexpected {kind.Noun} '{name}' in group {target}, which the list does not name"));
            }
        }
    }

    private static void CompareLicense(PackageList list, PackageContent package, List<Finding> findings)
    {
        if (list.License is null)
        {
            findings.Add(Error(list, 1, EngCodes.Eng207, "the list has no 'license' line"));
        }
        else if (package.License is null)
        {
            findings.Add(Error(list, list.LicenseLine, EngCodes.Eng207, $"the package has no license expression; the list expects '{list.License}'"));
        }
        else if (!string.Equals(package.License, list.License, StringComparison.Ordinal))
        {
            findings.Add(Error(list, list.LicenseLine, EngCodes.Eng207, $"the license expression '{package.License}' differs from '{list.License}' of the list"));
        }
    }

    private static Finding Error(PackageList list, int line, string code, string message) =>
        new(list.Path, line, 1, FindingSeverity.Error, code, message);
}
