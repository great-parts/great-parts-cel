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
internal sealed class PartialPlanner(CheckedExpression checkedExpression, Func<CelIdentNode, bool> isSlot)
{
    // The nodes of the chains that are not the outermost one: they are not wrapped again.
    private readonly HashSet<CelExprNode> _inner = new(ReferenceEqualityComparer.Instance);

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
        if (_inner.Contains(node) || AttributeShape.TryCreate(node, checkedExpression, isSlot) is not { } shape)
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

        _shapes.Push(shape);
    }

    // After a node is planned: the node with the wrappers it needs.
    internal PlanNode End(CelExprNode node, int offset, PlanNode planned)
    {
        AttributeShape? shape = _shapes.Pop();
        if (shape is not null && Steps(shape) is { IsDefault: false } steps)
        {
            planned = new AttributePlanNode(offset, planned, shape.Variable, steps, shape.Ids());
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
