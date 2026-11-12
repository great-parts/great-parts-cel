using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The codes of the nine diagnostics. A code is never removed or renumbered.
internal static class DiagnosticIds
{
    internal const string UnsupportedType = "GPCEL001";
    internal const string ExcludedMember = "GPCEL002";
    internal const string DuplicateField = "GPCEL003";
    internal const string DuplicateType = "GPCEL004";
    internal const string InvalidName = "GPCEL005";
    internal const string InvalidContext = "GPCEL006";
    internal const string DecimalMember = "GPCEL007";
    internal const string LanguageVersion = "GPCEL008";
    internal const string ContextTooLarge = "GPCEL009";
}

// The descriptors of the diagnostics: the identifier, the severity, the title and the message are those of the page
// docs/generator-diagnostics.md. HelpPage is the one constant of the address of that page; the help link of a code is the
// address with the code in lower case as the anchor, which the heading of the code on the page gives.
internal static class Descriptors
{
    internal const string Category = "Great.Parts.Cel.Generator";

    internal const string HelpPage = "https://github.com/great-parts/great-parts-cel/blob/main/docs/generator-diagnostics.md";

    internal static readonly DiagnosticDescriptor UnsupportedType = new(
        DiagnosticIds.UnsupportedType,
        "Type cannot be used by CEL expressions",
        "Type '{0}' cannot be used in a CEL type context: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel001");

    internal static readonly DiagnosticDescriptor ExcludedMember = new(
        DiagnosticIds.ExcludedMember,
        "Member excluded from CEL type",
        "Member '{0}' of type '{1}' is excluded from the CEL type: {2}",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel002");

    internal static readonly DiagnosticDescriptor DuplicateField = new(
        DiagnosticIds.DuplicateField,
        "Duplicate CEL field name",
        "Members '{0}' and '{1}' of type '{2}' both map to the CEL field name '{3}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel003");

    internal static readonly DiagnosticDescriptor DuplicateType = new(
        DiagnosticIds.DuplicateType,
        "Duplicate CEL type name",
        "Types '{0}' and '{1}' in type context '{2}' both map to the CEL type name '{3}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel004");

    internal static readonly DiagnosticDescriptor InvalidName = new(
        DiagnosticIds.InvalidName,
        "Invalid CEL name",
        "'{0}' is not a valid CEL {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel005");

    internal static readonly DiagnosticDescriptor InvalidContext = new(
        DiagnosticIds.InvalidContext,
        "Invalid CEL type context",
        "Type context '{0}' must be a non-generic partial class that derives from CelTypeContext and is not nested in a generic type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel006");

    internal static readonly DiagnosticDescriptor DecimalMember = new(
        DiagnosticIds.DecimalMember,
        "Decimal member excluded from CEL type",
        "Member '{0}' of type '{1}' is excluded from the CEL type: the CEL type decimal requires .NET 11 (System.Numerics.Decimal128)",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel007");

    internal static readonly DiagnosticDescriptor LanguageVersion = new(
        DiagnosticIds.LanguageVersion,
        "C# language version not supported",
        "The code generated for type context '{0}' requires C# 12 or later; the project uses C# {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel008");

    internal static readonly DiagnosticDescriptor ContextTooLarge = new(
        DiagnosticIds.ContextTooLarge,
        "CEL type context too large",
        "The type closure of type context '{0}' contains more than {1} types",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpPage + "#gpcel009");

    internal static DiagnosticDescriptor Find(string id) => id switch
    {
        DiagnosticIds.UnsupportedType => UnsupportedType,
        DiagnosticIds.ExcludedMember => ExcludedMember,
        DiagnosticIds.DuplicateField => DuplicateField,
        DiagnosticIds.DuplicateType => DuplicateType,
        DiagnosticIds.InvalidName => InvalidName,
        DiagnosticIds.InvalidContext => InvalidContext,
        DiagnosticIds.DecimalMember => DecimalMember,
        DiagnosticIds.LanguageVersion => LanguageVersion,
        _ => ContextTooLarge,
    };
}
