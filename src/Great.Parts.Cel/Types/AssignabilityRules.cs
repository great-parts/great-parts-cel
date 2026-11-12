using System;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Types;

/// <summary>
/// Assignability of CEL types, the most general of two types, the join of two types and erasure. A type
/// <see langword="null"/> is <c>error</c>, the type of a node with an error, which type checking treats as
/// <c>dyn</c>. The order of the checks is that of cel-go v0.32.0 <c>checker/types.go</c>; type values and the
/// most general type follow the language definition.
/// </summary>
internal static class AssignabilityRules
{
    private static readonly CelType DynTypeValue = CelType.TypeOf(CelType.Dyn);

    // No bindings, for types without type parameters: the rules only read it then.
    private static readonly TypeSubstitution NoBindings = new();

    // The outcome of trying a type against a type parameter: yes, no, or no decision, so that the later rules decide.
    private enum ParameterOutcome
    {
        Yes,
        No,
        Undecided,
    }

    /// <summary>
    /// Tells whether a value of the type <paramref name="from"/> can stand where <paramref name="to"/> is
    /// expected, binding type parameters in <paramref name="substitution"/> as the rules allow.
    /// </summary>
    /// <param name="from">The type of the value; <see langword="null"/> for <c>error</c>.</param>
    /// <param name="to">The expected type; <see langword="null"/> for <c>error</c>.</param>
    /// <param name="substitution">The bindings of the checked expression. A <see langword="false"/> answer can leave
    /// bindings made on the way; a caller that tries a candidate takes a mark first and rolls back on failure.</param>
    /// <param name="deducible">The variables that a trial inside a type value may bind: the fresh variables of the
    /// overload being tried; empty everywhere else.</param>
    /// <returns><see langword="true"/> when the type is assignable.</returns>
    internal static bool IsAssignable(CelType? from, CelType? to, TypeSubstitution substitution, ReadOnlySpan<CelType> deducible) =>
        Assign(from, to, substitution, deducible, restricted: false);

    /// <summary>
    /// The type kept where two types assignable to each other meet: the least upper bound of the two after the
    /// substitution.
    /// </summary>
    /// <param name="a">The first type; <see langword="null"/> for <c>error</c>.</param>
    /// <param name="b">The second type; <see langword="null"/> for <c>error</c>.</param>
    /// <param name="substitution">The bindings of the checked expression.</param>
    /// <returns>The most general type; the other type when one of them is <c>error</c>.</returns>
    internal static CelType? MostGeneral(CelType? a, CelType? b, TypeSubstitution substitution)
    {
        if (a is null || b is null)
        {
            return a ?? b;
        }

        return General(SubstitutionRules.Apply(substitution, a, freeToDyn: false), SubstitutionRules.Apply(substitution, b, freeToDyn: false));
    }

    /// <summary>
    /// The join of the type of the elements so far and the type of the next element of a list literal, or of the
    /// keys or the values of a map literal: the most general of the two when the next is assignable to the joined
    /// type, otherwise <c>dyn</c>. The check is a trial with nothing deducible: a failure binds nothing.
    /// </summary>
    /// <param name="joined">The type of the elements so far.</param>
    /// <param name="next">The type of the next element; <see langword="null"/> for <c>error</c>.</param>
    /// <param name="substitution">The bindings of the checked expression.</param>
    /// <returns>The joined type.</returns>
    internal static CelType Join(CelType joined, CelType? next, TypeSubstitution substitution)
    {
        int mark = substitution.Mark;
        if (Assign(next, joined, substitution, [], restricted: false))
        {
            return MostGeneral(joined, next, substitution)!;
        }

        substitution.Rollback(mark);
        return CelType.Dyn;
    }

    /// <summary>
    /// Replaces every type parameter by <c>dyn</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The erased type; the same instance when it has no type parameters.</returns>
    internal static CelType Erase(CelType type) => SubstitutionRules.Apply(NoBindings, type, freeToDyn: true);

    /// <summary>
    /// Tells whether two parameter types of overload declarations at one position overlap: after erasure one takes
    /// the other by the rules without a substitution.
    /// </summary>
    /// <param name="first">The parameter type of one overload.</param>
    /// <param name="second">The parameter type of the other overload at the same position.</param>
    /// <returns><see langword="true"/> when the types overlap.</returns>
    internal static bool Overlap(CelType first, CelType second)
    {
        CelType a = Erase(first);
        CelType b = Erase(second);
        return Assign(a, b, NoBindings, [], restricted: false) || Assign(b, a, NoBindings, [], restricted: false);
    }

