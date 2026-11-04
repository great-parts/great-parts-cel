using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The lazy views and the copies of the result rows of lists, maps and bytes. A view reads the list or the map of the
/// library on access and converts an element when it is read; it is created only when no element can fail its conversion
/// by the static type, so a read never fails. A copy converts every element at once and fails at the first that does not
/// convert. With a meter, the copy of an argument of an application function is charged first and allocates at most 64
/// bytes per unit of its formula: a list of n elements 1 + n, a map of n pairs 1 + 2n, a byte array of L bytes
/// 1 + ⌈L/10⌉.
/// </summary>
internal static partial class ConversionRules
{
    // A copy of a list: charged 1 + n first, then converted element by element, an element that is a copy charged too.
    private static bool TryCopy<T>(
        CelList list,
        ClrRow<T> element,
        CelType elementType,
        IEvaluationMeter? meter,
        [MaybeNull] out IReadOnlyList<T> result)
    {
        int count = list.Count;
        meter?.Charge(1 + (ulong)count);
        var items = new T[count];
        for (int index = 0; index < count; index++)
        {
            if (!element.TryConvert(list[index], elementType, meter, out T? item))
            {
                result = null;
                return false;
            }

            items[index] = item!;
        }

        result = ImmutableCollectionsMarshal.AsImmutableArray(items);
        return true;
    }

    // A copy of a map: charged 1 + 2n first, as a created map, then converted entry by entry, in the order of the map, a key
    // or a value that is a copy charged too. A string key, as a string or an object, is charged ⌈L/10⌉ before it is put
    // into the copy, which hashes it; the copy shares the string. An empty map is one instance.
    private static bool TryCopy<TKey, TValue>(
        CelMap map,
        ClrRow<TKey> keyRow,
        ClrRow<TValue> valueRow,
        CelType keyType,
        CelType valueType,
        IEvaluationMeter? meter,
        [MaybeNull] out IReadOnlyDictionary<TKey, TValue> result)
        where TKey : notnull
    {
        int count = map.Count;
        meter?.Charge(1 + (2 * (ulong)count));
        if (count == 0)
        {
            result = ReadOnlyDictionary<TKey, TValue>.Empty;
            return true;
        }

        var entries = new KeyValuePair<TKey, TValue>[count];
        int position = 0;
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if (!keyRow.TryConvert(entry.Key, keyType, meter, out TKey? key)
                || key is null
                || !valueRow.TryConvert(entry.Value, valueType, meter, out TValue? value))
            {
                result = null;
                return false;
            }

            if (meter is not null && entry.Key.TryGetString(out string? text))
            {
                meter.Charge(Tenths(text.Length));
            }

            entries[position++] = new(key, value!);
        }

        result = new MapCopy<TKey, TValue>(entries);
        return true;
    }

    // A copy of bytes as a byte array: charged 1 + ⌈L/10⌉ first when it is an argument.
    private static byte[] CopyBytes(ReadOnlyMemory<byte> bytes, IEvaluationMeter? meter)
    {
        meter?.Charge(1 + Tenths(bytes.Length));
        return bytes.ToArray();
    }

    // ⌈L/10⌉: a pass over a string or bytes of length L.
    private static ulong Tenths(int length) => ((ulong)length + 9) / 10;

    // A key of a lookup in a view, as a CEL value; a .NET value outside the rows finds nothing.
    private static bool TryKey<TKey>(TKey key, out CelValue result)
    {
        if (typeof(TKey) == typeof(object))
        {
            return TryFromObject(key, out result);
        }

        return TryFromClr(key, out result);
    }

    private sealed class ListView<T>(CelList list, ClrRow<T> element, CelType elementType) : IReadOnlyList<T>
    {
        public int Count => list.Count;

        public T this[int index]
        {
            get
            {
                _ = element.TryConvert(list[index], elementType, null, out T? item);
                return item!;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            int count = list.Count;
            for (int index = 0; index < count; index++)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // An immutable copy of a map: its entries in the order of the map, compared by EqualityComparer<TKey>.Default with
    // each up to UnindexedLimit entries, and found by an index of positions for more, so that a lookup takes constant
    // time. Keys equal in CEL are one key of the map, so the converted keys are distinct. A copy of n entries of s bytes
    // each, s at most 32, takes 56 + n·s bytes without the index.
    private sealed class MapCopy<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
        where TKey : notnull
    {
        // The most entries of a copy without an index.
        private const int UnindexedLimit = 8;

        private readonly KeyValuePair<TKey, TValue>[] _entries;

        // The positions of the keys; null for a copy of up to UnindexedLimit entries.
        private readonly Dictionary<TKey, int>? _index;

        internal MapCopy(KeyValuePair<TKey, TValue>[] entries)
        {
            _entries = entries;
            if (entries.Length > UnindexedLimit)
            {
                _index = new Dictionary<TKey, int>(entries.Length);
                for (int position = 0; position < entries.Length; position++)
                {
                    _index.Add(entries[position].Key, position);
                }
            }
        }

        public int Count => _entries.Length;

        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (KeyValuePair<TKey, TValue> entry in _entries)
                {
                    yield return entry.Key;
                }
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (KeyValuePair<TKey, TValue> entry in _entries)
                {
                    yield return entry.Value;
                }
            }
        }

        public TValue this[TKey key] => TryGetValue(key, out TValue? value)
            ? value
            : throw new KeyNotFoundException("The key is not in the map.");

        public bool ContainsKey(TKey key) => Find(key) >= 0;

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            int position = Find(key);
            if (position >= 0)
            {
                value = _entries[position].Value;
                return true;
            }

            value = default;
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            ((IEnumerable<KeyValuePair<TKey, TValue>>)_entries).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // The position of the key; -1 when it is not there. A null key is refused, as by a dictionary.
        private int Find(TKey key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (_index is not null)
            {
                return _index.TryGetValue(key, out int found) ? found : -1;
            }

            EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;
            for (int position = 0; position < _entries.Length; position++)
            {
                if (comparer.Equals(_entries[position].Key, key))
                {
                    return position;
                }
            }

            return -1;
        }
    }

    private sealed class MapView<TKey, TValue>(CelMap map, ClrRow<TKey> keyRow, ClrRow<TValue> valueRow, CelType keyType, CelType valueType)
        : IReadOnlyDictionary<TKey, TValue>
    {
        public int Count => map.Count;

        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (KeyValuePair<CelValue, CelValue> entry in map)
                {
                    yield return Key(entry.Key);
                }
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                foreach (KeyValuePair<CelValue, CelValue> entry in map)
                {
                    yield return Value(entry.Value);
                }
            }
        }

        public TValue this[TKey key] => TryGetValue(key, out TValue? value)
            ? value
            : throw new KeyNotFoundException("The key is not in the map.");

        public bool ContainsKey(TKey key) => TryKey(key, out CelValue celKey) && map.TryGetValue(celKey, out _);

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (TryKey(key, out CelValue celKey) && map.TryGetValue(celKey, out CelValue celValue))
            {
                value = Value(celValue);
                return true;
            }

            value = default;
            return false;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (KeyValuePair<CelValue, CelValue> entry in map)
            {
                yield return new KeyValuePair<TKey, TValue>(Key(entry.Key), Value(entry.Value));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private TKey Key(CelValue key)
        {
            _ = keyRow.TryConvert(key, keyType, null, out TKey? converted);
            return converted!;
        }

        private TValue Value(CelValue value)
        {
            _ = valueRow.TryConvert(value, valueType, null, out TValue? converted);
            return converted!;
        }
    }
}
