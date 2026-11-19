using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// An identifier that names a field of the input of the environment (UseInput): the variable is the field of the instance that the
// evaluation got as its input, read through the description of the input type, by its field number, as the selection of the field
// of a variable of that type reads it. Without an input in the frame the first read is Unbound at the identifier; a binding with the
// name of the field is never looked up. The field is read at each reading of the identifier, lazily, as order.total would be. A read
// costs 1, and the description adds the units it knows of the field (FieldReadCost) as for any field of an object. The frame reads the
// field, so that an input that is a structure is read in place, without a box.
internal sealed class InputFieldPlanNode : PlanNode
{
    private readonly CelObjectType _owner;
    private readonly int _number;
    private readonly string _name;

    internal InputFieldPlanNode(int offset, CelObjectType owner, int number, string name, ulong cost)
        : base(offset, cost)
    {
        _owner = owner;
        _number = number;
        _name = name;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        return frame.TryReadInputField(_owner, _number, Offset, out CelValue value)
            ? value
            : EvaluationErrors.Unbound(_name, frame.Lines, Offset);
    }
}
