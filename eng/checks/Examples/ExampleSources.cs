using System;
using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The C# examples of the repository: the blocks of <c>README.md</c> and <c>docs/**/*.md</c> and the <c>&lt;code&gt;</c> elements of the XML
/// documentation in <c>src/**/*.cs</c> (<c>docs/design/eng-docs.md</c>, "examples"). <c>examples</c> builds and runs them,
/// <c>suppressions</c> reads their option <c>skip</c>; the scan is made once per run.
/// </summary>
internal static class ExampleSources
{
    private const string ScanName = "examples";
    private const string Readme = "README.md";
    private const string DocsDirectory = "docs/";
    private const string SourceDirectory = "src/";

    /// <summary>Returns the examples of the run, sorted by path (ordinal) and line, finding them on the first call.</summary>
    /// <param name="context">The context of the run.</param>
    internal static IReadOnlyList<Example> Of(CheckContext context) => context.Scan(ScanName, () => Read(context));

    private static IReadOnlyList<Example> Read(CheckContext context)
    {
        List<Example> examples = [];
        foreach (RepositoryFile page in context.Files.Where(IsPage))
        {
            examples.AddRange(MarkdownExamples.Read(page.Path, page.ReadText()));
        }

        foreach (CSharpFile source in context.Sources.Files.Where(file => file.Path.StartsWith(SourceDirectory, StringComparison.Ordinal)))
        {
            examples.AddRange(XmlDocExamples.Read(source));
        }

        return [.. examples.OrderBy(example => example.Path, StringComparer.Ordinal).ThenBy(example => example.FirstLine)];
    }

    private static bool IsPage(RepositoryFile file) =>
        file.Path == Readme || (file.Path.StartsWith(DocsDirectory, StringComparison.Ordinal) && file.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
}
