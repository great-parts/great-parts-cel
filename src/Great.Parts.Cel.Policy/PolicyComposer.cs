using System;
using System.Collections.Generic;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Policy;

// The composition of the rules of a policy into one expression, as the composer of cel-go (policy/composer.go) does it: a rule becomes, from
// its last match to its first, a chain of conditionals; a rule that may give no output gives an optional value, which an enclosing
// rule unwraps or chains; an aggregate rule concatenates the lists of its matches; the variables are the slots of cel.@block, numbered in
// the order the composition registers them. Each expression is copied from its parsed tree, with its references to variables replaced
// by the slots; the nodes the composition adds have no position. The recursion is as deep as the rules are nested, which a policy
// that passed the limit of nested rules keeps small.
internal sealed class PolicyComposer(CelExprFactory factory, int unnestHeight) : ISlotLookup
{
    private const string BlockFunction = "cel.@block";
    private const string Conditional = "_?_:_";
    private const string Add = "_+_";
    private const string OptionalOf = "optional.of";
    private const string OptionalNone = "optional.none";

    private readonly Dictionary<string, int> _visible = new(StringComparer.Ordinal);
    private readonly List<(string Name, int Previous)> _undo = [];
    private readonly List<CelExprNode> _slots = [];

    // Whether the composed expression has optional values, and whether it has cel.@block.
    internal bool UsesOptional { get; private set; }

    internal bool UsesBlock => _slots.Count > 0;

    // The composed expression of the rule that the policy starts with.
    internal CelParsedExpr Compose(CompiledRule root)
    {
        CelExprNode body = ComposeRule(root, false).Expression;
        body = new ExpressionUnnester(factory, unnestHeight).Unnest(body, _slots);
        return factory.Build(_slots.Count == 0 ? body : factory.Call(BlockFunction, factory.List(_slots.ToArray()), body));
    }

    bool ISlotLookup.TryGetSlot(string name, out int slot) => _visible.TryGetValue(name, out slot);

    // ---- Steps ----

    // An intermediate result of the composition: an expression, whether it is an optional value, and the condition under which it is the
    // result; no condition means that it is the result always.
    private sealed class Step(bool isOptional, CelExprNode? condition, CelExprNode expression)
    {
        internal bool IsOptional { get; } = isOptional;

        internal CelExprNode? Condition { get; } = condition;

        internal CelExprNode Expression { get; } = expression;
    }

    private Step ComposeRule(CompiledRule rule, bool asList)
    {
        int mark = _undo.Count;
        foreach (CompiledVariable variable in rule.Variables)
        {
            RegisterVariable(variable);
        }

        bool isAggregate = rule.Semantic == PolicySemantic.Aggregate;
        bool returnList = isAggregate || asList;
        Step? output = BaseStep(returnList, rule.HasOptionalOutput);
        for (int i = rule.Matches.Count - 1; i >= 0; i--)
        {
            Step step = StepOf(rule.Matches[i], returnList);
            output = isAggregate ? CombineAggregate(step, output!) : Combine(step, output);
        }

        RestoreScope(mark);
        return output!;
    }

    // The step of a match: its output, as a list for a rule that gives one, or its nested rule.
    private Step StepOf(CompiledMatch match, bool returnList)
    {
        CelExprNode? condition = match.ConditionIsTrue ? null : Copy(match.Condition);
        if (match.Output is not null)
        {
            CelExprNode output = Copy(match.Output);
            return new Step(false, condition, returnList ? factory.List([output]) : output);
        }

        // A nested rule that may give no output is an optional step: a missing output falls through to the next match.
        CompiledRule nested = match.Nested!;
        CelExprNode nestedExpression = ComposeRule(nested, returnList).Expression;
        return new Step(nested.HasOptionalOutput, condition, nestedExpression);
    }

