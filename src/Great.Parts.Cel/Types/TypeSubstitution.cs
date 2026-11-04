using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Types;

/// <summary>
/// The bindings of type parameters of one checked expression: each name maps to a type. A trial takes
/// <see cref="Mark"/> first and goes back to it with <see cref="Rollback"/> when it fails, so a failed trial
/// leaves the bindings as they were without copying them. The rules that read and change the bindings are in
/// <see cref="SubstitutionRules"/> and <see cref="AssignabilityRules"/>.
/// </summary>
internal sealed class TypeSubstitution
{
    private readonly Dictionary<string, CelType> _bindings = new(StringComparer.Ordinal);

    // Each change as the name and the binding it replaced, null when there was none, so that Rollback undoes it.
    private readonly List<(string Name, CelType? Previous)> _trail = [];

    /// <summary>
    /// The point to which <see cref="Rollback"/> returns: the number of changes made so far.
    /// </summary>
    internal int Mark => _trail.Count;

    /// <summary>
    /// Finds the type bound to a type parameter.
    /// </summary>
    /// <param name="name">The name of the type parameter.</param>
    /// <param name="type">The bound type when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the parameter is bound.</returns>
    internal bool TryGet(string name, [NotNullWhen(true)] out CelType? type) => _bindings.TryGetValue(name, out type);

    /// <summary>
    /// Binds a type parameter, or binds it again, and records the change for <see cref="Rollback"/>.
    /// </summary>
    /// <param name="name">The name of the type parameter.</param>
    /// <param name="type">The type.</param>
    internal void Set(string name, CelType type)
    {
        ref CelType? slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_bindings, name, out bool _);
        _trail.Add((name, slot));
        slot = type;
    }

    /// <summary>
    /// Undoes every change made after <paramref name="mark"/>, the latest first.
    /// </summary>
    /// <param name="mark">A value of <see cref="Mark"/> taken earlier.</param>
    internal void Rollback(int mark)
    {
        for (int index = _trail.Count - 1; index >= mark; index--)
        {
            (string name, CelType? previous) = _trail[index];
            if (previous is null)
            {
                _ = _bindings.Remove(name);
            }
            else
            {
                _bindings[name] = previous;
            }
        }

        _trail.RemoveRange(mark, _trail.Count - mark);
    }
}
