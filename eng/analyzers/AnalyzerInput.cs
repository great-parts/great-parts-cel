using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>Helpers shared by the rules: additional files, places and the public API.</summary>
internal static class AnalyzerInput
{
    /// <summary>The layer table, relative to the repository root.</summary>
    internal const string LayerTablePath = "eng/layers.txt";

    /// <summary>The API concept list, relative to the repository root.</summary>
    internal const string ConceptListPath = "docs/api-concepts.md";

    /// <summary>Returns the additional file whose path ends with <paramref name="repositoryPath"/>, or <see langword="null"/>.</summary>
    /// <param name="files">The additional files of the compilation.</param>
    /// <param name="repositoryPath">The path of the file relative to the repository root, with forward slashes.</param>
    internal static AdditionalText? Find(ImmutableArray<AdditionalText> files, string repositoryPath)
    {
        foreach (AdditionalText file in files)
        {
            if (IsFile(file, repositoryPath))
            {
                return file;
            }
        }

        return null;
    }

    /// <summary>Tells whether the path of <paramref name="file"/> ends with <paramref name="repositoryPath"/>.</summary>
    /// <param name="file">An additional file.</param>
    /// <param name="repositoryPath">The path of the file relative to the repository root, with forward slashes.</param>
    internal static bool IsFile(AdditionalText file, string repositoryPath)
    {
        string path = file.Path.Replace('\\', '/');
        return string.Equals(path, repositoryPath, StringComparison.Ordinal)
            || path.EndsWith("/" + repositoryPath, StringComparison.Ordinal);
    }

    /// <summary>The place of a line of an additional file, at its first column.</summary>
    /// <param name="file">The additional file.</param>
    /// <param name="line">The line, starting with zero.</param>
    internal static Location LineLocation(AdditionalText file, TextLine line)
    {
        int number = line.LineNumber;
        return Location.Create(
            file.Path,
            line.Span,
            new LinePositionSpan(new LinePosition(number, 0), new LinePosition(number, line.Span.Length)));
    }

    /// <summary>Writes a place as <c>path(line,column)</c>, with the line and the column starting with one.</summary>
    /// <param name="location">A place in source code.</param>
    internal static string Format(Location location)
    {
        FileLinePositionSpan span = location.GetLineSpan();
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}({1},{2})",
            span.Path,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1);
    }

    /// <summary>Tells whether a symbol is part of the public API: it and every type that contains it are visible outside the assembly.</summary>
    /// <param name="symbol">A type or a member.</param>
    internal static bool IsPublicApi(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                    break;
                default:
                    return false;
            }
        }

        return true;
    }
}
