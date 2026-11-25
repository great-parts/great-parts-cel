using System;
using System.Buffers;
using System.Numerics;

namespace Great.Parts.Cel.Values;

// How an index looks up the values of one hash. The result and the units charged do not depend on it; the tests and the benchmark ask
// for each way, and a call of the library uses Automatic.
internal enum ValueIndexMode : byte
{
    // An index of at most ValueIndex.ScanLimit values scans the hashes in order and a longer one uses buckets.
    Automatic,

    // The hashes are scanned in order, whatever the length.
    Scan,

    // The values are looked up in buckets, whatever the length.
    Buckets,
}

// The values of a list kept in the order they came, looked up for the functions that need the first of each group of equal values or
// the membership of a value. A value is measured when it comes (ValueHasher), which charges the units of hashing it, and is then of
// one of three groups:
//
// - Hashed: looked up by its hash and, among the values of one hash, by CEL equality. The hash is of a value that CEL equality can
//   call equal, so a value of another hash is not compared. The hashes of these values are kept in an array of their own, in the
//   order the values came, next to the positions of the values. A short index scans that array with a vector search of the platform
//   (MemoryExtensions.IndexOf over ReadOnlySpan<int>) and calls the equality for a position of the same hash only; a longer one uses
//   buckets; the result and the charge are the same. The comparisons of values that only share a hash are not charged, for their
//   number depends on the seed of the process and the cost only on the data; the one comparison of a pair that is equal is charged,
//   once.
// - Pairwise: a value with an object of the application or a message of protobuf inside, one that is an error, or one with a number
//   of 2^53 or more in absolute value (an int, a uint or a double, an infinity included): it is compared with the values of its group
//   in the order they came, as the pairs were, each charged before it is compared, and an error of the equality ends the lookup. The
//   code of the application is called in the order of that pass. A value of this group is equal to no value of another group: a
//   comparison of such two meets an object and a value that is not one, or a number of 2^53 or more and a number below it, and gives
//   "not equal".
// - Single: a value with a NaN inside, equal to nothing: kept, and never compared.
//
// The arrays are rented from the shared pool; Release gives them back, cleared where they hold values, and the caller calls it in a
// finally block. The index is not used after that.
internal sealed class ValueIndex
{
    // An index of at most this many values scans the hashes in order and a longer one uses buckets. The limit is the length up to
    // which the scan is not slower than the buckets; it is below 64, the longest list for which a search of the hashes does a
    // constant number of steps per unit charged (docs/cost.md, "Hashing in distinct and sets"), and it changes nothing that a call
    // returns or charges.
    internal const int ScanLimit = 32;

    private readonly IEvaluationMeter? _meter;
    private readonly TypeIndex? _types;
    private readonly IApplicationBoundary? _boundary;
    private readonly int _capacity;
    private readonly bool _buckets;
    private readonly int _mask;
    private readonly CelValue[] _values;
    private readonly int[] _hashes;
    private readonly int[] _positions;
    private readonly int[]? _heads;
    private readonly int[]? _next;
    private int[]? _pairwise;
    private ValueHasher _hasher;
    private int _count;
    private int _hashedCount;
    private int _pairwiseCount;

    // An index that holds up to capacity values. With constantHash every hashed value has one hash, which only the tests ask for;
    // with mode the tests and the benchmark choose the scan or the buckets for a capacity of any size.
    internal ValueIndex(
        int capacity,
        IEvaluationMeter? meter,
        TypeIndex? types,
        IApplicationBoundary? boundary,
        bool constantHash = false,
        ValueIndexMode mode = ValueIndexMode.Automatic)
    {
        _meter = meter;
        _types = types;
        _boundary = boundary;
        _capacity = capacity;
        _hasher = new ValueHasher(meter, constantHash);
        _values = Rent<CelValue>(capacity);
        _hashes = Rent<int>(capacity);
        _positions = Rent<int>(capacity);
        _buckets = capacity > 0 && (mode == ValueIndexMode.Buckets || (mode == ValueIndexMode.Automatic && capacity > ScanLimit));
        if (_buckets)
        {
            int size = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
            _heads = Rent<int>(size);
            _heads.AsSpan(0, size).Clear();
            _next = Rent<int>(capacity);
            _mask = size - 1;
        }
    }

    // The number of values kept.
    internal int Count => _count;

    // The values in the order they came, in an array of their own.
    internal CelValue[] ToArray() => _values.AsSpan(0, _count).ToArray();

