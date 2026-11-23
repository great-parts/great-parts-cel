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

// The payload of an unknown value, which is a value of the kind Error: the entries, ordered by id, one for each id. A set is
// immutable.
internal sealed class UnknownSet
{
    private UnknownSet(ImmutableArray<UnknownEntry> entries)
    {
        Entries = entries;
    }

    internal ImmutableArray<UnknownEntry> Entries { get; }

    internal static UnknownSet Of(UnknownEntry entry) => new([entry]);

    // The union of two sets: the entries of both ordered by id, and for an id in both the entry of the first. An operand that
    // the other adds nothing to is the result itself.
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
            if (difference <= 0)
            {
                merged.Add(first.Entries[i++]);
                if (difference == 0)
                {
                    j++;
                }
            }
            else
            {
                merged.Add(second.Entries[j++]);
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

    // Whether every id of the other set is an id of this one.
    private static bool Contains(UnknownSet set, UnknownSet other)
    {
        int i = 0;
        foreach (UnknownEntry entry in other.Entries)
        {
            while (i < set.Entries.Length && set.Entries[i].Id < entry.Id)
            {
                i++;
            }

            if (i == set.Entries.Length || set.Entries[i].Id != entry.Id)
            {
                return false;
            }
        }

        return true;
    }
}
