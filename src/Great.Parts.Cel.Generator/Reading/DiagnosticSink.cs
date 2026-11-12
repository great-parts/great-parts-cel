using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// The diagnostics of one context, collected while it is read and returned in an order that does not depend on the order of the
// sources: by file, by place, by code, by arguments.
internal sealed class DiagnosticSink
{
    private readonly List<DiagnosticModel> items = [];

    internal void Report(string id, LocationInfo place, params string[] arguments)
    {
        items.Add(new DiagnosticModel(id, place, new EquatableArray<string>(arguments)));
    }

    internal EquatableArray<DiagnosticModel> ToArray()
    {
        items.Sort(Compare);
        return EquatableArray.From(items);
    }

    private static int Compare(DiagnosticModel left, DiagnosticModel right)
    {
        int result = string.CompareOrdinal(left.Location.FilePath, right.Location.FilePath);
        if (result == 0)
        {
            result = left.Location.Span.Start.CompareTo(right.Location.Span.Start);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(left.Id, right.Id);
        }

        return result != 0
            ? result
            : string.CompareOrdinal(string.Join("\u0001", left.Arguments.Items), string.Join("\u0001", right.Arguments.Items));
    }
}