    // The rules in their order. In a trial inside a type value (restricted) only the deducible variables are bound.
    private static bool Assign(CelType? from, CelType? to, TypeSubstitution substitution, ReadOnlySpan<CelType> deducible, bool restricted)
    {
        // 1. The expected type is a type parameter.
        if (to is { Kind: CelTypeKind.TypeParameter })
        {
            ParameterOutcome outcome = Substitute(from, to, substitution, deducible, restricted);
            if (outcome != ParameterOutcome.Undecided)
            {
                return outcome == ParameterOutcome.Yes;
            }
        }

        // 2. The type of the value is a type parameter; when the occurs check fails, the answer is no.
        if (from is { Kind: CelTypeKind.TypeParameter })
        {
            ParameterOutcome outcome = Substitute(to, from, substitution, deducible, restricted);
            if (outcome != ParameterOutcome.Undecided || !restricted || IsDeducible(from, deducible))
            {
                return outcome == ParameterOutcome.Yes;
            }
        }

        // 3. dyn, error and Any: Any takes every type and every type takes it, as dyn does.
        if (from is null || to is null || from.Kind is CelTypeKind.Dyn or CelTypeKind.Any || to.Kind is CelTypeKind.Dyn or CelTypeKind.Any)
        {
            return true;
        }

        // 4. null_type.
        if (from.Kind == CelTypeKind.Null)
        {
            return TakesNull(to);
        }

        if (to.Kind == CelTypeKind.Null)
        {
            return TakesNull(from);
        }

        return (from.Kind, to.Kind) switch
        {
            // 5. A wrapper and its primitive, both ways.
            (CelTypeKind.Wrapper, CelTypeKind.Wrapper) => from.Parameters[0].Kind == to.Parameters[0].Kind,
            (CelTypeKind.Wrapper, _) => from.Parameters[0].Kind == to.Kind,
            (_, CelTypeKind.Wrapper) => to.Parameters[0].Kind == from.Kind,

            // 6. Type values: always yes; the parameters are a trial that binds only deducible variables.
            (CelTypeKind.Type, CelTypeKind.Type) => TryTypeParameters(from, to, substitution, deducible),

            // 7. Lists and maps: the parameters pairwise.
            (CelTypeKind.List, CelTypeKind.List) => Assign(from.Parameters[0], to.Parameters[0], substitution, deducible, restricted),
            (CelTypeKind.Map, CelTypeKind.Map) =>
                Assign(from.Parameters[0], to.Parameters[0], substitution, deducible, restricted)
                && Assign(from.Parameters[1], to.Parameters[1], substitution, deducible, restricted),

            // 5 and 8. Primitive types of the same kind; object types of the same full name.
            (CelTypeKind.Object, CelTypeKind.Object) => string.Equals(from.Name, to.Name, StringComparison.Ordinal),

            // 8. Abstract types of the same full name, parameter by parameter.
            (CelTypeKind.Abstract, CelTypeKind.Abstract) => string.Equals(from.Name, to.Name, StringComparison.Ordinal)
                && AssignParameters(from, to, substitution, deducible, restricted),

            // 8. Optional types: the value types, by the same rules with the same substitution.
            (CelTypeKind.Optional, CelTypeKind.Optional) => AssignParameters(from, to, substitution, deducible, restricted),
            (CelTypeKind.List or CelTypeKind.Map or CelTypeKind.Type or CelTypeKind.TypeParameter or CelTypeKind.Optional, _) => false,
            _ => from.Kind == to.Kind,
        };
    }

    // The parameters of two abstract types of one name, one by one; the counts are equal when the names are, since a type of a
    // set has a fixed number of parameters.
    private static bool AssignParameters(CelType from, CelType to, TypeSubstitution substitution, ReadOnlySpan<CelType> deducible, bool restricted)
    {
        if (from.Parameters.Length != to.Parameters.Length)
        {
            return false;
        }

        for (int i = 0; i < from.Parameters.Length; i++)
        {
            if (!Assign(from.Parameters[i], to.Parameters[i], substitution, deducible, restricted))
            {
                return false;
            }
        }

        return true;
    }

    // Tries a type against a type parameter: by its binding when it is bound, binding it when it is free. In a trial
    // inside a type value, a variable that is not deducible is not bound again, and a free one is not bound at all.
    private static ParameterOutcome Substitute(CelType? other, CelType parameter, TypeSubstitution substitution, ReadOnlySpan<CelType> deducible, bool restricted)
    {
        if (other is not null && other.Equals(parameter))
        {
            return ParameterOutcome.Yes;
        }

        bool mayBind = !restricted || IsDeducible(parameter, deducible);
        if (substitution.TryGet(parameter.Name, out CelType? bound))
        {
            if (other is not null && other.Equals(bound))
            {
                return ParameterOutcome.Yes;
            }

            // Nothing is deducible in the check against the binding.
            if (!Assign(other, bound, substitution, [], restricted))
            {
                return ParameterOutcome.No;
            }

            CelType general = MostGeneral(other, bound, substitution)!;
            if (mayBind && !ReferenceEquals(general, bound) && !SubstitutionRules.Occurs(substitution, parameter, general))
            {
                substitution.Set(parameter.Name, general);
            }

            return ParameterOutcome.Yes;
        }

        if (!mayBind)
        {
            return ParameterOutcome.Undecided;
        }

        // An error binds nothing: the parameter stays free for the other arguments.
        if (other is null)
        {
            return ParameterOutcome.Yes;
        }

        return SubstitutionRules.TryBind(substitution, parameter, other) ? ParameterOutcome.Yes : ParameterOutcome.Undecided;
    }

