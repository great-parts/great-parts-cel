using System;
using System.Collections.Generic;
using System.Globalization;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>What <c>UnicodeData.txt</c> gives: the ranges of each general category it lists, and all the code points it lists.</summary>
/// <param name="Categories">The two-letter general category and its code points, by ordinal order of the category.</param>
/// <param name="Listed">The code points that the file lists, whatever their category; the others are unassigned.</param>
internal sealed record UnicodeDataTable(IReadOnlyDictionary<string, RangeSet> Categories, RangeSet Listed);

/// <summary>
/// The readers of the three files of the database (docs/design/unicode-tables.md, "The data"). Each reads the text of a file, makes
/// no use of the network, and stops with a <see cref="UcdFormatException"/> that names the file and the line.
/// </summary>
internal static class UcdReaders
{
    /// <summary>The general categories that <c>UnicodeData.txt</c> may give; <c>Cn</c> is what it does not list.</summary>
    internal static readonly string[] ListedCategories =
    [
        "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
        "Sm", "Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co",
    ];

    /// <summary>
    /// Reads <c>UnicodeData.txt</c>: lines of fields separated by <c>;</c>, the first the code point in hexadecimal, the second the
    /// name and the third the general category. A range is two lines whose names end in <c>, First&gt;</c> and <c>, Last&gt;</c>.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <exception cref="UcdFormatException">A line is not of that form, goes back, or names a category that is not listed.</exception>
    internal static UnicodeDataTable ReadUnicodeData(string text)
    {
        const string File = "UnicodeData.txt";
        Dictionary<string, List<(int Lo, int Hi)>> ranges = new(StringComparer.Ordinal);
        List<(int Lo, int Hi)> listed = [];
        int previousEnd = -1;
        (int Code, string Name, string Category)? start = null;
        int number = 0;
        foreach (string raw in text.Split('\n'))
        {
            number++;
            string line = raw.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(';');
            if (fields.Length < 3)
            {
                throw new UcdFormatException(File, number, "a line has fewer than 3 fields");
            }

            int code = ReadCodePoint(File, number, fields[0]);
            string name = fields[1];
            string category = fields[2];
            if (Array.IndexOf(ListedCategories, category) < 0)
            {
                throw new UcdFormatException(File, number, $"'{category}' is not a general category");
            }

            if (code <= previousEnd)
            {
                throw new UcdFormatException(File, number, $"the code point {code:X4} does not come after {previousEnd:X4}");
            }

            previousEnd = code;
            if (name.EndsWith(", First>", StringComparison.Ordinal))
            {
                start = start is null
                    ? (code, name[..^", First>".Length], category)
                    : throw new UcdFormatException(File, number, "a range that starts is not closed before the next one starts");
                continue;
            }

            int lo = code;
            if (name.EndsWith(", Last>", StringComparison.Ordinal))
            {
                if (start is not { } first || first.Category != category || first.Name + ", Last>" != name)
                {
                    throw new UcdFormatException(File, number, "a range that ends has no matching line that starts it");
                }

                lo = first.Code;
                start = null;
            }
            else if (start is not null)
            {
                throw new UcdFormatException(File, number, "a range that starts is not closed by the next line");
            }

            AddTo(ranges, category, lo, code);
            listed.Add((lo, code));
        }

        if (start is not null)
        {
            throw new UcdFormatException(File, 0, "the last range is not closed");
        }

        SortedDictionary<string, RangeSet> categories = new(StringComparer.Ordinal);
        foreach ((string category, List<(int Lo, int Hi)> list) in ranges)
        {
            categories[category] = RangeSet.From(list);
        }

        return new UnicodeDataTable(categories, RangeSet.From(listed));
    }

