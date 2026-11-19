using System;
using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The C# examples of the repository: the blocks of <c>README.md</c>, <c>docs/**/*.md</c> and <c>eng/packages/&lt;package&gt;.AGENTS.md</c> of the
/// packages that have a project, and the <c>&lt;code&gt;</c> elements of the XML documentation in <c>src/**/*.cs</c>
/// (<c>docs/design/eng-docs.md</c>, "examples"). <c>examples</c> builds and runs them, <c>suppressions</c> reads their option
/// <c>skip</c>; the scan is made once per run.
/// </summary>
internal static class ExampleSources
{
    private const string ScanName = "examples";
    private const string Readme = "README.md";
    private const string DocsDirectory = "docs/";
    private const string SourceDirectory = "src/";
    private const string PackagesDirectory = "eng/packages/";
    private const string AgentsSuffix = ".AGENTS.md";

    /// <summary>Returns the examples of the run, sorted by path (ordinal) and line, finding them on the first call.</summary>
    /// <param name="context">The context of the run.</param>
    internal static IReadOnlyList<Example> Of(CheckContext context) => context.Scan(ScanName, () => Read(context));

    private static IReadOnlyList<Example> Read(CheckContext context)
    {
        HashSet<string> paths = [.. context.Files.Select(file => file.Path)];
        List<Example> examples = [];
        foreach (RepositoryFile page in context.Files.Where(file => IsPage(file) || IsPackageAgents(file, paths)))
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

    // The AGENTS.md of a package, eng/packages/<id>.AGENTS.md, ships in the package. Its examples need the package, so the file is read only
    // when the project src/<id>/<id>.csproj exists: the file of a package that is not written yet has nothing to compile against.
    private static bool IsPackageAgents(RepositoryFile file, HashSet<string> paths)
    {
        if (!file.Path.StartsWith(PackagesDirectory, StringComparison.Ordinal) || !file.Path.EndsWith(AgentsSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        string name = file.Path[PackagesDirectory.Length..^AgentsSuffix.Length];
        return name.Length > 0 && !name.Contains('/', StringComparison.Ordinal) && paths.Contains($"{SourceDirectory}{name}/{name}.csproj");
    }
}
