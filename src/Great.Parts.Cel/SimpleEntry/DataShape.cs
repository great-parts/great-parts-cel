using System;
using System.Collections.Generic;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

// One variable of the data of a call: its name, CEL type and converted value. The slots of a dictionary are kept in a pooled buffer
// sorted by name with string.CompareOrdinal, so that two dictionaries of the same names and types have one shape whatever the order
// in which they enumerate.
internal struct ShapeSlot
{
    internal string Name;
    internal CelType Type;
    internal CelValue Value;
}

// The slots of the data of one call, to look a shape up in level 1 of the cache without making its key.
internal readonly struct ShapeProbe(ShapeSlot[] slots, int count)
{
    internal ShapeSlot[] Slots { get; } = slots;

    internal int Count { get; } = count;

    internal int Hash { get; } = ShapeKey.HashOf(slots, count);
}

// The key of level 1 of the cache: the type of an anonymous object, or the sorted list of the names and CEL types of a dictionary.
internal sealed class ShapeKey
{
    private readonly string[] _names;
    private readonly CelType[] _types;

    // The shape of the anonymous objects of one type.
    internal ShapeKey(Type anonymousType)
    {
        AnonymousType = anonymousType;
        _names = [];
        _types = [];
        Hash = anonymousType.GetHashCode();
    }

    // The shape of a dictionary: a copy of the names and types of the probe.
    internal ShapeKey(ShapeProbe probe)
    {
        _names = new string[probe.Count];
        _types = new CelType[probe.Count];
        for (int i = 0; i < probe.Count; i++)
        {
            _names[i] = probe.Slots[i].Name;
            _types[i] = probe.Slots[i].Type;
        }

        Hash = probe.Hash;
    }

    internal Type? AnonymousType { get; }

    internal int Hash { get; }

    internal int Count => _names.Length;

    internal string NameAt(int index) => _names[index];

    internal CelType TypeAt(int index) => _types[index];

    internal bool Matches(ShapeProbe probe)
    {
        if (AnonymousType is not null || probe.Count != _names.Length || probe.Hash != Hash)
        {
            return false;
        }

        for (int i = 0; i < _names.Length; i++)
        {
            if (!string.Equals(_names[i], probe.Slots[i].Name, StringComparison.Ordinal) || !_types[i].Equals(probe.Slots[i].Type))
            {
                return false;
            }
        }

        return true;
    }

    internal bool Matches(ShapeKey other)
    {
        if (AnonymousType is not null || other.AnonymousType is not null)
        {
            return AnonymousType == other.AnonymousType;
        }

        if (_names.Length != other._names.Length || Hash != other.Hash)
        {
            return false;
        }

        for (int i = 0; i < _names.Length; i++)
        {
            if (!string.Equals(_names[i], other._names[i], StringComparison.Ordinal) || !_types[i].Equals(other._types[i]))
            {
                return false;
            }
        }

        return true;
    }

    // The hash of the names and types of the first `count` slots; the order counts.
    internal static int HashOf(ShapeSlot[] slots, int count)
    {
        var hash = default(HashCode);
        for (int i = 0; i < count; i++)
        {
            hash.Add(slots[i].Name, StringComparer.Ordinal);
            hash.Add(slots[i].Type);
        }

        return hash.ToHashCode();
    }
}

// The comparer of level 1: a key by its shape, a probe as the alternate form of a key.
internal sealed class ShapeComparer : IEqualityComparer<ShapeKey>, IAlternateEqualityComparer<ShapeProbe, ShapeKey>
{
    internal static readonly ShapeComparer Instance = new();

    public bool Equals(ShapeKey? x, ShapeKey? y) => ReferenceEquals(x, y) || (x is not null && y is not null && x.Matches(y));

    public int GetHashCode(ShapeKey obj) => obj.Hash;

    public bool Equals(ShapeProbe alternate, ShapeKey other) => other.Matches(alternate);

    public int GetHashCode(ShapeProbe alternate) => alternate.Hash;

    public ShapeKey Create(ShapeProbe alternate) => new(alternate);
}
