using System;
using System.Collections.Generic;
using System.Numerics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelMap"/> over an array of entries in the order of insertion. Enumeration walks the array, so the
/// order never depends on hash codes. Keys compare by <see cref="EqualityRules"/>, and a key is found in constant
/// time: the empty map is one instance for the process, a map of up to 8 entries compares the key with each entry,
/// and a larger map has an index of positions with open addressing by <see cref="KeyHashRules"/>. A map of n entries
/// takes 56 + 64n bytes without the index and at most 80 + 80n with it.
/// </summary>
internal sealed class EntryTableMap : CelMap
{
    // The most entries of a map without an index.
    internal const int UnindexedLimit = 8;

    private readonly KeyValuePair<CelValue, CelValue>[] _entries;

    // The index of a map of more than UnindexedLimit entries, null for a smaller map: a power of two of slots, at least
    // twice the number of entries, each the position of an entry plus 1 or 0 when empty; a collision goes to the next slot.
    private readonly int[]? _slots;

    private EntryTableMap(KeyValuePair<CelValue, CelValue>[] entries, int[]? slots)
    {
        _entries = entries;
        _slots = slots;
    }

    /// <summary>
    /// Why a table of entries was not created.
    /// </summary>
    internal enum Refusal
    {
        /// <summary>The map was created.</summary>
        None,

        /// <summary>A key is of a kind that cannot be a map key.</summary>
        KeyKind,

        /// <summary>A key is equal by CEL equality to an earlier key.</summary>
        DuplicateKey,
    }

    /// <summary>
    /// The empty map, one instance for the process.
    /// </summary>
    internal static EntryTableMap Empty { get; } = new([], null);

    private protected override int CountCore => _entries.Length;

    /// <summary>
    /// Creates a map that takes over <paramref name="entries"/>; the caller does not change the array
    /// afterwards. A key of a kind that cannot be a map key is refused first, then a key equal to an
    /// earlier key, so <c>{1: 'a', 1u: 'b', 1.0: 'c'}</c> is refused for the kind of <c>1.0</c>.
    /// </summary>
    /// <param name="entries">The entries in the order of insertion.</param>
    /// <param name="refusal">Why the map was not created; <see cref="Refusal.None"/> when it was.</param>
    /// <param name="index">The position of the refused entry; otherwise -1.</param>
    /// <returns>The map, <see cref="Empty"/> for no entries; <see langword="null"/> when a key is refused.</returns>
    internal static EntryTableMap? TryCreate(KeyValuePair<CelValue, CelValue>[] entries, out Refusal refusal, out int index)
    {
        refusal = Refusal.KeyKind;
        for (index = 0; index < entries.Length; index++)
        {
            if (!KeyHashRules.IsKeyKind(entries[index].Key.Kind))
            {
                return null;
            }
        }

        refusal = Refusal.DuplicateKey;
        int[]? slots = entries.Length > UnindexedLimit ? new int[BitOperations.RoundUpToPowerOf2((uint)entries.Length * 2)] : null;
        for (index = 0; index < entries.Length; index++)
        {
            if (slots is null ? Find(entries, index, entries[index].Key) >= 0 : !TryInsert(entries, slots, index))
            {
                return null;
            }
        }

        refusal = Refusal.None;
        index = -1;
        return entries.Length == 0 ? Empty : new EntryTableMap(entries, slots);
    }

    /// <summary>
    /// Creates a map from the entries and the index of a <see cref="MapBuilder"/>, which already refused every key that
    /// cannot be a key and every repeated key. The map copies the entries into an array of their number, one allocation, and takes
    /// over the index; the array of the builder stays with the builder.
    /// </summary>
    /// <param name="entries">The array of the builder, whose first <paramref name="count"/> entries are the map.</param>
    /// <param name="count">The number of entries.</param>
    /// <param name="slots">The index of the builder: a power of two of slots, at least twice <paramref name="count"/> when the map has more than <see cref="UnindexedLimit"/> entries; otherwise it is not read.</param>
    /// <returns>The map, <see cref="Empty"/> for no entries.</returns>
    internal static EntryTableMap Seal(KeyValuePair<CelValue, CelValue>[] entries, int count, int[]? slots)
    {
        if (count == 0)
        {
            return Empty;
        }

        var exact = new KeyValuePair<CelValue, CelValue>[count];
        Array.Copy(entries, exact, count);
        return new EntryTableMap(exact, count > UnindexedLimit ? slots : null);
    }

    private protected override bool TryGetValueCore(CelValue key, out CelValue value)
    {
        int position = _slots is null ? Find(_entries, _entries.Length, key) : Find(_entries, _slots, key);
        if (position >= 0)
        {
            value = _entries[position].Value;
            return true;
        }

        value = default;
        return false;
    }

    private protected override IEnumerator<KeyValuePair<CelValue, CelValue>> GetEnumeratorCore()
    {
        return ((IEnumerable<KeyValuePair<CelValue, CelValue>>)_entries).GetEnumerator();
    }

    // The position of the key among the first count entries, compared with each; -1 when it is not there.
    internal static int Find(KeyValuePair<CelValue, CelValue>[] entries, int count, CelValue key)
    {
        ReadOnlySpan<KeyValuePair<CelValue, CelValue>> span = entries.AsSpan(0, count);
        for (int position = 0; position < span.Length; position++)
        {
            if (EqualityRules.KeysEqual(span[position].Key, key))
            {
                return position;
            }
        }

        return -1;
    }

    // The position of the key by the index; -1 when it is not there. A probe without a hash, such as a double with a
    // fraction or a list, equals no key.
    internal static int Find(KeyValuePair<CelValue, CelValue>[] entries, int[] slots, CelValue key)
    {
        if (!KeyHashRules.TryGetHash(key, out int hash))
        {
            return -1;
        }

        int mask = slots.Length - 1;
        for (int slot = hash & mask; slots[slot] != 0; slot = (slot + 1) & mask)
        {
            int position = slots[slot] - 1;
            if (EqualityRules.KeysEqual(entries[position].Key, key))
            {
                return position;
            }
        }

        return -1;
    }

    // Adds the entry at the position to the index; false when its key equals the key of an earlier entry.
    internal static bool TryInsert(KeyValuePair<CelValue, CelValue>[] entries, int[] slots, int position)
    {
        CelValue key = entries[position].Key;
        _ = KeyHashRules.TryGetHash(key, out int hash);
        int mask = slots.Length - 1;
        int slot = hash & mask;
        for (; slots[slot] != 0; slot = (slot + 1) & mask)
        {
            if (EqualityRules.KeysEqual(entries[slots[slot] - 1].Key, key))
            {
                return false;
            }
        }

        slots[slot] = position + 1;
        return true;
    }
}
