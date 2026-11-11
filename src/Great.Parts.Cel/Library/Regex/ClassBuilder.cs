using System;
using System.Buffers;
using Great.Parts.Cel.Library.Unicode;

namespace Great.Parts.Cel.Library.Regex;

// The list of ranges of a class: pairs lo, hi of code points, kept in the data of a node of the tree. Items are appended — a range,
// a range with its case folding, a table, a group — and then Clean sorts and merges the list; Negate complements it over 0 to
// U+10FFFF. The rules are those of regexp/syntax of Go, so that the lists are the same; the case folding reads the orbits of
// Library.Unicode and never a property of char. When the classes of the pattern own more ranges than a program may have, every
// operation does nothing: the parse goes on only to find a syntax error.
internal static class ClassBuilder
{
    // A list longer than this many ranges is cleaned after a bulk addition, so that repeated items do not pile duplicates up.
    private const int CleanThreshold = 4096;

    // Appends the range lo to hi. It joins the last range or the one before it when they overlap or touch, which keeps the list
    // short when a folded alphabet comes in as two runs.
    internal static void AppendRange(PatternTree tree, int node, int lo, int hi)
    {
        if (tree.RangesExceeded)
        {
            return;
        }

        Span<int> data = tree.WritableData(node);
        int n = data.Length;
        for (int i = 2; i <= 4; i += 2)
        {
            if (n >= i)
            {
                int rangeLo = data[n - i];
                int rangeHi = data[n - i + 1];
                if (lo <= rangeHi + 1 && rangeLo <= hi + 1)
                {
                    data[n - i] = Math.Min(lo, rangeLo);
                    data[n - i + 1] = Math.Max(hi, rangeHi);
                    return;
                }
            }
        }

        Span<int> room = tree.AppendRoom(node, 2);
        room[0] = lo;
        room[1] = hi;
    }

    // Appends the range and every code point whose orbit of the simple case folding meets it. The code points of the range that
    // have an orbit are found by a binary search to the first and a walk forward, each adds the other members of its orbit.
    internal static void AppendFoldedRange(PatternTree tree, int node, int lo, int hi)
    {
        AppendRange(tree, node, lo, hi);
        int count = UnicodeTables.OrbitCount;
        for (int index = UnicodeTables.FirstOrbitIndex(lo); index < count; index++)
        {
            int key = UnicodeTables.OrbitKey(index);
            if (key > hi)
            {
                break;
            }

            for (int other = UnicodeTables.Next(key); other != key; other = UnicodeTables.Next(other))
            {
                AppendRange(tree, node, other, other);
            }
        }

        CleanIfLarge(tree, node);
    }

    // Appends a literal code point, folded when the flags of the literal say so.
    internal static void AppendLiteral(PatternTree tree, int node, int codePoint, int flags)
    {
        if ((flags & PatternFlags.FoldCase) != 0)
        {
            AppendFoldedRange(tree, node, codePoint, codePoint);
        }
        else
        {
            AppendRange(tree, node, codePoint, codePoint);
        }
    }

    // Appends a clean list of ranges that lives outside the tree: a table.
    internal static void AppendClass(PatternTree tree, int node, ReadOnlySpan<int> ranges)
    {
        if (tree.RangesExceeded)
        {
            return;
        }

        if (tree.DataCount(node) == 0)
        {
            ranges.CopyTo(tree.AppendRoom(node, ranges.Length));
            return;
        }

        for (int i = 0; i < ranges.Length; i += 2)
        {
            AppendRange(tree, node, ranges[i], ranges[i + 1]);
        }

        CleanIfLarge(tree, node);
    }

    internal static void AppendFoldedClass(PatternTree tree, int node, ReadOnlySpan<int> ranges)
    {
        for (int i = 0; i < ranges.Length; i += 2)
        {
            AppendFoldedRange(tree, node, ranges[i], ranges[i + 1]);
        }
    }

    // Appends the complement of a clean list that lives outside the tree.
    internal static void AppendNegatedClass(PatternTree tree, int node, ReadOnlySpan<int> ranges)
    {
        int nextLo = 0;
        for (int i = 0; i < ranges.Length; i += 2)
        {
            if (nextLo <= ranges[i] - 1)
            {
                AppendRange(tree, node, nextLo, ranges[i] - 1);
            }

            nextLo = ranges[i + 1] + 1;
        }

        if (nextLo <= StaticTables.MaxCodePoint)
        {
            AppendRange(tree, node, nextLo, StaticTables.MaxCodePoint);
        }

        CleanIfLarge(tree, node);
    }

    // Appends the list of another node, or its complement. The other list is read by index, since appending may move the data.
    internal static void AppendNodeClass(PatternTree tree, int node, int source, bool negated)
    {
        int length = tree.DataCount(source);
        int nextLo = 0;
        for (int i = 0; i < length; i += 2)
        {
            ReadOnlySpan<int> data = tree.DataOf(source);
            int lo = data[i];
            int hi = data[i + 1];
            if (!negated)
            {
                AppendRange(tree, node, lo, hi);
                continue;
            }

            if (nextLo <= lo - 1)
            {
                AppendRange(tree, node, nextLo, lo - 1);
            }

            nextLo = hi + 1;
        }

        if (negated && nextLo <= StaticTables.MaxCodePoint)
        {
            AppendRange(tree, node, nextLo, StaticTables.MaxCodePoint);
        }

        CleanIfLarge(tree, node);
    }

