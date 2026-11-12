using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The members of an object type that can be fields: the public instance properties with a public get and the public instance
// fields, including the inherited ones, except indexers. When a derived type hides a member (new) or overrides it, the member of
// the most derived type is the only one. The members of an interface are those it declares and those of its base interfaces.
// Members that an interface implements explicitly are private, so they are not here.
internal static class MemberCatalog
{
    internal static List<ISymbol> Of(INamedTypeSymbol type)
    {
        var result = new List<ISymbol>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (type.TypeKind == TypeKind.Interface)
        {
            Collect(type, result, names);
            foreach (INamedTypeSymbol inherited in type.AllInterfaces)
            {
                Collect(inherited, result, names);
            }

            return result;
        }

        for (INamedTypeSymbol? current = type; current is not null && !IsRoot(current); current = current.BaseType)
        {
            Collect(current, result, names);
        }

        return result;
    }

    // The type of a member, and null for a member that is not one: a member whose type is a ref struct (Span<T>) or that returns
    // by reference.
    internal static ITypeSymbol? TypeOf(ISymbol member) => member switch
    {
        IPropertySymbol { RefKind: RefKind.None, Type.IsRefLikeType: false } property => property.Type,
        IFieldSymbol { Type.IsRefLikeType: false } field => field.Type,
        _ => null,
    };

    private static bool IsRoot(INamedTypeSymbol type) =>
        type.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType;

    private static void Collect(INamedTypeSymbol type, List<ISymbol> result, HashSet<string> names)
    {
        foreach (ISymbol member in type.GetMembers())
        {
            if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            bool readable = member is IFieldSymbol
                || member is IPropertySymbol { IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public };
            if (readable && names.Add(member.Name))
            {
                result.Add(member);
            }
        }
    }
}
