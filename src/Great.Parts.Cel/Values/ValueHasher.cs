using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Values;

// How the value of a list element is looked up by the functions that hash the elements of a list (distinct and the functions of the
// extension set sets).
internal enum ValueGroup : byte
{
    // The value has a hash that agrees with CEL equality; it is looked up by the hash and then by equality.
    Hashed,

    // Somewhere inside the value there is an object of the application, a message of protobuf, a value that is an error, a map that
    // gives an error before it compares, or an int, a uint or a double of 2^53 or more in absolute value, an infinity included: its
    // equality reads application data, gives an error, or is not transitive, so it has no hash and is compared in the order of the
    // pairs, with the values of its group only. The hash of a table of asynchronous calls puts only the objects, the errors and the
    // maps that give an error here: it hashes the large numbers and a NaN, see ValueHasher.
    Pairwise,

    // Somewhere inside the value there is a NaN, which equals nothing, not even itself: it is not compared with anything.
    Single,
}

// The measure of one value: its hash, its group, and the units that walking it charges. The walk is iterative, with a stack of its own
// that is kept between the values of one call, so that a value nested a hundred thousand levels is not a recursion and a flat value
// allocates nothing.
//
// The hash agrees with CEL equality: an int, a uint and a double of one value below 2^53 have one hash, so have two strings of one content,
// two lists of elements of equal hashes in order, two maps whose entries have equal hashes in any order, and two optionals of equal
// values. The hash of a number and of a string has the random seed of the process, so values chosen to collide do not make a lookup
// linear. A value of the group Pairwise or Single has no hash that is used.
//
// The units, charged before the node they pay for, in the order of the walk (docs/cost.md, "Hashing in distinct and sets"): 1 for a node, ⌈L/10⌉ more for a
// string or a bytes value of length L; a list charges its elements one by one after its own unit, a map the keys and values of its
// entries, an optional its value. An object of the application and a message of protobuf are one node.
internal struct ValueHasher
{
    private const int InitialDepth = 4;
    private const int EmptyOptionalHash = 0x4F505431;
    private const int NoHash = 0;
    private const int NaNHash = 0x4E614E21;

    // 2^53: the numbers below it in absolute value are the same as the doubles they round to.
    private const long ExactLimit = 1L << 53;

    private readonly IEvaluationMeter? _meter;
    private readonly bool _constantHash;
    private readonly bool _forCallTable;
    private Frame[]? _frames;
    private int _depth;

    // The kinds of node that decide the group of a value.
    private bool _pairwise;
    private bool _hasNaN;

    // With constantHash every value has the hash 0, so all the values of the group Hashed share one bucket: the tests make the
    // collisions that a random seed hardly gives. The units charged and the groups do not change.
    //
    // With forCallTable the hash serves the table of the asynchronous calls of one evaluation, which finds a call again by the
    // same arguments of the same node and not by a class of equal values: a NaN hashes by one constant, a number of 2^53 or more in
    // absolute value, an infinity included, hashes by the double it is compared as, and neither makes the group Single or
    // Pairwise. Equal numbers are the same double, so equal values hash alike; integers that round to one double share a hash, at
    // most 2^11 of them for each double.
    internal ValueHasher(IEvaluationMeter? meter, bool constantHash = false, bool forCallTable = false)
    {
        _meter = meter;
        _constantHash = constantHash;
        _forCallTable = forCallTable;
    }

    // Walks the value, charging its units, and gives its group and, for a value of the group Hashed, its hash.
    internal ValueGroup Measure(CelValue root, out int hash)
    {
        _pairwise = false;
        _hasNaN = false;
        try
        {
            hash = Walk(root);
        }
        finally
        {
            ReleaseFrames();
        }

        if (_constantHash)
        {
            hash = NoHash;
        }

        if (_pairwise)
        {
            return ValueGroup.Pairwise;
        }

        return _hasNaN ? ValueGroup.Single : ValueGroup.Hashed;
    }

