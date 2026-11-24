using System;
using System.Collections.Generic;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Policy;

// Moves the deep parts of a composed expression into slots of cel.@block, as the unnesting of the composer of cel-go does it, so that a
// policy of many matches does not give an expression nested as deep as it has matches. The height of a node is 0 for an identifier and
// a constant, else 1 more than the greatest height of its children; a call whose height reaches the limit, outside comprehensions, moves
// into a new slot, lowest first, and the heights of the nodes above it fall by the limit, so that the next slot is taken when the part
// above grows as high again. The nodes are walked with explicit stacks.
internal sealed class ExpressionUnnester(CelExprFactory factory, int height)
{
    // The height at which a call moves into a slot, as cel-go sets it.
    internal const int DefaultHeight = 25;

    // The nodes of a tree in post order, with their heights and their parents.
    private sealed class TreeFacts(List<CelExprNode> order, Dictionary<int, int> heights, Dictionary<int, CelExprNode> parents)
    {
        internal List<CelExprNode> Order { get; } = order;

        internal Dictionary<int, int> Heights { get; } = heights;

        internal Dictionary<int, CelExprNode> Parents { get; } = parents;
    }

    // The body with the deep calls replaced by slots; the expressions of the slots are added to the list, which holds the slots that
    // exist, so the new ones continue their numbering.
    internal CelExprNode Unnest(CelExprNode body, List<CelExprNode> slots)
    {
        TreeFacts facts = Measure(body);
        List<CelExprNode> candidates = Candidates(facts, out HashSet<int> eligible);
        if (candidates.Count < 2)
        {
            return body;
        }

        CelParsedExpr source = factory.Build(body);
        Dictionary<int, CelExprNode> replaced = [];
        Func<CelExprNode, CelExprNode?> replace = node => replaced.TryGetValue(node.Id, out CelExprNode? replacement) ? replacement : null;

        // The last candidate, the highest, stays where it is.
        for (int i = 0; i < candidates.Count - 1; i++)
        {
            CelExprNode candidate = candidates[i];
            if (!eligible.Contains(candidate.Id) || facts.Heights[candidate.Id] < height)
            {
                continue;
            }

            LowerAbove(facts, candidate);
            slots.Add(factory.Copy(source, candidate, replace));
            replaced[candidate.Id] = factory.Ident(VariableRewriter.SlotName(slots.Count - 1));
        }

        return replaced.Count == 0 ? body : factory.Copy(source, body, replace);
    }

    // The nodes of the tree in post order, children before their parent, with the height and the parent of each.
    private static TreeFacts Measure(CelExprNode root)
    {
        var facts = new TreeFacts([], [], []);
        Stack<(CelExprNode Node, bool Done)> pending = new();
        pending.Push((root, false));
        while (pending.Count > 0)
        {
            (CelExprNode node, bool done) = pending.Pop();
            if (done)
            {
                facts.Order.Add(node);
                facts.Heights[node.Id] = HeightOf(node, facts.Heights);
                continue;
            }

            pending.Push((node, true));
            for (int i = node.Children.Length - 1; i >= 0; i--)
            {
                CelExprNode child = node.Children[i];
                facts.Parents[child.Id] = node;
                pending.Push((child, false));
            }
        }

        return facts;
    }

    private static int HeightOf(CelExprNode node, Dictionary<int, int> heights)
    {
        if (node.Kind is CelExprKind.Ident or CelExprKind.Constant)
        {
            return 0;
        }

        int highest = 0;
        foreach (CelExprNode child in node.Children)
        {
            highest = Math.Max(highest, heights[child.Id]);
        }

        return highest + 1;
    }

    // The calls that reach the height and are not inside a comprehension, lowest first, the first found first among equals. A call
    // inside a comprehension is not eligible: it may use the variables of the comprehension. The range of a comprehension is outside it.
    private List<CelExprNode> Candidates(TreeFacts facts, out HashSet<int> eligible)
    {
        eligible = [];
        List<(int Height, int Index, CelExprNode Node)> found = [];
        foreach (CelExprNode node in facts.Order)
        {
            if (node is CelComprehensionNode comprehension)
            {
                if (eligible.Count > 0)
                {
                    RemoveInside(comprehension, eligible);
                }

                continue;
            }

            if (node.Kind == CelExprKind.Call && facts.Heights[node.Id] >= height)
            {
                eligible.Add(node.Id);
                found.Add((facts.Heights[node.Id], found.Count, node));
            }
        }

        found.Sort(static (a, b) => a.Height != b.Height ? a.Height.CompareTo(b.Height) : a.Index.CompareTo(b.Index));
        List<CelExprNode> candidates = new(found.Count);
        foreach ((int _, int _, CelExprNode node) in found)
        {
            candidates.Add(node);
        }

        return candidates;
    }

    // Removes the nodes under the initial value, the condition, the step and the result of a comprehension from the eligible ones.
    private static void RemoveInside(CelComprehensionNode comprehension, HashSet<int> eligible)
    {
        Stack<CelExprNode> pending = new();
        pending.Push(comprehension.AccuInit);
        pending.Push(comprehension.LoopCondition);
        pending.Push(comprehension.LoopStep);
        pending.Push(comprehension.Result);
        while (pending.Count > 0)
        {
            CelExprNode node = pending.Pop();
            eligible.Remove(node.Id);
            foreach (CelExprNode child in node.Children)
            {
                pending.Push(child);
            }
        }
    }

    // Lowers the heights of the node and of its ancestors by the limit, up to the first that is lower than the limit.
    private void LowerAbove(TreeFacts facts, CelExprNode node)
    {
        CelExprNode? current = node;
        while (current is not null)
        {
            int currentHeight = facts.Heights[current.Id];
            if (currentHeight < height)
            {
                return;
            }

            facts.Heights[current.Id] = currentHeight - height;
            current = facts.Parents.TryGetValue(current.Id, out CelExprNode? parent) ? parent : null;
        }
    }
}
