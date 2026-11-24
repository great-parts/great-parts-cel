using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Policy;

// The walk over the rules of the policy: every expression is parsed and copied into the probe, the variables become its slots, and the
// structure of the rules is checked as it goes.
internal sealed partial class PolicyCompiler
{
    // A rule being walked: the next of its matches to read, and where the declarations of its variables start in the undo log.
    private sealed class RuleFrame(CompiledRule rule, int undoMark)
    {
        internal CompiledRule Rule { get; } = rule;

        internal int UndoMark { get; } = undoMark;

        internal int NextMatch { get; set; }
    }

    // The slot of each variable name visible at the place being walked; a declaration that a rule leaves is undone from the log.
    private readonly Dictionary<string, int> _visible = new(StringComparer.Ordinal);
    private readonly List<(string Name, int Previous)> _undo = [];

    // The probe: the expression of each variable, in the order of the declarations, and the conditions and the outputs.
    private readonly List<CelExprNode> _slots = [];
    private readonly List<CompiledVariable> _slotVariables = [];
    private readonly List<CelExprNode> _parts = [];

    // The conditions of the matches, and the rules in post order, children first.
    private readonly List<CompiledExpression> _conditions = [];
    private readonly List<CompiledRule> _postOrder = [];

    private CompiledRule? _root;
    private int _nested;

    // Whether the text of an expression did not parse, so the probe does not hold every part and gives no types to check.
    private bool _parseFailed;

    private void Walk()
    {
        PolicyRule? source = _policy.Document.Rule;
        if (source is null)
        {
            Report(CelErrorCode.InvalidPolicy, "the policy has no rule", _policy.Document.Node);
            return;
        }

        Stack<RuleFrame> stack = new();
        _root = Start(source, null, stack);
        while (stack.Count > 0)
        {
            RuleFrame frame = stack.Peek();
            List<PolicyMatch> matches = frame.Rule.Source.Matches;
            if (frame.NextMatch < matches.Count)
            {
                ReadMatch(frame, matches[frame.NextMatch++], stack);
                continue;
            }

            Finish(frame);
            stack.Pop();
            if (stack.Count > 0)
            {
                CountNested(frame.Rule);
            }
        }
    }

    // A rule is created with its variables, which are parsed and declared at once, and waits on the stack for its matches.
    private CompiledRule Start(PolicyRule source, CompiledRule? parent, Stack<RuleFrame> stack)
    {
        int undoMark = _undo.Count;
        bool aggregateAncestor = parent is not null && (parent.HasAggregateAncestor || parent.Semantic == PolicySemantic.Aggregate);
        bool isAggregate = source.Semantic == PolicySemantic.Aggregate;
        var rule = new CompiledRule(source, source.Semantic, isAggregate || (parent?.ReturnsList ?? false), aggregateAncestor);
        if (aggregateAncestor && isAggregate)
        {
            Report(CelErrorCode.InvalidPolicy, "nested aggregate rules are not allowed", source.Node);
        }

        stack.Push(new RuleFrame(rule, undoMark));
        foreach (PolicyVariable variable in source.Variables)
        {
            rule.Variables.Add(DeclareVariable(variable));
        }

        return rule;
    }

    // The variable takes the next slot of the probe; its expression sees the variables declared before it, not itself.
    private CompiledVariable DeclareVariable(PolicyVariable source)
    {
        string name = source.Name?.Text ?? string.Empty;
        CompiledExpression expression = Parse(source.Expression ?? MissingExpression(source.Node));
        CelExprNode probe = expression.Parsed is null
            ? _factory.Call("dyn", _factory.Constant(CelValue.Null))
            : Copy(expression);
        int slot = _slots.Count;
        _slots.Add(probe);
        int shadowed = _visible.TryGetValue(name, out int visible) ? visible : -1;
        var variable = new CompiledVariable(source, name, expression, slot, shadowed);
        _slotVariables.Add(variable);
        _undo.Add((name, shadowed));
        _visible[name] = slot;
        CountNested(source.Name?.Node ?? source.Node, "variable exceeds nested expression limit");
        return variable;
    }

