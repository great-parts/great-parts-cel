using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A selection e.f that names no declaration, or the presence test has(e.f). The operand comes first, and
// an error value is the result. On a map, e.f is the value of the string key f, and no such key is
// EvalNoSuchField; has(e.f) is whether the key is present. On an object value the field is found by name in the
// description of the value — an instance without one is TypeNotAccessible — and read as by FieldPlanNode; no such
// field is EvalNoSuchField with the full name of the type, for has() too. Any other value is
// FieldSelectionNotSupported for both, after the constant unit only. Costs 1, and on a map ⌈L/10⌉ more, L the
// length of f; on a JSON object, which the lookup walks whole, n · (1 + ⌈L/10⌉) more for n properties.
internal sealed class SelectPlanNode : PlanNode
{
    // Null for a selection of a name without type checking, which selects on the value of the name.
    private readonly PlanNode? _operand;
    private readonly string _field;
    private readonly bool _isPresenceTest;
    private readonly ulong _keyCost;

    internal SelectPlanNode(int offset, PlanNode? operand, string field, bool isPresenceTest)
        : base(offset, 1)
    {
        _operand = operand;
        _field = field;
        _isPresenceTest = isPresenceTest;
        _keyCost = ((ulong)field.Length + 9) / 10;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue operand = _operand!.Evaluate(frame);
        return operand.Kind == CelValueKind.Error ? operand : Select(frame, operand);
    }

    // The selection on the value of the operand, which is not an error value.
    internal CelValue Select(Frame frame, CelValue operand)
    {
        Begin(frame);
        if (operand.Kind == CelValueKind.Object)
        {
            return SelectObject(frame, operand);
        }

        if (!operand.TryGetMap(out CelMap? map))
        {
            return EvaluationErrors.FieldSelectionNotSupported(operand.Kind, _field, frame.Lines, Offset);
        }

        if (frame.Metered)
        {
            frame.Charge(map.ProbeUnits(_keyCost));
        }

        bool found = map.TryRead(CelValue.FromString(_field), frame.Meter, readValue: !_isPresenceTest, out CelValue value);
        if (_isPresenceTest)
        {
            // The lookup of a key that a JSON object repeats is an error; any other value is not read by the test.
            return found && value.TryGetError(out CelError? repeated) && repeated.Code == CelErrorCode.DuplicateMapKey
                ? EvaluationErrors.Placed(value, frame.Lines, Offset)
                : CelValue.FromBoolean(found);
        }

        return found ? EvaluationErrors.Placed(value, frame.Lines, Offset) : EvaluationErrors.NoSuchField(_field, operand.Kind, frame.Lines, Offset);
    }

    // The field of an object value by its name in the description of the value.
    private CelValue SelectObject(Frame frame, CelValue operand)
    {
        if (!ObjectRules.TryDescribe(operand, frame.Types, out CelObjectType? description, out CelValue notAccessible))
        {
            return EvaluationErrors.Placed(notAccessible, frame.Lines, Offset);
        }

        if (!description!.TryGetField(_field, out CelObjectField? field))
        {
            return EvaluationErrors.NoSuchField(_field, description, frame.Lines, Offset);
        }

        _ = operand.TryGetObject(out object? instance);
        return FieldPlanNode.Read(frame, description, instance!, field.Number, _isPresenceTest, Offset);
    }
}
