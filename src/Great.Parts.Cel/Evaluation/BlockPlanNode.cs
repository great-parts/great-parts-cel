using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// cel.@block(slots, result) with a list literal of slots: entering the block takes a new epoch, which makes every slot of the
// block unread, and then the result is evaluated; a slot is evaluated by the first node that reads it. The block itself charges
// nothing and creates no list of slots.
internal sealed class BlockPlanNode : PlanNode
{
    private readonly int _block;
    private readonly PlanNode _result;

    internal BlockPlanNode(int offset, int block, PlanNode result)
        : base(offset, 0)
    {
        _block = block;
        _result = result;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        frame.EnterBlock(_block);
        return _result.Evaluate(frame);
    }
}
