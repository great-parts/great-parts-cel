using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>
/// ARCH008: a qualified name whose first identifier is <c>Cel</c> must start with <c>global::</c> or come from a
/// <c>using</c> directive. Inside a namespace under <c>Great.Parts.Cel</c>, <c>Cel</c> binds to <c>Great.Parts.Cel</c>
/// instead of the namespace <c>Cel</c> of the generated cel-spec types. The rule covers names of types and member
/// access in expressions, unless <c>Cel</c> there is a local, a parameter or a member.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QualifiedNameAnalyzer : DiagnosticAnalyzer
{
    private const string FirstIdentifier = "Cel";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rules.CelQualifiedName];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.IdentifierName);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        IdentifierNameSyntax identifier = (IdentifierNameSyntax)context.Node;
        if (!string.Equals(identifier.Identifier.ValueText, FirstIdentifier, StringComparison.Ordinal)
            || identifier.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null)
        {
            return;
        }

        bool qualifies = identifier.Parent switch
        {
            QualifiedNameSyntax name => name.Left == identifier,
            MemberAccessExpressionSyntax access => access.IsKind(SyntaxKind.SimpleMemberAccessExpression) && access.Expression == identifier && !IsValue(context, identifier),
            _ => false,
        };
        if (!qualifies)
        {
            return;
        }

        SyntaxNode outermost = identifier;
        while (outermost.Parent is QualifiedNameSyntax { Left: var left } && left == outermost
            || outermost.Parent is MemberAccessExpressionSyntax { Expression: var expression } && expression == outermost)
        {
            outermost = outermost.Parent;
        }

        // The name of a namespace declaration declares the namespace Cel; it does not refer to it.
        if (outermost.Parent is BaseNamespaceDeclarationSyntax declaration && declaration.Name == outermost)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rules.CelQualifiedName, outermost.GetLocation(), outermost.WithoutTrivia().ToString()));
    }

    private static bool IsValue(SyntaxNodeAnalysisContext context, IdentifierNameSyntax identifier)
    {
        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol;
        return symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol or IRangeVariableSymbol;
    }
}
