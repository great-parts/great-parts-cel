using System.Collections.Generic;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>Random choices of the generator, all from the CsCheck generator of the case, so a case depends on its seed alone.</summary>
internal static class Draw
{
    /// <summary>A number from 0 to <paramref name="count"/> − 1.</summary>
    /// <param name="pcg">The random source.</param>
    /// <param name="count">The number of choices, at least 1.</param>
    internal static int Below(this PCG pcg, int count) => (int)pcg.Next((uint)count);

    /// <summary>Whether a chance of one in <paramref name="count"/> happens.</summary>
    /// <param name="pcg">The random source.</param>
    /// <param name="count">The inverse of the chance.</param>
    internal static bool OneIn(this PCG pcg, int count) => pcg.Next((uint)count) == 0;

    /// <summary>An item of a list.</summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="pcg">The random source.</param>
    /// <param name="items">The items, at least one.</param>
    internal static T Pick<T>(this PCG pcg, IReadOnlyList<T> items) => items[pcg.Below(items.Count)];
}
