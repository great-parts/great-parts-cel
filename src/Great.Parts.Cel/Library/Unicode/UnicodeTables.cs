using System;

namespace Great.Parts.Cel.Library.Unicode;

// The reading of the generated Unicode tables (RangeData, TableStarts, OrbitKeys, OrbitNext and TryFindTable are in the files
// *.g.cs of this directory). The data are Unicode Character Database 17.0.0; the tables never use a char property, CharUnicodeInfo
// or Rune of .NET, so the classes and the folding do not depend on the version of .NET. A lookup allocates nothing: each array is a
// ReadOnlySpan<int> over constant data of the assembly.
internal static partial class UnicodeTables
{
    // The pairs lo, hi of the ranges of a table, ascending and merged. The table is a number from 0 to TableCount - 1.
    internal static ReadOnlySpan<int> Ranges(int table)
    {
        ReadOnlySpan<int> starts = TableStarts;
        int first = starts[table];
        return RangeData[first..starts[table + 1]];
    }

    // Whether a table has a code point: a binary search over the ranges of the table.
    internal static bool Contains(int table, int codePoint)
    {
        ReadOnlySpan<int> ranges = Ranges(table);
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

    // The number of code points that have an orbit of more than one member, and the code point at an index of that list,
    // ascending: the folding of a range of a class visits the code points of the range from FirstOrbitIndex on.
    internal static int OrbitCount => OrbitKeys.Length;

    internal static int OrbitKey(int index) => OrbitKeys[index];

    // The index of the first code point of the list that is not below the given one; OrbitCount when there is none.
    internal static int FirstOrbitIndex(int codePoint)
    {
        ReadOnlySpan<int> keys = OrbitKeys;
        int low = 0;
        int high = keys.Length;
        while (low < high)
        {
            int middle = low + ((high - low) >> 1);
            if (keys[middle] < codePoint)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    // The next member of the orbit of the simple case folding of a code point — the next in ascending order, the smallest after the
    // last — or the code point itself when it folds with no other: the step of unicode.SimpleFold of Go.
    internal static int Next(int codePoint)
    {
        ReadOnlySpan<int> keys = OrbitKeys;
        int low = 0;
        int high = keys.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            int key = keys[middle];
            if (codePoint < key)
            {
                high = middle - 1;
            }
            else if (codePoint > key)
            {
                low = middle + 1;
            }
            else
            {
                return OrbitNext[middle];
            }
        }

        return codePoint;
    }
}
