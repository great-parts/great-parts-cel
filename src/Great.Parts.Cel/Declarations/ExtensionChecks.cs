using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

// The checks of Build over the extension sets of a builder, before the declaration checks: a version the catalog lacks
// (4004), one name in two versions (4003), two different sets with one name and one version (4018) and a dependency that no
// set meets (4005), and the option of the decimal literals without the set decimal (4005 again). Each step reports every error it
// finds, in the order of the names, the versions and the source texts, so the order of the builder calls changes neither the list
// nor the text of an error. A set that fails the first three steps contributes nothing to the environment and causes no second
// error.
internal static class ExtensionChecks
{
    // The name of the set that the option of the decimal literals needs, and how the message of 4005 names the option.
    private const string DecimalSet = "decimal";
    private const string DecimalLiteralOption = "the decimal literal option";

    // Adds the errors to the list and returns the sets that remain, ordered by name (ordinal) and then by version. decimalLiterals
    // says that the builder enabled the decimal literals.
    internal static ImmutableArray<ExtensionDefinition> Check(
        ImmutableArray<SourcedDeclaration<CelExtension>> extensions,
        List<CelError> errors,
        bool decimalLiterals)
    {
        if (extensions.IsEmpty && !decimalLiterals)
        {
            return [];
        }

        List<SourcedDeclaration<CelExtension>> remaining = [.. extensions];
        remaining.Sort(Compare);
        RemoveUnknownVersions(remaining, errors);
        RemoveVersionConflicts(remaining, errors);
        RemoveDuplicates(remaining, errors);
        CheckDependencies(remaining, errors);
        CheckDecimalLiterals(remaining, errors, decimalLiterals);

        ImmutableArray<ExtensionDefinition>.Builder survivors = ImmutableArray.CreateBuilder<ExtensionDefinition>(remaining.Count);
        foreach (SourcedDeclaration<CelExtension> entry in remaining)
        {
            survivors.Add(entry.Value.Definition!);
        }

        return survivors.MoveToImmutable();
    }

    // Step 1: one error for each set whose definition is missing; the set is left out.
    private static void RemoveUnknownVersions(List<SourcedDeclaration<CelExtension>> remaining, List<CelError> errors)
    {
        for (int i = remaining.Count - 1; i >= 0; i--)
        {
            CelExtension extension = remaining[i].Value;
            if (extension.Definition is null)
            {
                errors.Add(BuildErrors.UnknownExtensionVersion(extension.Name, Text(extension.Version), Versions(extension), remaining[i].Position));
                remaining.RemoveAt(i);
            }
        }
    }

    // Step 2: for each name with more than one version, one error for each pair of versions, the smaller first, at the
    // element of the smaller version; every set of the name is left out.
    private static void RemoveVersionConflicts(List<SourcedDeclaration<CelExtension>> remaining, List<CelError> errors)
    {
        List<SourcedDeclaration<CelExtension>> kept = new(remaining.Count);
        int start = 0;
        while (start < remaining.Count)
        {
            string name = remaining[start].Value.Name;
            int end = start + 1;
            while (end < remaining.Count && string.Equals(remaining[end].Value.Name, name, StringComparison.Ordinal))
            {
                end++;
            }

            // The group is in the order of the versions; the first of each version represents it.
            List<SourcedDeclaration<CelExtension>> versions = [];
            for (int i = start; i < end; i++)
            {
                if (versions.Count == 0 || versions[^1].Value.Version != remaining[i].Value.Version)
                {
                    versions.Add(remaining[i]);
                }
            }

            if (versions.Count < 2)
            {
                for (int i = start; i < end; i++)
                {
                    kept.Add(remaining[i]);
                }
            }
            else
            {
                for (int smaller = 0; smaller < versions.Count; smaller++)
                {
                    for (int larger = smaller + 1; larger < versions.Count; larger++)
                    {
                        errors.Add(BuildErrors.ExtensionVersionConflict(
                            name,
                            Text(versions[smaller].Value.Version),
                            Text(versions[larger].Value.Version),
                            versions[smaller].Position ?? versions[larger].Position));
                    }
                }
            }

            start = end;
        }

        remaining.Clear();
        remaining.AddRange(kept);
    }

