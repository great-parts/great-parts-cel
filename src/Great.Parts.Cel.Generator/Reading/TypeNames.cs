using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The names of types: the display name of the messages, the name as C# writes it in the created code, the full metadata name
// that the types of the library, of System.Text.Json and of protobuf are recognized by, and the default CEL name.
internal static class TypeNames
{
    // The name of a message: C# form, the keyword for a special type, Namespace.Outer.Name, the type arguments of a generic type
    // in the same form, int? for Nullable<int>, no global:: and no assembly.
    private static readonly SymbolDisplayFormat DisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat AnnotatedFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) { "true", "false", "null", "in" };

    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "as", "break", "const", "continue", "else", "for", "function", "if", "import", "let", "loop", "package", "namespace",
        "return", "var", "void", "while",
    };

    internal static string Display(ITypeSymbol type) =>
        (type is INamedTypeSymbol { IsUnboundGenericType: true } ? type.OriginalDefinition : type).ToDisplayString(DisplayFormat);

    // The type as C# writes it with global::. With annotated, a reference type that is annotated ? keeps the question mark.
    internal static string ClrName(ITypeSymbol type, bool annotated) =>
        type.ToDisplayString(annotated ? AnnotatedFormat : SymbolDisplayFormat.FullyQualifiedFormat);

    // Namespace.Outer+Name`1: the name that the metadata of the type has, of the definition of a generic type.
    internal static string FullName(INamedTypeSymbol type)
    {
        INamedTypeSymbol definition = type.OriginalDefinition;
        if (definition.ContainingType is { } container)
        {
            return FullName(container) + "+" + definition.MetadataName;
        }

        string ns = NamespaceName(definition.ContainingNamespace);
        return ns.Length == 0 ? definition.MetadataName : ns + "." + definition.MetadataName;
    }

    internal static string NamespaceName(INamespaceSymbol? ns)
    {
        if (ns is null || ns.IsGlobalNamespace)
        {
            return string.Empty;
        }

        string parent = NamespaceName(ns.ContainingNamespace);
        return parent.Length == 0 ? ns.Name : parent + "." + ns.Name;
    }

    internal static bool ContainsTypeParameter(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol:
                return true;
            case IArrayTypeSymbol array:
                return ContainsTypeParameter(array.ElementType);
            case IPointerTypeSymbol pointer:
                return ContainsTypeParameter(pointer.PointedAtType);
            case INamedTypeSymbol named:
                if (named.ContainingType is not null && ContainsTypeParameter(named.ContainingType))
                {
                    return true;
                }

                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    if (ContainsTypeParameter(argument))
                    {
                        return true;
                    }
                }

                return false;
            default:
                return false;
        }
    }

    // The CEL name of a type that has no Name: the full .NET name with the + of a nested type written as a dot. A closed generic
    // type gets its definition name, then _ and the default names of its type arguments joined by _, each with its dots
    // replaced by _: Acme.Pair<int, Acme.Tag> is Acme.Pair_int_Acme_Tag.
    internal static string DefaultCelName(INamedTypeSymbol type)
    {
        var builder = new StringBuilder();
        AppendDefaultName(builder, type);
        return builder.ToString();
    }

    private static void AppendDefaultName(StringBuilder builder, INamedTypeSymbol type)
    {
        if (type.ContainingType is { } container)
        {
            AppendDefaultName(builder, container);
            builder.Append('.');
        }
        else
        {
            string ns = NamespaceName(type.ContainingNamespace);
            if (ns.Length > 0)
            {
                builder.Append(ns).Append('.');
            }
        }

        builder.Append(type.Name);
        foreach (ITypeSymbol argument in type.TypeArguments)
        {
            builder.Append('_').Append(ArgumentName(argument));
        }
    }

    private static string ArgumentName(ITypeSymbol argument)
    {
        string name = argument is INamedTypeSymbol { SpecialType: SpecialType.None } named
            ? DefaultCelName(named)
            : argument.ToDisplayString(DisplayFormat);
        return name.Replace('.', '_');
    }

    // A CEL qualified name: words separated by dots, none of them a keyword, the first not a reserved word. It is what
    // CelObjectType accepts as the name of a type.
    internal static bool IsQualifiedName(string name)
    {
        string[] segments = name.Split('.');
        for (int index = 0; index < segments.Length; index++)
        {
            string segment = segments[index];
            if (!IsWord(segment) || Keywords.Contains(segment) || (index == 0 && ReservedWords.Contains(segment)))
            {
                return false;
            }
        }

        return true;
    }

    // [_a-zA-Z][_a-zA-Z0-9]*: the name of a field. A keyword and a reserved word are allowed; an expression writes them in
    // backquotes.
    internal static bool IsWord(string name)
    {
        if (name.Length == 0 || IsAsciiDigit(name[0]))
        {
            return false;
        }

        foreach (char c in name)
        {
            if (c != '_' && !IsAsciiDigit(c) && !IsAsciiLetter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
}
