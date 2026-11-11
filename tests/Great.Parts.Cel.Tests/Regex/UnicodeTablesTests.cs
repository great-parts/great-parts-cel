using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Library.Unicode;
using Xunit;

namespace Great.Parts.Cel.Tests.Regex;

// The generated tables against what package unicode of the Go of the oracle says. The reference is the file go-1.27.3.txt that the
// program next to it in eng/unicode/reference/ wrote (docs/design/unicode-tables.md): the version, the ranges of every category and
// script, the aliases of the categories, the fold tables, and the simple case folding of every code point that it changes. The
// file is read as a file next to the test assembly.
public sealed class UnicodeTablesTests
{
    private static readonly Lazy<Reference> Data = new(Reference.Load);

    private static string Canonical(string name)
    {
        var builder = new StringBuilder();
        foreach (char c in name)
        {
            if (c is '_' or '-' or ' ')
            {
                continue;
            }

            bool upper = c is >= 'A' and <= 'Z';
            bool lower = c is >= 'a' and <= 'z';
            builder.Append(builder.Length == 0 && lower ? (char)(c - 32) : builder.Length > 0 && upper ? (char)(c + 32) : c);
        }

        return builder.ToString();
    }

    private static TableName Find(string name)
    {
        Assert.True(UnicodeTables.TryFindTable(Canonical(name), out TableName found), $"no table of the name {name}");
        return found;
    }

    private static bool SameRanges(ReadOnlySpan<int> table, int[] reference) => table.SequenceEqual(reference);

    [Fact]
    public static void Version_IsTheOneOfTheInputs()
    {
        string inputsPath = Path.Combine(AppContext.BaseDirectory, "Regex", "Data", "unicode-inputs.txt");
        string version = File.ReadLines(inputsPath).Single(line => line.StartsWith("version ", StringComparison.Ordinal))["version ".Length..];

        Assert.Equal(version, Data.Value.Version);
        Assert.Equal("17.0.0", version);
    }

    [Fact]
    public static void EveryCategoryAndScript_IsTheTableOfGo()
    {
        var wrong = new List<string>();
        foreach ((string key, int[] ranges) in Data.Value.Tables)
        {
            TableName found = Find(key);
            if (!SameRanges(UnicodeTables.Ranges(found.Table), ranges))
            {
                wrong.Add(key);
            }
        }

        Assert.Empty(wrong);
        Assert.Equal(212, Data.Value.Tables.Count);
    }

    // The fold tables are those of Go, and a name that Go has no fold table for has none. LC has none.
    [Fact]
    public static void EveryFoldTable_IsTheOneOfGo()
    {
        var wrong = new List<string>();
        foreach ((string key, int[] ranges) in Data.Value.Folds)
        {
            TableName found = Find(key);
            if (found.FoldTable < 0 || !SameRanges(UnicodeTables.Ranges(found.FoldTable), ranges))
            {
                wrong.Add(key);
            }
        }

        foreach (string key in Data.Value.Tables.Keys.Where(key => !Data.Value.Folds.ContainsKey(key)))
        {
            if (Find(key).FoldTable >= 0)
            {
                wrong.Add("unexpected fold table of " + key);
            }
        }

        Assert.Empty(wrong);
        Assert.Equal(9, Data.Value.Folds.Count);
        Assert.False(Data.Value.Folds.ContainsKey("LC"));
    }

    // Every table of the generated code is one of the reference, once: no table that Go does not have.
    [Fact]
    public static void TablesOfTheCode_AreTheTablesOfTheReferenceAndNoOthers()
    {
        var numbers = new HashSet<int>();
        foreach (string key in Data.Value.Tables.Keys)
        {
            Assert.True(numbers.Add(Find(key).Table));
        }

        foreach (string key in Data.Value.Folds.Keys)
        {
            Assert.True(numbers.Add(Find(key).FoldTable));
        }

        Assert.Equal(UnicodeTables.TableCount, numbers.Count);
        Assert.Equal(Enumerable.Range(0, UnicodeTables.TableCount), numbers.Order());
    }

