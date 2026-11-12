using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Great.Parts.Cel.Values;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples;

// What the examples of the generator share: a compilation in memory, one run of the generator and the reading of its
// result. The reference assemblies of the compilation are the implementation assemblies of the running process
// (TRUSTED_PLATFORM_ASSEMBLIES), so no package of reference assemblies is needed; they include Great.Parts.Cel and
// System.Text.Json. The sources that stand in for what the process does not hold — protobuf and Decimal128 — are
// declarations below, never compiled into an assembly that is run.
internal static class GeneratorExample
{
    internal const string Preamble = "using System;\nusing System.Collections.Generic;\nusing Great.Parts.Cel.Values;\nnamespace Acme;\n";

    // The shape of the classes of protoc that the generator recognizes by full name: IMessage and a static property
    // Descriptor of type MessageDescriptor. WellKnownTypes.Timestamp stands for the well-known types.
    internal const string ProtobufStub = """
        namespace Google.Protobuf
        {
            public interface IMessage { }
            public interface IMessage<T> : IMessage { }
        }
        namespace Google.Protobuf.Reflection
        {
            public sealed class MessageDescriptor { public string FullName => "acme.pb.Msg"; }
        }
        namespace Google.Protobuf.WellKnownTypes
        {
            public sealed class Timestamp : Google.Protobuf.IMessage<Timestamp>
            {
                public static Google.Protobuf.Reflection.MessageDescriptor Descriptor => null!;
                public long Seconds { get; set; }
            }
        }
        namespace Acme.Pb
        {
            public sealed class Msg : Google.Protobuf.IMessage<Msg>
            {
                public static Google.Protobuf.Reflection.MessageDescriptor Descriptor => new();
                public int Weight { get; set; }
            }
        }
        """;

    // A declaration of System.Numerics.Decimal128 as .NET 11 has it: the project runs on net10.0, so a compilation that
    // must "have Decimal128" gets this stub, and what it creates is not compiled.
    internal const string Decimal128Stub = """
        namespace System.Numerics
        {
            public readonly struct Decimal128 { }
        }
        """;

    private static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    internal static CSharpCompilation Compile(
        string source,
        LanguageVersion version = LanguageVersion.CSharp12,
        NullableContextOptions nullable = NullableContextOptions.Enable,
        IEnumerable<string>? more = null,
        IEnumerable<MetadataReference>? references = null)
    {
        var options = new CSharpParseOptions(version);
        var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(source, options, path: "Source.cs") };
        int index = 0;
        foreach (string text in more ?? [])
        {
            trees.Add(CSharpSyntaxTree.ParseText(text, options, path: "More" + index++ + ".cs"));
        }

        return CSharpCompilation.Create(
            "Application",
            trees,
            [.. References, .. references ?? []],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: nullable, allowUnsafe: true));
    }

    // Another assembly of the application: a library that the main compilation references.
    internal static MetadataReference Library(string source)
    {
        CSharpCompilation library = CSharpCompilation.Create(
            "Foreign",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.CSharp12))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        Assert.Empty(library.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        return library.ToMetadataReference();
    }

    internal static GeneratorDriver Driver(bool track = false, LanguageVersion version = LanguageVersion.CSharp12) =>
        CSharpGeneratorDriver.Create(
            [new CelTypeContextGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: track),
            parseOptions: new CSharpParseOptions(version));

    // The driver parses what the generator creates with the language version of the compilation: the created trees are added to
    // that compilation, and a tree of another version is "Inconsistent language versions".
    internal static GeneratorDriverRunResult Run(Compilation compilation) =>
        Driver(version: VersionOf(compilation)).RunGenerators(compilation).GetRunResult();

    private static LanguageVersion VersionOf(Compilation compilation) =>
        compilation.SyntaxTrees
            .Select(tree => tree.Options)
            .OfType<CSharpParseOptions>()
            .Select(options => options.SpecifiedLanguageVersion)
            .DefaultIfEmpty(LanguageVersion.CSharp12)
            .First();

    // The diagnostics of the generator only: the compiler's own are not the subject of these examples.
    internal static ImmutableArray<Diagnostic> Reported(GeneratorDriverRunResult result) =>
        [.. result.Diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("GPCEL", StringComparison.Ordinal))];

    internal static Diagnostic Single(string source, string id, LanguageVersion version = LanguageVersion.CSharp12, IEnumerable<string>? more = null, IEnumerable<MetadataReference>? references = null)
    {
        GeneratorDriverRunResult result = Run(Compile(source, version, more: more, references: references));
        return Assert.Single(Reported(result), diagnostic => diagnostic.Id == id);
    }

    // The text a location covers: the place of a diagnostic as the source shows it. The place is the argument of the
    // attribute (`typeof(T)` or `Name = "x"`), the identifier of a member, or the identifier of the context class.
    internal static string Place(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    internal static string CreatedText(GeneratorDriverRunResult result) =>
        Assert.Single(result.GeneratedTrees).GetText().ToString().Replace("\r\n", "\n", StringComparison.Ordinal);

    // The created code compiled with the sources, and the assembly loaded to read through the descriptions.
    internal sealed class Loaded : IDisposable
    {
        private readonly AssemblyLoadContext context = new("generated", isCollectible: true);

        internal Loaded(string source, IEnumerable<string>? more = null, LanguageVersion version = LanguageVersion.CSharp12)
        {
            CSharpCompilation compilation = Compile(source, version, more: more);
            GeneratorDriverRunResult result = Run(compilation);
            Assert.Empty(Reported(result).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
            CSharpCompilation withCreated = compilation.AddSyntaxTrees(result.GeneratedTrees);
            using var stream = new MemoryStream();
            var emitted = withCreated.Emit(stream);
            Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
            stream.Position = 0;
            Assembly = context.LoadFromStream(stream);
        }

        internal Assembly Assembly { get; }

        internal CelTypeProvider Provider(string contextName) =>
            (CelTypeProvider)Assembly.GetType(contextName, throwOnError: true)!.GetProperty("Default")!.GetValue(null)!;

        internal object New(string typeName) => Activator.CreateInstance(Assembly.GetType(typeName, throwOnError: true)!)!;

        public void Dispose() => context.Unload();
    }

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        string[] paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        string own = typeof(CelObjectType).Assembly.Location;
        return [.. paths.Concat([own]).Distinct(StringComparer.OrdinalIgnoreCase).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
    }
}
