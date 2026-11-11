using System.Collections.Immutable;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The declarations collected by a builder, as a set: an environment keeps the set it was built from, and
// a builder made from the environment starts with it. Repeated declarations are already removed; a variable, a
// function, a container and an extension set keep the source and the position in the JSON text that the build errors name.
internal sealed class DeclarationSet
{
    internal DeclarationSet(
        ImmutableArray<SourcedDeclaration<CelVariableDecl>> variables,
        ImmutableArray<SourcedDeclaration<CelFunctionDecl>> functions,
        ImmutableArray<ImplementationEntry> implementations,
        ImmutableArray<SourcedDeclaration<string>> containers,
        ImmutableArray<UnmappedDeclaration> unmapped,
        ImmutableArray<TypeSource> typeSources,
        ImmutableArray<SourcedDeclaration<CelExtension>> extensions = default,
        CelTimeZoneDatabase? timeZones = null,
        bool timeZoneConflict = false)
    {
        Variables = variables;
        Functions = functions;
        Implementations = implementations;
        Containers = containers;
        Unmapped = unmapped;
        TypeSources = typeSources;
        Extensions = extensions.IsDefault ? [] : extensions;
        TimeZones = timeZones;
        TimeZoneConflict = timeZoneConflict;
    }

    internal ImmutableArray<SourcedDeclaration<CelVariableDecl>> Variables { get; }

    internal ImmutableArray<SourcedDeclaration<CelFunctionDecl>> Functions { get; }

    internal ImmutableArray<ImplementationEntry> Implementations { get; }

    // The non-empty containers, each once.
    internal ImmutableArray<SourcedDeclaration<string>> Containers { get; }

    internal ImmutableArray<UnmappedDeclaration> Unmapped { get; }

    // The type sources, each provider once, in the order of the calls.
    internal ImmutableArray<TypeSource> TypeSources { get; }

    // The extension sets, each once by the rule of IsSameSetAs, in the order of the calls.
    internal ImmutableArray<SourcedDeclaration<CelExtension>> Extensions { get; }

    // The first time zone database that UseTimeZones added; null without one.
    internal CelTimeZoneDatabase? TimeZones { get; }

    // Whether a database other than the first was added: the build error TimeZoneDatabaseConflict.
    internal bool TimeZoneConflict { get; }
}
