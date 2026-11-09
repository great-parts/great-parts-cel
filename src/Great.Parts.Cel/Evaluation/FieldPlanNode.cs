using System;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A selection e.f or a presence test has(e.f) whose operand has an object type T: it holds the description of T and
// the field number that type checking recorded, so it reads the field without looking up a name. The operand comes
// first, and an error value is the result. An object value whose description is T — a pair of T, or a bare instance
// that belongs to T by an instance test of its class, not by the type index — is read with the bound number; any
// other value is taken as by a selection by name, since a static type is no guarantee of the value. Costs 1.
internal sealed class FieldPlanNode : PlanNode
{
    private readonly PlanNode _operand;
    private readonly CelObjectType _owner;
    private readonly int _number;
    private readonly bool _isPresenceTest;

    // The selection by name, for an operand value of another description or kind.
    private readonly SelectPlanNode _byName;

    internal FieldPlanNode(int offset, PlanNode operand, CelObjectType owner, int number, bool isPresenceTest)
        : base(offset, 1)
    {
        _operand = operand;
        _owner = owner;
        _number = number;
        _isPresenceTest = isPresenceTest;
        _byName = new SelectPlanNode(offset, null, owner.Fields[number].Name, isPresenceTest);
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue operand = _operand.Evaluate(frame);
        if (operand.Kind == CelValueKind.Error)
        {
            return operand;
        }

        if (operand.TryGetObject(out object? instance)
            && (ReferenceEquals(operand.PairedType, _owner) || (operand.PairedType is null && _owner.Belongs(instance))))
        {
            Begin(frame);
            return Read(frame, _owner, instance, _number, _isPresenceTest, Offset);
        }

        return _byName.Select(frame, operand);
    }

    // Reads a field of an instance that belongs to the description, after the unit of the node: counts one event, since
    // it calls code of the application, then reads the field by the place rule, or tests its presence, inside the
    // exception boundary of application code. An error value without a place gets the place of the node.
    internal static CelValue Read(Frame frame, CelObjectType owner, object instance, int number, bool isPresenceTest, int offset)
    {
        frame.Event();
        CelValue value;
        try
        {
            value = isPresenceTest
                ? CelValue.FromBoolean(owner.HasField(instance, number))
                : ObjectRules.ReadField(owner, instance, number, frame.Types);
        }
        catch (Exception exception) when (frame.IsApplicationException(exception))
        {
            return EvaluationErrors.FieldException(exception, owner, number, isPresenceTest, frame.Lines, offset);
        }

        return EvaluationErrors.Placed(value, frame.Lines, offset);
    }
}
