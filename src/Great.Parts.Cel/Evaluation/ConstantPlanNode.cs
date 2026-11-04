using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A literal, which costs nothing, or a type identifier with its type value, which costs 1 as a name read.
internal sealed class ConstantPlanNode : PlanNode
{
    private readonly CelValue _value;

    internal ConstantPlanNode(int offset, CelValue value, ulong cost = 0)
        : base(offset, cost)
    {
        _value = value;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        return _value;
    }
}