    // The step that ends the chain: an empty list, or none for a rule that may give no output; no step for a rule whose last match
    // always gives an output.
    private Step? BaseStep(bool returnList, bool hasOptionalOutput)
    {
        if (returnList)
        {
            return new Step(false, null, factory.List([]));
        }

        return hasOptionalOutput ? new Step(true, null, NoneCall()) : null;
    }

    // ---- Combining ----

    // The step "current, else next" for a first-match rule.
    private Step Combine(Step current, Step? next)
    {
        if (next is null)
        {
            return current;
        }

        return current.IsOptional ? CombineOptional(current, next) : CombineNonOptional(current, next);
    }

    private Step CombineNonOptional(Step current, Step next)
    {
        if (next.IsOptional)
        {
            // A conditional value in front of an optional one becomes an optional value; an unconditional one hides the rest.
            return current.Condition is null
                ? current
                : new Step(true, null, factory.Call(Conditional, current.Condition, OfCall(current.Expression), next.Expression));
        }

        return current.Condition is null
            ? current
            : new Step(false, null, factory.Call(Conditional, current.Condition, current.Expression, next.Expression));
    }

    private Step CombineOptional(Step current, Step next)
    {
        if (next.IsOptional)
        {
            if (current.Condition is not null)
            {
                return new Step(true, null, factory.Call(Conditional, current.Condition, current.Expression, next.Expression));
            }

            // An optional value in front of none is the optional value; in front of another, the first that has a value.
            return IsNone(next.Expression) ? current : new Step(true, null, OrCall("or", current.Expression, next.Expression));
        }

        if (current.Condition is not null)
        {
            return new Step(true, null, factory.Call(Conditional, current.Condition, current.Expression, OfCall(next.Expression)));
        }

        // An unconditional optional value in front of a value: the value of the optional one, or else the next.
        return new Step(false, null, OrCall("orValue", current.Expression, next.Expression));
    }

    // The step of an aggregate rule: the list of the match, empty when its condition does not hold, in front of the lists after it.
    private Step CombineAggregate(Step current, Step accumulated)
    {
        CelExprNode part = current.Condition is null
            ? current.Expression
            : factory.Call(Conditional, current.Condition, current.Expression, factory.List([]));
        if (accumulated.Expression is CelListNode { Elements.Length: 0 })
        {
            return new Step(false, null, part);
        }

        return new Step(false, null, factory.Call(Add, part, accumulated.Expression));
    }

    private static bool IsNone(CelExprNode node) =>
        node is CelCallNode { Target: null, Arguments.Length: 0 } call && string.Equals(call.Function, OptionalNone, StringComparison.Ordinal);

    private CelCallNode OfCall(CelExprNode value)
    {
        UsesOptional = true;
        return factory.Call(OptionalOf, value);
    }

    private CelCallNode NoneCall()
    {
        UsesOptional = true;
        return factory.Call(OptionalNone);
    }

    private CelCallNode OrCall(string function, CelExprNode target, CelExprNode alternative)
    {
        UsesOptional = true;
        return factory.MemberCall(function, target, alternative);
    }

    // ---- Variables and copies ----

    // The variable becomes the next slot; its expression sees the variables registered before it.
    private void RegisterVariable(CompiledVariable variable)
    {
        int slot = _slots.Count;
        _slots.Add(Copy(variable.Expression));
        int previous = _visible.TryGetValue(variable.Name, out int visible) ? visible : -1;
        _undo.Add((variable.Name, previous));
        _visible[variable.Name] = slot;
    }

    private void RestoreScope(int mark)
    {
        while (_undo.Count > mark)
        {
            (string name, int previous) = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            if (previous < 0)
            {
                _visible.Remove(name);
            }
            else
            {
                _visible[name] = previous;
            }
        }
    }

    // A copy of an expression of the policy, whose references to variables are the slots visible now.
    private CelExprNode Copy(CompiledExpression? expression) =>
        expression is { Parsed: not null } ? new VariableRewriter(factory, expression, this).Copy() : factory.Constant(CelValue.Null);
}
