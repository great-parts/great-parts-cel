using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Evaluation;

// What a branch of the conditional expression at the base of a chain of attributes is, for the partial plan: an attribute of a
// variable with the qualifiers applied to it, a conditional expression of its own with the qualifiers applied to it, or an expression
// that is no attribute, which no pattern matches.
internal sealed class AttributeSource
{
    // The expression that is no attribute.
    internal static readonly AttributeSource None = new(null, null, null, null, [], []);

    private AttributeSource(
        string? variable,
        KeyPlanNode? condition,
        AttributeSource? then,
        AttributeSource? otherwise,
        ImmutableArray<AttributeStep> steps,
        ImmutableArray<int> ids)
    {
        Variable = variable;
        Condition = condition;
        Then = then;
        Else = otherwise;
        Steps = steps;
        Ids = ids;
    }

    // The full name of the variable of an attribute; null for the other two kinds.
    internal string? Variable { get; }

    // The condition of a conditional expression; null for the other two kinds.
    internal KeyPlanNode? Condition { get; }

    // The branches of a conditional expression; null for the other two kinds.
    internal AttributeSource? Then { get; }

    internal AttributeSource? Else { get; }

    // The qualifiers applied to the attribute or to the conditional expression.
    internal ImmutableArray<AttributeStep> Steps { get; }

    // The id of the syntax node at each length of the access, as AttributeShape.Ids gives them: the first is that of the variable or
    // of the conditional expression, then one after each qualifier.
    internal ImmutableArray<int> Ids { get; }

    // The qualifiers of the longest access that goes through the source.
    internal int Length => (Then is { } then && Else is { } otherwise ? Math.Max(then.Length, otherwise.Length) : 0) + Steps.Length;

    internal static AttributeSource ForAttribute(string variable, ImmutableArray<AttributeStep> steps, ImmutableArray<int> ids) =>
        new(variable, null, null, null, steps, ids);

    internal static AttributeSource ForConditional(
        KeyPlanNode condition,
        AttributeSource then,
        AttributeSource otherwise,
        ImmutableArray<AttributeStep> steps,
        ImmutableArray<int> ids) =>
        new(null, condition, then, otherwise, steps, ids);

    // Adds the variables of the attributes below the source that are not in the list, and the slots of the conditions and the keys.
    internal void Collect(List<string> variables, List<int> slots)
    {
        if (Variable is { } variable && !variables.Contains(variable))
        {
            variables.Add(variable);
        }

        if (this is { Condition: { } condition, Then: { } then, Else: { } otherwise })
        {
            slots.Add(condition.Slot);
            then.Collect(variables, slots);
            otherwise.Collect(variables, slots);
        }

        foreach (AttributeStep step in Steps)
        {
            if (step.Key is { } key)
            {
                slots.Add(key.Slot);
            }
        }
    }
}