    [Fact]
    public static void Contains_AgreesAtTheEndsOfEveryRange()
    {
        var wrong = new List<string>();
        for (int table = 0; table < UnicodeTables.TableCount; table++)
        {
            ReadOnlySpan<int> ranges = UnicodeTables.Ranges(table);
            for (int i = 0; i < ranges.Length; i += 2)
            {
                int lo = ranges[i];
                int hi = ranges[i + 1];
                bool ok = UnicodeTables.Contains(table, lo) && UnicodeTables.Contains(table, hi)
                    && (lo == 0 || !UnicodeTables.Contains(table, lo - 1))
                    && (hi == 0x10FFFF || !UnicodeTables.Contains(table, hi + 1));
                if (!ok)
                {
                    wrong.Add($"table {table} range {lo:X}-{hi:X}");
                }
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public static void Aliases_AreTheCategoriesOfGo()
    {
        var wrong = new List<string>();
        foreach ((string alias, string category) in Data.Value.Aliases)
        {
            if (Find(alias).Table != Find(category).Table || Find(alias).FoldTable != Find(category).FoldTable)
            {
                wrong.Add(alias);
            }
        }

        Assert.Empty(wrong);
        Assert.Equal(42, Data.Value.Aliases.Count);
    }

    [Fact]
    public static void Assigned_IsTheComplementOfTheUnassigned()
    {
        TableName assigned = Find("Assigned");
        TableName unassigned = Find("Cn");

        Assert.True(assigned.IsComplement);
        Assert.Equal(unassigned.Table, assigned.Table);
        Assert.False(unassigned.IsComplement);
    }

    // A name is found in the canonical form only; Go makes every name canonical first, and so does the parser.
    [Theory]
    [InlineData("Lu")]
    [InlineData("Greek")]
    [InlineData("Olditalic")]
    [InlineData("Lc")]
    [InlineData("Assigned")]
    [InlineData("Letter")]
    public static void CanonicalName_IsFound(string name)
    {
        Assert.True(UnicodeTables.TryFindTable(name, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("lu")]
    [InlineData("GREEK")]
    [InlineData("Old_Italic")]
    [InlineData("Any")]
    [InlineData("Ascii")]
    [InlineData("Grek")]
    [InlineData("Foo")]
    [InlineData("L&")]
    public static void NotCanonicalOrNotATable_IsNotFound(string name)
    {
        Assert.False(UnicodeTables.TryFindTable(name, out TableName found));
        Assert.Equal(0, found.Table);
        Assert.False(found.IsComplement);
    }

    // The orbits are the simple case folding of Go: every code point that Go changes has the same next member, and every other
    // code point is its own.
    [Fact]
    public static void Next_IsSimpleFoldOfGoForEveryCodePoint()
    {
        var wrong = new List<string>();
        Dictionary<int, int> folds = Data.Value.SimpleFold;
        for (int codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            int expected = folds.TryGetValue(codePoint, out int next) ? next : codePoint;
            int actual = UnicodeTables.Next(codePoint);
            if (actual != expected && wrong.Count < 10)
            {
                wrong.Add($"{codePoint:X}: Go {expected:X}, here {actual:X}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public static void Orbits_AreTheCodePointsThatGoChanges()
    {
        int[] keys = Data.Value.SimpleFold.Keys.Order().ToArray();

        Assert.Equal(keys.Length, UnicodeTables.OrbitCount);
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(keys[i], UnicodeTables.OrbitKey(i));
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0x41, 0)]
    [InlineData(0x42, 1)]
    [InlineData(0x5B, 26)]
    [InlineData(0x61, 26)]
    [InlineData(0x10FFFF, -1)]
    public static void FirstOrbitIndex_IsTheFirstKeyNotBelow(int codePoint, int expected)
    {
        int index = UnicodeTables.FirstOrbitIndex(codePoint);
        int count = UnicodeTables.OrbitCount;

        if (expected == -1)
        {
            Assert.Equal(count, index);
            return;
        }

        Assert.True(index <= count);
        Assert.True(index == count || UnicodeTables.OrbitKey(index) >= codePoint);
        Assert.True(index == 0 || UnicodeTables.OrbitKey(index - 1) < codePoint);
        Assert.Equal(expected, index);
    }

    // The orbits of the examples of the standard: k has three members, ß two, the Greek final sigma three.
    [Theory]
    [InlineData(0x4B, 0x6B)]
    [InlineData(0x6B, 0x212A)]
    [InlineData(0x212A, 0x4B)]
    [InlineData(0xDF, 0x1E9E)]
    [InlineData(0x1E9E, 0xDF)]
    [InlineData(0x3A3, 0x3C2)]
    [InlineData(0x3C2, 0x3C3)]
    [InlineData(0x3C3, 0x3A3)]
    [InlineData(0x130, 0x130)]
    [InlineData(0x131, 0x131)]
    [InlineData(0x31, 0x31)]
    public static void Next_StepsRoundTheOrbit(int codePoint, int next)
    {
        Assert.Equal(next, UnicodeTables.Next(codePoint));
    }

    private sealed class Reference
    {
        internal string Version { get; private set; } = string.Empty;

        internal Dictionary<string, int[]> Tables { get; } = [];

        internal Dictionary<string, int[]> Folds { get; } = [];

        internal Dictionary<string, string> Aliases { get; } = [];

        internal Dictionary<int, int> SimpleFold { get; } = [];

        internal static Reference Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Regex", "Data", "go-1.27.3.txt");
            var reference = new Reference();
            foreach (string line in File.ReadLines(path))
            {
                string[] fields = line.Split(' ');
                switch (fields[0])
                {
                    case "version":
                        reference.Version = fields[1];
                        break;
                    case "category" or "script":
                        reference.Tables.Add(fields[1], Ranges(fields));
                        break;
                    case "foldcategory" or "foldscript":
                        reference.Folds.Add(fields[1], Ranges(fields));
                        break;
                    case "alias":
                        reference.Aliases.Add(fields[1], fields[2]);
                        break;
                    case "simplefold":
                        reference.SimpleFold.Add(Hex(fields[1]), Hex(fields[2]));
                        break;
                    default:
                        throw new InvalidDataException("unknown line of the reference: " + line);
                }
            }

            return reference;
        }

        private static int Hex(string text) => int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        private static int[] Ranges(string[] fields)
        {
            var ranges = new List<int>();
            foreach (string range in fields.Skip(2))
            {
                string[] ends = range.Split('-');
                ranges.Add(Hex(ends[0]));
                ranges.Add(Hex(ends[1]));
            }

            return [.. ranges];
        }
    }
}
