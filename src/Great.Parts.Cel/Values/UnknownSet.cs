using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;

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
    // The groups of one id of two sets are compared entry by entry when the shorter has at most ScanSideLimit entries or when the
    // pairs are at most ScanPairLimit: that is within a constant of the entries of both, and allocates nothing, as the one entry
    // that a loop adds in each run needs. Longer groups are looked up by a hash of the trail.
    private const int ScanSideLimit = 4;
    private const int ScanPairLimit = 32;

    // The hash of a qualifier that is null, which stands for any key.
    private const int NullQualifierHash = 0x4E554C4C;

    private UnknownSet(ImmutableArray<UnknownEntry> entries)
    {
        Entries = entries;
    }

    internal ImmutableArray<UnknownEntry> Entries { get; }

    internal static UnknownSet Of(UnknownEntry entry) => new([entry]);

    // The units that a union of two sets costs, charged before it is made: 1 plus 1 per 10 entries of both sets, rounded up.
    internal static ulong MergeUnits(UnknownSet first, UnknownSet second) =>
        1 + (((ulong)first.Entries.Length + (ulong)second.Entries.Length + 9) / 10);

    // The union of two sets: the entries of both ordered by id, and for an id in both the entries of the first, then those of the
    // second that the first has no entry of the same trail as. An operand that the other adds nothing to is the result itself. The work
    // is linear in the entries of both sets (see GroupIndex for how an entry of an id is looked up among many of one id).
    internal static UnknownSet Merge(UnknownSet first, UnknownSet second)
    {
        if (ReferenceEquals(first, second))
        {
            return first;
        }

        var scratch = new Scratch(first.Entries, second.Entries);
        Holding holding = Compare(ref scratch);
        if ((holding & Holding.FirstHoldsSecond) != 0)
        {
            return first;
        }

        if ((holding & Holding.SecondHoldsFirst) != 0)
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
                MergeId(ref scratch, ref i, ref j, merged);
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
    private static void MergeId(ref Scratch scratch, ref int i, ref int j, ImmutableArray<UnknownEntry>.Builder merged)
    {
        ImmutableArray<UnknownEntry> first = scratch.First;
        ImmutableArray<UnknownEntry> second = scratch.Second;
        int firstStart = i;
        int secondStart = j;
        i = GroupEnd(first, i);
        j = GroupEnd(second, j);
        for (int k = firstStart; k < i; k++)
        {
            merged.Add(first[k]);
        }

        if (!UsesIndex(i - firstStart, j - secondStart))
        {
            for (int k = secondStart; k < j; k++)
            {
                if (!Has(first, firstStart, i, second[k]))
                {
                    merged.Add(second[k]);
                }
            }

            return;
        }

        (GroupIndex firstIndex, GroupIndex secondIndex) = scratch.IndexesOf(firstStart, i, secondStart, j);
        for (int k = 0; k < j - secondStart; k++)
        {
            if (!firstIndex.Has(secondIndex, k))
            {
                merged.Add(second[secondStart + k]);
            }
        }
    }

    // Which of the two sets holds the other: whether every entry of the second has an entry of the same id and the same trail in the
    // first, and the other way round. One pass over the ids of both sets; it ends early when neither can hold the other.
    private static Holding Compare(ref Scratch scratch)
    {
        ImmutableArray<UnknownEntry> first = scratch.First;
        ImmutableArray<UnknownEntry> second = scratch.Second;
        Holding holding = Holding.Both;
        int i = 0;
        int j = 0;
        while (i < first.Length && j < second.Length && holding != Holding.Neither)
        {
            int difference = first[i].Id.CompareTo(second[j].Id);
            if (difference < 0)
            {
                holding &= ~Holding.SecondHoldsFirst;
                i = GroupEnd(first, i);
            }
            else if (difference > 0)
            {
                holding &= ~Holding.FirstHoldsSecond;
                j = GroupEnd(second, j);
            }
            else
            {
                int firstEnd = GroupEnd(first, i);
                int secondEnd = GroupEnd(second, j);
                holding = CompareGroups(ref scratch, i, firstEnd, j, secondEnd, holding);
                i = firstEnd;
                j = secondEnd;
            }
        }

        // An id that only one set has is not held by the other.
        if (i < first.Length)
        {
            holding &= ~Holding.SecondHoldsFirst;
        }

        if (j < second.Length)
        {
            holding &= ~Holding.FirstHoldsSecond;
        }

        return holding;
    }

    // The part of holding that stays true after the entries of one id, from firstStart up to firstEnd in the first set and from
    // secondStart up to secondEnd in the second, are compared.
    private static Holding CompareGroups(ref Scratch scratch, int firstStart, int firstEnd, int secondStart, int secondEnd, Holding holding)
    {
        ImmutableArray<UnknownEntry> first = scratch.First;
        ImmutableArray<UnknownEntry> second = scratch.Second;
        bool firstMayHold = (holding & Holding.FirstHoldsSecond) != 0;
        bool secondMayHold = (holding & Holding.SecondHoldsFirst) != 0;
        if (!UsesIndex(firstEnd - firstStart, secondEnd - secondStart))
        {
            if (firstMayHold && !HoldsAll(first, firstStart, firstEnd, second, secondStart, secondEnd))
            {
                holding &= ~Holding.FirstHoldsSecond;
            }

            if (secondMayHold && !HoldsAll(second, secondStart, secondEnd, first, firstStart, firstEnd))
            {
                holding &= ~Holding.SecondHoldsFirst;
            }

            return holding;
        }

        (GroupIndex firstIndex, GroupIndex secondIndex) = scratch.IndexesOf(firstStart, firstEnd, secondStart, secondEnd);
        if (firstMayHold && !firstIndex.HoldsAll(secondIndex))
        {
            holding &= ~Holding.FirstHoldsSecond;
        }

        if (secondMayHold && !secondIndex.HoldsAll(firstIndex))
        {
            holding &= ~Holding.SecondHoldsFirst;
        }

        return holding;
    }

    // The end of the entries of one id that start at start.
    private static int GroupEnd(ImmutableArray<UnknownEntry> entries, int start)
    {
        int id = entries[start].Id;
        int end = start + 1;
        while (end < entries.Length && entries[end].Id == id)
        {
            end++;
        }

        return end;
    }

    // Whether two groups of one id are looked up by a hash of the trail, and not compared entry by entry (see ScanSideLimit).
    private static bool UsesIndex(int firstCount, int secondCount) =>
        Math.Min(firstCount, secondCount) > ScanSideLimit && (long)firstCount * secondCount > ScanPairLimit;

    // Whether every entry of the other group has the same unknown part in this one, all compared one by one.
    private static bool HoldsAll(
        ImmutableArray<UnknownEntry> entries,
        int start,
        int end,
        ImmutableArray<UnknownEntry> others,
        int otherStart,
        int otherEnd)
    {
        for (int i = otherStart; i < otherEnd; i++)
        {
            if (!Has(entries, start, end, others[i]))
            {
                return false;
            }
        }

        return true;
    }

    // Whether one of the entries from start up to end, which have the id of the entry, is the same unknown part: the same trail, an
    // int and a uint of one value naming one element; or, for a call, any entry, since a node makes one call. The entries are compared
    // one by one, which only a short group of one id is (UsesIndex); a long one is looked up by the hash of the trail and takes an
    // expected constant number of comparisons for an entry, one by one only for the entries that have no hash.
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

    // How an entry is looked up among those of its id.
    private enum EntryKind : byte
    {
        // An attribute whose qualifiers all have a hash that agrees with CEL equality: looked up by the hash and then by IsSame.
        Hashed,

        // An attribute with a qualifier that has no such hash (an object of the application, an error): compared one by one.
        Unhashed,

        // A call: it is the same as any call of its id.
        Call,

        // An attribute with a NaN as a qualifier: it is the same as no entry, itself included.
        NeverSame,
    }

    // Which of two sets holds the other, as flags: all of them while nothing is found against it.
    [Flags]
    private enum Holding : byte
    {
        Neither = 0,
        FirstHoldsSecond = 1,
        SecondHoldsFirst = 2,
        Both = 3,
    }

    // What one union keeps between its steps: the two sets, the hasher of the trails, and the lookups of the pairs of groups that
    // are too long to compare one by one, each built once for a union although the checks and the union itself ask for it.
    private struct Scratch
    {
        private ValueHasher _hasher;
        private Dictionary<int, (GroupIndex First, GroupIndex Second)>? _indexes;

        internal Scratch(ImmutableArray<UnknownEntry> first, ImmutableArray<UnknownEntry> second)
        {
            First = first;
            Second = second;
            _hasher = new ValueHasher(null, forCallTable: true);
            _indexes = null;
        }

        internal ImmutableArray<UnknownEntry> First { get; }

        internal ImmutableArray<UnknownEntry> Second { get; }

        // The lookups of the group of one id in both sets; the group of the first set starts at firstStart.
        internal (GroupIndex First, GroupIndex Second) IndexesOf(int firstStart, int firstEnd, int secondStart, int secondEnd)
        {
            _indexes ??= [];
            ref (GroupIndex First, GroupIndex Second) pair = ref CollectionsMarshal.GetValueRefOrAddDefault(_indexes, firstStart, out bool exists);
            if (!exists)
            {
                pair = (new GroupIndex(First, firstStart, firstEnd, ref _hasher), new GroupIndex(Second, secondStart, secondEnd, ref _hasher));
            }

            return pair;
        }
    }

    // The entries of one id of a set, looked up by their trail. An entry of an attribute has the hash of its variable and of its
    // qualifiers in order (ValueHasher in its mode for a table of calls, which agrees with CEL equality and keeps the random seed of
    // the process, so trails chosen to collide do not make a lookup linear), and is looked up among the entries of one hash by IsSame,
    // which is the equality that the comparison entry by entry used, so the answer is the same. An entry that no hash serves is kept
    // apart and compared one by one with every probe, as a probe that no hash serves is with every entry. The hashes are not charged:
    // the units of a union cover them (UnknownSet.MergeUnits).
    private sealed class GroupIndex
    {
        private readonly ImmutableArray<UnknownEntry> _entries;
        private readonly int _start;
        private readonly int[] _hashes;
        private readonly EntryKind[] _kinds;

        // The buckets of the hashed entries and, in _next, the entries of one bucket, as positions plus one; zero ends a chain.
        private readonly int[] _heads;
        private readonly int[] _next;
        private readonly int _unhashed;
        private readonly bool _hasCall;

        internal GroupIndex(ImmutableArray<UnknownEntry> entries, int start, int end, ref ValueHasher hasher)
        {
            int count = end - start;
            _entries = entries;
            _start = start;
            _hashes = new int[count];
            _kinds = new EntryKind[count];
            _heads = new int[(int)BitOperations.RoundUpToPowerOf2((uint)count)];
            _next = new int[count];
            int unhashed = 0;
            for (int position = 0; position < count; position++)
            {
                EntryKind kind = Classify(entries[start + position], ref hasher, out int hash);
                _hashes[position] = hash;
                _kinds[position] = kind;
                switch (kind)
                {
                    case EntryKind.Hashed:
                        int bucket = hash & (_heads.Length - 1);
                        _next[position] = _heads[bucket];
                        _heads[bucket] = position + 1;
                        break;
                    case EntryKind.Unhashed:
                        _next[position] = unhashed;
                        unhashed = position + 1;
                        break;
                    case EntryKind.Call:
                        _hasCall = true;
                        break;
                    default:
                        break;
                }
            }

            _unhashed = unhashed;
        }

        // Whether every entry of the other group has the same unknown part in this group.
        internal bool HoldsAll(GroupIndex other)
        {
            for (int position = 0; position < other._hashes.Length; position++)
            {
                if (!Has(other, position))
                {
                    return false;
                }
            }

            return true;
        }

        // Whether the entry of the other group at the position has the same unknown part in this group.
        internal bool Has(GroupIndex other, int position)
        {
            UnknownEntry probe = other._entries[other._start + position];
            switch (other._kinds[position])
            {
                case EntryKind.Call:
                    return _hasCall;
                case EntryKind.NeverSame:
                    return false;
                case EntryKind.Unhashed:
                    return HasOneByOne(probe);
                default:
                    return HasInBucket(probe, other._hashes[position]) || HasAmongUnhashed(probe);
            }
        }

        private static EntryKind Classify(UnknownEntry entry, ref ValueHasher hasher, out int hash)
        {
            hash = 0;
            if (entry.Variable is null)
            {
                return EntryKind.Call;
            }

            var code = default(HashCode);
            code.Add(string.GetHashCode(entry.Variable, StringComparison.Ordinal));
            foreach (CelValue? qualifier in entry.Qualifiers)
            {
                if (qualifier is not { } value)
                {
                    code.Add(NullQualifierHash);
                    continue;
                }

                if (value.Kind == CelValueKind.Double && value.TryGetDouble(out double number) && double.IsNaN(number))
                {
                    return EntryKind.NeverSame;
                }

                if (hasher.Measure(value, out int part) != ValueGroup.Hashed)
                {
                    return EntryKind.Unhashed;
                }

                code.Add(part);
            }

            code.Add(entry.Qualifiers.Length);
            hash = code.ToHashCode();
            return EntryKind.Hashed;
        }

        private bool HasOneByOne(UnknownEntry probe)
        {
            for (int position = 0; position < _hashes.Length; position++)
            {
                if (IsSame(_entries[_start + position], probe))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasInBucket(UnknownEntry probe, int hash)
        {
            for (int link = _heads[hash & (_heads.Length - 1)]; link != 0; link = _next[link - 1])
            {
                if (_hashes[link - 1] == hash && IsSame(_entries[_start + link - 1], probe))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAmongUnhashed(UnknownEntry probe)
        {
            for (int link = _unhashed; link != 0; link = _next[link - 1])
            {
                if (IsSame(_entries[_start + link - 1], probe))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
