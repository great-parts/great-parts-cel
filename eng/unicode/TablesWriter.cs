using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>A file of the output.</summary>
/// <param name="Name">The name of the file in the output directory.</param>
/// <param name="Text">The text, LF line ends, ending in one line feed.</param>
internal sealed record GeneratedFile(string Name, string Text);

/// <summary>
/// The text of the generated files (docs/design/unicode-tables.md, "The form of the output"). The output is deterministic: the tables go
/// in the order of the model, numbers are hexadecimal with the invariant culture and six digits, lines end in LF, a line holds at most
/// 8 values, and a file ends in one line feed. Nothing in it depends on the date, the machine, the culture or the version of the generator.
/// </summary>
internal static class TablesWriter
{
    /// <summary>The name of the file with the ranges of the tables.</summary>
    internal const string RangesFile = "UnicodeTables.Ranges.g.cs";

    /// <summary>The name of the file with the orbits of the folding.</summary>
    internal const string FoldingFile = "UnicodeTables.Folding.g.cs";

    /// <summary>The name of the file with the names of the tables.</summary>
    internal const string NamesFile = "UnicodeTables.Names.g.cs";

    private const int ValuesPerLine = 8;

    /// <summary>The three files of a model.</summary>
    /// <param name="model">The model.</param>
    internal static IReadOnlyList<GeneratedFile> Write(TableModel model) =>
    [
        new(FoldingFile, WriteFolding(model)),
        new(NamesFile, WriteNames(model)),
        new(RangesFile, WriteRanges(model)),
    ];

    private static string WriteRanges(TableModel model)
    {
        StringBuilder text = Begin(model);
        text.Append("internal static partial class UnicodeTables\n{\n");
        text.Append(Invariant($"    // The number of tables: the categories and the scripts, then the fold tables.\n    internal const int TableCount = {model.Tables.Count};\n\n"));

        // Table t is RangeData[TableStarts[t]..TableStarts[t + 1]].
        List<int> starts = [];
        int start = 0;
        foreach (TableDefinition table in model.Tables)
        {
            starts.Add(start);
            start += table.Ranges.Pairs.Length;
        }

        starts.Add(start);
        text.Append("    // The index in RangeData of the first value of each table, and the end of the last: table t is RangeData[TableStarts[t]..TableStarts[t + 1]].\n");
        text.Append("    private static ReadOnlySpan<int> TableStarts =>\n    [\n");
        AppendValues(text, starts);
        text.Append("    ];\n\n");

        text.Append("    // The ranges of all tables, each table as the pairs lo, hi of its ranges in ascending order, merged. A comment names each table by its number.\n");
        text.Append("    private static ReadOnlySpan<int> RangeData =>\n    [\n");
        for (int number = 0; number < model.Tables.Count; number++)
        {
            TableDefinition table = model.Tables[number];
            text.Append(Invariant($"        // {number} {table.Name}, {table.Ranges.Count} ranges\n"));
            AppendValues(text, table.Ranges.Pairs.ToArray());
        }

        text.Append("    ];\n}\n");
        return text.ToString();
    }

    private static string WriteFolding(TableModel model)
    {
        StringBuilder text = Begin(model);
        text.Append("internal static partial class UnicodeTables\n{\n");
        text.Append("    // The code points that fold together with another, ascending: the members of the orbits of the simple case folding.\n");
        text.Append("    private static ReadOnlySpan<int> OrbitKeys =>\n    [\n");
        AppendValues(text, model.OrbitKeys);
        text.Append("    ];\n\n");
        text.Append("    // For each code point of OrbitKeys, the next member of its orbit: the next in ascending order, the smallest after the last.\n");
        text.Append("    private static ReadOnlySpan<int> OrbitNext =>\n    [\n");
        AppendValues(text, model.OrbitNext);
        text.Append("    ];\n}\n");
        return text.ToString();
    }

    private static string WriteNames(TableModel model)
    {
        StringBuilder text = Begin(model);
        text.Append("internal static partial class UnicodeTables\n{\n");
        text.Append("    /// <summary>\n");
        text.Append("    /// Finds a table by its canonical name: the name of Go after canonicalName, such as Lu, Greek or Olditalic. A class takes the\n");
        text.Append("    /// table and, under (?i), the fold table; Assigned is the complement of the table of the unassigned.\n");
        text.Append("    /// </summary>\n");
        text.Append("    /// <param name=\"canonicalName\">The canonical name.</param>\n");
        text.Append("    /// <param name=\"name\">The tables of the name; the default value when there is none.</param>\n");
        text.Append("    /// <returns>Whether the name is that of a table.</returns>\n");
        text.Append("    internal static bool TryFindTable(ReadOnlySpan<char> canonicalName, out TableName name)\n    {\n");
        text.Append("        switch (canonicalName)\n        {\n");
        foreach (TableNameEntry entry in model.Names)
        {
            text.Append(Invariant($"            case \"{entry.Name}\":\n"));
            text.Append(Invariant($"                name = new TableName({entry.Table}, {entry.FoldTable}, {(entry.Complement ? "true" : "false")});\n"));
            text.Append("                return true;\n");
        }

        text.Append("            default:\n                name = default;\n                return false;\n        }\n    }\n}\n");
        return text.ToString();
    }

    // The header and the namespace of a file.
    private static StringBuilder Begin(TableModel model)
    {
        StringBuilder text = new();
        text.Append("// <auto-generated>\n");
        text.Append(Invariant($"// Written by eng/unicode from the Unicode Character Database {model.Inputs.Version}:\n"));
        foreach (UcdFile file in model.Inputs.Files)
        {
            text.Append(UcdInputs.HeaderLine(file)).Append('\n');
        }

        text.Append("// Changes to this file are lost when the generator runs again.\n");
        text.Append("// </auto-generated>\n\n");
        text.Append("using System;\n\n");
        text.Append("namespace Great.Parts.Cel.Library.Unicode;\n\n");
        return text;
    }

    private static void AppendValues(StringBuilder text, IReadOnlyList<int> values)
    {
        for (int index = 0; index < values.Count; index += ValuesPerLine)
        {
            text.Append("       ");
            int end = Math.Min(index + ValuesPerLine, values.Count);
            for (int at = index; at < end; at++)
            {
                text.Append(" 0x").Append(values[at].ToString("X6", CultureInfo.InvariantCulture)).Append(',');
            }

            text.Append('\n');
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
