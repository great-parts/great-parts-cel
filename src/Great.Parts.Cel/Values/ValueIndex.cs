using System.Collections.Generic;
using System.Numerics;

namespace Great.Parts.Cel.Values;

// The values of a list kept in the order they came, looked up for the functions that need the first of each group of equal values or
// the membership of a value. A value is measured when it comes (ValueHasher), which charges the units of hashing it, and is then of
// one of three groups:
//
// - Hashed: looked up by its hash and, among the values of one hash, by CEL equality. The hash is of a value that CEL equality can
//   call equal, so a value of another hash is not compared. A short index scans the hashes in order and a longer one uses buckets;
//   the result and the charge are the same. The comparisons of values that only share a hash are not charged, for their number
//   depends on the seed of the process and the cost only on the data; the one comparison of a pair that is equal is charged, once.
// - Pairwise: a value with an object of the application or a message of protobuf inside, one that is an error, or one with a number
//   of 2^53 or more in absolute value (an int, a uint or a double, an infinity included): it is compared with the values of its group
//   in the order they came, as the pairs were, each charged before it is compared, and an error of the equality ends the lookup. The
//   code of the application is called in the order of that pass. A value of this group is equal to no value of another group: a
//   comparison of such two meets an object and a value that is not one, or a number of 2^53 or more and a number below it, and gives
//   "not equal".
// - Single: a value with a NaN inside, equal to nothing: kept, and never compared.
internal sealed class ValueIndex
{
    // An index of at most this many values scans the hashes in order and a longer one uses buckets. It is below 32, the longest list for
    // which a pass over the pairs does a constant number of steps per unit charged (docs/cost.md, "Hashing in distinct and sets"), and it
    // changes nothing that a call returns or charges.
    private const int ScanLimit = 24;

    private readonly IEvaluationMeter? _meter;
    private readonly TypeIndex? _types;
    private readonly IApplicationBoundary? _boundary;
    private readonly Slot[] _slots;
    private readonly int[]? _heads;
    private readonly int[]? _next;
    private readonly int _mask;
    private ValueHasher _hasher;
    private List<int>? _pairwise;
    private int _count;

    // An index that holds up to capacity values, with the buckets when capacity is above the limit of the scan. With constantHash
    // every hashed value has one hash, which only the tests ask for.
    internal ValueIndex(
        int capacity,
        IEvaluationMeter? meter,
        TypeIndex? types,
        IApplicationBoundary? boundary,
        bool constantHash = false)
    {
        _meter = meter;
        _types = types;
        _boundary = boundary;
        _hasher = new ValueHasher(meter, constantHash);
        _slots = capacity == 0 ? [] : new Slot[capacity];
        if (capacity > ScanLimit)
        {
            int size = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
            _heads = new int[size];
            _next = new int[capacity];
            _mask = size - 1;
        }
    }

    // The number of values kept.
    internal int Count => _count;

    // The values in the order they came.
    internal CelValue[] ToArray()
    {
        var values = new CelValue[_count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = _slots[i].Value;
        }

        return values;
    }

    // distinct: keeps the value when no equal value has come before it. Equal: one has, and the value is not kept. NotEqual: the value is
    // kept. Error and InsufficientStack: the equality of the value with one of its group failed.
    internal ValueEquality AddIfAbsent(CelValue value, out CelValue error)
    {
        ValueGroup group = _hasher.Measure(value, out int hash);
        ValueEquality result = Find(value, group, hash, out error);
        if (result == ValueEquality.NotEqual)
        {
            Store(value, group, hash);
            if (_heads is not null && group == ValueGroup.Hashed)
            {
                LinkLast(_count - 1);
            }
        }

        return result;
    }

    // sets: keeps the value without looking for an equal one, as the elements of the list that the others are looked up in; Seal ends
    // the building.
    internal void Append(CelValue value)
    {
        ValueGroup group = _hasher.Measure(value, out int hash);
        Store(value, group, hash);
    }

    // Links the hashed values so that a lookup finds the first of the equal ones, as the pairs in order did.
    internal void Seal()
    {
        if (_heads is null)
        {
            return;
        }

        for (int position = _count - 1; position >= 0; position--)
        {
            if (_slots[position].Group == ValueGroup.Hashed)
            {
                Link(position);
            }
        }
    }

