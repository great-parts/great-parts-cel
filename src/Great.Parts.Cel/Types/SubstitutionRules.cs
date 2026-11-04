using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Great.Parts.Cel.Types;

/// <summary>
/// The substitution of type parameters: binding a parameter with the occurs check, and applying the bindings
/// to a type, recursively through the parameters of lists, maps, wrappers and type values. A chain of bindings from one
/// type parameter to the next is followed in a loop, so that its length does not use the stack.
/// </summary>
internal static class SubstitutionRules
{
    /// <summary>
    /// Binds <paramref name="parameter"/> to <paramref name="type"/> when the parameter does not occur in the type
    /// after the substitution is applied, so that no binding refers to itself.
    /// </summary>
    /// <param name="substitution">The bindings.</param>
    /// <param name="parameter">A type parameter.</param>
    /// <param name="type">The type to bind it to.</param>
    /// <returns><see langword="false"/> when the occurs check fails; nothing is bound then.</returns>
    internal static bool TryBind(TypeSubstitution substitution, CelType parameter, CelType type)
    {
        if (Occurs(substitution, parameter, type))
        {
            return false;
        }

        substitution.Set(parameter.Name, type);
        return true;
    }

    /// <summary>
    /// Tells whether <paramref name="parameter"/> occurs in <paramref name="type"/> after the substitution is
    /// applied: it is the parameter itself, or a parameter bound to a type in which it occurs, or a type with such a
    /// parameter.
    /// </summary>
    /// <param name="substitution">The bindings.</param>
    /// <param name="parameter">A type parameter.</param>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the parameter occurs.</returns>
    internal static bool Occurs(TypeSubstitution substitution, CelType parameter, CelType type)
    {
        while (type.Kind == CelTypeKind.TypeParameter)
        {
            if (string.Equals(type.Name, parameter.Name, System.StringComparison.Ordinal))
            {
                return true;
            }

            if (!substitution.TryGet(type.Name, out CelType? bound))
            {
                return false;
            }

            type = bound;
        }

        foreach (CelType inner in type.Parameters)
        {
            if (Occurs(substitution, parameter, inner))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Applies the substitution to a type: each bound type parameter is replaced by its binding, recursively.
    /// </summary>
    /// <param name="substitution">The bindings.</param>
    /// <param name="type">The type.</param>
    /// <param name="freeToDyn">Whether a type parameter that remains free is replaced by <c>dyn</c>.</param>
    /// <returns>The type after the substitution; the same instance when nothing is replaced.</returns>
    internal static CelType Apply(TypeSubstitution substitution, CelType type, bool freeToDyn) =>
        Apply(substitution, type, freeToDyn, kept: null);

    /// <summary>
    /// Applies the substitution to a type as <see cref="Apply(TypeSubstitution, CelType, bool)"/> does and keeps the
    /// result for each type parameter met in <paramref name="kept"/>, so that applying it to many types walks each chain
    /// of bindings once. The results hold while the bindings and <paramref name="freeToDyn"/> stay the same.
    /// </summary>
    /// <param name="substitution">The bindings.</param>
    /// <param name="type">The type.</param>
    /// <param name="freeToDyn">Whether a type parameter that remains free is replaced by <c>dyn</c>.</param>
    /// <param name="kept">The results for type parameters by name, one box for the parameters of one chain;
    /// <see langword="null"/> to keep none.</param>
    /// <returns>The type after the substitution; the same instance when nothing is replaced.</returns>
    internal static CelType Apply(TypeSubstitution substitution, CelType type, bool freeToDyn, Dictionary<string, StrongBox<CelType>>? kept)
    {
        if (type.Kind == CelTypeKind.TypeParameter)
        {
            return ApplyToParameter(substitution, type, freeToDyn, kept);
        }

        ImmutableArray<CelType> parameters = type.Parameters;
        ImmutableArray<CelType>.Builder? replaced = null;
        for (int index = 0; index < parameters.Length; index++)
        {
            CelType applied = Apply(substitution, parameters[index], freeToDyn, kept);
            if (replaced is null && !ReferenceEquals(applied, parameters[index]))
            {
                replaced = ImmutableArray.CreateBuilder<CelType>(parameters.Length);
                replaced.AddRange(parameters, index);
            }

            replaced?.Add(applied);
        }

        return replaced is null ? type : new CelType(type.Kind, type.Name, replaced.MoveToImmutable());
    }

    // Follows the bindings from the parameter to the last type parameter of the chain: one that is free, bound to a type that
    // is not a type parameter, or kept. Each parameter passed is kept with one box, which gets the result at the end.
    private static CelType ApplyToParameter(TypeSubstitution substitution, CelType parameter, bool freeToDyn, Dictionary<string, StrongBox<CelType>>? kept)
    {
        StrongBox<CelType>? box = null;
        CelType last = parameter;
        CelType result;
        while (true)
        {
            if (kept is not null)
            {
                ref StrongBox<CelType>? slot = ref CollectionsMarshal.GetValueRefOrAddDefault(kept, last.Name, out bool exists);
                if (exists)
                {
                    // A parameter is kept only with a box that has its result: chains of bindings have no cycles.
                    result = slot!.Value!;
                    break;
                }

                slot = box ??= new StrongBox<CelType>();
            }

            if (!substitution.TryGet(last.Name, out CelType? bound))
            {
                result = freeToDyn ? CelType.Dyn : last;
                break;
            }

            if (bound.Kind != CelTypeKind.TypeParameter)
            {
                result = Apply(substitution, bound, freeToDyn, kept);
                break;
            }

            last = bound;
        }

        if (box is not null)
        {
            box.Value = result;
        }

        return result;
    }
}
