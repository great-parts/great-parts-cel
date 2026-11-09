using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

// A declaration with its source: the text by which build errors name it — "application", or the label of an
// environment description in JSON — and, for a description in JSON, the position of the declaration in the JSON text and
// the positions of its parts, such as the overloads of a function, keyed by instance. A declaration of the application
// has no position.
internal readonly record struct SourcedDeclaration<T>(
    T Value,
    string Source,
    CelSourceLocation? Position,
    IReadOnlyDictionary<object, CelSourceLocation>? Parts = null)
    where T : notnull
{
    internal static SourcedDeclaration<T> InApplication(T value) => new(value, DeclarationChecks.ApplicationSource, null);

    // The position of a part of the declaration, an overload of a function; null when it has none.
    internal CelSourceLocation? PartPosition(object part) =>
        Parts is not null && Parts.TryGetValue(part, out CelSourceLocation position) ? position : null;

    // Orders declarations by the source text, ordinal, then by the position in the JSON text, so that the order of the
    // calls does not decide which of two declarations a message names first.
    internal int CompareTo(SourcedDeclaration<T> other)
    {
        int bySource = string.CompareOrdinal(Source, other.Source);
        return bySource != 0 ? bySource : (Position?.Offset ?? int.MaxValue).CompareTo(other.Position?.Offset ?? int.MaxValue);
    }
}
