using System.Collections.Immutable;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A name without type checking: an identifier, or an identifier with the selections of its chain. At
// evaluation it takes the first candidate, the longest qualified name first, that has a value in the
// bindings or is a type identifier of the version, and selects the fields that follow that candidate.
// No candidate is Unbound for the name as written. Values are not checked against declared types; a variable declared
// with an object type applies the place rule to a value of any kind, so that a dictionary which carries the type enters its place.
// The read costs 1, and each selection its own cost.
internal sealed class NamePlanNode : PlanNode
{
    private readonly string _written;
    private readonly ImmutableArray<Candidate> _candidates;

    // The selections of the chain, the innermost first.
    private readonly SelectPlanNode[] _selections;

    internal NamePlanNode(int offset, string written, ImmutableArray<Candidate> candidates, SelectPlanNode[] selections, ulong cost = 1)
        : base(offset, cost)
    {
        _written = written;
        _candidates = candidates;
        _selections = selections;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        Begin(frame);
        foreach (Candidate candidate in _candidates)
        {
            CelValue value;
            if (frame.TryLookup(candidate.Name, out CelValue bound))
            {
                value = candidate.ObjectType is { } objectType
                    && bound.Kind != CelValueKind.Error
                    && ObjectRules.TryEnterPlace(bound, objectType, frame.Types, out CelValue placed, out _)
                    ? placed
                    : bound;

                // A JSON string that the binding holds charges the units of its read.
                if (frame.Metered && value.ReadUnits is var units and not 0)
                {
                    frame.Charge(units);
                }
            }
            else if (candidate.TypeValue is not null)
            {
                value = CelValue.FromType(candidate.TypeValue);
            }
            else
            {
                continue;
            }

            for (int i = candidate.Selections; i < _selections.Length && value.Kind != CelValueKind.Error; i++)
            {
                value = _selections[i].Select(frame, value);
            }

            return value;
        }

        return EvaluationErrors.Unbound(_written, frame.Lines, Offset);
    }

    // A qualified name that the identifier and its first selections may stand for, the type value of a type identifier
    // of that name, and the object type of a variable declared with one.
    internal readonly record struct Candidate(string Name, int Selections, CelType? TypeValue, CelType? ObjectType = null);
}