    // Step 3: two sets of one name and one version that are not the same set. One error for each pair of sets, the
    // sources in ordinal order; both are left out.
    private static void RemoveDuplicates(List<SourcedDeclaration<CelExtension>> remaining, List<CelError> errors)
    {
        List<SourcedDeclaration<CelExtension>> kept = new(remaining.Count);
        int start = 0;
        while (start < remaining.Count)
        {
            CelExtension first = remaining[start].Value;
            int end = start + 1;
            while (end < remaining.Count
                && remaining[end].Value.Version == first.Version
                && string.Equals(remaining[end].Value.Name, first.Name, StringComparison.Ordinal))
            {
                end++;
            }

            // The distinct sets of the group, in the order of their sources.
            List<SourcedDeclaration<CelExtension>> sets = [];
            for (int i = start; i < end; i++)
            {
                if (!Contains(sets, remaining[i].Value))
                {
                    sets.Add(remaining[i]);
                }
            }

            sets.Sort(CompareBySource);

            if (sets.Count < 2)
            {
                kept.Add(remaining[start]);
            }
            else
            {
                for (int a = 0; a < sets.Count; a++)
                {
                    for (int b = a + 1; b < sets.Count; b++)
                    {
                        errors.Add(BuildErrors.DuplicateExtension(
                            first.Name,
                            Text(first.Version),
                            sets[a].Value.Definition!.Source,
                            sets[b].Value.Definition!.Source,
                            sets[a].Position ?? sets[b].Position));
                    }
                }
            }

            start = end;
        }

        remaining.Clear();
        remaining.AddRange(kept);
    }

    // Step 4: a dependency is met by a set of its name in any version among the remaining sets; each missing name is one
    // error at the element of the set that asks. The set stays.
    private static void CheckDependencies(List<SourcedDeclaration<CelExtension>> remaining, List<CelError> errors)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (SourcedDeclaration<CelExtension> entry in remaining)
        {
            names.Add(entry.Value.Name);
        }

        foreach (SourcedDeclaration<CelExtension> entry in remaining)
        {
            ExtensionDefinition definition = entry.Value.Definition!;
            foreach (string dependency in definition.Dependencies)
            {
                if (!names.Contains(dependency))
                {
                    errors.Add(BuildErrors.MissingExtensionDependency(Label(definition), dependency, entry.Position));
                }
            }
        }
    }

    // Step 5: the option of the decimal literals needs a set named decimal among the remaining sets, in any version; without one it is
    // one error, whatever the number of calls that enabled it. The option has no place in an expression and no position: it is a
    // call of the builder, not an element of a description.
    private static void CheckDecimalLiterals(List<SourcedDeclaration<CelExtension>> remaining, List<CelError> errors, bool decimalLiterals)
    {
        if (!decimalLiterals)
        {
            return;
        }

        foreach (SourcedDeclaration<CelExtension> entry in remaining)
        {
            if (string.Equals(entry.Value.Name, DecimalSet, StringComparison.Ordinal))
            {
                return;
            }
        }

        errors.Add(BuildErrors.MissingExtensionDependency(DecimalLiteralOption, DecimalSet));
    }

    private static bool Contains(List<SourcedDeclaration<CelExtension>> sets, CelExtension extension)
    {
        foreach (SourcedDeclaration<CelExtension> set in sets)
        {
            if (set.Value.IsSameSetAs(extension))
            {
                return true;
            }
        }

        return false;
    }

    // By the source of the definition, as the message of 4018 names it — application stands before extension strings v5 —
    // then by the position in the text of a description.
    private static int CompareBySource(SourcedDeclaration<CelExtension> left, SourcedDeclaration<CelExtension> right)
    {
        int bySource = string.CompareOrdinal(left.Value.Definition!.Source, right.Value.Definition!.Source);
        return bySource != 0 ? bySource : (left.Position?.Offset ?? int.MaxValue).CompareTo(right.Position?.Offset ?? int.MaxValue);
    }

    // By name (ordinal), then version, then source text and position, so the groups of the steps are contiguous and the
    // first set of a group does not depend on the order of the calls.
    private static int Compare(SourcedDeclaration<CelExtension> left, SourcedDeclaration<CelExtension> right)
    {
        int byName = string.CompareOrdinal(left.Value.Name, right.Value.Name);
        if (byName != 0)
        {
            return byName;
        }

        int byVersion = left.Value.Version.CompareTo(right.Value.Version);
        return byVersion != 0 ? byVersion : left.CompareTo(right);
    }

    // The label of a set as the text of 4005 names it: extension <name> v<version>, for a set of the application too.
    private static string Label(ExtensionDefinition definition) =>
        "extension " + definition.Name + " v" + Text(definition.Version);

    private static string Text(uint number) => number.ToString(CultureInfo.InvariantCulture);

    // The versions the catalog has, ascending and separated by ", "; "none" for a name without any.
    private static string Versions(CelExtension extension)
    {
        if (extension.AvailableVersions.IsEmpty)
        {
            return "none";
        }

        var text = new StringBuilder();
        foreach (uint version in extension.AvailableVersions)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }

            text.Append(Text(version));
        }

        return text.ToString();
    }
}