    private int Walk(CelValue root)
    {
        CelValue current = root;
        bool done = false;
        int result = 0;
        while (true)
        {
            if (!done)
            {
                done = Enter(current, out result);
            }

            if (done)
            {
                if (_depth == 0)
                {
                    return result;
                }

                _frames![_depth - 1].Take(result);
                done = false;
            }

            if (_frames![_depth - 1].Next(_meter, out current))
            {
                continue;
            }

            result = _frames[--_depth].Finish();
            _frames[_depth] = default;
            done = true;
        }
    }

    // Charges the node and gives its hash when it has no children; a node with children is pushed on the stack and gives false.
    private bool Enter(CelValue value, out int hash)
    {
        _meter?.Charge(1);
        hash = 0;
        switch (value.Kind)
        {
            case CelValueKind.Int:
            case CelValueKind.UInt:
            case CelValueKind.Double:
            case CelValueKind.Bool:
                hash = Scalar(value);
                return true;
            case CelValueKind.String:
                hash = StringHash(value);
                return true;
            case CelValueKind.Bytes:
                hash = BytesHash(value);
                return true;
            case CelValueKind.Object:
            case CelValueKind.Error:
                _pairwise = true;
                return true;
            case CelValueKind.List:
                return EnterList(value, out hash);
            case CelValueKind.Map:
                return EnterMap(value, out hash);
            case CelValueKind.Optional:
                return EnterOptional(value, out hash);
            default:
                hash = OtherHash(value);
                return true;
        }
    }

    // A type by its name, as CEL equality compares types; null, timestamps, durations, decimals and abstract values by the .NET equality
    // of the value, which is the CEL equality of the kind (two decimals are equal by value, not by cohort).
    private static int OtherHash(CelValue value)
    {
        if (value.Kind == CelValueKind.Type)
        {
            return value.TryGetType(out Types.CelType? type) ? string.GetHashCode(type.Name, StringComparison.Ordinal) : NoHash;
        }

        return value.GetHashCode();
    }

    // A bool, or a number below 2^53 in absolute value by the double it is compared as: CEL equality compares an int or a uint with a
    // double as the integer rounded to the nearest double, and an int with a uint exactly. Below 2^53 the rounding is exact, so two
    // numbers that are equal are the same double and two that are not equal are not. A number of 2^53 or more, an infinity included,
    // is not hashed: integers that round to one double are equal to it but not to each other, so equality is not transitive there and
    // a hash by the double would give a list of such integers as many comparisons as a call has, and the value goes to the group
    // Pairwise. Both zeros are one. The hash of a table of calls takes such numbers by the double all the same, and a NaN by a constant.
    private int Scalar(CelValue value)
    {
        double number;
        bool exact = true;
        switch (value.Kind)
        {
            case CelValueKind.Bool:
                return value.TryGetBoolean(out bool flag) && flag ? 1 : 0;
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long integer);
                exact = integer is > -ExactLimit and < ExactLimit;
                number = integer;
                break;
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                exact = unsigned < ExactLimit;
                number = unsigned;
                break;
            default:
                _ = value.TryGetDouble(out number);
                exact = double.IsNaN(number) || Math.Abs(number) < ExactLimit;
                break;
        }

        _pairwise |= !exact && !_forCallTable;
        if (_pairwise)
        {
            return NoHash;
        }

        if (double.IsNaN(number))
        {
            _hasNaN |= !_forCallTable;
            return _forCallTable ? NaNHash : NoHash;
        }

