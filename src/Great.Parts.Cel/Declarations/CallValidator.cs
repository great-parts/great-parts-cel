using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

// A check of a call at compilation that an overload of an extension set carries. The checker runs it after the traversal of the
// tree, when it has found no error, with a neutral description of the call: the final static types of its parts and the
// literals among them. It answers Ok or an error code with a detail and, optionally, the offset of the part at fault.
internal delegate CallValidation CallValidator(CallShape call);

// The answer of a validator. A Code of 0 means nothing is wrong; an Offset of null puts the error at the call.
internal readonly record struct CallValidation(CelErrorCode Code, string Detail, int? Offset)
{
    internal static CallValidation Ok => default;

    internal bool IsOk => Code == 0;
}

// The receiver, if the call has one, and the arguments of a call.
internal readonly record struct CallShape(ValueShape? Receiver, ImmutableArray<ValueShape> Arguments);

// One part of a call as the checker saw it: the offset of its node, its final static type with the substitution applied and
// the free variables replaced by dyn, the value of a string literal, the elements of a list literal and the entries of a map
// literal. A part that is none of these literals has null in their places.
internal sealed record ValueShape(
    int Offset,
    CelType Type,
    string? StringConstant,
    ImmutableArray<ValueShape>? Elements,
    bool HasOptionalElements,
    ImmutableArray<(ValueShape Key, ValueShape Value)>? Entries);
