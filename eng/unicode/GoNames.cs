using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>Which table adds the code points that a class takes under <c>(?i)</c>.</summary>
internal enum FoldSource
{
    /// <summary>None: the class takes nothing more.</summary>
    None,

    /// <summary>The fold table of the table, when it has one.</summary>
    FoldTableOfTable,

    /// <summary>The table itself: <c>\p{Assigned}</c> takes <c>Cn</c> and, under <c>(?i)</c>, <c>Cn</c> again, as Go does.</summary>
    TableItself,
}

/// <summary>A name that <c>\p{…}</c> accepts, in its canonical form, and what it stands for.</summary>
/// <param name="Name">The canonical name.</param>
/// <param name="Table">The key of the category or script whose table the name gives.</param>
/// <param name="Fold">Which table adds the code points of the folding.</param>
/// <param name="Complement">Whether the class is the complement of the table.</param>
internal sealed record NameEntry(string Name, string Table, FoldSource Fold, bool Complement);

/// <summary>
/// The names of the tables as <c>regexp/syntax</c> of Go 1.27.3 reads them (<c>canonicalName</c> and <c>unicodeTable</c> of
/// <c>parse.go</c>), without <c>Any</c> and <c>Ascii</c>, which the parser of the pattern knows by itself
/// (docs/design/unicode-tables.md, "What is generated").
/// </summary>
internal static class GoNames
{
    /// <summary>The key of the cased letters, the only key of <c>unicode.Categories</c> that is not in the canonical form.</summary>
    internal const string CasedLetters = "LC";

    /// <summary>The key of the unassigned code points.</summary>
    internal const string Unassigned = "Cn";

    /// <summary>
    /// <c>unicode.CategoryAliases</c> of Go 1.27.3: the alias and the category it stands for, in the order of the aliases.
    /// </summary>
    internal static readonly (string Alias, string Category)[] CategoryAliases =
    [
        ("Cased_Letter", "LC"), ("Close_Punctuation", "Pe"), ("Combining_Mark", "M"), ("Connector_Punctuation", "Pc"),
        ("Control", "Cc"), ("Currency_Symbol", "Sc"), ("Dash_Punctuation", "Pd"), ("Decimal_Number", "Nd"),
        ("Enclosing_Mark", "Me"), ("Final_Punctuation", "Pf"), ("Format", "Cf"), ("Initial_Punctuation", "Pi"),
        ("Letter", "L"), ("Letter_Number", "Nl"), ("Line_Separator", "Zl"), ("Lowercase_Letter", "Ll"),
        ("Mark", "M"), ("Math_Symbol", "Sm"), ("Modifier_Letter", "Lm"), ("Modifier_Symbol", "Sk"),
        ("Nonspacing_Mark", "Mn"), ("Number", "N"), ("Open_Punctuation", "Ps"), ("Other", "C"),
        ("Other_Letter", "Lo"), ("Other_Number", "No"), ("Other_Punctuation", "Po"), ("Other_Symbol", "So"),
        ("Paragraph_Separator", "Zp"), ("Private_Use", "Co"), ("Punctuation", "P"), ("Separator", "Z"),
        ("Space_Separator", "Zs"), ("Spacing_Mark", "Mc"), ("Surrogate", "Cs"), ("Symbol", "S"),
        ("Titlecase_Letter", "Lt"), ("Unassigned", "Cn"), ("Uppercase_Letter", "Lu"), ("cntrl", "Cc"),
        ("digit", "Nd"), ("punct", "P"),
    ];

    /// <summary>
    /// <c>canonicalName</c> of Go: the first letter in upper case, the others in lower case, without underscores, spaces and hyphens.
    /// </summary>
    /// <param name="name">The name.</param>
    internal static string Canonical(string name)
    {
        StringBuilder builder = new(name.Length);
        bool first = true;
        foreach (char c in name)
        {
            if (c is '_' or '-' or ' ')
            {
                continue;
            }

            if (first)
            {
                builder.Append(c is >= 'a' and <= 'z' ? (char)(c - ('a' - 'A')) : c);
                first = false;
            }
            else
            {
                builder.Append(c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The entries for the names of the categories and the scripts, as <c>unicodeTable</c> of Go looks a canonical name up: first the
    /// special names <c>Assigned</c> and <c>Lc</c>, then the keys of the categories that are canonical themselves, then the keys of
    /// the scripts that are, then the canonical forms of the aliases of the categories, then those of the scripts. The first match
    /// wins; two different tables for one canonical name in the last two steps are an error, since Go builds those maps in no
    /// fixed order.
    /// </summary>
    /// <param name="categories">The keys of <c>unicode.Categories</c>.</param>
    /// <param name="scripts">The keys of <c>unicode.Scripts</c>.</param>
    /// <returns>The entries by ordinal order of the names.</returns>
    /// <exception cref="UcdFormatException">Two aliases or two scripts have one canonical name, or an alias names a category that is not there.</exception>
    internal static IReadOnlyList<NameEntry> Entries(IReadOnlyCollection<string> categories, IReadOnlyCollection<string> scripts)
    {
        SortedDictionary<string, NameEntry> entries = new(StringComparer.Ordinal);
        Add(entries, new NameEntry("Assigned", Unassigned, FoldSource.TableItself, Complement: true));
        Add(entries, new NameEntry("Lc", CasedLetters, FoldSource.None, Complement: false));
        foreach (string key in categories)
        {
            if (Canonical(key) == key)
            {
                Add(entries, new NameEntry(key, key, FoldSource.FoldTableOfTable, Complement: false));
            }
        }

        foreach (string key in scripts)
        {
            if (Canonical(key) == key)
            {
                Add(entries, new NameEntry(key, key, FoldSource.FoldTableOfTable, Complement: false));
            }
        }

        // The maps of the aliases are built in no fixed order by Go, so a collision among them is an error here.
        SortedDictionary<string, NameEntry> aliasEntries = new(StringComparer.Ordinal);
        foreach ((string alias, string category) in CategoryAliases)
        {
            if (!categories.Contains(category))
            {
                throw new UcdFormatException("GoNames", 0, $"the alias {alias} names the category {category}, which is not a category");
            }

            AddUnique(aliasEntries, new NameEntry(Canonical(alias), category, category == CasedLetters ? FoldSource.None : FoldSource.FoldTableOfTable, Complement: false));
        }

        SortedDictionary<string, NameEntry> scriptEntries = new(StringComparer.Ordinal);
        foreach (string key in scripts)
        {
            AddUnique(scriptEntries, new NameEntry(Canonical(key), key, FoldSource.FoldTableOfTable, Complement: false));
        }

        foreach (NameEntry entry in aliasEntries.Values)
        {
            Add(entries, entry);
        }

        foreach (NameEntry entry in scriptEntries.Values)
        {
            Add(entries, entry);
        }

        return [.. entries.Values];
    }

    // First match wins.
    private static void Add(SortedDictionary<string, NameEntry> entries, NameEntry entry) => entries.TryAdd(entry.Name, entry);

    private static void AddUnique(SortedDictionary<string, NameEntry> entries, NameEntry entry)
    {
        if (!entries.TryAdd(entry.Name, entry) && entries[entry.Name].Table != entry.Table)
        {
            throw new UcdFormatException("GoNames", 0, $"the canonical name {entry.Name} stands for {entries[entry.Name].Table} and for {entry.Table}");
        }
    }
}
