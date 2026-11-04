using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// An identifier, or a selection chain that names a declared variable: reads the slot of the variable. The
// first read looks the name up in the bindings and checks the value against the declared type at any
// depth, charging the descent; a value that does not fit is BindingTypeMismatch and a name without a value
// Unbound, both at the identifier. The slot keeps the value or the mismatch, so later reads check nothing.
// A bound ConversionRange gets the place of the identifier first. Without type checking nothing is declared and nothing
// is checked. A read costs 1.
internal sealed class VariablePlanNode : PlanNode
{
    private readonly int _slot;

    // The declared type; null without type checking.
    private readonly CelType? _declaredType;

    internal VariablePlanNode(int offset, int slot, string name, CelType? declaredType)
        : base(offset, 1)
    {
        _slot = slot;
        _declaredType = declaredType;
        Name = name;
    }

    // The full name of the variable, as the bindings hold it.
    internal string Name { get; }

    // The declared type, for the message of a mismatch; null without type checking.
    internal CelType? DeclaredType => _declaredType;

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        if (!frame.TryRead(_slot, Name, out CelValue value, out bool first))
        {
            return EvaluationErrors.Unbound(Name, frame.Lines, Offset);
        }

        if (first)
        {
            // A value that its conversion could not represent, such as an enumeration on ulong above the
            // range of int, is ConversionRange without a place: the read gives it the place of the identifier.
            if (value.TryGetError(out CelError? unplaced) && unplaced.Code == CelErrorCode.ConversionRange)
            {
                value = EvaluationErrors.AtRead(unplaced, frame.Lines, Offset);
            }
            else if (_declaredType is not null && !ValueTypeRules.Fits(value, _declaredType, frame.Meter))
            {
                value = EvaluationErrors.BindingTypeMismatch(this, value, frame.Lines, Offset);
            }

            frame.Keep(_slot, value);
        }

        return value;
    }
}
