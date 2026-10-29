using System;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// A table of cases in one string constant: one case per line, "<input> → <expected>".
internal static class CaseTable
{
    private const string Separator = " → ";

    internal static TheoryData<string, string> Parse(string table)
    {
        var data = new TheoryData<string, string>();
        foreach (string line in table.ReplaceLineEndings("\n").Split('\n'))
        {
            int separator = line.LastIndexOf(Separator, StringComparison.Ordinal);
            data.Add(line[..separator], line[(separator + Separator.Length)..]);
        }

        return data;
    }
}