    /// <summary>
    /// Reads <c>Scripts.txt</c>: lines <c>&lt;range&gt; ; &lt;Script&gt; # …</c> where a range is one code point or
    /// <c>&lt;first&gt;..&lt;last&gt;</c> in hexadecimal; blank lines and lines that start with <c>#</c> are skipped.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <returns>The code points of each script by its name, by ordinal order of the names.</returns>
    /// <exception cref="UcdFormatException">A line is not of that form.</exception>
    internal static IReadOnlyDictionary<string, RangeSet> ReadScripts(string text)
    {
        const string File = "Scripts.txt";
        Dictionary<string, List<(int Lo, int Hi)>> ranges = new(StringComparer.Ordinal);
        int number = 0;
        foreach (string raw in text.Split('\n'))
        {
            number++;
            string line = StripComment(raw);
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(';');
            if (fields.Length != 2)
            {
                throw new UcdFormatException(File, number, "a line has not 2 fields");
            }

            string script = fields[1].Trim();
            if (script.Length == 0)
            {
                throw new UcdFormatException(File, number, "the script has no name");
            }

            (int lo, int hi) = ReadRange(File, number, fields[0].Trim());
            AddTo(ranges, script, lo, hi);
        }

        SortedDictionary<string, RangeSet> scripts = new(StringComparer.Ordinal);
        foreach ((string script, List<(int Lo, int Hi)> list) in ranges)
        {
            scripts[script] = RangeSet.From(list);
        }

        return scripts;
    }

    /// <summary>
    /// Reads <c>CaseFolding.txt</c>: lines <c>&lt;code&gt;; &lt;status&gt;; &lt;mapping&gt;; # …</c>. Only the statuses <c>C</c> and
    /// <c>S</c> are read, each with one code point as the mapping; <c>F</c> and <c>T</c> lines are skipped.
    /// </summary>
    /// <param name="text">The text of the file.</param>
    /// <returns>The code points that fold to another, with the code point they fold to, ascending.</returns>
    /// <exception cref="UcdFormatException">A line is not of that form, or a code point has two lines of the statuses read.</exception>
    internal static IReadOnlyList<(int Code, int Folded)> ReadCaseFolding(string text)
    {
        const string File = "CaseFolding.txt";
        SortedDictionary<int, int> folds = [];
        int number = 0;
        foreach (string raw in text.Split('\n'))
        {
            number++;
            string line = StripComment(raw);
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(';');
            if (fields.Length < 3)
            {
                throw new UcdFormatException(File, number, "a line has fewer than 3 fields");
            }

            string status = fields[1].Trim();
            if (status is not ("C" or "S" or "F" or "T"))
            {
                throw new UcdFormatException(File, number, $"'{status}' is not a status of the file");
            }

            if (status is "F" or "T")
            {
                continue;
            }

            int code = ReadCodePoint(File, number, fields[0].Trim());
            string mapping = fields[2].Trim();
            if (mapping.Contains(' ', StringComparison.Ordinal))
            {
                throw new UcdFormatException(File, number, $"a line of status {status} has more than one code point");
            }

            if (!folds.TryAdd(code, ReadCodePoint(File, number, mapping)))
            {
                throw new UcdFormatException(File, number, $"the code point {code:X4} has two lines of the statuses C and S");
            }
        }

        List<(int Code, int Folded)> result = [];
        foreach ((int code, int folded) in folds)
        {
            result.Add((code, folded));
        }

        return result;
    }

    private static string StripComment(string raw)
    {
        int hash = raw.IndexOf('#', StringComparison.Ordinal);
        return (hash >= 0 ? raw[..hash] : raw).Trim();
    }

    private static (int Lo, int Hi) ReadRange(string file, int line, string text)
    {
        int dots = text.IndexOf("..", StringComparison.Ordinal);
        if (dots < 0)
        {
            int single = ReadCodePoint(file, line, text);
            return (single, single);
        }

        int lo = ReadCodePoint(file, line, text[..dots]);
        int hi = ReadCodePoint(file, line, text[(dots + 2)..]);
        return lo <= hi ? (lo, hi) : throw new UcdFormatException(file, line, $"the range {text} goes back");
    }

    private static int ReadCodePoint(string file, int line, string text)
    {
        if (text.Length is >= 4 and <= 6
            && int.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int value)
            && value <= RangeSet.MaxCodePoint)
        {
            return value;
        }

        throw new UcdFormatException(file, line, $"'{text}' is not a code point of 4 to 6 hexadecimal digits up to 10FFFF");
    }

    private static void AddTo(Dictionary<string, List<(int Lo, int Hi)>> ranges, string key, int lo, int hi)
    {
        if (!ranges.TryGetValue(key, out List<(int Lo, int Hi)>? list))
        {
            list = [];
            ranges[key] = list;
        }

        list.Add((lo, hi));
    }
}
