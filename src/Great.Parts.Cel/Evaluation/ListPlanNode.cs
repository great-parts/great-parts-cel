using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A list literal: evaluates the elements from left to right and creates a list. The literal is strict:
// the first error value of an element is its value. Costs 1 + n for n elements.
internal sealed class ListPlanNode : PlanNode
{
    internal ListPlanNode(int offset, ImmutableArray<PlanNode> elements)
        : base(offset, 1 + (ulong)elements.Length)
    {
        Elements = elements;
    }

    internal ImmutableArray<PlanNode> Elements { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        var items = new CelValue[Elements.Length];
        for (int i = 0; i < items.Length; i++)
        {
            CelValue item = Elements[i].Evaluate(frame);
            if (item.Kind == CelValueKind.Error)
            {
                return item;
            }

            items[i] = item;
        }

        return CelValue.FromList(new ValueArrayList(items));
    }
}