    // Makes a class that refers to a table own its ranges, so that something can be added to it.
    internal static void MakeOwned(PatternTree tree, int node)
    {
        if (!tree.IsStaticClass(node))
        {
            return;
        }

        int table = tree.StaticTable(node);
        bool negated = tree.IsNegatedStatic(node);
        tree.SetStaticClass(node, -1, false);
        if (negated)
        {
            AppendNegatedClass(tree, node, StaticTables.Ranges(table));
        }
        else
        {
            AppendClass(tree, node, StaticTables.Ranges(table));
        }
    }

    // Sorts the ranges (by lo, and by hi downward for a tie) and merges the ones that overlap or touch.
    internal static void Clean(PatternTree tree, int node)
    {
        if (tree.RangesExceeded || tree.IsStaticClass(node))
        {
            return;
        }

        Span<int> data = tree.WritableData(node);
        int pairs = data.Length / 2;
        if (pairs < 2)
        {
            return;
        }

        if (!IsSorted(data))
        {
            SortPairs(data);
        }

        int write = 2;
        for (int i = 2; i < data.Length; i += 2)
        {
            int lo = data[i];
            int hi = data[i + 1];
            if (lo <= data[write - 1] + 1)
            {
                if (hi > data[write - 1])
                {
                    data[write - 1] = hi;
                }

                continue;
            }

            data[write] = lo;
            data[write + 1] = hi;
            write += 2;
        }

        tree.TruncateData(node, write);
    }

    // Replaces a clean list with its complement.
    internal static void Negate(PatternTree tree, int node)
    {
        if (tree.RangesExceeded)
        {
            return;
        }

        Span<int> data = tree.WritableData(node);
        int nextLo = 0;
        int write = 0;
        for (int i = 0; i < data.Length; i += 2)
        {
            int lo = data[i];
            int hi = data[i + 1];
            if (nextLo <= lo - 1)
            {
                data[write] = nextLo;
                data[write + 1] = lo - 1;
                write += 2;
            }

            nextLo = hi + 1;
        }

        tree.TruncateData(node, write);
        if (nextLo <= StaticTables.MaxCodePoint)
        {
            Span<int> room = tree.AppendRoom(node, 2);
            room[0] = nextLo;
            room[1] = StaticTables.MaxCodePoint;
        }
    }

    // Whether a class owning its ranges holds the code point, by a binary search of its clean list.
    internal static bool Contains(PatternTree tree, int node, int codePoint)
    {
        return StaticTables.ContainsRange(tree.DataOf(node), codePoint);
    }

    // Whether the list is one code point: a class like [a] is a literal.
    internal static bool IsSinglePoint(ReadOnlySpan<int> ranges) => ranges.Length == 2 && ranges[0] == ranges[1];

    // Whether the list is the two code points of one orbit of two, like [Aa] or [Δδ]: a class like that is a literal that folds.
    internal static bool IsFoldPair(ReadOnlySpan<int> ranges)
    {
        if (ranges.Length == 4)
        {
            return ranges[0] == ranges[1] && ranges[2] == ranges[3]
                && UnicodeTables.Next(ranges[0]) == ranges[2] && UnicodeTables.Next(ranges[2]) == ranges[0];
        }

        return ranges.Length == 2 && ranges[0] + 1 == ranges[1]
            && UnicodeTables.Next(ranges[0]) == ranges[1] && UnicodeTables.Next(ranges[1]) == ranges[0];
    }

    // The smallest code point of the orbit of the given one: the code point a literal under (?i) is kept as.
    internal static int MinFold(int codePoint)
    {
        int minimum = codePoint;
        for (int other = UnicodeTables.Next(codePoint); other != codePoint; other = UnicodeTables.Next(other))
        {
            minimum = Math.Min(minimum, other);
        }

        return minimum;
    }

    private static void CleanIfLarge(PatternTree tree, int node)
    {
        if (tree.DataCount(node) > CleanThreshold * 2)
        {
            Clean(tree, node);
        }
    }

    private static bool IsSorted(ReadOnlySpan<int> data)
    {
        for (int i = 2; i < data.Length; i += 2)
        {
            if (data[i - 2] > data[i] || (data[i - 2] == data[i] && data[i - 1] < data[i + 1]))
            {
                return false;
            }
        }

        return true;
    }

    // Sorts the pairs in place: each pair is a long whose order is the order wanted, lo first and hi downward.
    private static void SortPairs(Span<int> data)
    {
        int pairs = data.Length / 2;
        long[] rented = ArrayPool<long>.Shared.Rent(pairs);
        try
        {
            Span<long> keys = rented.AsSpan(0, pairs);
            for (int i = 0; i < pairs; i++)
            {
                keys[i] = ((long)data[2 * i] << 32) | (uint)(StaticTables.MaxCodePoint - data[(2 * i) + 1]);
            }

            keys.Sort();
            for (int i = 0; i < pairs; i++)
            {
                data[2 * i] = (int)(keys[i] >> 32);
                data[(2 * i) + 1] = StaticTables.MaxCodePoint - (int)(keys[i] & 0xFFFFFFFF);
            }
        }
        finally
        {
            ArrayPool<long>.Shared.Return(rented);
        }
    }
}