    // The text of a variable without an expression is empty, as in cel-go: parsing it gives the error of the empty text, at the variable.
    private PolicyString MissingExpression(YamlNode node) => new(string.Empty, [new CelSourceSegment(0, Locate(node.Start))], node);

    private void ReadMatch(RuleFrame frame, PolicyMatch source, Stack<RuleFrame> stack)
    {
        CompiledExpression? condition = source.Condition is null ? null : ParseAndProbe(source.Condition);
        CompiledExpression? output = source.Output is null ? null : ParseAndProbe(source.Output);
        var match = new CompiledMatch(source, condition, output);
        if (condition is not null)
        {
            _conditions.Add(condition);
        }

        if (source.Rule is not null)
        {
            match.Nested = Start(source.Rule, frame.Rule, stack);
        }

        if (match.IsLive)
        {
            frame.Rule.Matches.Add(match);
        }
    }

    // A rule whose matches are all read: the declarations of its variables end, and its checks that need its children run.
    private void Finish(RuleFrame frame)
    {
        while (_undo.Count > frame.UndoMark)
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

        CompiledRule rule = frame.Rule;
        rule.HasOptionalOutput = HasOptionalOutput(rule);
        if (rule.Matches.Count == 0 && !rule.ReturnsList)
        {
            Report(CelErrorCode.InvalidPolicy, "the rule has no match with an output or a nested rule", rule.Source.Node);
        }

        _postOrder.Add(rule);
    }

    // The nested rule or variable that is the one beyond the limit is the error; the count goes on after it, as in cel-go.
    private void CountNested(CompiledRule rule) => CountNested(rule.Source.Node, "rule exceeds nested expression limit");

    private void CountNested(YamlNode node, string message)
    {
        _nested++;
        if (_nested == MaxNested + 1)
        {
            Report(CelErrorCode.InvalidPolicy, message, node);
        }
    }

    // Whether a rule may give no output, as HasOptionalOutput of cel-go decides it.
    private static bool HasOptionalOutput(CompiledRule rule)
    {
        if (rule.Semantic == PolicySemantic.Aggregate)
        {
            return false;
        }

        bool optional = false;
        foreach (CompiledMatch match in rule.Matches)
        {
            if (match.Nested is { HasOptionalOutput: true })
            {
                // An unconditional nested rule that may give none lets the matching go on to the next match.
                if (!match.ConditionIsTrue)
                {
                    return true;
                }

                optional = true;
            }
            else if (match.ConditionIsTrue)
            {
                return false;
            }
            else
            {
                optional = true;
            }
        }

        return optional;
    }

    // ---- Expressions ----

    private CompiledExpression ParseAndProbe(PolicyString source)
    {
        CompiledExpression expression = Parse(source);
        if (expression.Parsed is not null)
        {
            expression.Probe = Copy(expression);
            _parts.Add(expression.Probe);
        }

        return expression;
    }

    // Parses the text of an expression with the syntax of the environment; the errors of the text are reported at its place in the document.
    private CompiledExpression Parse(PolicyString source)
    {
        var map = new SegmentMap(source.Segments, _policy.Text.Length);
        CelParseResult result = _policyEnvironment.Parse(source.Text, new CelCompileOptions { SourceSegments = source.Segments });
        if (!result.IsSuccess)
        {
            _parseFailed = true;
            AddAll(result.Errors);
            return new CompiledExpression(source, null, map);
        }

        return new CompiledExpression(source, result.GetParsedExprOrThrow(), map);
    }

    // The copy of the tree of an expression for the probe, with the references to variables as slots.
    private CelExprNode Copy(CompiledExpression expression) => new VariableRewriter(_factory, expression, this).Copy();
}
