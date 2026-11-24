using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The outermost node of a chain of attributes in a partial plan: a variable read with the selections, optional selections, indexes,
// optional indexes and presence tests applied to it. It holds the chain as the planner knows it — the full name of the variable, the
// qualifiers in order, and the id of the syntax node at each length — and wraps the node that evaluates the chain.
//
// Without patterns of unknown data in the frame it evaluates the wrapped node. With patterns for its variable it first evaluates the
// keys that are expressions, from left to right — an error or an unknown value is the result — and matches the patterns in the order
// they were given: the variable name equal, and each qualifier that both have equal, a null qualifier of the pattern matching any;
// numbers of the kinds int, uint and double are equal by their value, as CEL equality has it. The first pattern that matches gives
// an unknown value with the trail of the access up to the length of the pattern, and the id of the node of the chain at that length,
// or of the last node when the access is shorter; it charges 1. When none matches, the wrapped node is evaluated and reads the keys
// that were evaluated from the partial state, so that they are evaluated once.
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
        for (int i = 0; i < access.Length; i++)
        {
            if (_steps[i].Key is not { } key)
            {
                access[i] = _steps[i].Constant;
                continue;
            }

            CelValue value = key.EvaluateKey(frame);
            if (value.Kind == CelValueKind.Error)
            {
                Forget(state);
                return value;
            }

            access[i] = value;
            state.StoreKey(key.Slot, value);
        }

        CelValue result;
        if (Match(state.Patterns, _variable, _ids.AsSpan(), access) is { } entry)
        {
            frame.Offset = Offset;
            if (frame.Metered)
            {
                frame.Charge(1);
            }

            result = CelValue.FromUnknown(UnknownSet.Of(entry));
        }
        else
        {
            result = _chain.Evaluate(frame);
        }

        Forget(state);
        return result;
    }

    // The entry of the first pattern that matches the access of the variable, or null. ids has the id of the syntax node at each length
    // of the access: the variable first, then after each qualifier.
    internal static UnknownEntry? Match(UnknownPattern[] patterns, string variable, ReadOnlySpan<int> ids, ReadOnlySpan<CelValue> access)
    {
        foreach (UnknownPattern pattern in patterns)
        {
            if (!string.Equals(pattern.Variable, variable, StringComparison.Ordinal))
            {
                continue;
            }

            int length = Math.Min(pattern.Qualifiers.Length, access.Length);
            if (!Agrees(pattern, access, length))
            {
                continue;
            }

            // The trail has the qualifier of the pattern where it names one, and that of the access where the pattern matches any.
            ImmutableArray<CelValue?>.Builder trail = ImmutableArray.CreateBuilder<CelValue?>(length);
            for (int i = 0; i < length; i++)
            {
                trail.Add(pattern.Qualifiers[i] ?? TrailQualifier(access[i]));
            }

            return new UnknownEntry(ids[length], variable, trail.MoveToImmutable());
        }

        return null;
    }

    // A trail names a qualifier by a string, an int, a uint or a bool. A double key that is a whole number names the same element
    // as the int or the uint of its value (the keys are equal by CEL equality), and cel-go writes it so; another double stays as it is.
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

    // Whether the qualifiers of the pattern and the access are equal as far as both go.
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
