using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A qualifier of a chain of attributes: a field name or a constant key, known at planning, or the node of a key that is an
// expression, whose value is known at evaluation.
internal sealed class AttributeStep
{
    internal AttributeStep(CelValue constant)
    {
        Constant = constant;
    }

    internal AttributeStep(KeyPlanNode key)
    {
        Key = key;
    }

    internal CelValue Constant { get; }

    internal KeyPlanNode? Key { get; }
}
