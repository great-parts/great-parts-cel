using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>
/// ARCH002-ARCH004: the public types of a package against the API concept list <c>docs/api-concepts.md</c>. The rules run
/// only in a compilation that has the list as an additional file. A row is checked by the compilation that declares types
/// in the namespace of the row.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ApiConceptListAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rules.TypeWithoutRow, Rules.RowWithoutType, Rules.InvalidRow];

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
        AdditionalText? file = AnalyzerInput.Find(context.Options.AdditionalFiles, AnalyzerInput.ConceptListPath);
        SourceText? text = file?.GetText(context.CancellationToken);
        if (file is null || text is null)
        {
            return;
        }

        ConceptList list = ConceptList.Parse(text);
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (ConceptList.Row row in list.Rows)
        {
            if (row.Key is not null)
            {
                _ = keys.Add(row.Key);
            }
        }

        context.RegisterSymbolAction(symbolContext => AnalyzeType(symbolContext, keys), SymbolKind.NamedType);
        context.RegisterAdditionalFileAction(fileContext => AnalyzeRows(fileContext, file, list));
    }

    private static void AnalyzeType(SymbolAnalysisContext context, HashSet<string> keys)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        if (AnalyzerInput.IsPublicApi(type) && !keys.Contains(ConceptList.Row.KeyOf(type)) && type.Locations.Length > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rules.TypeWithoutRow, type.Locations[0], type.ToDisplayString()));
        }
    }

    private static void AnalyzeRows(AdditionalFileAnalysisContext context, AdditionalText file, ConceptList list)
    {
        if (!ReferenceEquals(context.AdditionalFile, file))
        {
            return;
        }

        HashSet<string> namespaces = new(StringComparer.Ordinal);
        HashSet<string> publicTypes = new(StringComparer.Ordinal);
        Collect(context.Compilation.Assembly.GlobalNamespace, namespaces, publicTypes);

        foreach (ConceptList.Row row in list.Rows)
        {
            // A row whose namespace cannot be read belongs to no assembly, so every compilation with the list reports it.
            bool own = row.Namespace is not { Length: > 0 } || namespaces.Contains(row.Namespace);
            if (!own)
            {
                continue;
            }

            Location location = AnalyzerInput.LineLocation(file, row.Line);
            string? problem = row.Problem();
            if (problem is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.InvalidRow, location, row.FullName, problem));
            }

            if (row.Key is not null && !publicTypes.Contains(row.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rules.RowWithoutType, location, row.FullName));
            }
        }
    }

    // Namespaces where the assembly declares types, and the keys of its public types, nested types included.
    private static void Collect(INamespaceSymbol ns, HashSet<string> namespaces, HashSet<string> publicTypes)
    {
        foreach (INamespaceOrTypeSymbol member in ns.GetMembers())
        {
            if (member is INamespaceSymbol nested)
            {
                Collect(nested, namespaces, publicTypes);
            }
            else if (member is INamedTypeSymbol type && type.Locations.Length > 0 && type.Locations[0].IsInSource)
            {
                _ = namespaces.Add(ns.ToDisplayString());
                CollectTypes(type, publicTypes);
            }
        }
    }

    private static void CollectTypes(INamedTypeSymbol type, HashSet<string> publicTypes)
    {
        if (AnalyzerInput.IsPublicApi(type))
        {
            _ = publicTypes.Add(ConceptList.Row.KeyOf(type));
        }

        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            CollectTypes(nested, publicTypes);
        }
    }
}
