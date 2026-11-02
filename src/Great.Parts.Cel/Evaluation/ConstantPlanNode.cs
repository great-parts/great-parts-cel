using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A literal: returns its value. Costs nothing.
internal sealed class ConstantPlanNode : PlanNode
{
    private readonly CelValue _value;

    internal ConstantPlanNode(int offset, CelValue value)
        : base(offset, 0)
    {
        _value = value;
    }

    internal override CelValue Evaluate(Frame frame) => _value;
}
