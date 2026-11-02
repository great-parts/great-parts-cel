using System.Collections.Generic;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelMap"/> over an array of entries in the order of insertion and an index from keys to
/// positions. Enumeration walks the array, so the order never depends on hash codes. The index is built by
/// insertions only and finds keys by <see cref="KeyHashRules"/> and <see cref="EqualityRules"/>.
/// </summary>
internal sealed class EntryTableMap : CelMap
{
    private readonly KeyValuePair<CelValue, CelValue>[] _entries;
    private readonly Dictionary<CelValue, int> _index;

    private EntryTableMap(KeyValuePair<CelValue, CelValue>[] entries, Dictionary<CelValue, int> index)
    {
        _entries = entries;
        _index = index;
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

    private protected override int CountCore => _entries.Length;

    /// <summary>
    /// Creates a map that takes over <paramref name="entries"/>; the caller does not change the array
    /// afterwards. A key of a kind that cannot be a map key is refused first, then a key equal to an
    /// earlier key, so <c>{1: 'a', 1u: 'b', 1.0: 'c'}</c> is refused for the kind of <c>1.0</c>.
    /// </summary>
    /// <param name="entries">The entries in the order of insertion.</param>
    /// <param name="refusal">Why the map was not created; <see cref="Refusal.None"/> when it was.</param>
    /// <param name="index">The position of the refused entry; otherwise -1.</param>
    /// <returns>The map; <see langword="null"/> when a key is refused.</returns>
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
        var positions = new Dictionary<CelValue, int>(entries.Length, KeyComparer.Instance);
        for (index = 0; index < entries.Length; index++)
        {
            if (!positions.TryAdd(entries[index].Key, index))
            {
                return null;
            }
        }

        refusal = Refusal.None;
        index = -1;
        return new EntryTableMap(entries, positions);
    }

    private protected override bool TryGetValueCore(CelValue key, out CelValue value)
    {
        if (_index.TryGetValue(key, out int position))
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

    // The index compares keys by the key rules. A probe without a hash, such as a double with a fraction or a
    // list, gets the hash 0 and equals no key, so a lookup by it finds nothing.
    private sealed class KeyComparer : IEqualityComparer<CelValue>
    {
        internal static readonly KeyComparer Instance = new();

        public bool Equals(CelValue x, CelValue y) => EqualityRules.KeysEqual(x, y);

        public int GetHashCode(CelValue obj)
        {
            _ = KeyHashRules.TryGetHash(obj, out int hash);
            return hash;
        }
    }
}
