using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>
/// ARCH006 and ARCH007: names and attributes of the public API of a package. The rules run only in a compilation that
/// has the API concept list <c>docs/api-concepts.md</c> as an additional file, so tests, the source generator and
/// <c>eng/</c> are not checked.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PublicNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> ReservedNames = ImmutableHashSet.Create(StringComparer.Ordinal, "Great", "Parts", "Cel", "Policy", "Filtering", "Protobuf");

    // The attributes of trimming and Native AOT analysis and the names that messages use for them.
    private static readonly (string Attribute, string Name)[] RequiresAttributes =
    [
        ("System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute", "RequiresUnreferencedCode"),
        ("System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute", "RequiresDynamicCode"),
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rules.ReservedName, Rules.RequiresCode];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (AnalyzerInput.Find(context.Options.AdditionalFiles, AnalyzerInput.ConceptListPath) is null)
        {
            return;
        }

        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeAttributes, SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field, SymbolKind.Event);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        ISymbol type = context.Symbol;
        if (ReservedNames.Contains(type.Name) && AnalyzerInput.IsPublicApi(type) && type.Locations.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.ReservedName, type.Locations[0], type.Name));
        }
    }

    private static void AnalyzeAttributes(SymbolAnalysisContext context)
    {
        ISymbol symbol = context.Symbol;
        if (!AnalyzerInput.IsPublicApi(symbol) || symbol.Locations.Length == 0)
        {
            return;
        }

        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            string? attributeName = attribute.AttributeClass?.ToDisplayString();
            foreach ((string fullName, string shortName) in RequiresAttributes)
            {
                if (string.Equals(attributeName, fullName, StringComparison.Ordinal))
                {
                    string name = symbol is IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } ? symbol.ContainingType.Name : symbol.Name;
                    context.ReportDiagnostic(Diagnostic.Create(Rules.RequiresCode, symbol.Locations[0], name, shortName));
                }
            }
        }
    }
}
