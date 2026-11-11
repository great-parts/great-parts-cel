using System;
using System.Collections.Generic;
using System.Numerics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The accumulator of <c>transformMap</c> and <c>transformMapEntry</c> during the loop: an array of entries in the order of
/// insertion that grows by doubling from 4 entries as pairs are added, and, from the ninth entry on, the index of an
/// <see cref="EntryTableMap"/>, kept up to date so that a pair is found or put in with one probe. It never leaves the
/// comprehension: its result turns it into a map over the same array and index, made once.
/// </summary>
internal sealed class MapBuilder : CelMap
{
    private const int FirstCapacity = 4;

    private KeyValuePair<CelValue, CelValue>[] _entries = [];

    // The index of a builder of more than EntryTableMap.UnindexedLimit entries: a power of two of slots, at least twice the number
    // of entries, each the position of an entry plus 1 or 0 when empty; null for a smaller builder.
    private int[]? _slots;
    private int _count;

    private protected override int CountCore => _count;

    /// <summary>
    /// Adds a pair, unless the key equals by CEL equality the key of a pair that is in. The caller has checked that the key is
    /// of a kind of map keys.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the pair was added; <see langword="false"/> for a repeated key.</returns>
    internal bool TryAdd(CelValue key, CelValue value)
    {
        if (_count == _entries.Length)
        {
            Array.Resize(ref _entries, Math.Max(_entries.Length * 2, FirstCapacity));
        }

        _entries[_count] = new(key, value);
        if (_count + 1 > EntryTableMap.UnindexedLimit)
        {
            if (_slots is null || (_count + 1) * 2 > _slots.Length)
            {
                Reindex();
            }

            if (!EntryTableMap.TryInsert(_entries, _slots!, _count))
            {
                return false;
            }
        }
        else if (EntryTableMap.Find(_entries, _count, key) >= 0)
        {
            return false;
        }

        _count++;
        return true;
    }

    /// <summary>
    /// Turns the builder into the map of its pairs. The builder is not used afterwards.
    /// </summary>
    /// <returns>The map, in the order of insertion.</returns>
    internal CelMap ToMap() => EntryTableMap.Seal(_entries, _count, _slots);

    private protected override bool TryGetValueCore(CelValue key, out CelValue value)
    {
        int position = _slots is null ? EntryTableMap.Find(_entries, _count, key) : EntryTableMap.Find(_entries, _slots, key);
        if (position >= 0)
        {
            value = _entries[position].Value;
            return true;
        }

        value = default;
        return false;
    }

    private protected override IEnumerator<KeyValuePair<CelValue, CelValue>> GetEnumeratorCore() =>
        ((IEnumerable<KeyValuePair<CelValue, CelValue>>)new ArraySegment<KeyValuePair<CelValue, CelValue>>(_entries, 0, _count)).GetEnumerator();

    // A new index for the entries that are in and the one that comes: the next power of two of at least twice as many slots.
    private void Reindex()
    {
        int[] slots = new int[BitOperations.RoundUpToPowerOf2((uint)(_count + 1) * 2)];
        for (int position = 0; position < _count; position++)
        {
            _ = EntryTableMap.TryInsert(_entries, slots, position);
        }

        _slots = slots;
    }
}
