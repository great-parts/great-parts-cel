using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The wrapper of a node of a partial plan: evaluates the node and writes its value to the record of the partial state under the id of
// the syntax node, for the residual expression. Without a partial state it only evaluates the node. It costs nothing and has the
// position of the node.
internal sealed class RecordPlanNode : PlanNode
{
    private readonly int _id;
    private readonly PlanNode _node;

    internal RecordPlanNode(int offset, int id, PlanNode node)
        : base(offset, 0)
    {
        _id = id;
        _node = node;
    }

    // The node that is recorded. Only the tests read it: they check the kind of the node in a plan for partial evaluation, such as that
    // a comparison there is not fused. Nothing else reaches the node of a plan from outside, so the tests cannot tell it by evaluating.
    internal PlanNode Node => _node;

    internal override CelValue Evaluate(Frame frame)
    {
        CelValue value = _node.Evaluate(frame);
        frame.Partial?.Record(_id, value);
        return value;
    }
}
