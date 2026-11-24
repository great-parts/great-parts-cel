using System.Collections.Generic;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Policy;

// What the compilation of a policy knows of its rules while it checks and composes them: the model of the document with the parsed
// expressions, and what the checks find. The rules, matches and variables mirror CompiledRule, CompiledMatch and CompiledVariable
// of cel-go.

// An expression of the policy: its string of the document and its tree when it parsed; the map of the tree to the document.
internal sealed class CompiledExpression(PolicyString source, CelParsedExpr? parsed, SegmentMap map)
{
    internal PolicyString Source { get; } = source;

    // Null when the text did not parse; the errors are reported then.
    internal CelParsedExpr? Parsed { get; } = parsed;

    internal SegmentMap Map { get; } = map;

    // The copy of the tree in the probe of the checks; null when the text did not parse or the expression is not part of the probe.
    internal CelExprNode? Probe { get; set; }

    // Whether the expression is the literal bool of the value.
    internal bool IsLiteral(bool value) =>
        Parsed?.Root is CelConstantNode constant && constant.Value.TryGetBoolean(out bool actual) && actual == value;
}

// A variable of a rule, with its slot in the probe.
internal sealed class CompiledVariable(PolicyVariable source, string name, CompiledExpression? expression, int slot, int shadowed)
{
    internal PolicyVariable Source { get; } = source;

    internal string Name { get; } = name;

    // Null when the variable has no expression in the document.
    internal CompiledExpression? Expression { get; } = expression;

    // The index of the slot of the probe that holds the expression.
    internal int Slot { get; } = slot;

    // The slot of the variable of the same name that was visible when this one was declared, or -1.
    internal int Shadowed { get; } = shadowed;

    // The type of the expression, once the probe is checked.
    internal CelType? Type { get; set; }
}

// A match of a rule: a condition, and an output or a nested rule.
internal sealed class CompiledMatch(PolicyMatch source, CompiledExpression? condition, CompiledExpression? output)
{
    internal PolicyMatch Source { get; } = source;

    // Null for a match without a condition, which is the literal true.
    internal CompiledExpression? Condition { get; } = condition;

    internal CompiledExpression? Output { get; } = output;

    internal CompiledRule? Nested { get; set; }

    internal bool ConditionIsTrue => Condition is null || Condition.IsLiteral(true);

    internal bool ConditionIsFalse => Condition is not null && Condition.IsLiteral(false);

    // A match that has neither an output nor a nested rule is checked for its condition only: the explanation policy has such
    // matches, where the document has an output without an explanation.
    internal bool IsLive => Output is not null || Nested is not null;
}

// A rule: its variables, its live matches and what the checks find.
internal sealed class CompiledRule(PolicyRule source, PolicySemantic semantic, bool returnsList, bool hasAggregateAncestor)
{
    internal PolicyRule Source { get; } = source;

    internal PolicySemantic Semantic { get; } = semantic;

    // Whether the rule gives a list: it is an aggregate rule or is nested in a rule that gives one.
    internal bool ReturnsList { get; } = returnsList;

    // Whether an aggregate rule encloses this one.
    internal bool HasAggregateAncestor { get; } = hasAggregateAncestor;

    internal List<CompiledVariable> Variables { get; } = [];

    internal List<CompiledMatch> Matches { get; } = [];

    // Whether the rule may give no output: every path has a condition or a nested rule that may give none.
    internal bool HasOptionalOutput { get; set; }

    // The type of the output of the first match, a list of it for an aggregate rule; dyn without matches. Set by the checks.
    internal CelType? OutputType { get; set; }
}
