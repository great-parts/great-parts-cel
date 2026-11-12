using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The attributes are found by the full name of their class, so the generator references no package: not the library, not
// System.Text.Json.
internal static class Attributes
{
    // An attribute that marks a type or a member as one that created code must not call or reference (the first one found,
    // in this order), with the name that a reason shows.
    private static readonly string[][] Marks =
    [
        ["System.ObsoleteAttribute", "Obsolete"],
        ["System.Diagnostics.CodeAnalysis.ExperimentalAttribute", "Experimental"],
        ["System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute", "RequiresUnreferencedCode"],
        ["System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute", "RequiresDynamicCode"],
    ];

    internal static AttributeData? Find(ISymbol? symbol, string fullName)
    {
        if (symbol is null)
        {
            return null;
        }

        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass is { } attributeClass && TypeNames.FullName(attributeClass) == fullName)
            {
                return attribute;
            }
        }

        return null;
    }

    internal static bool Has(ISymbol? symbol, string fullName) => Find(symbol, fullName) is not null;

    // The name of the first mark that the symbol carries; RequiresUnreferencedCode and RequiresDynamicCode have no target
    // property, so a property is marked when its get accessor is.
    internal static string? Mark(ISymbol symbol)
    {
        IMethodSymbol? getter = (symbol as IPropertySymbol)?.GetMethod;
        foreach (string[] mark in Marks)
        {
            if (Has(symbol, mark[0]) || Has(getter, mark[0]))
            {
                return mark[1];
            }
        }

        return null;
    }
}
