using System.Collections.Immutable;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A name without type checking: an identifier, or an identifier with the selections of its chain. At
// evaluation it takes the first candidate, the longest qualified name first, that has a value in the
// bindings or is a type identifier of the version, and selects the fields that follow that candidate.
// No candidate is Unbound for the name as written. Values are not checked against declared types.
// The read costs 1, and each selection its own cost.
internal sealed class NamePlanNode : PlanNode
{
    private readonly string _written;
    private readonly ImmutableArray<Candidate> _candidates;

    // The selections of the chain, the innermost first.
    private readonly SelectPlanNode[] _selections;

    internal NamePlanNode(int offset, string written, ImmutableArray<Candidate> candidates, SelectPlanNode[] selections)
        : base(offset, 1)
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
                value = bound;
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

    // A qualified name that the identifier and its first selections may stand for, and the type value of
    // a type identifier of that name.
    internal readonly record struct Candidate(string Name, int Selections, CelType? TypeValue);
}
