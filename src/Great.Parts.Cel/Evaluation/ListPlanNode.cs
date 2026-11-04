using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A list literal: evaluates the elements from left to right onto the argument stack and creates a list.
// The literal is strict: the first error value of an element is its value, and the array of the list is
// allocated only after every element has a value. Costs 1 + n for n elements.
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
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        if (!TryPushArguments(frame, Elements, out error))
        {
            return error;
        }

        Begin(frame);
        CelValue[] items = Elements.Length == 0 ? [] : frame.Peek(Elements.Length).ToArray();
        frame.Pop(Elements.Length);
        return CelValue.FromList(new ValueArrayList(items));
    }
}
