using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Great.Parts.Cel.Generator;

// The roots of a context: its [CelObject] attributes. Each is classified (GPCEL001), its name checked (GPCEL005) and the names
// compared (GPCEL004), and an accepted root is registered with its name. The roots are taken in the order of the display name of
// the type and then of the CEL name, never in the order of the text, so the second of two that conflict does not depend on the
// order of the attributes.
internal sealed class RootReader
{
    private readonly string contextName;
    private readonly TypeClassifier classifier;
    private readonly TypeRegistry registry;
    private readonly DiagnosticSink sink;
    private readonly Dictionary<string, RootInfo> byName = new(StringComparer.Ordinal);
    private readonly Dictionary<ITypeSymbol, RootInfo> byType = new(SymbolEqualityComparer.Default);

    internal RootReader(string contextName, TypeClassifier classifier, TypeRegistry registry, DiagnosticSink sink)
    {
        this.contextName = contextName;
        this.classifier = classifier;
        this.registry = registry;
        this.sink = sink;
    }

    internal List<RootInfo> Read(List<AttributeData> attributes, LocationInfo fallback, CancellationToken cancellationToken)
    {
        List<RootInfo> candidates = Collect(attributes, fallback, cancellationToken);
        var accepted = new List<RootInfo>();
        foreach (RootInfo root in candidates)
        {
            if (Admit(root))
            {
                accepted.Add(root);
            }
        }

        return accepted;
    }

    private static List<RootInfo> Collect(List<AttributeData> attributes, LocationInfo fallback, CancellationToken cancellationToken)
    {
        var roots = new List<RootInfo>();
        foreach (AttributeData attribute in attributes)
        {
            RootInfo? root = ReadAttribute(attribute, fallback, cancellationToken);
            if (root is not null)
            {
                Merge(roots, root);
            }
        }

        roots.Sort(Compare);
        return roots;
    }

    // Two attributes for one type with one name are one root.
    private static void Merge(List<RootInfo> roots, RootInfo root)
    {
        for (int index = 0; index < roots.Count; index++)
        {
            RootInfo existing = roots[index];
            if (SymbolEqualityComparer.Default.Equals(existing.Type, root.Type) && existing.EffectiveName == root.EffectiveName)
            {
                string[] ignored = [.. existing.IgnoreMembers, .. root.IgnoreMembers];
                roots[index] = existing with { IgnoreMembers = ignored };
                return;
            }
        }

        roots.Add(root);
    }

    private static int Compare(RootInfo left, RootInfo right)
    {
        int result = string.CompareOrdinal(left.Display, right.Display);
        if (result == 0)
        {
            result = string.CompareOrdinal(left.EffectiveName, right.EffectiveName);
        }

        return result != 0 ? result : left.TypePlace.Span.Start.CompareTo(right.TypePlace.Span.Start);
    }

    private static RootInfo? ReadAttribute(AttributeData attribute, LocationInfo fallback, CancellationToken cancellationToken)
    {
        if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not ITypeSymbol type || type.TypeKind == TypeKind.Error)
        {
            return null;
        }

        string? name = null;
        var ignored = new List<string>();
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (argument.Key == "Name")
            {
                name = argument.Value.Value as string;
            }
            else if (argument.Key == "IgnoreMembers" && argument.Value.Kind == TypedConstantKind.Array)
            {
                foreach (TypedConstant member in argument.Value.Values)
                {
                    if (member.Value is string text)
                    {
                        ignored.Add(text);
                    }
                }
            }
        }

        string display = TypeNames.Display(type);
        string effective = name ?? (type is INamedTypeSymbol named ? TypeNames.DefaultCelName(named) : display);
        (LocationInfo typePlace, LocationInfo namePlace) = PlacesOf(attribute, fallback, cancellationToken);
        return new RootInfo(type, display, name, effective, ignored.ToArray(), typePlace, namePlace);
    }

    // The place of an argument is the whole attribute argument as written: typeof(T) for the first, Name = "x" for the named one.
    private static (LocationInfo Type, LocationInfo Name) PlacesOf(AttributeData attribute, LocationInfo fallback, CancellationToken cancellationToken)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is not AttributeSyntax syntax)
        {
            return (fallback, fallback);
        }

        LocationInfo whole = LocationInfo.From(syntax.GetLocation());
        LocationInfo? typePlace = null;
        LocationInfo? namePlace = null;
        if (syntax.ArgumentList is { } list)
        {
            foreach (AttributeArgumentSyntax argument in list.Arguments)
            {
                LocationInfo place = LocationInfo.From(argument.GetLocation());
                if (argument.NameEquals is null)
                {
                    typePlace ??= place;
                }
                else if (argument.NameEquals.Name.Identifier.ValueText == "Name")
                {
                    namePlace = place;
                }
            }
        }

        return (typePlace ?? whole, namePlace ?? typePlace ?? whole);
    }

    private bool Admit(RootInfo root)
    {
        Classification result = classifier.Classify(root.Type, true);
        if (result.Kind is not (ClassificationKind.RootObject or ClassificationKind.RootEnum))
        {
            string reason = result.Kind == ClassificationKind.Refused ? result.Text : Reasons.OwnType(root.Display);
            sink.Report(DiagnosticIds.UnsupportedType, root.TypePlace, root.Display, reason);
            return false;
        }

        if (!IsNameValid(root) || HasConflict(root))
        {
            return false;
        }

        return Register(root, result.Symbol!, result.Kind == ClassificationKind.RootEnum);
    }

    private bool IsNameValid(RootInfo root)
    {
        if (TypeNames.IsQualifiedName(root.EffectiveName))
        {
            return true;
        }

        LocationInfo place = root.ExplicitName is null ? root.TypePlace : root.NamePlace;
        sink.Report(DiagnosticIds.InvalidName, place, root.EffectiveName, "qualified type name");
        return false;
    }

    // A type listed again with another name, or two types with one name: the second one is reported and left out.
    private bool HasConflict(RootInfo root)
    {
        if (byType.TryGetValue(root.Type, out RootInfo? sameType))
        {
            sink.Report(DiagnosticIds.DuplicateType, root.TypePlace, root.Display, root.Display, contextName, sameType.EffectiveName);
            return true;
        }

        if (byName.TryGetValue(root.EffectiveName, out RootInfo? owner))
        {
            sink.Report(DiagnosticIds.DuplicateType, root.TypePlace, owner.Display, root.Display, contextName, root.EffectiveName);
            return true;
        }

        return false;
    }

    private bool Register(RootInfo root, INamedTypeSymbol symbol, bool isEnum)
    {
        if (isEnum)
        {
            registry.AddRootEnum(symbol, root);
        }
        else if (registry.AddRootObject(symbol, root) is null)
        {
            return false;
        }

        byName[root.EffectiveName] = root;
        byType[root.Type] = root;
        return true;
    }
}
