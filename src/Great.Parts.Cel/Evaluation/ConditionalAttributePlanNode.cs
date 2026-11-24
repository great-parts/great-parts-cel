using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The outermost node of a chain of attributes in a partial plan that starts at a conditional expression: (c ? a : b).f[k]. cel-go
// appends the qualifiers of the chain to both branches, so the access that a pattern is matched against is that of the branch the
// condition chooses with the qualifiers of the chain after it. Planned as two short attributes and a chain over them, the branch
// would be matched with an access that is too short: unknown where the whole access is known, and a trail that stops too soon. The
// branches therefore match nothing themselves, and this node matches for them.
//
// Without a state of partial evaluation, or with no pattern for the variable of any branch, it evaluates the wrapped node. With
// patterns it evaluates the condition, once, and stores the value in the partial state, where the wrapped chain takes it from the
// KeyPlanNode of the condition. A condition that is not a bool, which is an error, an unknown value or an operand that no overload
// takes, is left to the wrapped node, which reports it. Then, as AttributePlanNode does, it evaluates the keys of the chosen branch
// and of the chain from left to right — an error or an unknown value is the result — and matches the patterns against the access of
// the branch followed by the access of the chain, and gives the first match as an unknown value that charges 1. A chosen branch
// that is no attribute of a variable that a pattern names has no access to match. When none matches, the wrapped node is evaluated
// and reads the stored values, so that the condition and the keys are evaluated once.
internal sealed class ConditionalAttributePlanNode : PlanNode
{
    private readonly PlanNode _chain;
    private readonly AttributeSource _source;

    // The variables of the attributes at the leaves of the source, the slots of the conditions and the keys under it, and the most
    // qualifiers of an access.
    private readonly string[] _variables;
    private readonly int[] _slots;
    private readonly int _length;

    internal ConditionalAttributePlanNode(int offset, PlanNode chain, AttributeSource source)
        : base(offset, 0)
    {
        _chain = chain;
        _source = source;
        var variables = new List<string>();
        var slots = new List<int>();
        source.Collect(variables, slots);
        _variables = [.. variables];
        _slots = [.. slots];
        _length = source.Length;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (frame.Partial is not { } state || !HasPattern(state))
        {
            return _chain.Evaluate(frame);
        }

        var access = new Access(_length);
        CelValue result;
        if (!TryCollect(frame, state, _source, access, out CelValue error))
        {
            result = error;
        }
        else if (access.Variable is { } variable
            && AttributePlanNode.Match(state.Patterns, variable, access.Ids.AsSpan(0, access.IdCount), access.Qualifiers.AsSpan(0, access.Count)) is { } entry)
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

    // Whether a pattern is for the variable of an attribute that a branch can be.
    private bool HasPattern(PartialState state)
    {
        foreach (string variable in _variables)
        {
            if (state.HasVariable(variable))
            {
                return true;
            }
        }

        return false;
    }

    // Evaluates the conditions and the keys along the branches that the conditions choose, from left to right, and adds the access to
    // the arrays. False, with the error, when a key is an error or an unknown value. When the access has no variable to match, the
    // variable of the access is left null: the chosen branch is no attribute of a variable with a pattern, or a condition is not a
    // bool.
    private static bool TryCollect(Frame frame, PartialState state, AttributeSource source, Access access, out CelValue error)
    {
        error = default;
        int first = 0;
        if (source is { Condition: { } condition, Then: { } then, Else: { } otherwise })
        {
            CelValue chosen = condition.EvaluateKey(frame);
            state.StoreKey(condition.Slot, chosen);
            if (!chosen.TryGetBoolean(out bool branch))
            {
                access.Variable = null;
                return true;
            }

            if (!TryCollect(frame, state, branch ? then : otherwise, access, out error))
            {
                return false;
            }

            first = 1;
        }
        else
        {
            access.Variable = source.Variable is { } variable && state.HasVariable(variable) ? variable : null;
        }

        if (access.Variable is null)
        {
            return true;
        }

        for (int i = first; i < source.Ids.Length; i++)
        {
            access.Ids[access.IdCount++] = source.Ids[i];
        }

        return TryAddSteps(frame, state, source, access, out error);
    }

    // Adds the qualifiers applied to the source, evaluating the keys that are expressions and storing their values.
    private static bool TryAddSteps(Frame frame, PartialState state, AttributeSource source, Access access, out CelValue error)
    {
        error = default;
        foreach (AttributeStep step in source.Steps)
        {
            if (step.Key is not { } key)
            {
                access.Qualifiers[access.Count++] = step.Constant;
                continue;
            }

            CelValue value = key.EvaluateKey(frame);
            if (value.Kind == CelValueKind.Error)
            {
                error = value;
                return false;
            }

            access.Qualifiers[access.Count++] = value;
            state.StoreKey(key.Slot, value);
        }

        return true;
    }

    // Drops the conditions and the keys that this node stored and the wrapped node did not read.
    private void Forget(PartialState state)
    {
        foreach (int slot in _slots)
        {
            state.DropKey(slot);
        }
    }

    // The access that TryCollect builds: the qualifiers, and the id of the syntax node at each length, the variable of the attribute
    // it goes through first.
    private sealed class Access(int length)
    {
        internal CelValue[] Qualifiers { get; } = new CelValue[length];

        internal int[] Ids { get; } = new int[length + 1];

        internal int Count { get; set; }

        internal int IdCount { get; set; }

        internal string? Variable { get; set; }
    }
}
