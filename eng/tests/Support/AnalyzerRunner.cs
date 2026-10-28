using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>A source file or an additional file of an in-memory compilation, with its path in the repository.</summary>
/// <param name="Path">The path relative to the repository root, such as <c>src/Great.Parts.Cel/Values/CelList.cs</c>.</param>
/// <param name="Text">The content of the file.</param>
internal sealed record RepositoryFile(string Path, string Text);

/// <summary>Runs one analyzer of <c>eng/analyzers</c> on an in-memory compilation with Roslyn 5.0.0.</summary>
internal static class AnalyzerRunner
{
    // The assemblies of the running .NET runtime.
    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
    [
        .. (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? throw new InvalidOperationException("The runtime host gives no list of trusted platform assemblies."))
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ]);

    /// <summary>Compiles the sources into an assembly and returns the findings of the analyzer.</summary>
    /// <param name="analyzer">The analyzer under test.</param>
    /// <param name="assemblyName">The name of the compiled assembly; the layer table has a section per assembly.</param>
    /// <param name="sources">The C# sources. They must compile without errors.</param>
    /// <param name="additionalFiles">The additional files, such as <c>eng/layers.txt</c>.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    internal static async Task<IReadOnlyList<AnalyzerFinding>> RunAsync(
        DiagnosticAnalyzer analyzer,
        string assemblyName,
        IEnumerable<RepositoryFile> sources,
        IEnumerable<RepositoryFile> additionalFiles,
        CancellationToken cancellationToken)
    {
        Compilation compilation = Compile(assemblyName, sources, cancellationToken);
        AnalyzerOptions options = new([.. additionalFiles.Select(file => new InMemoryAdditionalText(file.Path, file.Text))]);
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers([analyzer], options)
            .GetAnalyzerDiagnosticsAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. diagnostics.Select(AnalyzerFinding.From)];
    }

    private static CSharpCompilation Compile(string assemblyName, IEnumerable<RepositoryFile> sources, CancellationToken cancellationToken)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.CSharp14, DocumentationMode.Parse);
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            sources.Select(file => CSharpSyntaxTree.ParseText(file.Text, parseOptions, file.Path, Encoding.UTF8, cancellationToken)),
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        // A violating example must be valid C#: the analyzer runs on code that the compiler accepts.
        Assert.Empty(compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => AnalyzerFinding.From(diagnostic).Format()));
        return compilation;
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(text, Encoding.UTF8);

        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
