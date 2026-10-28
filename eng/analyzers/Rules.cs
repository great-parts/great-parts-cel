using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>The descriptors of the rules ARCH001-ARCH008; every rule is an error of the category <c>Architecture</c>.</summary>
internal static class Rules
{
    private const string Category = "Architecture";

    /// <summary>ARCH001: a cycle between namespaces of one assembly.</summary>
    internal static readonly DiagnosticDescriptor NamespaceCycle = new(
        id: "ARCH001",
        title: "Namespace cycle",
        messageFormat: "namespace cycle {0}; other edge: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Namespaces of one assembly must not reference each other in a cycle.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>ARCH002: a public type without a row of the API concept list.</summary>
    internal static readonly DiagnosticDescriptor TypeWithoutRow = new(
        id: "ARCH002",
        title: "Public type missing from the API concept list",
        messageFormat: "public type '{0}' is missing from the API concept list",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every public type of a package has a row in docs/api-concepts.md.");

    /// <summary>ARCH003: a row of the API concept list without a public type.</summary>
    internal static readonly DiagnosticDescriptor RowWithoutType = new(
        id: "ARCH003",
        title: "API concept list row without a public type",
        messageFormat: "API concept list row '{0}' has no public type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every row of docs/api-concepts.md names a public type of its assembly.");

    /// <summary>ARCH004: a row of the API concept list with an empty cell, or a malformed row.</summary>
    internal static readonly DiagnosticDescriptor InvalidRow = new(
        id: "ARCH004",
        title: "Empty cell or malformed row of the API concept list",
        messageFormat: "row '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every row of docs/api-concepts.md has four cells, a concept and an explanation of the name.");

    /// <summary>ARCH005: a reference outside the layer table, a namespace without a line, a malformed line.</summary>
    internal static readonly DiagnosticDescriptor LayerViolation = new(
        id: "ARCH005",
        title: "Reference outside the layer table",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A namespace references only the namespaces of its line in eng/layers.txt.");

    /// <summary>ARCH006: a public type with a reserved name.</summary>
    internal static readonly DiagnosticDescriptor ReservedName = new(
        id: "ARCH006",
        title: "Reserved public type name",
        messageFormat: "public type name '{0}' is reserved",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Public types are not named Great, Parts, Cel, Policy, Filtering or Protobuf.");

    /// <summary>ARCH007: a public type or member that requires unreferenced or dynamic code.</summary>
    internal static readonly DiagnosticDescriptor RequiresCode = new(
        id: "ARCH007",
        title: "Public member requires unreferenced or dynamic code",
        messageFormat: "public member '{0}' is marked {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The public API works under Native AOT as a whole.");

    /// <summary>ARCH008: a qualified name that starts with <c>Cel</c> without <c>global::</c>.</summary>
    internal static readonly DiagnosticDescriptor CelQualifiedName = new(
        id: "ARCH008",
        title: "Qualified name starting with Cel without global::",
        messageFormat: "qualified name '{0}' must use a using directive or global::",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Inside the Great.Parts.Cel namespaces the name Cel binds to Great.Parts.Cel, not to the namespace Cel.");
}
