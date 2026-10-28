using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>
/// The table of <c>docs/api-concepts.md</c>: the first Markdown table whose first header cell is <c>Type</c>.
/// Columns: the type as in a C# declaration, the namespace, the concept, the explanation of the name.
/// </summary>
internal sealed class ConceptList
{
    private const int CellCount = 4;

    private ConceptList(List<Row> rows)
    {
        Rows = rows;
    }

    /// <summary>The rows of the table in the order of the file.</summary>
    internal IReadOnlyList<Row> Rows { get; }

    /// <summary>Reads the table of the file.</summary>
    /// <param name="text">The text of the file.</param>
    internal static ConceptList Parse(SourceText text)
    {
        List<Row> rows = [];
        bool inTable = false;
        TextLineCollection lines = text.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            string content = lines[i].ToString().Trim();
            if (!inTable)
            {
                List<string>? header = Cells(content);
                inTable = header is { Count: > 0 } && string.Equals(header[0], "Type", StringComparison.Ordinal)
                    && i + 1 < lines.Count && IsSeparator(lines[i + 1].ToString().Trim());
                if (inTable)
                {
                    i++;
                }

                continue;
            }

            List<string>? cells = Cells(content);
            if (cells is null)
            {
                break;
            }

            rows.Add(new Row(lines[i], cells));
        }

        return new ConceptList(rows);
    }

    private static List<string>? Cells(string line)
    {
        if (line.Length == 0 || line[0] != '|')
        {
            return null;
        }

        string inner = line.Length > 1 && line[line.Length - 1] == '|' ? line.Substring(1, line.Length - 2) : line.Substring(1);
        List<string> cells = [];
        foreach (string cell in inner.Split('|'))
        {
            cells.Add(cell.Trim());
        }

        return cells;
    }

    private static bool IsSeparator(string line)
    {
        List<string>? cells = Cells(line);
        if (cells is null)
        {
            return false;
        }

        foreach (string cell in cells)
        {
            if (cell.Length == 0 || cell.Trim(':', '-').Length != 0 || cell.IndexOf('-') < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A row of the table.</summary>
    internal sealed class Row
    {
        internal Row(TextLine line, List<string> cells)
        {
            Line = line;
            Cells = cells;
            if (cells.Count >= 2)
            {
                Namespace = Unformat(cells[1]);
                TypeKey = KeyOf(Unformat(cells[0]));
                if (Namespace.Length > 0 && cells[0].Length > 0)
                {
                    FullName = Namespace + "." + Unformat(cells[0]);
                }
            }

            FullName ??= cells.Count > 0 && cells[0].Length > 0 ? Unformat(cells[0]) : "?";
        }

        /// <summary>The line of the row in the file.</summary>
        internal TextLine Line { get; }

        /// <summary>The cells of the row.</summary>
        internal List<string> Cells { get; }

        /// <summary>The namespace of the row, or <see langword="null"/> when the row has fewer than two cells.</summary>
        internal string? Namespace { get; }

        /// <summary>The full name of the type as written in the row, for messages.</summary>
        internal string FullName { get; }

        /// <summary>The metadata name of the type relative to its namespace, such as <c>CelProgram`1</c>; <see langword="null"/> when the type cell does not parse.</summary>
        internal string? TypeKey { get; }

        /// <summary>The key of the row: namespace and metadata name, as <see cref="KeyOf(INamedTypeSymbol)"/> gives for a type.</summary>
        internal string? Key => Namespace is { Length: > 0 } && TypeKey is not null ? Namespace + "." + TypeKey : null;

        /// <summary>Describes what makes the row invalid, or returns <see langword="null"/> for a valid row.</summary>
        internal string? Problem()
        {
            if (Cells.Count != CellCount)
            {
                return string.Format(CultureInfo.InvariantCulture, "the row has {0} cells instead of {1}", Cells.Count, CellCount);
            }

            if (Key is null)
            {
                return "the type or the namespace cell does not hold a name";
            }

            if (Cells[2].Length == 0)
            {
                return "the cel-spec concept or requirement is empty";
            }

            return Cells[3].Length == 0 ? "the name differs from the cel-spec term and the difference is not explained" : null;
        }

        /// <summary>Returns the key of a public type: its namespace and its metadata name with containing types, such as <c>Great.Parts.Cel.CelProgram`1</c>.</summary>
        /// <param name="type">A type.</param>
        internal static string KeyOf(INamedTypeSymbol type)
        {
            StringBuilder name = new(type.MetadataName);
            for (INamedTypeSymbol? outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
            {
                _ = name.Insert(0, outer.MetadataName + ".");
            }

            return type.ContainingNamespace.IsGlobalNamespace ? name.ToString() : type.ContainingNamespace.ToDisplayString() + "." + name;
        }

        private static string Unformat(string cell) => cell.Replace("`", string.Empty).Trim();

        // "Outer.Inner<T, U>" becomes "Outer.Inner`2"; generic parameters match by their number.
        private static string? KeyOf(string type)
        {
            StringBuilder key = new();
            int depth = 0;
            int arity = 0;
            StringBuilder part = new();
            foreach (char c in type)
            {
                switch (c)
                {
                    case '<':
                        depth++;
                        arity = depth == 1 ? 1 : arity;
                        break;
                    case '>':
                        depth--;
                        break;
                    case ',' when depth == 1:
                        arity++;
                        break;
                    case '.' when depth == 0:
                        if (!Append(key, part, arity))
                        {
                            return null;
                        }

                        _ = key.Append('.');
                        arity = 0;
                        break;
                    default:
                        if (depth == 0 && !char.IsWhiteSpace(c))
                        {
                            _ = part.Append(c);
                        }

                        break;
                }

                if (depth < 0)
                {
                    return null;
                }
            }

            return depth == 0 && Append(key, part, arity) ? key.ToString() : null;
        }

        private static bool Append(StringBuilder key, StringBuilder part, int arity)
        {
            string name = part.ToString();
            _ = part.Clear();
            if (!SyntaxFacts.IsValidIdentifier(name))
            {
                return false;
            }

            _ = key.Append(name);
            if (arity > 0)
            {
                _ = key.Append('`').Append(arity.ToString(CultureInfo.InvariantCulture));
            }

            return true;
        }
    }
}
