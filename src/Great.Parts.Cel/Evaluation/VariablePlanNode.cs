using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// An identifier: reads the slot of its variable; a name without a value in the bindings is Unbound at
// the identifier. A read costs 1.
internal sealed class VariablePlanNode : PlanNode
{
    private readonly int _slot;

    internal VariablePlanNode(int offset, int slot, string name)
        : base(offset, 1)
    {
        _slot = slot;
        Name = name;
    }

    internal string Name { get; }

    internal override CelValue Evaluate(Frame frame) =>
        frame.TryRead(_slot, Name, out CelValue value)
            ? value
            : EvaluationErrors.Unbound(Name, frame.Lines.GetLocation(Offset));
}
