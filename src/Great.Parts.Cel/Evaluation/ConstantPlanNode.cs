using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A literal, which costs nothing, a type identifier with its type value, which costs 1 as a name read, or a list literal of constants
// that the planner made into its list once, which costs 1 + n for n elements as the literal does.
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