    // type(A) and type(B): the parameters are tried, keeping the bindings when the trial succeeds; the answer is yes.
    private static bool TryTypeParameters(CelType from, CelType to, TypeSubstitution substitution, ReadOnlySpan<CelType> deducible)
    {
        int mark = substitution.Mark;
        if (!Assign(from.Parameters[0], to.Parameters[0], substitution, deducible, restricted: true))
        {
            substitution.Rollback(mark);
        }

        return true;
    }

    private static bool IsDeducible(CelType parameter, ReadOnlySpan<CelType> deducible)
    {
        foreach (CelType variable in deducible)
        {
            if (string.Equals(variable.Name, parameter.Name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The types that take null: null_type, wrappers, object types, abstract types, and the types of time, optional and decimal that do once they are supported.
    private static bool TakesNull(CelType type) => type.Kind is CelTypeKind.Null or CelTypeKind.Wrapper or CelTypeKind.Object
        or CelTypeKind.Timestamp or CelTypeKind.Duration or CelTypeKind.Any or CelTypeKind.Optional or CelTypeKind.Abstract;

    // The least upper bound of two types after the substitution.
    private static CelType General(CelType a, CelType b)
    {
        // 1. dyn; a free type parameter.
        if (a.Kind == CelTypeKind.Dyn || b.Kind == CelTypeKind.Dyn)
        {
            return CelType.Dyn;
        }

        if (a.Kind == CelTypeKind.TypeParameter)
        {
            return a;
        }

        if (b.Kind == CelTypeKind.TypeParameter)
        {
            return b;
        }

        // Any with Any is Any; with any other type it is dyn.
        if (a.Kind == CelTypeKind.Any || b.Kind == CelTypeKind.Any)
        {
            return a.Kind == b.Kind ? a : CelType.Dyn;
        }

        return a.Kind switch
        {
            // 2. Two type values: one when they are equal, otherwise type(dyn).
            CelTypeKind.Type when b.Kind == CelTypeKind.Type => a.Parameters[0].Equals(b.Parameters[0]) ? a : DynTypeValue,

            // 3. Lists and maps parameter by parameter.
            CelTypeKind.List or CelTypeKind.Map when b.Kind == a.Kind => GeneralParameters(a, b),

            // 3. Optional types, and abstract types of one name with the same number of parameters, parameter by parameter.
            CelTypeKind.Optional when b.Kind == CelTypeKind.Optional => GeneralNamedParameters(a, b),
            CelTypeKind.Abstract when b.Kind == CelTypeKind.Abstract
                && string.Equals(a.Name, b.Name, StringComparison.Ordinal)
                && a.Parameters.Length == b.Parameters.Length => GeneralNamedParameters(a, b),

            // 4. A wrapper and its primitive; null_type and a type that takes null.
            CelTypeKind.Wrapper when a.Parameters[0].Kind == b.Kind => a,
            _ when b.Kind == CelTypeKind.Wrapper && b.Parameters[0].Kind == a.Kind => b,
            CelTypeKind.Null when TakesNull(b) => b,

            // 5. Otherwise the types are equal.
            _ => a,
        };
    }

    // list(MostGeneral(A, B)), map(MostGeneral(K1, K2), MostGeneral(V1, V2)); the first type when nothing changes.
    private static CelType GeneralParameters(CelType a, CelType b)
    {
        CelType first = General(a.Parameters[0], b.Parameters[0]);
        if (a.Kind == CelTypeKind.List)
        {
            return ReferenceEquals(first, a.Parameters[0]) ? a : CelType.ListOf(first);
        }

        CelType second = General(a.Parameters[1], b.Parameters[1]);
        return ReferenceEquals(first, a.Parameters[0]) && ReferenceEquals(second, a.Parameters[1]) ? a : CelType.MapOf(first, second);
    }

    // N(MostGeneral(A1, B1), ...) for two types of one kind and name, an optional type or an abstract type; the first type when
    // nothing changes.
    private static CelType GeneralNamedParameters(CelType a, CelType b)
    {
        ImmutableArray<CelType>.Builder? general = null;
        for (int i = 0; i < a.Parameters.Length; i++)
        {
            CelType parameter = General(a.Parameters[i], b.Parameters[i]);
            if (general is null && !ReferenceEquals(parameter, a.Parameters[i]))
            {
                general = ImmutableArray.CreateBuilder<CelType>(a.Parameters.Length);
                general.AddRange(a.Parameters, i);
            }

            general?.Add(parameter);
        }

        return general is null ? a : new CelType(a.Kind, a.Name, general.MoveToImmutable());
    }
}
