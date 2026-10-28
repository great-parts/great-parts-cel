using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>
/// The section of <c>eng/layers.txt</c> for one assembly: for each namespace, the namespaces it may reference.
/// Format: a line <c>[assembly]</c> opens a section, a line <c>namespace -> namespace, namespace</c> is a row,
/// lines that start with <c>#</c> and empty lines are not read.
/// </summary>
internal sealed class LayerTable
{
    private readonly Dictionary<string, HashSet<string>> _rows = new(StringComparer.Ordinal);
    private readonly List<(TextLine Line, string Reason)> _malformedLines = [];

    private LayerTable(AdditionalText file)
    {
        File = file;
    }

    /// <summary>The file of the table.</summary>
    internal AdditionalText File { get; }

    /// <summary>Whether the file has a section for the assembly; without one, the assembly is not checked against the table.</summary>
    internal bool HasSection { get; private set; }

    /// <summary>Lines of the file that do not parse and belong to the section of the assembly or to no section.</summary>
    internal IReadOnlyList<(TextLine Line, string Reason)> MalformedLines => _malformedLines;

    /// <summary>Reads the table for an assembly, or returns <see langword="null"/> when the compilation has no layer table.</summary>
    /// <param name="files">The additional files of the compilation.</param>
    /// <param name="assemblyName">The name of the assembly.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    internal static LayerTable? Read(ImmutableArray<AdditionalText> files, string? assemblyName, CancellationToken cancellationToken)
    {
        AdditionalText? file = AnalyzerInput.Find(files, AnalyzerInput.LayerTablePath);
        SourceText? text = file?.GetText(cancellationToken);
        if (file is null || text is null)
        {
            return null;
        }

        LayerTable table = new(file);
        table.Parse(text, assemblyName ?? string.Empty);
        return table;
    }

    /// <summary>Tells whether the table has a row for a namespace.</summary>
    /// <param name="ns">The full name of the namespace.</param>
    internal bool HasRow(string ns) => _rows.ContainsKey(ns);

    /// <summary>Tells whether the row of <paramref name="source"/> lists <paramref name="target"/>.</summary>
    /// <param name="source">The namespace that references.</param>
    /// <param name="target">The referenced namespace.</param>
    internal bool Allows(string source, string target) => _rows.TryGetValue(source, out HashSet<string>? allowed) && allowed.Contains(target);

    private static bool IsNamespaceName(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (string part in name.Split('.'))
        {
            if (!SyntaxFacts.IsValidIdentifier(part))
            {
                return false;
            }
        }

        return true;
    }

    private void Parse(SourceText text, string assemblyName)
    {
        HashSet<string> sections = new(StringComparer.Ordinal);
        string? section = null;
        foreach (TextLine line in text.Lines)
        {
            string content = line.ToString().Trim();
            if (content.Length == 0 || content[0] == '#')
            {
                continue;
            }

            if (content[0] == '[')
            {
                string name = content.Length > 1 && content[content.Length - 1] == ']' ? content.Substring(1, content.Length - 2).Trim() : string.Empty;
                if (!IsNamespaceName(name))
                {
                    section = null;
                    _malformedLines.Add((line, "a section line is '[<assembly name>]'"));
                    continue;
                }

                section = name;
                if (!sections.Add(name) && IsOwn(name, assemblyName))
                {
                    _malformedLines.Add((line, $"the section '{name}' is repeated"));
                }

                HasSection |= IsOwn(name, assemblyName);
                continue;
            }

            if (section is null)
            {
                _malformedLines.Add((line, "the line is outside a section; a section starts with '[<assembly name>]'"));
                continue;
            }

            if (IsOwn(section, assemblyName))
            {
                ParseRow(line, content);
            }
        }
    }

    private void ParseRow(TextLine line, string content)
    {
        int arrow = content.IndexOf("->", StringComparison.Ordinal);
        if (arrow < 0 || content.IndexOf("->", arrow + 2, StringComparison.Ordinal) >= 0)
        {
            _malformedLines.Add((line, "a line is '<namespace> -> <namespace>, <namespace>, ...'"));
            return;
        }

        string source = content.Substring(0, arrow).Trim();
        if (!IsNamespaceName(source))
        {
            _malformedLines.Add((line, $"'{source}' before '->' is not a namespace name"));
            return;
        }

        HashSet<string> allowed = new(StringComparer.Ordinal);
        string list = content.Substring(arrow + 2).Trim();
        if (list.Length > 0)
        {
            foreach (string item in list.Split(','))
            {
                string target = item.Trim();
                if (!IsNamespaceName(target))
                {
                    _malformedLines.Add((line, $"'{target}' after '->' is not a namespace name"));
                    return;
                }

                allowed.Add(target);
            }
        }

        if (_rows.ContainsKey(source))
        {
            _malformedLines.Add((line, $"the namespace '{source}' has a second line"));
            return;
        }

        _rows.Add(source, allowed);
    }

    private static bool IsOwn(string section, string assemblyName) => string.Equals(section, assemblyName, StringComparison.Ordinal);
}
