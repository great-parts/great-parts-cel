using System.Collections;
using System.Collections.Generic;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// A read-only dictionary of objects over the non-generic dictionary of a map field: no entry is copied, a lookup asks
/// the field whether the key is there and then reads its value, and entries are enumerated in the order of insertion, which the map field keeps.
/// </summary>
/// <typeparam name="TKey">The key type of the field: <see cref="int"/>, <see cref="long"/>, <see cref="uint"/>, <see cref="ulong"/>, <see cref="bool"/> or <see cref="string"/>.</typeparam>
internal sealed class ObjectMapView<TKey> : IReadOnlyDictionary<TKey, object?>
    where TKey : notnull
{
    private readonly IDictionary _source;

    internal ObjectMapView(IDictionary source)
    {
        _source = source;
    }

    /// <inheritdoc/>
    public int Count => _source.Count;

    /// <inheritdoc/>
    public IEnumerable<TKey> Keys
    {
        get
        {
            IDictionaryEnumerator entries = _source.GetEnumerator();
            while (entries.MoveNext())
            {
                yield return (TKey)entries.Key;
            }
        }
    }

    /// <inheritdoc/>
    public IEnumerable<object?> Values
    {
        get
        {
            IDictionaryEnumerator entries = _source.GetEnumerator();
            while (entries.MoveNext())
            {
                yield return entries.Value;
            }
        }
    }

    /// <inheritdoc/>
    public object? this[TKey key] =>
        TryGetValue(key, out object? value) ? value : throw new KeyNotFoundException();

    /// <inheritdoc/>
    public bool ContainsKey(TKey key) => _source.Contains(key);

    // The non-generic indexer of a map field gives the default of the value type for a key that is not there, such as 0 for an
    // integer, so it cannot tell an absent key from an entry that holds the default: the presence is asked first.
    /// <inheritdoc/>
    public bool TryGetValue(TKey key, out object? value)
    {
        if (_source.Contains(key))
        {
            value = _source[key];
            return true;
        }

        value = null;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, object?>> GetEnumerator()
    {
        IDictionaryEnumerator entries = _source.GetEnumerator();
        while (entries.MoveNext())
        {
            yield return new KeyValuePair<TKey, object?>((TKey)entries.Key, entries.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
