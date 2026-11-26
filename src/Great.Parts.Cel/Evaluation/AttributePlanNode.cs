using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The outermost node of a chain of attributes in a partial plan: a variable read with the selections, optional selections, indexes,
// optional indexes and presence tests applied to it. It holds the chain as the planner knows it — the full name of the variable, the
// qualifiers in order, and the id of the syntax node at each length — and wraps the node that evaluates the chain.
//
// Without patterns of unknown data in the frame it evaluates the wrapped node. With patterns for its variable it first evaluates the
// keys that are expressions, from left to right — an error is the result — and matches the patterns in the order they were given:
// the variable name equal, and each qualifier that both have equal, a null qualifier of the pattern matching any; numbers of the
// kinds int, uint and double are equal by their value, as CEL equality has it; a key that is an unknown value is equal to no
// qualifier that a pattern names, and a null qualifier matches it. The first pattern that matches gives an unknown value with the
// trail of the access up to the length of the pattern, and the id of the node of the chain at that length, or of the last node when
// the access is shorter; it charges 1. When none matches, the wrapped node is evaluated and reads the keys that were evaluated from
// the partial state, so that they are evaluated once. A key that is an unknown value makes the unknown key the result, whether a
// pattern matches or not, unless none does and the wrapped chain reports an error before it reaches the key (OfUnknownKey).
internal sealed class AttributePlanNode : PlanNode
{
    private readonly PlanNode _chain;
    private readonly string _variable;
    private readonly ImmutableArray<AttributeStep> _steps;

    // The id of the syntax node at each length of the access: the variable first, then after each qualifier.
    private readonly ImmutableArray<int> _ids;

    internal AttributePlanNode(int offset, PlanNode chain, string variable, ImmutableArray<AttributeStep> steps, ImmutableArray<int> ids)
        : base(offset, 0)
    {
        _chain = chain;
        _variable = variable;
        _steps = steps;
        _ids = ids;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (frame.Partial is not { } state || !state.HasVariable(_variable))
        {
            return _chain.Evaluate(frame);
        }

        CelValue[] access = _steps.Length == 0 ? [] : new CelValue[_steps.Length];
        CelValue unknownKey = default;
        for (int i = 0; i < access.Length; i++)
        {
            if (_steps[i].Key is not { } key)
            {
                access[i] = _steps[i].Constant;
                continue;
            }

            CelValue value = key.EvaluateKey(frame);
            if (value.Kind == CelValueKind.Error && !value.IsUnknown)
            {
                Forget(state);
                return value;
            }

            if (value.IsUnknown && !unknownKey.IsUnknown)
            {
                unknownKey = value;
            }

            access[i] = value;
            state.StoreKey(key.Slot, value);
        }

        CelValue result = Resolve(frame, state, access, unknownKey);
        Forget(state);
        return result;
    }

    // The result for the keys that were evaluated: the unknown of the first pattern that matches the access, the unknown key (see
    // OfUnknownKey), or the value of the wrapped chain.
    private CelValue Resolve(Frame frame, PartialState state, CelValue[] access, CelValue unknownKey)
    {
        UnknownPattern? pattern = Find(state.Patterns, _variable, access);
        if (unknownKey.IsUnknown)
        {
            return OfUnknownKey(frame, _chain, unknownKey, pattern is not null);
        }

        if (pattern is null)
        {
            return _chain.Evaluate(frame);
        }

        frame.Offset = Offset;
        if (frame.Metered)
        {
            frame.Charge(1);
        }

        return CelValue.FromUnknown(UnknownSet.Of(EntryOf(pattern, _variable, _ids.AsSpan(), access)));
    }

