using System.Collections.Immutable;

namespace Great.Parts.Cel.Declarations;

// One version of one extension set, with what an environment takes from it: overloads with their implementations and
// costs, abstract types, variables, the names of the sets it needs and the forms of its macros. It is immutable and has no
// state of an environment, so any number of environments and threads share it.
internal abstract class ExtensionDefinition
{
    protected ExtensionDefinition(
        string name,
        uint version,
        string source,
        ImmutableArray<OverloadBinding> overloads,
        ImmutableArray<AbstractTypeDeclaration> abstractTypes = default,
        ImmutableArray<CelVariableDecl> variables = default,
        ImmutableArray<string> dependencies = default,
        ImmutableArray<MacroForm> macroForms = default)
    {
        Name = name;
        Version = version;
        Source = source;
        Overloads = overloads;
        AbstractTypes = abstractTypes.IsDefault ? [] : abstractTypes;
        Variables = variables.IsDefault ? [] : variables;
        Dependencies = dependencies.IsDefault ? [] : dependencies;
        MacroForms = macroForms.IsDefault ? [] : macroForms;
    }

    // The short name of the set, as an environment description writes it.
    internal string Name { get; }

    internal uint Version { get; }

    // The label that build errors and the declaration index use: extension <name> v<version> for a set of the catalog,
    // application for a set of the application.
    internal string Source { get; }

    // The overloads in the order of the table of the set.
    internal ImmutableArray<OverloadBinding> Overloads { get; }

    internal ImmutableArray<AbstractTypeDeclaration> AbstractTypes { get; }

    // Declarations of variables, for the internal sets of the library only.
    internal ImmutableArray<CelVariableDecl> Variables { get; }

    // The names of the sets that this one needs; an environment with this set has to add them.
    internal ImmutableArray<string> Dependencies { get; }

    // The keys of the macros the set brings, for the check of the functions of the application against them.
    internal ImmutableArray<MacroForm> MacroForms { get; }
}
