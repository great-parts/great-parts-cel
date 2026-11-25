using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Generator;

// The fact of the compilation that the reading depends on: the effective language version, with default and latest resolved.
internal sealed record CompilationFacts(int Language)
{
    // The numeric value of LanguageVersion.CSharp12, the first version that the created code compiles with.
    internal const int MinimumLanguage = 1200;

    internal string LanguageText => TextOf(Language);

    // The language version of the project as C# writes it: 11, 7.3.
    internal static string TextOf(int language)
    {
        string text = LanguageVersionFacts.ToDisplayString((LanguageVersion)language);
        return text.EndsWith(".0", System.StringComparison.Ordinal) ? text.Substring(0, text.Length - 2) : text;
    }

    // The effective language version of the syntax tree, with default and latest resolved.
    internal static int LanguageOf(SyntaxTree tree)
    {
        LanguageVersion version = tree.Options is CSharpParseOptions options
            ? options.LanguageVersion.MapSpecifiedToEffectiveVersion()
            : LanguageVersion.Default.MapSpecifiedToEffectiveVersion();
        return (int)version;
    }

    internal static CompilationFacts From(SyntaxTree tree) => new(LanguageOf(tree));

    // Whether the assembly of the library gives the compilation access to its internal members (InternalsVisibleTo). The members that
    // a description overrides are protected internal: an override in an assembly that has the access is protected internal too, and in
    // any other assembly it is protected.
    internal static bool SeesLibraryInternals(Compilation compilation)
    {
        INamedTypeSymbol? type = compilation.GetTypeByMetadataName(WellKnown.CelObjectType);
        return type is not null && type.ContainingAssembly.GivesAccessTo(compilation.Assembly);
    }
}
