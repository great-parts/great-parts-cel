using System.Collections.Immutable;

namespace Great.Parts.Cel.Declarations;

// The declarations collected by a builder, as a set: an environment keeps the set it was built from, and
// a builder made from the environment starts with it. Repeated declarations are already removed.
internal sealed class DeclarationSet
{
    internal DeclarationSet(
        ImmutableArray<CelVariableDecl> variables,
        ImmutableArray<CelFunctionDecl> functions,
        ImmutableArray<ImplementationEntry> implementations,
        ImmutableArray<string> containers,
        ImmutableArray<UnmappedDeclaration> unmapped)
    {
        Variables = variables;
        Functions = functions;
        Implementations = implementations;
        Containers = containers;
        Unmapped = unmapped;
    }

    internal ImmutableArray<CelVariableDecl> Variables { get; }

    internal ImmutableArray<CelFunctionDecl> Functions { get; }

    internal ImmutableArray<ImplementationEntry> Implementations { get; }

    // The non-empty containers, each once.
    internal ImmutableArray<string> Containers { get; }

    internal ImmutableArray<UnmappedDeclaration> Unmapped { get; }
}
