using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>
/// A set of code points as ascending, merged, closed ranges: <c>Pairs</c> holds <c>lo</c>, <c>hi</c> of the first range, then of the
/// second, and so on. Two ranges that overlap or touch are one.
/// </summary>
internal sealed class RangeSet
{
    /// <summary>The largest code point.</summary>
    internal const int MaxCodePoint = 0x10FFFF;

    private readonly int[] pairs;

    private RangeSet(int[] pairs) => this.pairs = pairs;

    /// <summary>The set without code points.</summary>
    internal static RangeSet Empty { get; } = new([]);

    /// <summary>The pairs <c>lo</c>, <c>hi</c> of the ranges, ascending.</summary>
    internal ReadOnlySpan<int> Pairs => pairs;

    /// <summary>The number of ranges.</summary>
    internal int Count => pairs.Length / 2;

    /// <summary>The number of code points.</summary>
    internal int Size
    {
        get
        {
            int size = 0;
            for (int index = 0; index < pairs.Length; index += 2)
            {
                size += pairs[index + 1] - pairs[index] + 1;
            }

            return size;
        }
    }

    /// <summary>Makes a set of ranges in any order, which may overlap.</summary>
    /// <param name="ranges">The ranges, each with <c>Lo</c> not above <c>Hi</c>, both from 0 to U+10FFFF.</param>
    internal static RangeSet From(IEnumerable<(int Lo, int Hi)> ranges)
    {
        List<(int Lo, int Hi)> sorted = [.. ranges];
        foreach ((int lo, int hi) in sorted)
        {
            if (lo < 0 || hi > MaxCodePoint || lo > hi)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges), $"The range {lo:X}..{hi:X} is not within 0..10FFFF.");
            }
        }

        sorted.Sort(static (left, right) => left.Lo.CompareTo(right.Lo));
        List<int> merged = new(sorted.Count * 2);
        foreach ((int lo, int hi) in sorted)
        {
            int last = merged.Count - 1;
            if (last >= 0 && lo <= merged[last] + 1)
            {
                merged[last] = Math.Max(merged[last], hi);
            }
            else
            {
                merged.Add(lo);
                merged.Add(hi);
            }
        }

        return new RangeSet([.. merged]);
    }

    /// <summary>The union of sets.</summary>
    /// <param name="sets">The sets.</param>
    internal static RangeSet Union(IEnumerable<RangeSet> sets)
    {
        List<(int Lo, int Hi)> ranges = [];
        foreach (RangeSet set in sets)
        {
            for (int index = 0; index < set.pairs.Length; index += 2)
            {
                ranges.Add((set.pairs[index], set.pairs[index + 1]));
            }
        }

        return From(ranges);
    }

    /// <summary>The code points from 0 to U+10FFFF that are not in the set.</summary>
    internal RangeSet Complement()
    {
        List<(int Lo, int Hi)> ranges = [];
        int next = 0;
        for (int index = 0; index < pairs.Length; index += 2)
        {
            if (pairs[index] > next)
            {
                ranges.Add((next, pairs[index] - 1));
            }

            next = pairs[index + 1] + 1;
        }

        if (next <= MaxCodePoint)
        {
            ranges.Add((next, MaxCodePoint));
        }

        return From(ranges);
    }

    /// <summary>Whether a code point is in the set: a binary search over the ranges.</summary>
    /// <param name="codePoint">The code point.</param>
    internal bool Contains(int codePoint)
    {
        int low = 0;
        int high = (pairs.Length / 2) - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) / 2);
            if (codePoint < pairs[middle * 2])
            {
                high = middle - 1;
            }
            else if (codePoint > pairs[(middle * 2) + 1])
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
}
