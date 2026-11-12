using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// An array that compares by length and by element and has a stable hash code, so that a model that holds one is equal to the
// model of the previous run when nothing changed (the cookbook of incremental generators).
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
{
    private readonly T[]? items;

    internal EquatableArray(T[] items)
    {
        this.items = items;
    }

    internal T[] Items => items ?? Array.Empty<T>();

    internal int Length => items?.Length ?? 0;

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);

    public bool Equals(EquatableArray<T> other)
    {
        T[] left = Items;
        T[] right = other.Items;
        if (left.Length != right.Length)
        {
            return false;
        }

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int index = 0; index < left.Length; index++)
        {
            if (!comparer.Equals(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (T item in Items)
        {
            hash = unchecked((hash * 31) + (item?.GetHashCode() ?? 0));
        }

        return hash;
    }
}

internal static class EquatableArray
{
    internal static EquatableArray<T> From<T>(List<T> list) => new(list.ToArray());
}
