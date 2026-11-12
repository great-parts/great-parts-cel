using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// An identifier, or a selection chain that names a declared variable: reads the slot of the variable. The
// first read looks the name up in the bindings and checks the value against the declared type at any
// depth, charging the descent; a value that does not fit is BindingTypeMismatch and a name without a value
// Unbound, both at the identifier; an error value that an element of the value is, is the value of the read. A variable of an object type applies the place rule: a bare instance that belongs
// to the type is kept, paired with its description when the type index describes the instance otherwise. The slot
// keeps the value or the mismatch, so later reads check nothing.
// A bound ConversionRange gets the place of the identifier first. Without type checking nothing is declared and nothing
// is checked. A read costs 1.
internal sealed class VariablePlanNode : PlanNode
{
    private readonly int _slot;

    // The declared type; null without type checking.
    private readonly CelType? _declaredType;

    internal VariablePlanNode(int offset, int slot, string name, CelType? declaredType, TypeIndex types, ulong cost = 1)
        : base(offset, cost)
    {
        _slot = slot;
        _declaredType = declaredType;
        Name = name;
        Types = types;
    }

    // The type index of the environment, which describes object values in the check and in the message of a mismatch.
    internal TypeIndex Types { get; }

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
            // A .NET collection that nothing describes is TypeNotAccessible at the identifier, before the check against the
            // declared type; a value that its conversion could not represent, such as an enumeration on ulong above the
            // range of int, is ConversionRange without a place: the read gives it the place of the identifier.
            if (value.Kind == CelValueKind.Object && ObjectRules.TryRejectCollection(value, _declaredType, Types, out CelValue rejected))
            {
                value = EvaluationErrors.Placed(rejected, frame.Lines, Offset);
            }
            else if (value.TryGetError(out CelError? unplaced) && unplaced.Code == CelErrorCode.ConversionRange)
            {
                value = EvaluationErrors.AtRead(unplaced, frame.Lines, Offset);
            }
            else if (_declaredType is { Kind: CelTypeKind.Object })
            {
                value = value.Kind != CelValueKind.Error && ObjectRules.TryEnterPlace(value, _declaredType, Types, out CelValue placed, out _)
                    ? placed
                    : EvaluationErrors.BindingTypeMismatch(this, value, frame.Lines, Offset);
            }
            else if (_declaredType is not null)
            {
                // An error value that an element of the value is, is the value read: the identifier gives it its place.
                TypeFit fit = ValueTypeRules.Fits(value, _declaredType, frame.Meter, false, Types);
                if (fit.ElementError is { } elementError)
                {
                    value = EvaluationErrors.Placed(CelValue.FromError(elementError), frame.Lines, Offset);
                }
                else if (!fit.IsFit)
                {
                    value = EvaluationErrors.BindingTypeMismatch(this, value, frame.Lines, Offset);
                }
            }

            frame.Keep(_slot, value);
        }

        // A JSON string that the binding holds charges the units of its read at each read of the identifier.
        if (frame.Metered && value.ReadUnits is var units and not 0)
        {
            frame.Charge(units);
        }

        return value;
    }
}
