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
}