    // The result of a chain that has a key that is an unknown value. An unknown key matches no qualifier of a pattern that names one
    // by its value, and the unknown of a key is the unknown of the access, so the unknown key is the result. Where no pattern matches
    // the access, the wrapped chain is evaluated first, and an error that it reports before it reaches the key, as the field that is
    // not there in x.b[k], is the result instead: the calls of a chain are strict, and an error takes precedence over an unknown value.
    // Where a pattern matches, the chain is not evaluated, since the data that it reads may be the unknown data itself. The wrapped
    // chain reads the keys that were stored, so that none is evaluated twice.
    internal static CelValue OfUnknownKey(Frame frame, PlanNode chain, CelValue unknownKey, bool matched)
    {
        if (matched)
        {
            return unknownKey;
        }

        CelValue value = chain.Evaluate(frame);
        return value.Kind == CelValueKind.Error && !value.IsUnknown ? value : unknownKey;
    }

    // The first pattern that matches the access of the variable, or null. It builds nothing: where only the fact of a match is needed,
    // the trail of the entry is not made.
    internal static UnknownPattern? Find(UnknownPattern[] patterns, string variable, ReadOnlySpan<CelValue> access)
    {
        foreach (UnknownPattern pattern in patterns)
        {
            if (string.Equals(pattern.Variable, variable, StringComparison.Ordinal)
                && Agrees(pattern, access, Math.Min(pattern.Qualifiers.Length, access.Length)))
            {
                return pattern;
            }
        }

        return null;
    }

    // The entry of a pattern that Find returned for the access of the variable. ids has the id of the syntax node at each length of the
    // access: the variable first, then after each qualifier.
    internal static UnknownEntry EntryOf(UnknownPattern pattern, string variable, ReadOnlySpan<int> ids, ReadOnlySpan<CelValue> access)
    {
        int length = Math.Min(pattern.Qualifiers.Length, access.Length);

        // The trail has the qualifier of the pattern where it names one, and that of the access where the pattern matches any. It
        // stops before a key that no string, int, uint or bool names, which is the whole of the data under the qualifiers before it.
        ImmutableArray<CelValue?>.Builder trail = ImmutableArray.CreateBuilder<CelValue?>(length);
        for (int i = 0; i < length; i++)
        {
            CelValue qualifier = pattern.Qualifiers[i] ?? TrailQualifier(access[i]);
            if (!IsNamed(qualifier))
            {
                break;
            }

            trail.Add(qualifier);
        }

        return new UnknownEntry(ids[length], variable, trail.ToImmutable());
    }

    // Whether a qualifier of a trail is one that the public pattern can hold: a string, an int, a uint or a bool.
    private static bool IsNamed(CelValue qualifier) =>
        qualifier.TryGetString(out _) || qualifier.TryGetInt64(out _) || qualifier.TryGetUInt64(out _) || qualifier.TryGetBoolean(out _);

    // A trail names a qualifier by a string, an int, a uint or a bool. A double key that is a whole number names the same element
    // as the int or the uint of its value (the keys are equal by CEL equality), and cel-go writes it so; another double stays as it is,
    // and so does a key of any other kind that a pattern with a qualifier that matches any lets through.
    private static CelValue TrailQualifier(CelValue qualifier)
    {
        if (qualifier.TryGetDouble(out double number) && Math.Floor(number) == number)
        {
            if (number >= -9223372036854775808.0 && number < 9223372036854775808.0)
            {
                return CelValue.FromInt64((long)number);
            }

            if (number >= 9223372036854775808.0 && number < 18446744073709551616.0)
            {
                return CelValue.FromUInt64((ulong)number);
            }
        }

        return qualifier;
    }

    // Whether the qualifiers of the pattern and the access are equal as far as both go. A key that is an unknown value is equal to no
    // qualifier of the pattern that names one, and a qualifier that matches any matches it.
    private static bool Agrees(UnknownPattern pattern, ReadOnlySpan<CelValue> access, int length)
    {
        for (int i = 0; i < length; i++)
        {
            if (pattern.Qualifiers[i] is { } qualifier
                && !(CelValue.CelEquals(qualifier, access[i]).TryGetBoolean(out bool equal) && equal))
            {
                return false;
            }
        }

        return true;
    }

    // Drops the keys that this chain stored and the wrapped node did not read.
    private void Forget(PartialState state)
    {
        foreach (AttributeStep step in _steps)
        {
            if (step.Key is { } key)
            {
                state.DropKey(key.Slot);
            }
        }
    }
}
