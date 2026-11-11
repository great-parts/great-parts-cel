using System;
using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>A table of the output: its name for the reader of the output and its code points.</summary>
/// <param name="Name">The key of the category or script, or that key and <c>(fold)</c> for the fold table of it.</param>
/// <param name="Ranges">The code points.</param>
internal sealed record TableDefinition(string Name, RangeSet Ranges);

/// <summary>A name of <c>\p{…}</c> with the numbers of its tables.</summary>
/// <param name="Name">The canonical name.</param>
/// <param name="Table">The number of the table.</param>
/// <param name="FoldTable">The number of the table that adds the code points of the folding, or -1.</param>
/// <param name="Complement">Whether the class is the complement of the table.</param>
internal sealed record TableNameEntry(string Name, int Table, int FoldTable, bool Complement);

/// <summary>
/// What the generator writes, as numbers and ranges (docs/design/unicode-tables.md, "What is generated"): the tables of the
/// categories, the scripts and the fold tables, the orbits of the simple case folding and the names.
/// </summary>
internal sealed class TableModel
{
    // The 30 two-letter general categories: 29 that UnicodeData.txt lists, and Cn, which it does not.
    private static readonly string[] ListedUnions = ["L", "M", "N", "P", "S", "Z", "C"];

    private TableModel(
        UcdInputs inputs,
        IReadOnlyList<TableDefinition> tables,
        IReadOnlyList<int> orbitKeys,
        IReadOnlyList<int> orbitNext,
        IReadOnlyList<TableNameEntry> names)
    {
        Inputs = inputs;
        Tables = tables;
        OrbitKeys = orbitKeys;
        OrbitNext = orbitNext;
        Names = names;
    }

    /// <summary>The database the tables come from.</summary>
    internal UcdInputs Inputs { get; }

    /// <summary>The tables by number: the categories and the scripts by ordinal order of their keys, then the fold tables in the same order.</summary>
    internal IReadOnlyList<TableDefinition> Tables { get; }

    /// <summary>The code points that have an orbit of more than one member, ascending.</summary>
    internal IReadOnlyList<int> OrbitKeys { get; }

    /// <summary>For each code point of <see cref="OrbitKeys"/>, the next member of its orbit: the next in ascending order, the smallest after the last.</summary>
    internal IReadOnlyList<int> OrbitNext { get; }

    /// <summary>The names by ordinal order.</summary>
    internal IReadOnlyList<TableNameEntry> Names { get; }

    /// <summary>The number of a table by its name.</summary>
    /// <param name="name">A key of a category or script, or the key and <c>(fold)</c>.</param>
    internal int NumberOf(string name) => Tables.ToList().FindIndex(table => table.Name == name);

    /// <summary>Builds the tables from the text of the three files.</summary>
    /// <param name="inputs">The version and the files of the database.</param>
    /// <param name="unicodeData">The text of <c>UnicodeData.txt</c>.</param>
    /// <param name="scriptsText">The text of <c>Scripts.txt</c>.</param>
    /// <param name="caseFolding">The text of <c>CaseFolding.txt</c>.</param>
    /// <exception cref="UcdFormatException">A file does not have the form the generator reads, or the names do not resolve as Go resolves them.</exception>
    internal static TableModel Build(UcdInputs inputs, string unicodeData, string scriptsText, string caseFolding)
    {
        UnicodeDataTable data = UcdReaders.ReadUnicodeData(unicodeData);
        IReadOnlyDictionary<string, RangeSet> scripts = UcdReaders.ReadScripts(scriptsText);
        IReadOnlyList<(int Code, int Folded)> folds = UcdReaders.ReadCaseFolding(caseFolding);

        SortedDictionary<string, RangeSet> primary = new(StringComparer.Ordinal);
        foreach ((string key, RangeSet set) in Categories(data))
        {
            primary.Add(key, set);
        }

        foreach ((string script, RangeSet set) in scripts)
        {
            if (!primary.TryAdd(script, set))
            {
                throw new UcdFormatException(UcdInputs.ScriptsName, 0, $"the script {script} has the name of a category");
            }
        }

        List<int[]> orbits = Orbits(folds);
        (List<int> orbitKeys, List<int> orbitNext) = OrbitTables(orbits);

        List<TableDefinition> tables = [.. primary.Select(pair => new TableDefinition(pair.Key, pair.Value))];
        Dictionary<string, int> foldNumbers = new(StringComparer.Ordinal);
        foreach ((string key, RangeSet set) in primary)
        {
            // unicode.FoldCategory of Go has no key LC, so the cased letters have no fold table.
            if (key == GoNames.CasedLetters)
            {
                continue;
            }

            RangeSet fold = FoldTable(set, orbits);
            if (fold.Count > 0)
            {
                foldNumbers[key] = tables.Count;
                tables.Add(new TableDefinition(key + " (fold)", fold));
            }
        }

        Dictionary<string, int> numbers = new(StringComparer.Ordinal);
        for (int number = 0; number < primary.Count; number++)
        {
            numbers[tables[number].Name] = number;
        }

        List<TableNameEntry> names = [];
        foreach (NameEntry entry in GoNames.Entries([.. primary.Keys.Where(key => !scripts.ContainsKey(key))], [.. scripts.Keys]))
        {
            int table = numbers[entry.Table];
            int fold = entry.Fold switch
            {
                FoldSource.FoldTableOfTable => foldNumbers.GetValueOrDefault(entry.Table, -1),
                FoldSource.TableItself => table,
                _ => -1,
            };
            names.Add(new TableNameEntry(entry.Name, table, fold, entry.Complement));
        }

        return new TableModel(inputs, tables, orbitKeys, orbitNext, names);
    }