    // sets: whether a value equal to the one given is among those appended, the first of them in order for a charge. Equal and NotEqual
    // are the answer; Error and InsufficientStack are as in AddIfAbsent.
    internal ValueEquality Contains(CelValue value, out CelValue error)
    {
        ValueGroup group = _hasher.Measure(value, out int hash);
        return Find(value, group, hash, out error);
    }

    private void Store(CelValue value, ValueGroup group, int hash)
    {
        int position = _count++;
        _slots[position] = new Slot(value, hash, group);
        if (group == ValueGroup.Pairwise)
        {
            (_pairwise ??= []).Add(position);
        }
    }

    // Links the value after the values of its bucket, which are the earlier ones: those of one bucket that are not equal to each other
    // are few, the collisions of the hash.
    private void LinkLast(int position)
    {
        int bucket = _slots[position].Hash & _mask;
        int entry = _heads![bucket];
        if (entry == 0)
        {
            _heads[bucket] = position + 1;
            return;
        }

        while (_next![entry - 1] != 0)
        {
            entry = _next[entry - 1];
        }

        _next[entry - 1] = position + 1;
    }

    // Links the value before the values of its bucket: for the values taken in the reverse order.
    private void Link(int position)
    {
        int bucket = _slots[position].Hash & _mask;
        _next![position] = _heads![bucket];
        _heads[bucket] = position + 1;
    }

    private ValueEquality Find(CelValue value, ValueGroup group, int hash, out CelValue error)
    {
        error = default;
        switch (group)
        {
            case ValueGroup.Single:
                return ValueEquality.NotEqual;
            case ValueGroup.Pairwise:
                return FindPairwise(value, out error);
            default:
                return _heads is null ? ScanHashed(value, hash, out error) : FindInBucket(value, hash, out error);
        }
    }

    // The pairs of the group in the order they came, each charged before it is compared, up to the first equal one.
    private ValueEquality FindPairwise(CelValue value, out CelValue error)
    {
        error = default;
        if (_pairwise is null)
        {
            return ValueEquality.NotEqual;
        }

        foreach (int position in _pairwise)
        {
            ValueEquality result = EqualityRules.AreEqual(value, _slots[position].Value, _meter, out error, _types, _boundary);
            if (result != ValueEquality.NotEqual)
            {
                return result;
            }
        }

        return ValueEquality.NotEqual;
    }

    private ValueEquality ScanHashed(CelValue value, int hash, out CelValue error)
    {
        error = default;
        for (int position = 0; position < _count; position++)
        {
            if (_slots[position].Group != ValueGroup.Hashed || _slots[position].Hash != hash)
            {
                continue;
            }

            ValueEquality result = Compare(value, position, out error);
            if (result != ValueEquality.NotEqual)
            {
                return result;
            }
        }

        return ValueEquality.NotEqual;
    }

    private ValueEquality FindInBucket(CelValue value, int hash, out CelValue error)
    {
        error = default;
        for (int entry = _heads![hash & _mask]; entry != 0; entry = _next![entry - 1])
        {
            int position = entry - 1;
            if (_slots[position].Hash != hash)
            {
                continue;
            }

            ValueEquality result = Compare(value, position, out error);
            if (result != ValueEquality.NotEqual)
            {
                return result;
            }
        }

        return ValueEquality.NotEqual;
    }

    // One comparison of a value with a hashed one of its hash: not charged when the values differ, and charged once, as the comparison
    // that found it, when they are equal.
    private ValueEquality Compare(CelValue value, int position, out CelValue error)
    {
        CelValue other = _slots[position].Value;
        ValueEquality result = EqualityRules.AreEqual(value, other, null, out error, _types, _boundary);
        if (result == ValueEquality.Equal && _meter is not null)
        {
            _ = EqualityRules.AreEqual(value, other, _meter, out _, _types, _boundary);
        }

        return result;
    }

    // A value, its hash and its group.
    private readonly struct Slot(CelValue value, int hash, ValueGroup group)
    {
        internal CelValue Value { get; } = value;

        internal int Hash { get; } = hash;

        internal ValueGroup Group { get; } = group;
    }
}
