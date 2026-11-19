using System;
using System.Buffers;

namespace Great.Parts.Cel.Library.Regex;

// A list over an array rented from the shared pool, for the build of an automaton: it grows by renting a larger array, and Dispose
// returns the array. A rented array has old contents, so the items added with AddZeros are written. Not a copyable value: a copy of a
// list that grows is a list that no longer owns the array it holds.
internal struct PooledList<T>
{
    private T[] items;
    private int count;

    internal PooledList(int capacity)
    {
        items = ArrayPool<T>.Shared.Rent(capacity);
        count = 0;
    }

    internal readonly int Count => count;

    // The rented array; read it again after any Add, since an Add may replace it.
    internal readonly T[] Items => items;

    internal void Add(T item)
    {
        if (count == items.Length)
        {
            Grow(count + 1);
        }

        items[count++] = item;
    }

    internal void AddRange(ReadOnlySpan<T> range)
    {
        if (count + range.Length > items.Length)
        {
            Grow(count + range.Length);
        }

        range.CopyTo(items.AsSpan(count));
        count += range.Length;
    }

    // Appends the given number of items of the default value.
    internal void AddZeros(int length)
    {
        if (count + length > items.Length)
        {
            Grow(count + length);
        }

        items.AsSpan(count, length).Clear();
        count += length;
    }

    internal void Dispose()
    {
        if (items is not null)
        {
            ArrayPool<T>.Shared.Return(items);
            items = null!;
        }
    }

    private void Grow(int minimum)
    {
        T[] larger = ArrayPool<T>.Shared.Rent(Math.Max(minimum, items.Length * 2));
        items.AsSpan(0, count).CopyTo(larger);
        ArrayPool<T>.Shared.Return(items);
        items = larger;
    }
}
