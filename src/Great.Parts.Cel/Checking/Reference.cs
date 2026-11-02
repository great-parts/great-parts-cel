using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;

namespace Great.Parts.Cel.Checking;

// What an identifier or a call refers to: the variable of an identifier, or the overload candidates of
// a call in the order of the declaration index.
internal sealed class Reference
{
    private Reference(CelVariableDecl? variable, ImmutableArray<OverloadBinding> candidates)
    {
        Variable = variable;
        Candidates = candidates;
    }

    // The variable of an identifier; null for a call.
    internal CelVariableDecl? Variable { get; }

    // The candidates of a call; empty for an identifier.
    internal ImmutableArray<OverloadBinding> Candidates { get; }

    internal static Reference ToVariable(CelVariableDecl variable) => new(variable, []);

    internal static Reference ToCandidates(ImmutableArray<OverloadBinding> candidates) => new(null, candidates);
}
