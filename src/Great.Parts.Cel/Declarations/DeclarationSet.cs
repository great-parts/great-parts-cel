using System.Collections.Immutable;

namespace Great.Parts.Cel.Declarations;

// The declarations collected by a builder, as a set: an environment keeps the set it was built from, and
// a builder made from the environment starts with it. Repeated declarations are already removed; a variable, a
// function and a container keep the source and the position in the JSON text that the build errors name.
internal sealed class DeclarationSet
{
    internal DeclarationSet(
        ImmutableArray<SourcedDeclaration<CelVariableDecl>> variables,
        ImmutableArray<SourcedDeclaration<CelFunctionDecl>> functions,
        ImmutableArray<ImplementationEntry> implementations,
        ImmutableArray<SourcedDeclaration<string>> containers,
        ImmutableArray<UnmappedDeclaration> unmapped,
        ImmutableArray<TypeSource> typeSources)
    {
        Variables = variables;
        Functions = functions;
        Implementations = implementations;
        Containers = containers;
        Unmapped = unmapped;
        TypeSources = typeSources;
    }

    internal ImmutableArray<SourcedDeclaration<CelVariableDecl>> Variables { get; }

    internal ImmutableArray<SourcedDeclaration<CelFunctionDecl>> Functions { get; }

    internal ImmutableArray<ImplementationEntry> Implementations { get; }

    // The non-empty containers, each once.
    internal ImmutableArray<SourcedDeclaration<string>> Containers { get; }

    internal ImmutableArray<UnmappedDeclaration> Unmapped { get; }

    // The type sources, each provider once, in the order of the calls.
    internal ImmutableArray<TypeSource> TypeSources { get; }
}