    // The 38 categories: the 30 two-letter ones, their unions and the cased letters.
    private static IEnumerable<(string Key, RangeSet Set)> Categories(UnicodeDataTable data)
    {
        Dictionary<string, RangeSet> categories = new(StringComparer.Ordinal);
        foreach (string key in UcdReaders.ListedCategories)
        {
            categories[key] = data.Categories.GetValueOrDefault(key) ?? RangeSet.Empty;
        }

        categories[GoNames.Unassigned] = data.Listed.Complement();
        foreach (string union in ListedUnions)
        {
            categories[union] = RangeSet.Union(categories.Where(pair => pair.Key.Length == 2 && pair.Key[0] == union[0]).Select(pair => pair.Value));
        }

        categories[GoNames.CasedLetters] = RangeSet.Union([categories["Lu"], categories["Ll"], categories["Lt"]]);
        return categories.Select(pair => (pair.Key, pair.Value));
    }

    // The classes of code points that fold together: the code points with one simple case folding, and that folding.
    private static List<int[]> Orbits(IReadOnlyList<(int Code, int Folded)> folds)
    {
        SortedDictionary<int, List<int>> classes = [];
        foreach ((int code, int folded) in folds)
        {
            if (!classes.TryGetValue(folded, out List<int>? members))
            {
                members = [folded];
                classes[folded] = members;
            }

            members.Add(code);
        }

        List<int[]> orbits = [];
        foreach (List<int> members in classes.Values)
        {
            int[] sorted = [.. members.Distinct().Order()];
            if (sorted.Length > 1)
            {
                orbits.Add(sorted);
            }
        }

        return orbits;
    }

    // The table of pairs: the code point and the next member of its orbit.
    private static (List<int> Keys, List<int> Next) OrbitTables(List<int[]> orbits)
    {
        SortedDictionary<int, int> next = [];
        foreach (int[] orbit in orbits)
        {
            for (int index = 0; index < orbit.Length; index++)
            {
                if (!next.TryAdd(orbit[index], orbit[(index + 1) % orbit.Length]))
                {
                    throw new UcdFormatException(UcdInputs.CaseFoldingName, 0, $"the code point {orbit[index]:X4} is in two orbits");
                }
            }
        }

        return ([.. next.Keys], [.. next.Values]);
    }

    // The code points that are not in the table and whose orbit has a member in it (unicode.FoldCategory, unicode.FoldScript).
    private static RangeSet FoldTable(RangeSet table, List<int[]> orbits)
    {
        List<(int Lo, int Hi)> added = [];
        foreach (int[] orbit in orbits)
        {
            if (!orbit.Any(table.Contains))
            {
                continue;
            }

            foreach (int member in orbit)
            {
                if (!table.Contains(member))
                {
                    added.Add((member, member));
                }
            }
        }

        return RangeSet.From(added);
    }
}
