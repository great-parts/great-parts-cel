using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Generator;

// The two facts of the compilation that the reading depends on: whether the type System.Numerics.Decimal128 exists (it does
// in net11.0) and the effective language version, with default and latest resolved.
internal sealed record CompilationFacts(bool HasDecimal128, int Language)
{
    // The numeric value of LanguageVersion.CSharp12, the first version that the created code compiles with.
    internal const int MinimumLanguage = 1200;

    internal string LanguageText
    {
        get
        {
            string text = LanguageVersionFacts.ToDisplayString((LanguageVersion)Language);
            return text.EndsWith(".0", System.StringComparison.Ordinal) ? text.Substring(0, text.Length - 2) : text;
        }
    }

    internal static CompilationFacts From(Compilation compilation, SyntaxTree tree)
    {
        LanguageVersion version = tree.Options is CSharpParseOptions options
            ? options.LanguageVersion.MapSpecifiedToEffectiveVersion()
            : LanguageVersion.Default.MapSpecifiedToEffectiveVersion();
        INamedTypeSymbol? decimal128 = compilation.GetTypeByMetadataName(WellKnown.Decimal128);
        return new CompilationFacts(decimal128 is { DeclaredAccessibility: Accessibility.Public }, (int)version);
    }
}
