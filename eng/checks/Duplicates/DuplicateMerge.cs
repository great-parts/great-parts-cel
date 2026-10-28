using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Joins the pairs that the searches under different symbol sets found. Pairs whose runs overlap in the source text at both
/// places are one pair, also through a chain of such pairs: a branch near the start of a repeat moves the run, and the move must
/// not add a pair. The joined pair keeps the places and the length of its longest run.
/// </summary>
internal static class DuplicateMerge
{
    /// <summary>Joins the pairs of the searches of all the symbol sets.</summary>
    /// <param name="bySet">The pairs found under each symbol set, in the order of the sets.</param>
    /// <returns>The pairs sorted by the first place, then by the second one.</returns>
    internal static IReadOnlyList<RawDuplicate> Join(IReadOnlyList<IReadOnlyList<RawDuplicate>> bySet)
    {
        List<(int Set, RawDuplicate Pair)> all = [.. bySet.SelectMany((pairs, set) => pairs.Select(pair => (set, pair)))];
        int[] roots = [.. Enumerable.Range(0, all.Count)];
        foreach (IGrouping<(int, int), int> group in Enumerable.Range(0, all.Count).GroupBy(index => (all[index].Pair.First.File, all[index].Pair.Second.File)))
        {
            Link(all, [.. group], roots);
        }

        return [.. Enumerable.Range(0, all.Count)
            .GroupBy(index => Root(roots, index))
            .Select(group => Longest(group.Select(index => all[index].Pair)))
            .OrderBy(pair => pair.First.File)
            .ThenBy(pair => pair.First.Start)
            .ThenBy(pair => pair.Second.File)
            .ThenBy(pair => pair.Second.Start)];
    }

    // The pairs of one pair of files that overlap at both places and come from different sets are one pair.
    private static void Link(List<(int Set, RawDuplicate Pair)> all, int[] members, int[] roots)
    {
        for (int left = 0; left < members.Length; left++)
        {
            for (int right = left + 1; right < members.Length; right++)
            {
                (int leftSet, RawDuplicate leftPair) = all[members[left]];
                (int rightSet, RawDuplicate rightPair) = all[members[right]];
                if (leftSet != rightSet && leftPair.First.Overlaps(rightPair.First) && leftPair.Second.Overlaps(rightPair.Second))
                {
                    roots[Root(roots, members[left])] = Root(roots, members[right]);
                }
            }
        }
    }

    private static int Root(int[] roots, int index)
    {
        while (roots[index] != index)
        {
            roots[index] = roots[roots[index]];
            index = roots[index];
        }

        return index;
    }

    // The longest run; for equal lengths the one with the earlier places, so the result does not depend on the order of the sets.
    private static RawDuplicate Longest(IEnumerable<RawDuplicate> pairs) => pairs
        .OrderByDescending(pair => pair.Tokens)
        .ThenBy(pair => pair.First.Start)
        .ThenBy(pair => pair.Second.Start)
        .First();
}
