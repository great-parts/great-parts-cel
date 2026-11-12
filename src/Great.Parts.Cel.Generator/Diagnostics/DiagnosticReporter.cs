using System;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// Reports the diagnostics of a context. The place of a diagnostic is a path and spans in the model; it is turned back into a
// location of the syntax tree of the compilation here, so that the diagnostic points into the source. The output runs for each
// compilation, but only does work for a context that has diagnostics.
internal static class DiagnosticReporter
{
    internal static void Report(SourceProductionContext production, EquatableArray<DiagnosticModel> diagnostics, Compilation compilation)
    {
        foreach (DiagnosticModel model in diagnostics.Items)
        {
            production.ReportDiagnostic(Diagnostic.Create(Descriptors.Find(model.Id), ToLocation(model.Location, compilation), model.Arguments.Items));
        }
    }

    private static Location ToLocation(LocationInfo info, Compilation compilation)
    {
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (string.Equals(tree.FilePath, info.FilePath, StringComparison.Ordinal) && info.Span.End <= tree.Length)
            {
                return Location.Create(tree, info.Span);
            }
        }

        return Location.Create(info.FilePath, info.Span, info.LineSpan);
    }
}
