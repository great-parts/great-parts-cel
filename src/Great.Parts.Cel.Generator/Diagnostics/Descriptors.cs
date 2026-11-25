using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The codes of the diagnostics. A code is never renumbered: GPCEL007 is no longer reported and its number is not used again.
internal static class DiagnosticIds
{
    internal const string UnsupportedType = "GPCEL001";
    internal const string ExcludedMember = "GPCEL002";
    internal const string DuplicateField = "GPCEL003";
    internal const string DuplicateType = "GPCEL004";
    internal const string InvalidName = "GPCEL005";
    internal const string InvalidContext = "GPCEL006";
    internal const string LanguageVersion = "GPCEL008";
    internal const string ContextTooLarge = "GPCEL009";
    internal const string SimpleEntryDataType = "GPCEL010";
    internal const string SimpleEntryPropertyType = "GPCEL011";
    internal const string ObjectLiteralType = "GPCEL012";
}

// The descriptors of the diagnostics: the identifier, the severity, the title and the message are those of the page
// docs/generator-diagnostics.md. HelpPage is the one constant of the address of that page; the help link of a code is the
// address with the code in lower case as the anchor, which the heading of the code on the page gives.
internal static class Descriptors
{
    internal const string Category = "Great.Parts.Cel.Generator";

    internal const string HelpPage = "https://github.com/great-parts/great-parts-cel/blob/main/docs/generator-diagnostics.md";

    private const string IdPrefix = "GPCEL";

    internal static readonly DiagnosticDescriptor UnsupportedType = Describe(
        DiagnosticIds.UnsupportedType,
        DiagnosticSeverity.Error,
        "Type cannot be used by CEL expressions",
        "Type '{0}' cannot be used in a CEL type context: {1}");

    internal static readonly DiagnosticDescriptor ExcludedMember = Describe(
        DiagnosticIds.ExcludedMember,
        DiagnosticSeverity.Warning,
        "Member excluded from CEL type",
        "Member '{0}' of type '{1}' is excluded from the CEL type: {2}");

    internal static readonly DiagnosticDescriptor DuplicateField = Describe(
        DiagnosticIds.DuplicateField,
        DiagnosticSeverity.Error,
        "Duplicate CEL field name",
        "Members '{0}' and '{1}' of type '{2}' both map to the CEL field name '{3}'");

    internal static readonly DiagnosticDescriptor DuplicateType = Describe(
        DiagnosticIds.DuplicateType,
        DiagnosticSeverity.Error,
        "Duplicate CEL type name",
        "Types '{0}' and '{1}' in type context '{2}' both map to the CEL type name '{3}'");

    internal static readonly DiagnosticDescriptor InvalidName = Describe(
        DiagnosticIds.InvalidName,
        DiagnosticSeverity.Error,
        "Invalid CEL name",
        "'{0}' is not a valid CEL {1}");

    internal static readonly DiagnosticDescriptor InvalidContext = Describe(
        DiagnosticIds.InvalidContext,
        DiagnosticSeverity.Error,
        "Invalid CEL type context",
        "Type context '{0}' must be a non-generic partial class that derives from CelTypeContext and is not nested in a generic type");

    internal static readonly DiagnosticDescriptor LanguageVersion = Describe(
        DiagnosticIds.LanguageVersion,
        DiagnosticSeverity.Error,
        "C# language version not supported",
        "The code generated for type context '{0}' requires C# 12 or later; the project uses C# {1}");

    internal static readonly DiagnosticDescriptor ContextTooLarge = Describe(
        DiagnosticIds.ContextTooLarge,
        DiagnosticSeverity.Error,
        "CEL type context too large",
        "The type closure of type context '{0}' contains more than {1} types");

    internal static readonly DiagnosticDescriptor SimpleEntryDataType = Describe(
        DiagnosticIds.SimpleEntryDataType,
        DiagnosticSeverity.Warning,
        "CEL simple entry cannot see the data type",
        "The static type '{0}' of the data argument of CelExpression.Evaluate is not an anonymous type or a supported dictionary; the call fails at run time unless the value is one of them: {1}");

    internal static readonly DiagnosticDescriptor SimpleEntryPropertyType = Describe(
        DiagnosticIds.SimpleEntryPropertyType,
        DiagnosticSeverity.Error,
        "Property type not supported by the CEL simple entry",
        "Property '{0}' of the anonymous data has type '{1}', which the CEL simple entry cannot read: {2}");

    internal static readonly DiagnosticDescriptor ObjectLiteralType = Describe(
        DiagnosticIds.ObjectLiteralType,
        DiagnosticSeverity.Error,
        "Type cannot be created by CEL object literals",
        "Type '{0}' is listed with AllowObjectLiterals = true but cannot be created by an object literal: {1}");

    // The parts that every descriptor shares: the category, the default state and the help link, which is the page with the code
    // in lower case as the anchor.
    private static DiagnosticDescriptor Describe(string id, DiagnosticSeverity severity, string title, string message) =>
        new(
            id,
            title,
            message,
            Category,
            severity,
            isEnabledByDefault: true,
            helpLinkUri: HelpPage + "#gpcel" + id.Substring(IdPrefix.Length));

    internal static DiagnosticDescriptor Find(string id) => id switch
    {
        DiagnosticIds.UnsupportedType => UnsupportedType,
        DiagnosticIds.ExcludedMember => ExcludedMember,
        DiagnosticIds.DuplicateField => DuplicateField,
        DiagnosticIds.DuplicateType => DuplicateType,
        DiagnosticIds.InvalidName => InvalidName,
        DiagnosticIds.InvalidContext => InvalidContext,
        DiagnosticIds.LanguageVersion => LanguageVersion,
        DiagnosticIds.ContextTooLarge => ContextTooLarge,
        DiagnosticIds.SimpleEntryDataType => SimpleEntryDataType,
        DiagnosticIds.ObjectLiteralType => ObjectLiteralType,
        _ => SimpleEntryPropertyType,
    };
}
