using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A pattern of unknown data as the evaluation reads it: the full name of a variable and the qualifiers under it, in order — a string,
// an int, a uint or a bool, and null for a qualifier that matches any. The facade makes it from a CelAttributePattern, which the
// layers below it cannot name.
internal sealed class UnknownPattern(string variable, ImmutableArray<CelValue?> qualifiers)
{
    internal string Variable { get; } = variable;

    internal ImmutableArray<CelValue?> Qualifiers { get; } = qualifiers;
}
