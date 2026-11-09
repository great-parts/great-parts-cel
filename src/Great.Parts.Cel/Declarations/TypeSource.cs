using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// A type source added to a builder: the provider, the text by which build errors name it — type source '<SourceName>',
// or the label of an environment description in JSON — and, for a description in JSON, the positions of its object
// types and their fields in the JSON text, keyed by the description or field instance.
internal sealed class TypeSource(CelTypeProvider provider, string text, IReadOnlyDictionary<object, CelSourceLocation>? positions)
{
    internal CelTypeProvider Provider { get; } = provider;

    internal string Text { get; } = text;

    // The position of a description or a field; null when the source has none.
    internal CelSourceLocation? PositionOf(object declaration) =>
        positions is not null && positions.TryGetValue(declaration, out CelSourceLocation position) ? position : null;
}
