using System;
using System.Buffers;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// What a frame holds only during a partial evaluation: the patterns of the unknown data, the values of the keys that a chain of
// attributes evaluated before it looked at the chain (read once by the nodes of the chain), and the record of the values of the
// nodes, which the residual expression needs. The record is indexed by node id and rented from the array pool; Release gives it
// back and returns the compact record. One state serves one evaluation on one thread.
internal sealed class PartialState
{
    private readonly CelValue[] _keys;
    private readonly bool[] _keyReady;
    private readonly int _length;
    private CelValue[]? _values;
    private bool[]? _known;

    internal PartialState(UnknownPattern[] patterns, int idCount, int keyCount)
    {
        Patterns = patterns;
        _keys = keyCount == 0 ? [] : new CelValue[keyCount];
        _keyReady = keyCount == 0 ? [] : new bool[keyCount];
        _length = idCount + 1;
        _values = ArrayPool<CelValue>.Shared.Rent(_length);
        _known = ArrayPool<bool>.Shared.Rent(_length);
        _values.AsSpan(0, _length).Clear();
        _known.AsSpan(0, _length).Clear();
    }

    internal UnknownPattern[] Patterns { get; }

    // Whether a pattern is for the variable.
    internal bool HasVariable(string variable)
    {
        foreach (UnknownPattern pattern in Patterns)
        {
            if (string.Equals(pattern.Variable, variable, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal void StoreKey(int slot, CelValue value)
    {
        _keys[slot] = value;
        _keyReady[slot] = true;
    }

    // Takes the stored value of a key, which is read once; false when the chain stored none.
    internal bool TryTakeKey(int slot, out CelValue value)
    {
        if (!_keyReady[slot])
        {
            value = default;
            return false;
        }

        _keyReady[slot] = false;
        value = _keys[slot];
        _keys[slot] = default;
        return true;
    }

    internal void DropKey(int slot)
    {
        _keyReady[slot] = false;
        _keys[slot] = default;
    }

    // Writes the value of a node, unless it is an error or an unknown: the residual replaces only known values.
    internal void Record(int id, CelValue value)
    {
        if (value.Kind != CelValueKind.Error && _values is not null)
        {
            _values[id] = value;
            _known![id] = true;
        }
    }

    // Gives the arrays back to the pool without reading the values: the state of a pass whose record nobody needs.
    internal void Discard()
    {
        if (_values is not { } values || _known is not { } known)
        {
            return;
        }

        _values = null;
        _known = null;
        ArrayPool<CelValue>.Shared.Return(values, clearArray: true);
        ArrayPool<bool>.Shared.Return(known);
    }

    // Gives the arrays back to the pool and returns the values that were written, in ascending order of the ids.
    internal ValueRecord Release()
    {
        if (_values is not { } values || _known is not { } known)
        {
            return ValueRecord.Empty;
        }

        int count = known.AsSpan(0, _length).Count(true);
        var ids = new int[count];
        var written = new CelValue[count];
        int next = 0;
        for (int id = 0; id < _length && next < count; id++)
        {
            if (known[id])
            {
                ids[next] = id;
                written[next++] = values[id];
            }
        }

        _values = null;
        _known = null;
        ArrayPool<CelValue>.Shared.Return(values, clearArray: true);
        ArrayPool<bool>.Shared.Return(known);
        return new ValueRecord(ids, written);
    }
}
