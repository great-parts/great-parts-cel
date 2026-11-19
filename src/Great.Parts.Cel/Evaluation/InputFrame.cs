using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The frame of an evaluation whose input is a structure. The structure lives in the frame, which the evaluation allocates anyway, so
// the call allocates nothing for it: a field is read through the typed description, CelObjectType<TInput>, with the structure passed
// by reference. An input whose description is not that class — a description written over CelObjectType, or an input interface that
// the structure implements — is read through the object members: the structure is boxed once, at the first read, and the box is kept
// in Input for the rest of the evaluation.
internal sealed class InputFrame<TInput> : Frame
{
    private readonly TInput _input;

    // The description of the input as the typed class, or null when the description is another one.
    private readonly CelObjectType<TInput>? _typed;

    internal InputFrame(
        Plan plan,
        IReadOnlyDictionary<string, CelValue> bindings,
        ulong? costLimit,
        TInput input,
        CelObjectType<TInput>? typed,
        CancellationToken cancellationToken)
        : base(plan, bindings, costLimit, cancellationToken)
    {
        _input = input;
        _typed = typed;
    }

    internal override bool TryReadInputField(CelObjectType owner, int number, int offset, out CelValue value)
    {
        if (ReferenceEquals(owner, _typed))
        {
            value = FieldPlanNode.ReadTyped(this, _typed!, in _input, number, offset);
            return true;
        }

        Input ??= _input;
        value = FieldPlanNode.Read(this, owner, Input!, number, false, offset);
        return true;
    }
}
