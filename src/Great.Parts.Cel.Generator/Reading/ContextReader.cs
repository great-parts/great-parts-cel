using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Great.Parts.Cel.Generator;

// The transform of the pipeline: everything about one context is read here, into values. The result holds no symbol and no
// syntax node, so the steps after it compare it by value. The order of the reading, so that every diagnostic that applies is
// reported: the declaration (GPCEL006), the language version (GPCEL008), the options, the roots (GPCEL001, GPCEL004, GPCEL005),
// the closure over the members (GPCEL002, GPCEL003, GPCEL005, GPCEL007, GPCEL009) and the check of the names of the whole (GPCEL004).
internal static class ContextReader
{
    internal static ContextReadResult Read(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var declaration = (ClassDeclarationSyntax)context.TargetNode;
        var sink = new DiagnosticSink();
        if (!ContextDeclaration.IsPrimary(symbol, declaration, cancellationToken))
        {
            return new ContextReadResult(null, default);
        }

        LocationInfo place = LocationInfo.From(declaration.Identifier.GetLocation());
        if (!ContextDeclaration.IsValid(symbol, declaration))
        {
            sink.Report(DiagnosticIds.InvalidContext, place, symbol.Name);
            return new ContextReadResult(null, sink.ToArray());
        }

        CompilationFacts facts = CompilationFacts.From(context.SemanticModel.Compilation, declaration.SyntaxTree);
        bool supported = facts.Language >= CompilationFacts.MinimumLanguage;
        if (!supported)
        {
            sink.Report(DiagnosticIds.LanguageVersion, place, symbol.Name, facts.LanguageText);
        }

        TypeRegistry registry = Walk(symbol, facts, place, sink, cancellationToken);
        if (registry.LimitExceeded)
        {
            sink.Report(DiagnosticIds.ContextTooLarge, place, symbol.Name, TypeRegistry.Limit.ToString(CultureInfo.InvariantCulture));
        }

        ContextModel? model = supported && !registry.LimitExceeded ? ModelBuilder.Build(symbol, registry, sink) : null;
        return new ContextReadResult(model, sink.ToArray());
    }

    // The closure: the roots, then each root's walk over the members of the types it reaches, with an explicit stack so that a
    // deep graph cannot overflow the stack of the compiler. A type that two walks reach belongs to the first by the order of the roots.
    private static TypeRegistry Walk(INamedTypeSymbol symbol, CompilationFacts facts, LocationInfo place, DiagnosticSink sink, CancellationToken cancellationToken)
    {
        var registry = new TypeRegistry();
        var classifier = new TypeClassifier(facts, registry);
        var roots = new RootReader(symbol.Name, classifier, registry, sink);
        List<RootInfo> accepted = roots.Read(RootAttributes(symbol), place, cancellationToken);
        var members = new MemberReader(classifier, ContextOptions.Read(symbol), sink, symbol.ContainingAssembly);
        foreach (RootInfo root in accepted)
        {
            registry.ReachedBy = root.TypePlace;
            ObjectEntry? entry = registry.Objects.Find(candidate => ReferenceEquals(candidate.Root, root));
            if (entry is not null)
            {
                registry.Queue(entry);
            }

            while (registry.Pending.Count > 0 && !registry.LimitExceeded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                members.Read(registry.Pending.Pop());
            }

            if (registry.LimitExceeded)
            {
                break;
            }
        }

        return registry;
    }

    // The [CelObject] attributes of every part of the class.
    private static List<AttributeData> RootAttributes(INamedTypeSymbol symbol)
    {
        var attributes = new List<AttributeData>();
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass && TypeNames.FullName(attributeClass) == WellKnown.CelObjectAttribute)
            {
                attributes.Add(attribute);
            }
        }

        return attributes;
    }
}
