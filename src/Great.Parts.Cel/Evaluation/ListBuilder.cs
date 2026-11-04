using System;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The accumulator of map and filter during the loop: an array that grows by doubling from 4 elements up to the size of
// the range, as elements are added, so that the bytes follow the units charged for the elements and a cost limit that
// stops the loop stops the allocation. It never leaves the comprehension: its result turns it into a list over the array.
internal sealed class ListBuilder : CelList
{
    private const int FirstCapacity = 4;

    private readonly int _limit;
    private CelValue[] _items = [];
    private int _count;

    // limit is the size of the range: the loop adds at most one element per element of the range.
    internal ListBuilder(int limit)
    {
        _limit = limit;
    }

    private protected override int CountCore => _count;

    // Called after the unit of the element is charged, so the allocation of a larger array follows a charged unit.
    internal void Add(CelValue value)
    {
        if (_count == _items.Length)
        {
            Array.Resize(ref _items, Math.Min(Math.Max(_items.Length * 2, FirstCapacity), _limit));
        }

        _items[_count++] = value;
    }

    // The list of the elements added; the array is trimmed once when it has room left.
    internal CelList ToList()
    {
        if (_count < _items.Length)
        {
            Array.Resize(ref _items, _count);
        }

        return new ValueArrayList(_items);
    }

    private protected override CelValue GetCore(int index) => _items[index];
}
