using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Generator;

// The place of a diagnostic as values. A Location holds a syntax tree, which would keep the compilation alive and compare by
// reference, so a model holds the path and the spans and the place is made again when the diagnostic is reported.
internal readonly record struct LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    internal static LocationInfo From(Location location)
    {
        FileLinePositionSpan lineSpan = location.GetLineSpan();
        return new LocationInfo(lineSpan.Path, location.SourceSpan, lineSpan.Span);
    }
}
