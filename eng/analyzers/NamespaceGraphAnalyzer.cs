using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>
/// ARCH001 and ARCH005: the graph of references between the namespaces of one assembly. A name in source code whose
/// symbol is a type of the same assembly, or a member of such a type, is an edge from the innermost namespace
/// declaration around the name to the namespace of the type. A name in a documentation comment, such as a <c>cref</c>
/// attribute, is not a reference of the code and adds no edge. ARCH005 checks each edge against <c>eng/layers.txt</c>;
/// ARCH001 reports the edges that lie on a cycle at the end of the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamespaceGraphAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rules.NamespaceCycle, Rules.LayerViolation];

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
        LayerTable? table = LayerTable.Read(context.Options.AdditionalFiles, context.Compilation.AssemblyName, context.CancellationToken);
        Graph graph = new(context.Compilation.Assembly, table is { HasSection: true } ? table : null);
        context.RegisterSyntaxNodeAction(graph.AnalyzeName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        if (table is not null)
        {
            context.RegisterAdditionalFileAction(fileContext => ReportMalformedLines(fileContext, table));
            if (table.HasSection)
            {
                context.RegisterSyntaxNodeAction(graph.AnalyzeNamespace, SyntaxKind.NamespaceDeclaration, SyntaxKind.FileScopedNamespaceDeclaration);
            }
        }

        context.RegisterCompilationEndAction(graph.ReportCycles);
    }

    private static void ReportMalformedLines(AdditionalFileAnalysisContext context, LayerTable table)
    {
        if (!ReferenceEquals(context.AdditionalFile, table.File))
        {
            return;
        }

        foreach ((TextLine line, string reason) in table.MalformedLines)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rules.LayerViolation,
                AnalyzerInput.LineLocation(table.File, line),
                $"line of the layer table {AnalyzerInput.LayerTablePath} does not parse: {reason}"));
        }
    }

    /// <summary>The edges of one compilation; actions add to it concurrently.</summary>
    private sealed class Graph(IAssemblySymbol assembly, LayerTable? layers)
    {
        // The first reference of each edge in the order of file path and position, so the result does not depend on the
        // order in which the actions run.
        private readonly ConcurrentDictionary<(string From, string To), Location> _edges = new();

        internal void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
        {
            BaseNamespaceDeclarationSyntax declaration = (BaseNamespaceDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamespaceSymbol symbol)
            {
                return;
            }

            string name = NameOf(symbol);
            if (layers is not null && !layers.HasRow(name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.LayerViolation,
                    declaration.Name.GetLocation(),
                    $"namespace {name} has no line in the layer table {AnalyzerInput.LayerTablePath}"));
            }
        }

        internal void AnalyzeName(SyntaxNodeAnalysisContext context)
        {
            SimpleNameSyntax name = (SimpleNameSyntax)context.Node;
            if (name is IdentifierNameSyntax { IsVar: true }
                || name.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null
                || name.FirstAncestorOrSelf<DocumentationCommentTriviaSyntax>() is not null)
            {
                return;
            }

            SymbolInfo info = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
            ISymbol? symbol = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
            INamedTypeSymbol? type = symbol as INamedTypeSymbol ?? (symbol is IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol ? symbol.ContainingType : null);
            if (type is null || !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, assembly))
            {
                return;
            }

            type = Outermost(type);

            // In 'Planner.Depth' and 'Outer.Inner' the type on the left is the reference; the name on the right adds no edge.
            ExpressionSyntax? left = name.Parent switch
            {
                QualifiedNameSyntax qualified when qualified.Right == name => qualified.Left,
                MemberAccessExpressionSyntax access when access.Name == name => access.Expression,
                _ => null,
            };
            if (left is not null
                && context.SemanticModel.GetSymbolInfo(left, context.CancellationToken).Symbol is INamedTypeSymbol leftType
                && SymbolEqualityComparer.Default.Equals(Outermost(leftType).OriginalDefinition, type.OriginalDefinition))
            {
                return;
            }

            string target = NameOf(type.ContainingNamespace);
            string source = SourceNamespace(name, context);
            if (string.Equals(source, target, StringComparison.Ordinal))
            {
                return;
            }

            Location location = name.GetLocation();
            _ = _edges.AddOrUpdate((source, target), location, (_, existing) => Precedes(location, existing) ? location : existing);

            if (layers is not null && layers.HasRow(source) && !layers.Allows(source, target))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.LayerViolation,
                    location,
                    $"{source} must not reference {target} (layer table {AnalyzerInput.LayerTablePath})"));
            }
        }

        internal void ReportCycles(CompilationAnalysisContext context)
        {
            Dictionary<string, List<string>> successors = new(StringComparer.Ordinal);
            foreach ((string from, string to) in _edges.Keys)
            {
                if (!successors.TryGetValue(from, out List<string>? list))
                {
                    successors.Add(from, list = []);
                }

                list.Add(to);
            }

            foreach (List<string> list in successors.Values)
            {
                list.Sort(StringComparer.Ordinal);
            }

            Dictionary<string, int> component = StronglyConnectedComponents.Find(successors);
            foreach (KeyValuePair<(string From, string To), Location> edge in _edges.OrderBy(pair => pair.Key.From, StringComparer.Ordinal).ThenBy(pair => pair.Key.To, StringComparer.Ordinal))
            {
                (string from, string to) = edge.Key;
                if (component[from] != component[to])
                {
                    continue;
                }

                // The shortest path back from the target closes the shortest cycle through this edge.
                List<string> back = ShortestPath(successors, component, to, from);
                List<string> cycle = [from, .. back];
                Location next = _edges[(back[0], back[1])];
                context.ReportDiagnostic(Diagnostic.Create(
                    Rules.NamespaceCycle,
                    edge.Value,
                    string.Join(" -> ", cycle),
                    AnalyzerInput.Format(next)));
            }
        }

        private static List<string> ShortestPath(Dictionary<string, List<string>> successors, Dictionary<string, int> component, string start, string end)
        {
            Dictionary<string, string> previous = new(StringComparer.Ordinal) { [start] = start };
            Queue<string> queue = new();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                if (string.Equals(current, end, StringComparison.Ordinal))
                {
                    break;
                }

                if (!successors.TryGetValue(current, out List<string>? next))
                {
                    continue;
                }

                foreach (string candidate in next)
                {
                    if (component[candidate] == component[start] && !previous.ContainsKey(candidate))
                    {
                        previous.Add(candidate, current);
                        queue.Enqueue(candidate);
                    }
                }
            }

            List<string> path = [end];
            for (string node = end; !string.Equals(node, start, StringComparison.Ordinal); node = previous[node])
            {
                path.Add(previous[node]);
            }

            path.Reverse();
            return path;
        }

        private static bool Precedes(Location candidate, Location existing)
        {
            int byPath = string.CompareOrdinal(candidate.SourceTree?.FilePath, existing.SourceTree?.FilePath);
            return byPath < 0 || (byPath == 0 && candidate.SourceSpan.Start < existing.SourceSpan.Start);
        }

        private static string SourceNamespace(SyntaxNode node, SyntaxNodeAnalysisContext context)
        {
            BaseNamespaceDeclarationSyntax? declaration = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
            return declaration is not null && context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is INamespaceSymbol symbol
                ? NameOf(symbol)
                : NameOf(context.Compilation.GlobalNamespace);
        }

        private static INamedTypeSymbol Outermost(INamedTypeSymbol type)
        {
            while (type.ContainingType is not null)
            {
                type = type.ContainingType;
            }

            return type;
        }

        private static string NameOf(INamespaceSymbol ns) => ns.ToDisplayString();
    }
}
