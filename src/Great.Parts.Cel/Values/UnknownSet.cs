using System;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Values;

// One unknown part of a partial evaluation: the id of the node of the parsed expression where a value became unknown, and what it
// depends on — the trail of an attribute that matched a pattern of unknown data (a variable and the qualifiers of the access,
// none of which matches any), or a call not yet made (function, overload and arguments). The ids order the entries of a set.
internal sealed class UnknownEntry
{
    // An attribute: the full name of the variable and the qualifiers of the access.
    internal UnknownEntry(int id, string variable, ImmutableArray<CelValue?> qualifiers)
    {
        Id = id;
        Variable = variable;
        Qualifiers = qualifiers;
        Arguments = [];
    }

    // A call whose arguments are known and which has not been made.
    internal UnknownEntry(int id, string function, string overloadId, ImmutableArray<CelValue> arguments)
    {
        Id = id;
        Function = function;
        OverloadId = overloadId;
        Arguments = arguments;
        Qualifiers = [];
    }

    internal int Id { get; }

    // The variable of an attribute; null for a call.
    internal string? Variable { get; }

    internal ImmutableArray<CelValue?> Qualifiers { get; }

    // The function and the overload of a call; null for an attribute.
    internal string? Function { get; }

    internal string? OverloadId { get; }

    internal ImmutableArray<CelValue> Arguments { get; }
}

// The payload of an unknown value, which is a value of the kind Error: the entries, ordered by id. A node that a loop runs many times
// becomes unknown with a trail for each run — lx[0] and lx[1] for lx[i] — and cel-go keeps every trail under the id of the node, so
// a set has one entry of an attribute for each different trail of an id, and one entry of a call for an id. A set is immutable.
internal sealed class UnknownSet
{
    private UnknownSet(ImmutableArray<UnknownEntry> entries)
    {
        Entries = entries;
    }

    internal ImmutableArray<UnknownEntry> Entries { get; }

    internal static UnknownSet Of(UnknownEntry entry) => new([entry]);

    // The union of two sets: the entries of both ordered by id, and for an id in both the entries of the first, then those of the
    // second that the first has no entry of the same trail as. An operand that the other adds nothing to is the result itself.
    internal static UnknownSet Merge(UnknownSet first, UnknownSet second)
    {
        if (ReferenceEquals(first, second) || Contains(first, second))
        {
            return first;
        }

        if (Contains(second, first))
        {
            return second;
        }

        ImmutableArray<UnknownEntry>.Builder merged = ImmutableArray.CreateBuilder<UnknownEntry>(first.Entries.Length + second.Entries.Length);
        int i = 0;
        int j = 0;
        while (i < first.Entries.Length && j < second.Entries.Length)
        {
            int difference = first.Entries[i].Id.CompareTo(second.Entries[j].Id);
            if (difference < 0)
            {
                merged.Add(first.Entries[i++]);
            }
            else if (difference > 0)
            {
                merged.Add(second.Entries[j++]);
            }
            else
            {
                MergeId(first.Entries, ref i, second.Entries, ref j, merged);
            }
        }

        while (i < first.Entries.Length)
        {
            merged.Add(first.Entries[i++]);
        }

        while (j < second.Entries.Length)
        {
            merged.Add(second.Entries[j++]);
        }

        return new UnknownSet(merged.ToImmutable());
    }

    // The entries of one id, which both sets have at i and j: those of the first, then those of the second that the first has no
    // entry of the same trail as. i and j move past the entries of that id.
    private static void MergeId(
        ImmutableArray<UnknownEntry> first,
        ref int i,
        ImmutableArray<UnknownEntry> second,
        ref int j,
        ImmutableArray<UnknownEntry>.Builder merged)
    {
        int id = first[i].Id;
        int start = i;
        while (i < first.Length && first[i].Id == id)
        {
            merged.Add(first[i++]);
        }

        while (j < second.Length && second[j].Id == id)
        {
            if (!Has(first, start, i, second[j]))
            {
                merged.Add(second[j]);
            }

            j++;
        }
    }

    // Whether every entry of the other set has an entry of the same id and the same trail in this one.
    private static bool Contains(UnknownSet set, UnknownSet other)
    {
        int start = 0;
        foreach (UnknownEntry entry in other.Entries)
        {
            while (start < set.Entries.Length && set.Entries[start].Id < entry.Id)
            {
                start++;
            }

            int end = start;
            while (end < set.Entries.Length && set.Entries[end].Id == entry.Id)
            {
                end++;
            }

            if (!Has(set.Entries, start, end, entry))
            {
                return false;
            }
        }

        return true;
    }

    // Whether one of the entries from start up to end, which have the id of the entry, is the same unknown part: the same trail, an
    // int and a uint of one value naming one element; or, for a call, any entry, since a node makes one call.
    private static bool Has(ImmutableArray<UnknownEntry> entries, int start, int end, UnknownEntry entry)
    {
        for (int i = start; i < end; i++)
        {
            if (IsSame(entries[i], entry))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSame(UnknownEntry left, UnknownEntry right)
    {
        if (left.Variable is null || right.Variable is null)
        {
            return left.Variable is null && right.Variable is null;
        }

        if (!string.Equals(left.Variable, right.Variable, StringComparison.Ordinal) || left.Qualifiers.Length != right.Qualifiers.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Qualifiers.Length; i++)
        {
            if (!QualifiersEqual(left.Qualifiers[i], right.Qualifiers[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool QualifiersEqual(CelValue? left, CelValue? right) =>
        left is { } a && right is { } b
            ? CelValue.CelEquals(a, b).TryGetBoolean(out bool equal) && equal
            : left is null && right is null;
}
