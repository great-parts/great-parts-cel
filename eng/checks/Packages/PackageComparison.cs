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
        CompareGroups(list, package, findings);
        CompareLicense(list, package, findings);
        return findings;
    }

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

    private static void CompareGroups(PackageList list, PackageContent package, List<Finding> findings)
    {
        foreach (PackageListGroup expected in list.Groups)
        {
            PackageGroup[] groups = [.. package.Groups.Where(group => group.Target == expected.Target)];
            if (groups.Length == 0)
            {
                findings.Add(Error(list, expected.Line, EngCodes.Eng202, $"the package has no dependency group '{expected.Target}'"));
                continue;
            }

            HashSet<string> actual = new(groups.SelectMany(group => group.Dependencies), StringComparer.OrdinalIgnoreCase);
            foreach (string dependency in expected.Dependencies.Where(dependency => !actual.Contains(dependency)))
            {
                findings.Add(Error(list, expected.Line, EngCodes.Eng202, $"missing dependency '{dependency}' in group {expected.Target}"));
            }

            HashSet<string> named = new(expected.Dependencies, StringComparer.OrdinalIgnoreCase);
            foreach (string dependency in actual.Where(dependency => !named.Contains(dependency)).Order(StringComparer.Ordinal))
            {
                findings.Add(Error(list, expected.Line, EngCodes.Eng201, $"unexpected dependency '{dependency}' in group {expected.Target}"));
            }
        }

        // A group that the list does not name has no line of its own: its dependencies, or the group itself, are put on the first line.
        foreach (PackageGroup group in package.Groups.Where(group => !list.Groups.Any(expected => expected.Target == group.Target)))
        {
            string name = group.Target.Length == 0 ? "without a target" : group.Target;
            if (group.Dependencies.Count == 0)
            {
                findings.Add(Error(list, 1, EngCodes.Eng201, $"unexpected dependency group {name}, which the list does not name"));
            }

            foreach (string dependency in group.Dependencies)
            {
                findings.Add(Error(list, 1, EngCodes.Eng201, $"unexpected dependency '{dependency}' in group {name}, which the list does not name"));
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
