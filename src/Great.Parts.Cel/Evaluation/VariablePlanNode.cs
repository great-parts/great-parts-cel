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
// In an environment whose bindings created code writes from the same static types as the declarations (the hidden environment of a
// registered anonymous type), the check is the top level only: it does not descend into a collection and charges nothing, and an
// error value or a null that an element is, is met when the expression reads that element.
// A bound ConversionRange gets the place of the identifier first. Without type checking nothing is declared and nothing
// is checked. A read costs 1.
internal sealed class VariablePlanNode : PlanNode
{
    private readonly int _slot;

    // The declared type; null without type checking.
    private readonly CelType? _declaredType;

    // Whether the first read checks the top level of the value only.
    private readonly bool _topLevelOnly;

    internal VariablePlanNode(int offset, int slot, string name, CelType? declaredType, TypeIndex types, bool topLevelOnly = false, ulong cost = 1)
        : base(offset, cost)
    {
        _slot = slot;
        _declaredType = declaredType;
        _topLevelOnly = topLevelOnly;
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
            // A .NET collection that nothing describes, and a decimal that the environment has no set for, are TypeNotAccessible at the
            // identifier, before the check against the declared type; a value that its conversion could not represent, such as an
            // enumeration on ulong above the range of int, is ConversionRange without a place, and a collection of the application
            // that is null or a default ImmutableArray<T> is UnexpectedNull without a place: the read gives either the place of the
            // identifier.
            if (ObjectRules.TryRejectValue(value, _declaredType, Types, out CelValue rejected))
            {
                value = EvaluationErrors.Placed(rejected, frame.Lines, Offset);
            }
            else if (value.TryGetError(out CelError? unplaced) && unplaced.Code is CelErrorCode.ConversionRange or CelErrorCode.UnexpectedNull)
            {
                value = EvaluationErrors.AtRead(unplaced, frame.Lines, Offset);
            }
            else if (_declaredType is { Kind: CelTypeKind.Object })
            {
                value = value.Kind != CelValueKind.Error && ObjectRules.TryEnterPlace(value, _declaredType, Types, out CelValue placed, out _)
                    ? placed
                    : EvaluationErrors.BindingTypeMismatch(this, value, frame.Lines, Offset);
            }
            else if (_declaredType is { } declared)
            {
                value = Checked(value, declared, frame);
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

    // The value against a declared type that is not an object type: at any depth, charging the descent, or at the top level only.
    private CelValue Checked(CelValue value, CelType declared, Frame frame)
    {
        if (_topLevelOnly)
        {
            return ValueTypeRules.FitsTopLevel(value, declared, Types) ? value : EvaluationErrors.BindingTypeMismatch(this, value, frame.Lines, Offset);
        }

        // An error value that an element of the value is, is the value read: the identifier gives it its place.
        TypeFit fit = ValueTypeRules.Fits(value, declared, frame.Meter, false, Types);
        if (fit.ElementError is { } elementError)
        {
            return EvaluationErrors.Placed(CelValue.FromError(elementError), frame.Lines, Offset);
        }

        return fit.IsFit ? value : EvaluationErrors.BindingTypeMismatch(this, value, frame.Lines, Offset);
    }
}
