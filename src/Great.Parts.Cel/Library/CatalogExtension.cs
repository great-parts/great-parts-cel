using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Library;

// The definition of a set of the catalog: what every definition has, and the macros and the flag of the syntax that the set
// brings. A set of the application has none of them. The macros belong here, not in ExtensionDefinition, because
// Declarations does not reference Syntax; Library does. The source of a catalog set is extension <name> v<version>.
internal sealed class CatalogExtension : ExtensionDefinition
{
    internal CatalogExtension(
        string name,
        uint version,
        ImmutableArray<OverloadBinding> overloads,
        ImmutableArray<AbstractTypeDeclaration> abstractTypes = default,
        ImmutableArray<CelVariableDecl> variables = default,
        ImmutableArray<string> dependencies = default,
        ImmutableArray<Macro> macros = default,
        bool optionalSyntax = false)
        : base(name, version, SourceOf(name, version), overloads, abstractTypes, variables, dependencies, FormsOf(macros))
    {
        Macros = macros.IsDefault ? [] : macros;
        OptionalSyntax = optionalSyntax;
    }

    // The macros in the order of the table of the set.
    internal ImmutableArray<Macro> Macros { get; }

    // Whether the set turns on the optional syntax: .?, [? and ? before an element.
    internal bool OptionalSyntax { get; }

    // The syntax of an environment with these sets, which are in the order of their names and versions: the standard macros and
    // those of the sets, and the flags the sets turn on. An environment whose sets bring neither has SyntaxOptions.Standard
    // itself. A set of the application brings none.
    internal static SyntaxOptions SyntaxOf(ImmutableArray<ExtensionDefinition> extensions)
    {
        List<Macro> added = [];
        bool optionalSyntax = false;
        foreach (ExtensionDefinition extension in extensions)
        {
            if (extension is CatalogExtension catalog)
            {
                added.AddRange(catalog.Macros);
                optionalSyntax |= catalog.OptionalSyntax;
            }
        }

        if (added.Count == 0 && !optionalSyntax)
        {
            return SyntaxOptions.Standard;
        }

        List<Macro> macros = [.. MacroTable.Standard.Macros, .. added];
        return new SyntaxOptions(optionalSyntax, MacroTable.Create(macros));
    }

    private static string SourceOf(string name, uint version) =>
        "extension " + name + " v" + version.ToString(CultureInfo.InvariantCulture);

    private static ImmutableArray<MacroForm> FormsOf(ImmutableArray<Macro> macros)
    {
        if (macros.IsDefaultOrEmpty)
        {
            return [];
        }

        ImmutableArray<MacroForm>.Builder forms = ImmutableArray.CreateBuilder<MacroForm>(macros.Length);
        foreach (Macro macro in macros)
        {
            forms.Add(new MacroForm(macro.Name, macro.Namespace, macro.IsReceiver, macro.ArgumentCount));
        }

        return forms.MoveToImmutable();
    }
}
