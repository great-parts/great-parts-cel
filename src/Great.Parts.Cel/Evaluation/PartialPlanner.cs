using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Evaluation;

// What the planner adds when it plans for partial evaluation. It finds the chains of attributes of the checked tree and wraps the
// outermost node of each in an AttributePlanNode, wraps a node that is a key of a chain in a KeyPlanNode, and wraps every node
// that is not inside the loop condition or the loop step of a comprehension in a RecordPlanNode: a loop runs its step many times
// and the record keeps one value per node, so the nodes of a loop have no value for the residual expression. The nodes of the
// chain below the outermost one, and the keys, are planned as the planner plans them.
//
// A chain may start at a conditional expression, (c ? a : b).f: cel-go applies the qualifiers to both branches, so the branch that
// the condition chooses is matched with the qualifiers appended to it. The outermost node of such a chain is a
// ConditionalAttributePlanNode; the condition is a key, which the node evaluates once; the branches are not wrapped, they only
// describe their attributes to it.
internal sealed class PartialPlanner(CheckedExpression checkedExpression, Func<CelIdentNode, bool> isSlot)
{
    // The nodes of the chains that are not the outermost one: they are not wrapped again.
    private readonly HashSet<CelExprNode> _inner = new(ReferenceEqualityComparer.Instance);

    // The branches of the conditional expressions at the base of a chain, and what each is once it is planned: an attribute, a
    // conditional expression of its own, or, when it is neither, nothing.
    private readonly HashSet<CelExprNode> _branches = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CelExprNode, AttributeSource> _sources = new(ReferenceEqualityComparer.Instance);

    // The slot of each key that is an expression, and the node that plans it, once it is planned.
    private readonly Dictionary<CelExprNode, int> _keySlots = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<CelExprNode, KeyPlanNode> _keyPlans = new(ReferenceEqualityComparer.Instance);

    // The chain that each node being planned ends, or null, the innermost last: Begin pushes it and End takes it.
    private readonly Stack<AttributeShape?> _shapes = new();

    private int _loopDepth;

    // The number of keys that are expressions: the slots of the partial state.
    internal int KeyCount { get; private set; }

    internal void EnterLoop() => _loopDepth++;

    internal void ExitLoop() => _loopDepth--;

    // Before a node is planned: finds the chain it ends, if it ends one, and marks the parts of the chain and its keys.
    internal void Begin(CelExprNode node)
    {
        if (_inner.Contains(node) || AttributeShape.TryCreate(node, checkedExpression, isSlot, _branches.Contains(node)) is not { } shape)
        {
            _shapes.Push(null);
            return;
        }

        for (int i = 1; i < shape.Spine.Length; i++)
        {
            _ = _inner.Add(shape.Spine[i]);
        }

        foreach (AttributeQualifier qualifier in shape.Qualifiers)
        {
            if (qualifier.Key is { } key)
            {
                _keySlots[key] = KeyCount++;
            }
        }

        if (shape.Conditional is { } conditional)
        {
            _keySlots[conditional.Arguments[0]] = KeyCount++;
            _ = _branches.Add(conditional.Arguments[1]);
            _ = _branches.Add(conditional.Arguments[2]);
        }

        _shapes.Push(shape);
    }

    // After a node is planned: the node with the wrappers it needs.
    internal PlanNode End(CelExprNode node, int offset, PlanNode planned)
    {
        AttributeShape? shape = _shapes.Pop();
        if (shape is not null && Steps(shape) is { IsDefault: false } steps)
        {
            planned = Wrap(node, offset, planned, shape, steps);
        }

        if (_loopDepth == 0)
        {
            planned = new RecordPlanNode(offset, node.Id, planned);
        }

        if (_keySlots.TryGetValue(node, out int slot))
        {
            var key = new KeyPlanNode(offset, slot, planned);
            _keyPlans[node] = key;
            return key;
        }

        return planned;
    }

    // The wrapper of a chain whose steps are known. A branch of the conditional expression at the base of another chain is not wrapped:
    // that chain matches the patterns for it, and the branch only tells it what it is. Any other chain that starts at a variable is
    // wrapped in an AttributePlanNode, and one that starts at a conditional expression in a ConditionalAttributePlanNode.
    private PlanNode Wrap(CelExprNode node, int offset, PlanNode planned, AttributeShape shape, ImmutableArray<AttributeStep> steps)
    {
        bool branch = _branches.Contains(node);
        if (shape.Conditional is null && !branch)
        {
            return new AttributePlanNode(offset, planned, shape.Variable, steps, shape.Ids());
        }

        if (SourceOf(shape, steps) is not { } source)
        {
            return planned;
        }

        if (branch)
        {
            _sources[node] = source;
            return planned;
        }

        return new ConditionalAttributePlanNode(offset, planned, source);
    }

    // What the chain is, as a branch or as the whole of a chain over a conditional expression; null when the condition was not planned.
    private AttributeSource? SourceOf(AttributeShape shape, ImmutableArray<AttributeStep> steps)
    {
        if (shape.Conditional is not { } conditional)
        {
            return AttributeSource.ForAttribute(shape.Variable, steps, shape.Ids());
        }

        return _keyPlans.TryGetValue(conditional.Arguments[0], out KeyPlanNode? condition)
            ? AttributeSource.ForConditional(condition, BranchSource(conditional.Arguments[1]), BranchSource(conditional.Arguments[2]), steps, shape.Ids())
            : null;
    }

    // What a branch is once it is planned: nothing when it is neither an attribute nor a conditional expression.
    private AttributeSource BranchSource(CelExprNode branch) =>
        _sources.TryGetValue(branch, out AttributeSource? source) ? source : AttributeSource.None;

    // The steps of a chain, whose keys are planned by now; default when a key was not planned, which leaves the chain unwrapped.
    private ImmutableArray<AttributeStep> Steps(AttributeShape shape)
    {
        ImmutableArray<AttributeStep>.Builder steps = ImmutableArray.CreateBuilder<AttributeStep>(shape.Qualifiers.Length);
        foreach (AttributeQualifier qualifier in shape.Qualifiers)
        {
            if (qualifier.Key is null)
            {
                steps.Add(new AttributeStep(qualifier.Constant));
            }
            else if (_keyPlans.TryGetValue(qualifier.Key, out KeyPlanNode? key))
            {
                steps.Add(new AttributeStep(key));
            }
            else
            {
                return default;
            }
        }

        return steps.MoveToImmutable();
    }
}