        return HashCode.Combine(BitConverter.DoubleToUInt64Bits(number == 0 ? 0 : number));
    }

    private int StringHash(CelValue value)
    {
        string text = value.TryGetString(out string? held) ? held : string.Empty;
        _meter?.Charge(((ulong)text.Length + 9) / 10);
        return text.GetHashCode(StringComparison.Ordinal);
    }

    private int BytesHash(CelValue value)
    {
        _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
        _meter?.Charge(((ulong)bytes.Length + 9) / 10);
        var hash = default(HashCode);
        hash.AddBytes(bytes.Span);
        return hash.ToHashCode();
    }

    private bool EnterList(CelValue value, out int hash)
    {
        _ = value.TryGetList(out CelList? list);
        Push(Frame.ForList(list!));
        hash = 0;
        return false;
    }

    private bool EnterMap(CelValue value, out int hash)
    {
        hash = 0;
        _ = value.TryGetMap(out CelMap? map);

        // A map that looks at all its keys for an error, a JSON object with a repeated name, gives it when it is compared.
        if (map!.TryGetKeyError(_meter, out _))
        {
            _pairwise = true;
            return true;
        }

        if (_meter is not null)
        {
            map.ChargeEnumeration(_meter);
        }

        Push(Frame.ForMap(map, map.EnumerateEntries(_meter, readValues: true)));
        return false;
    }

    private bool EnterOptional(CelValue value, out int hash)
    {
        _ = value.TryGetOptional(out CelOptional<CelValue> optional);
        if (!optional.HasValue)
        {
            hash = EmptyOptionalHash;
            return true;
        }

        Push(Frame.ForOptional(optional.Value));
        hash = 0;
        return false;
    }

    private void Push(Frame frame)
    {
        _frames ??= new Frame[InitialDepth];
        if (_depth == _frames.Length)
        {
            Array.Resize(ref _frames, _depth * 2);
        }

        _frames[_depth++] = frame;
    }

    // Disposes the enumerators of the frames that an exception of the meter left on the stack.
    private void ReleaseFrames()
    {
        while (_depth > 0)
        {
            _frames![--_depth].Entries?.Dispose();
            _frames[_depth] = default;
        }
    }

    // A value with children that is being walked: the list, map or optional, and how far its children are.
    private struct Frame
    {
        private const int NextEntry = 0;
        private const int Key = 1;
        private const int KeyDone = 2;
        private const int Value = 3;

        private CelList? _list;
        private CelValue _single;
        private CelValue _entryValue;
        private int _count;
        private int _index;
        private int _accumulator;
        private int _pendingKey;
        private int _phase;
        private bool _isOptional;

        internal IEnumerator<KeyValuePair<CelValue, CelValue>>? Entries { get; private set; }

        internal static Frame ForList(CelList list) => new() { _list = list, _count = list.Count };

        internal static Frame ForMap(CelMap map, IEnumerator<KeyValuePair<CelValue, CelValue>> entries) =>
            new() { Entries = entries, _count = map.Count };

        internal static Frame ForOptional(CelValue held) => new() { _single = held, _isOptional = true, _count = 1 };

        // Ends the frame and gives its hash.
        internal int Finish()
        {
            Entries?.Dispose();
            return _isOptional ? _accumulator : HashCode.Combine(_accumulator, _count);
        }

        // Takes the hash of the child that was delivered last.
        internal void Take(int hash)
        {
            if (Entries is null)
            {
                _accumulator = _isOptional ? hash : HashCode.Combine(_accumulator, hash);
            }
            else if (_phase == Key)
            {
                _pendingKey = hash;
                _phase = KeyDone;
            }
            else
            {
                // The hash of a map does not depend on the order of the entries: the sum of the hashes of the entries.
                _accumulator += HashCode.Combine(_pendingKey, hash);
                _phase = NextEntry;
            }
        }

        // Delivers the next child, or gives false when there is none.
        internal bool Next(IEvaluationMeter? meter, out CelValue child)
        {
            child = default;
            if (_list is not null)
            {
                if (_index >= _count)
                {
                    return false;
                }

                child = _list.Read(_index++, meter);
                return true;
            }

            if (_isOptional)
            {
                if (_index++ > 0)
                {
                    return false;
                }

                child = _single;
                return true;
            }

            return NextOfMap(out child);
        }

        private bool NextOfMap(out CelValue child)
        {
            child = default;
            if (_phase == KeyDone)
            {
                child = _entryValue;
                _phase = Value;
                return true;
            }

            if (!Entries!.MoveNext())
            {
                return false;
            }

            child = Entries.Current.Key;
            _entryValue = Entries.Current.Value;
            _phase = Key;
            return true;
        }
    }
}
