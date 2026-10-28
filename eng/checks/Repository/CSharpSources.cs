using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The parse of a file under one symbol set. Sets that read the file the same way share one tree.</summary>
/// <param name="Set">The symbol set.</param>
/// <param name="Tree">The syntax tree of the file under the set.</param>
internal readonly record struct SourceParse(SymbolSet Set, SyntaxTree Tree);

/// <summary>A place found in a file, once for all the parses.</summary>
/// <typeparam name="T">What was found.</typeparam>
/// <param name="Path">The path of the file, relative to the root.</param>
/// <param name="Span">The span in the text of the file.</param>
/// <param name="Position">The line and column of the start of the span.</param>
/// <param name="Value">The found node or value, from the first parse that has the place.</param>
internal readonly record struct SourcePlace<T>(string Path, TextSpan Span, Position Position, T Value);

/// <summary>A C# file that is not generated, parsed under every symbol set.</summary>
internal sealed class CSharpFile
{
    /// <summary>Creates the file.</summary>
    /// <param name="path">The path relative to the root, with <c>/</c>.</param>
    /// <param name="text">The text of the file, read as UTF-8.</param>
    /// <param name="parses">One parse per symbol set.</param>
    internal CSharpFile(string path, string text, IReadOnlyList<SourceParse> parses)
    {
        Path = path;
        Text = text;
        Lines = new SourceLines(text);
        Parses = parses;
    }

    /// <summary>The path relative to the root, with <c>/</c>.</summary>
    internal string Path { get; }

    /// <summary>The text of the file.</summary>
    internal string Text { get; }

    /// <summary>The lines of the text, for line and column from an offset.</summary>
    internal SourceLines Lines { get; }

    /// <summary>One parse per symbol set, in the order of the sets.</summary>
    internal IReadOnlyList<SourceParse> Parses { get; }

    /// <summary>
    /// Runs a search in every parse and returns each place once: a place that several symbol sets read the same way, such as
    /// code outside any <c>#if</c>, is one place. Code that only some sets activate is found when one of them does.
    /// </summary>
    /// <typeparam name="T">What the search finds.</typeparam>
    /// <param name="find">Returns the spans and values found in one tree.</param>
    /// <returns>The places in the order of their position in the text.</returns>
    internal IReadOnlyList<SourcePlace<T>> Find<T>(Func<SyntaxTree, IEnumerable<(TextSpan Span, T Value)>> find)
    {
        HashSet<SyntaxTree> trees = [];
        HashSet<TextSpan> spans = [];
        List<SourcePlace<T>> places = [];
        foreach (SourceParse parse in Parses)
        {
            if (!trees.Add(parse.Tree))
            {
                continue;
            }

            foreach ((TextSpan span, T value) in find(parse.Tree))
            {
                if (spans.Add(span))
                {
                    places.Add(new SourcePlace<T>(Path, span, Lines.Locate(span.Start), value));
                }
            }
        }

        places.Sort((left, right) => left.Span.Start.CompareTo(right.Span.Start));
        return places;
    }
}

/// <summary>
/// The C# files of the file set parsed with Roslyn syntax trees, without a compilation, once per symbol set. Generated files
/// are kept apart: the marker comment and the name are the signs, see <see cref="GeneratedCode"/>.
/// </summary>
internal sealed class CSharpSources
{
    private CSharpSources(IReadOnlyList<SymbolSet> sets, IReadOnlyList<CSharpFile> files, IReadOnlyList<GeneratedFile> generatedFiles)
    {
        Sets = sets;
        Files = files;
        GeneratedFiles = generatedFiles;
    }

    /// <summary>The symbol sets of the repository.</summary>
    internal IReadOnlyList<SymbolSet> Sets { get; }

    /// <summary>The C# files that are not generated, sorted by path.</summary>
    internal IReadOnlyList<CSharpFile> Files { get; }

    /// <summary>The generated C# files, inside the allowed directories and outside them, sorted by path.</summary>
    internal IReadOnlyList<GeneratedFile> GeneratedFiles { get; }

    /// <summary>Reads and parses the C# files of the file set.</summary>
    /// <param name="files">The file set.</param>
    internal static CSharpSources Load(IReadOnlyList<RepositoryFile> files)
    {
        IReadOnlyList<SymbolSet> sets = SymbolSets.Discover(files);
        List<CSharpFile> sources = [];
        List<GeneratedFile> generated = [];
        foreach (RepositoryFile file in files.Where(candidate => candidate.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            string text = file.ReadText();
            SourceLines lines = new(text);
            SyntaxTree first = ParseUnder(text, file.Path, sets[0]);
            if (GeneratedCode.Inspect(file.Path, first, lines) is { } generatedFile)
            {
                generated.Add(generatedFile);
            }
            else
            {
                sources.Add(new CSharpFile(file.Path, text, ParseUnderAll(text, file.Path, sets, first)));
            }
        }

        return new CSharpSources(sets, sources, generated);
    }

    // The first set decides the signs of a generated file. A file without #if and #elif reads the same under every set, so
    // its tree is shared; otherwise each set gets its own parse.
    private static SourceParse[] ParseUnderAll(string text, string path, IReadOnlyList<SymbolSet> sets, SyntaxTree first)
    {
        bool differs = SyntaxQueries.HasConditionalDirective(first.GetRoot());
        return [.. sets.Select((set, index) => new SourceParse(set, index == 0 || !differs ? first : ParseUnder(text, path, set)))];
    }

    // The newest language version that Roslyn 5.0.0 knows. Code for a newer one parses with errors, which are not findings.
    private static SyntaxTree ParseUnder(string text, string path, SymbolSet set) => CSharpSyntaxTree.ParseText(
        text,
        new CSharpParseOptions(LanguageVersion.CSharp14, DocumentationMode.None, SourceCodeKind.Regular, set.Symbols),
        path);
}
