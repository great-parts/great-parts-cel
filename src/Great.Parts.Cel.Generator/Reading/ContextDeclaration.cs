using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Great.Parts.Cel.Generator;

// The declaration of a type context: whether the class can be completed by a created file, and where it is.
internal static class ContextDeclaration
{
    // A context is a non-generic partial class that derives from CelTypeContext and is not nested in a generic type. A containing
    // type that is not partial is an error of the compiler at the application, so it is not checked here.
    internal static bool IsValid(INamedTypeSymbol symbol, ClassDeclarationSyntax declaration)
    {
        if (!IsPartial(declaration) || symbol.IsGenericType)
        {
            return false;
        }

        for (INamedTypeSymbol? outer = symbol.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            if (outer.IsGenericType)
            {
                return false;
            }
        }

        return DerivesFromContext(symbol);
    }

    // A context whose parts carry [CelObject] in more than one place is read once: by the part that comes first by the path of
    // its file and its position, whatever the order the compiler lists the parts in. Another part gives nothing, so the created
    // file is added once.
    internal static bool IsPrimary(INamedTypeSymbol symbol, ClassDeclarationSyntax declaration, CancellationToken cancellationToken)
    {
        if (symbol.DeclaringSyntaxReferences.Length < 2)
        {
            return true;
        }

        ClassDeclarationSyntax? primary = null;
        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is ClassDeclarationSyntax part
                && HasRootAttribute(part)
                && (primary is null || IsBefore(part, primary)))
            {
                primary = part;
            }
        }

        return primary is null || (primary.SyntaxTree == declaration.SyntaxTree && primary.SpanStart == declaration.SpanStart);
    }

    // The containing types of the context, outermost first.
    internal static EquatableArray<ContainingTypeModel> ContainingTypes(INamedTypeSymbol symbol)
    {
        var containers = new List<ContainingTypeModel>();
        for (INamedTypeSymbol? outer = symbol.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            containers.Insert(0, new ContainingTypeModel(outer.Name, KeywordOf(outer)));
        }

        return EquatableArray.From(containers);
    }

    // The attribute is told by the last identifier of its name, with or without the suffix Attribute.
    private static bool HasRootAttribute(ClassDeclarationSyntax declaration)
    {
        foreach (AttributeListSyntax list in declaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in list.Attributes)
            {
                string name = attribute.Name is QualifiedNameSyntax qualified ? qualified.Right.Identifier.ValueText : attribute.Name.ToString();
                if (name is "CelObject" or "CelObjectAttribute")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsBefore(ClassDeclarationSyntax left, ClassDeclarationSyntax right)
    {
        int result = string.CompareOrdinal(left.SyntaxTree.FilePath, right.SyntaxTree.FilePath);
        return result != 0 ? result < 0 : left.SpanStart < right.SpanStart;
    }

    private static bool IsPartial(ClassDeclarationSyntax declaration)
    {
        foreach (SyntaxToken modifier in declaration.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DerivesFromContext(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol.BaseType; current is not null; current = current.BaseType)
        {
            if (TypeNames.FullName(current) == WellKnown.CelTypeContext)
            {
                return true;
            }
        }

        return false;
    }

    private static string KeywordOf(INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.IsValueType ? "record struct" : "record";
        }

        return type.TypeKind switch
        {
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => "class",
        };
    }
}