    // Gives the arrays back to the pool; the index is not used afterwards.
    internal void Release()
    {
        _values.AsSpan(0, _count).Clear();
        Return(_values);
        Return(_hashes);
        Return(_positions);
        Return(_heads);
        Return(_next);
        Return(_pairwise);
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
            if (_buckets && group == ValueGroup.Hashed)
            {
                LinkLast(_hashedCount - 1);
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
        if (!_buckets)
        {
            return;
        }

        for (int entry = _hashedCount - 1; entry >= 0; entry--)
        {
            Link(entry);
        }
    }

    // sets: whether a value equal to the one given is among those appended, the first of them in order for a charge. Equal and NotEqual
    // are the answer; Error and InsufficientStack are as in AddIfAbsent.
    internal ValueEquality Contains(CelValue value, out CelValue error)
    {
        ValueGroup group = _hasher.Measure(value, out int hash);
        return Find(value, group, hash, out error);
    }

    private static T[] Rent<T>(int length) => length == 0 ? [] : ArrayPool<T>.Shared.Rent(length);

    private static void Return<T>(T[]? array)
    {
        if (array is { Length: > 0 })
        {
            ArrayPool<T>.Shared.Return(array);
        }
    }

    private void Store(CelValue value, ValueGroup group, int hash)
    {
        int position = _count++;
        _values[position] = value;
        switch (group)
        {
            case ValueGroup.Hashed:
                _hashes[_hashedCount] = hash;
                _positions[_hashedCount++] = position;
                break;
            case ValueGroup.Pairwise:
                (_pairwise ??= Rent<int>(_capacity))[_pairwiseCount++] = position;
                break;
            default:
                break;
        }
    }

    // Links the hashed value, numbered in the order the hashed values came, after the values of its bucket, which are the earlier ones:
    // those of one bucket that are not equal to each other are few, the collisions of the hash.
    private void LinkLast(int entry)
    {
        int bucket = _hashes[entry] & _mask;
        _next![entry] = 0;
        int last = _heads![bucket];
        if (last == 0)
        {
            _heads[bucket] = entry + 1;
            return;
        }

        while (_next[last - 1] != 0)
        {
            last = _next[last - 1];
        }

        _next[last - 1] = entry + 1;
    }

    // Links the value before the values of its bucket: for the values taken in the reverse order.
    private void Link(int entry)
    {
        int bucket = _hashes[entry] & _mask;
        _next![entry] = _heads![bucket];
        _heads[bucket] = entry + 1;
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
                return _buckets ? FindInBucket(value, hash, out error) : ScanHashed(value, hash, out error);
        }
    }

    // The pairs of the group in the order they came, each charged before it is compared, up to the first equal one.
    private ValueEquality FindPairwise(CelValue value, out CelValue error)
    {
        error = default;
        for (int i = 0; i < _pairwiseCount; i++)
        {
            ValueEquality result = EqualityRules.AreEqual(value, _values[_pairwise![i]], _meter, out error, _types, _boundary);
            if (result != ValueEquality.NotEqual)
            {
                return result;
            }
        }

        return ValueEquality.NotEqual;
    }

    // The hashed values with the hash given, in the order they came, found by a vector search of the array of hashes, each compared
    // by CEL equality up to the first equal one.
    private ValueEquality ScanHashed(CelValue value, int hash, out CelValue error)
    {
        error = default;
        ReadOnlySpan<int> hashes = _hashes.AsSpan(0, _hashedCount);
        int from = 0;
        while (from < hashes.Length)
        {
            int offset = hashes.Slice(from).IndexOf(hash);
            if (offset < 0)
            {
                break;
            }

            int entry = from + offset;
            ValueEquality result = Compare(value, _positions[entry], out error);
            if (result != ValueEquality.NotEqual)
            {
                return result;
            }

            from = entry + 1;
        }

        return ValueEquality.NotEqual;
    }

    private ValueEquality FindInBucket(CelValue value, int hash, out CelValue error)
    {
        error = default;
        for (int link = _heads![hash & _mask]; link != 0; link = _next![link - 1])
        {
            int entry = link - 1;
            if (_hashes[entry] != hash)
            {
                continue;
            }

            ValueEquality result = Compare(value, _positions[entry], out error);
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
        CelValue other = _values[position];
        ValueEquality result = EqualityRules.AreEqual(value, other, null, out error, _types, _boundary);
        if (result == ValueEquality.Equal && _meter is not null)
        {
            _ = EqualityRules.AreEqual(value, other, _meter, out _, _types, _boundary);
        }

        return result;
    }
}
