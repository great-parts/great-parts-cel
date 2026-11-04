using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A selection e.f that names no declaration, or the presence test has(e.f). The operand comes first, and
// an error value is the result. On a map, e.f is the value of the string key f, and no such key is
// EvalNoSuchField; has(e.f) is whether the key is present. Any other value is FieldSelectionNotSupported
// for both, after the constant unit only. Costs 1, and on a map ⌈L/10⌉ more, L the length of f.
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
        if (!operand.TryGetMap(out CelMap? map))
        {
            return EvaluationErrors.FieldSelectionNotSupported(operand.Kind, _field, frame.Lines, Offset);
        }

        if (frame.Metered)
        {
            frame.Charge(_keyCost);
        }

        bool found = map.TryGetValue(CelValue.FromString(_field), out CelValue value);
        if (_isPresenceTest)
        {
            return CelValue.FromBoolean(found);
        }

        return found ? value : EvaluationErrors.NoSuchField(_field, operand.Kind, frame.Lines, Offset);
    }
}
