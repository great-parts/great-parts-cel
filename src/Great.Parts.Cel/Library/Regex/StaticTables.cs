using System;
using Great.Parts.Cel.Library.Unicode;

namespace Great.Parts.Cel.Library.Regex;

// The tables that a class written as one escape refers to instead of owning its ranges. A table is a number: the tables of
// Library.Unicode keep their numbers, and the five small tables of this namespace follow them. All of them are read-only data of
// the assembly, and a lookup allocates nothing.
internal static class StaticTables
{
    // The largest code point.
    internal const int MaxCodePoint = 0x10FFFF;

    // [0-9], the class \d.
    internal const int Digit = UnicodeTables.TableCount;

    // [\t\n\f\r ], the class \s.
    internal const int Space = UnicodeTables.TableCount + 1;

    // [0-9A-Za-z_], the class \w.
    internal const int Word = UnicodeTables.TableCount + 2;

    // Every code point, \p{Any}.
    internal const int Any = UnicodeTables.TableCount + 3;

    // U+0000 to U+007F, \p{Ascii}.
    internal const int Ascii = UnicodeTables.TableCount + 4;

    // The two code points beyond ASCII that fold into it, U+017F and U+212A: what p{Ascii} adds under (?i). Not a class by itself.
    internal const int AsciiFold = UnicodeTables.TableCount + 5;

    // The pairs lo, hi of the ranges of a table, ascending and merged.
    internal static ReadOnlySpan<int> Ranges(int table)
    {
        return table switch
        {
            Digit => DigitRanges,
            Space => SpaceRanges,
            Word => WordRanges,
            Any => AnyRanges,
            Ascii => AsciiRanges,
            AsciiFold => AsciiFoldRanges,
            _ => UnicodeTables.Ranges(table),
        };
    }

    // Whether a table has a code point.
    internal static bool Contains(int table, int codePoint)
    {
        return table < UnicodeTables.TableCount ? UnicodeTables.Contains(table, codePoint) : ContainsRange(Ranges(table), codePoint);
    }

    // The same for a sorted list of ranges that is not a table of the namespace.
    internal static bool ContainsRange(ReadOnlySpan<int> ranges, int codePoint)
    {
        int low = 0;
        int high = (ranges.Length >> 1) - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            if (codePoint < ranges[middle << 1])
            {
                high = middle - 1;
            }
            else if (codePoint > ranges[(middle << 1) + 1])
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    private static ReadOnlySpan<int> DigitRanges => [0x30, 0x39];

    private static ReadOnlySpan<int> SpaceRanges => [0x09, 0x0A, 0x0C, 0x0D, 0x20, 0x20];

    private static ReadOnlySpan<int> WordRanges => [0x30, 0x39, 0x41, 0x5A, 0x5F, 0x5F, 0x61, 0x7A];

    private static ReadOnlySpan<int> AnyRanges => [0, MaxCodePoint];

    private static ReadOnlySpan<int> AsciiRanges => [0, 0x7F];

    private static ReadOnlySpan<int> AsciiFoldRanges => [0x17F, 0x17F, 0x212A, 0x212A];
}
