using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Generator.Tests.CallSites;

// What the tests of the generator of call sites share: a driver for it alone, one run on a compilation in memory of the examples
// (GeneratorExample.Compile) and the reading of its result.
internal static class CallSiteRun
{
    internal const string Head = "using System.Collections.Generic;\nusing Great.Parts.Cel;\nnamespace Acme;\n";

    internal static string Body(string statements) =>
        Head + "public sealed class Order { }\nclass C\n{\n    void M()\n    {\n" + statements + "    }\n}\n";

    internal static GeneratorDriver Driver(bool track = false, LanguageVersion version = LanguageVersion.CSharp12) =>
        CSharpGeneratorDriver.Create(
            [new CelAnonymousDataGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: track),
            parseOptions: new CSharpParseOptions(version));

    internal static Outcome Run(
        string source,
        IEnumerable<string>? more = null,
        LanguageVersion version = LanguageVersion.CSharp12,
        NullableContextOptions nullable = NullableContextOptions.Enable,
        IEnumerable<MetadataReference>? references = null)
    {
        CSharpCompilation compilation = GeneratorExample.Compile(source, version, nullable, more, references);
        GeneratorDriver driver = Driver(version: version).RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out _);
        GeneratorDriverRunResult result = driver.GetRunResult();
        GeneratedSourceResult[] sources = [.. result.Results.SelectMany(run => run.GeneratedSources).OrderBy(source => source.HintName, StringComparer.Ordinal)];

        return new Outcome(
            [.. result.Diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("GPCEL", StringComparison.Ordinal))],
            [.. sources.Select(source => source.HintName)],
            [.. sources.Select(source => source.SourceText.ToString().Replace("\r\n", "\n", StringComparison.Ordinal))],
            [.. output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)]);
    }

    internal static string Message(this Diagnostic diagnostic) => diagnostic.GetMessage(CultureInfo.InvariantCulture);

    internal static string Place(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    internal sealed record Outcome(ImmutableArray<Diagnostic> Reported, string[] HintNames, string[] Texts, ImmutableArray<Diagnostic> CompilerErrors);
}
