using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Evaluation;

// The part of planning that makes the nodes of map literals and of list and map literals with optional entries. The planner plans the
// keys, the values and the elements, which are its own nodes, and this class makes the node from them.
internal static class LiteralPlanner
{
    // A map literal.
    internal static PlanNode Map(Planner planner, CelMapNode map, int offset, out int stackSize)
    {
        Entries(planner, map, out ImmutableArray<PlanNode> keys, out ImmutableArray<PlanNode> values, out stackSize);
        return new MapPlanNode(offset, keys, values);
    }

    // A map literal with optional entries, ?k: v: the room on the stack is that of a plain map.
    internal static PlanNode OptionalMap(Planner planner, CelMapNode map, int offset, out int stackSize)
    {
        Entries(planner, map, out ImmutableArray<PlanNode> keys, out ImmutableArray<PlanNode> values, out stackSize);
        return new OptionalMapPlanNode(offset, keys, values, map.OptionalFlags());
    }

    // A list literal with optional elements, ?e: the elements that are not stored leave the stack, so the room is that of a plain list.
    internal static PlanNode OptionalList(Planner planner, CelListNode list, int offset, out int stackSize)
    {
        ImmutableArray<PlanNode> elements = planner.VisitAll(list.Elements, out stackSize);
        var optional = new bool[elements.Length];
        foreach (int index in list.OptionalIndices)
        {
            optional[index] = true;
        }

        return new OptionalListPlanNode(offset, elements, ImmutableArray.Create(optional));
    }

    // The keys and the values of the entries of a map literal, in the order of the text, and the room that they need: the keys and the
    // values before an entry stay on the stack, and then its key.
    private static void Entries(Planner planner, CelMapNode map, out ImmutableArray<PlanNode> keys, out ImmutableArray<PlanNode> values, out int stackSize)
    {
        int count = map.Count;
        ImmutableArray<PlanNode>.Builder keyNodes = ImmutableArray.CreateBuilder<PlanNode>(count);
        ImmutableArray<PlanNode>.Builder valueNodes = ImmutableArray.CreateBuilder<PlanNode>(count);
        stackSize = 0;
        for (int i = 0; i < count; i++)
        {
            int pushed = 2 * keyNodes.Count;
            keyNodes.Add(planner.Visit(map.KeyOf(i), out int keyStackSize));
            valueNodes.Add(planner.Visit(map.ValueOf(i), out int valueStackSize));
            stackSize = Math.Max(stackSize, Math.Max(pushed + keyStackSize, pushed + 1 + valueStackSize));
        }

        stackSize = Math.Max(stackSize, 2 * count);
        keys = keyNodes.MoveToImmutable();
        values = valueNodes.MoveToImmutable();
    }
}
