using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The check of the abbreviations of qualified names that the builder collected, as cel-go checks them (common/containers, Abbrevs and
// aliasAs): a name, without its surrounding white space, is a qualified identifier with at least two parts and no leading dot; the
// last part, the short name, is used by no other abbreviation, even one with the same name; and the container neither equals the
// short name nor begins with it and a dot. The names are checked in the order of string.CompareOrdinal, so that the order of the
// calls changes neither the errors nor which of two abbreviations with one short name is kept.
internal static class AbbreviationChecks
{
    // The abbreviations as the checker looks them up: the short name to the qualified name.
    internal static readonly FrozenDictionary<string, string> None = new Dictionary<string, string>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

    // The errors of the abbreviations, in the order of their names, and the valid ones by short name.
    internal static ImmutableArray<CelError> Check(ImmutableArray<string> abbreviations, string container, out FrozenDictionary<string, string> accepted)
    {
        if (abbreviations.IsEmpty)
        {
            accepted = None;
            return [];
        }

        // Surrounding white space is dropped before any check, as Abbrevs of cel-go does: a name in a folded block of YAML has the
        // indentation of the block.
        string[] names = new string[abbreviations.Length];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = abbreviations[i].Trim();
        }

        Array.Sort(names, StringComparer.Ordinal);
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        ImmutableArray<CelError>.Builder errors = ImmutableArray.CreateBuilder<CelError>();
        foreach (string name in names)
        {
            if (name.Length > 0 && name[0] == '.')
            {
                errors.Add(BuildErrors.InvalidAbbreviation(name, "qualified name must not begin with a leading '.': " + name));
                continue;
            }

            int lastDot = name.AsSpan().LastIndexOf('.');
            if (lastDot < 0 || !DeclarationNames.IsQualifiedIdentifier(name))
            {
                errors.Add(BuildErrors.InvalidAbbreviation(name, "invalid qualified name: " + name + ", wanted name of the form 'qualified.name'"));
                continue;
            }

            string shortName = name[(lastDot + 1)..];
            if (kept.TryGetValue(shortName, out string? existing))
            {
                errors.Add(BuildErrors.InvalidAbbreviation(
                    name,
                    "abbreviation collides with existing reference: name=" + name + ", abbreviation=" + shortName + ", existing=" + existing));
            }
            else if (container.AsSpan().SequenceEqual(shortName) || IsContainerPrefix(container, shortName))
            {
                errors.Add(BuildErrors.InvalidAbbreviation(
                    name,
                    "abbreviation collides with container name: name=" + name + ", abbreviation=" + shortName + ", container=" + container));
            }
            else
            {
                kept.Add(shortName, name);
            }
        }

        accepted = kept.Count == 0 ? None : kept.ToFrozenDictionary(StringComparer.Ordinal);
        return errors.ToImmutable();
    }

    // Whether the container begins with the short name and a dot.
    private static bool IsContainerPrefix(string container, string shortName) =>
        container.Length > shortName.Length
        && container[shortName.Length] == '.'
        && container.AsSpan(0, shortName.Length).SequenceEqual(shortName);
}
