using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The step @result + [e] of map and filter: appends the value of e to the list builder in the accumulator
// slot, without the one-element list and the concatenation the expansion writes. It is strict like that
// concatenation: an accumulator that is an error value stays, and an error value of e becomes the
// accumulator. Costs 1 per added element.
internal sealed class AppendPlanNode : PlanNode
{
    private readonly int _accumulator;
    private readonly PlanNode _element;

    internal AppendPlanNode(int offset, int accumulator, PlanNode element)
        : base(offset, 1)
    {
        _accumulator = accumulator;
        _element = element;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        CelValue accumulator = frame.Local(_accumulator);
        if (accumulator.Kind == CelValueKind.Error)
        {
            return accumulator;
        }

        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue element = _element.Evaluate(frame);
        if (element.Kind == CelValueKind.Error)
        {
            return element;
        }

        Begin(frame);
        _ = accumulator.TryGetList(out CelList? list);
        ((ListBuilder)list!).Add(element);
        return accumulator;
    }
}
