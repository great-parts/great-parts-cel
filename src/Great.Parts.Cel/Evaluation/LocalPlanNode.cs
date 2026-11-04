using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A read of the iteration variable or the accumulator of an enclosing comprehension. A read the author
// wrote costs 1; the reads that the expansion of a standard macro adds cost nothing.
internal sealed class LocalPlanNode : PlanNode
{
    internal LocalPlanNode(int offset, int slot, ulong cost)
        : base(offset, cost)
    {
        Slot = slot;
    }

    internal int Slot { get; }

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        return frame.Local(Slot);
    }
}
