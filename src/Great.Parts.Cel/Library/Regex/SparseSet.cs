using System;

namespace Great.Parts.Cel.Library.Regex;

// A set of the numbers 0 to n - 1 over two arrays: Dense lists the members in the order they came, and Sparse maps a number to its
// place in Dense. A number is a member when its place is below the count and holds it, so the arrays never need clearing: a rented
// array with old contents, which may hold any int, is fine, and Clear is one assignment.
internal ref struct SparseSet
{
    private readonly Span<int> dense;
    private readonly Span<int> sparse;
    private int count;

    // Both arrays hold at least as many ints as the set has numbers.
    internal SparseSet(Span<int> dense, Span<int> sparse)
    {
        this.dense = dense;
        this.sparse = sparse;
        count = 0;
    }

    internal readonly int Count => count;

    // The member at a place from 0 to Count - 1, in the order the members came.
    internal readonly int Member(int place) => dense[place];

    internal readonly bool Contains(int number)
    {
        int place = sparse[number];
        return (uint)place < (uint)count && dense[place] == number;
    }

    // Adds a number that is not a member.
    internal void Add(int number)
    {
        sparse[number] = count;
        dense[count] = number;
        count++;
    }

    internal void Clear() => count = 0;
}
