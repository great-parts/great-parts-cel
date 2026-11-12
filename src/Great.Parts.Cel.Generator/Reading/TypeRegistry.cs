using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// What the reading holds while it works; none of it reaches a model, so symbols are fine here.

// One [CelObject] attribute of the context: the type, the Name and IgnoreMembers arguments and the places of the type argument
// and of the Name argument.
internal sealed record RootInfo(
    ITypeSymbol Type,
    string Display,
    string? ExplicitName,
    string EffectiveName,
    string[] IgnoreMembers,
    LocationInfo TypePlace,
    LocationInfo NamePlace);

// An object type that the context describes: a root, or a type that the members of the types reach.
internal sealed class ObjectEntry
{
    internal ObjectEntry(INamedTypeSymbol symbol, string celName, bool nameValid, RootInfo? root, LocationInfo reachedBy)
    {
        Symbol = symbol;
        Display = TypeNames.Display(symbol);
        CelName = celName;
        NameValid = nameValid;
        Root = root;
        ReachedBy = reachedBy;
    }

    internal INamedTypeSymbol Symbol { get; }

    internal string Display { get; }

    internal string CelName { get; }

    // A default name that is not a CEL qualified name: the type is not described, and a member that names it is left out.
    internal bool NameValid { get; }

    internal RootInfo? Root { get; }

    // The place of the type argument of the root attribute that reached the type first: where a diagnostic about a member
    // or about the type goes when it has no place of its own in the source.
    internal LocationInfo ReachedBy { get; }

    internal bool Queued { get; set; }

    internal EquatableArray<FieldModel> Fields { get; set; }

    internal EquatableArray<ExcludedMemberModel> Excluded { get; set; }
}

internal sealed class EnumEntry
{
    internal EnumEntry(INamedTypeSymbol symbol, string celName, bool nameValid, LocationInfo reachedBy)
    {
        Symbol = symbol;
        Display = TypeNames.Display(symbol);
        CelName = celName;
        NameValid = nameValid;
        ReachedBy = reachedBy;
    }

    internal INamedTypeSymbol Symbol { get; }

    internal string Display { get; }

    internal string CelName { get; }

    internal bool NameValid { get; }

    internal LocationInfo ReachedBy { get; }
}

// The object types and the enumerations of one context, and the limit of the closure. Only the object types that the context
// describes count: an enumeration is a leaf and cannot widen the graph.
internal sealed class TypeRegistry
{
    internal const int Limit = 250;

    private readonly Dictionary<INamedTypeSymbol, ObjectEntry> objects = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<INamedTypeSymbol, EnumEntry> enums = new(SymbolEqualityComparer.Default);

    internal List<ObjectEntry> Objects { get; } = [];

    internal List<EnumEntry> Enums { get; } = [];

    internal Stack<ObjectEntry> Pending { get; } = new();

    internal bool LimitExceeded { get; private set; }

    // The place that a type reached from now on is attributed to: the root being walked.
    internal LocationInfo ReachedBy { get; set; }

    internal ObjectEntry? AddRootObject(INamedTypeSymbol symbol, RootInfo root)
    {
        return Create(symbol, root.EffectiveName, root, root.TypePlace);
    }

    internal void AddRootEnum(INamedTypeSymbol symbol, RootInfo root)
    {
        var entry = new EnumEntry(symbol, root.EffectiveName, true, root.TypePlace);
        enums[symbol] = entry;
        Enums.Add(entry);
    }

    // A type that a member reaches: the entry that exists, or a new one, queued to be read. Null when the new type is over the
    // limit.
    internal ObjectEntry? ReachObject(INamedTypeSymbol symbol)
    {
        if (objects.TryGetValue(symbol, out ObjectEntry? existing))
        {
            return existing;
        }

        string name = TypeNames.DefaultCelName(symbol);
        if (!TypeNames.IsQualifiedName(name))
        {
            var rejected = new ObjectEntry(symbol, name, false, null, ReachedBy);
            objects[symbol] = rejected;
            return rejected;
        }

        ObjectEntry? created = Create(symbol, name, null, ReachedBy);
        if (created is not null)
        {
            Queue(created);
        }

        return created;
    }

    internal EnumEntry ReachEnum(INamedTypeSymbol symbol)
    {
        if (enums.TryGetValue(symbol, out EnumEntry? existing))
        {
            return existing;
        }

        string name = TypeNames.DefaultCelName(symbol);
        bool valid = TypeNames.IsQualifiedName(name);
        var entry = new EnumEntry(symbol, name, valid, ReachedBy);
        enums[symbol] = entry;
        if (valid)
        {
            Enums.Add(entry);
        }

        return entry;
    }

    internal void Queue(ObjectEntry entry)
    {
        if (!entry.Queued)
        {
            entry.Queued = true;
            Pending.Push(entry);
        }
    }

    private ObjectEntry? Create(INamedTypeSymbol symbol, string name, RootInfo? root, LocationInfo reachedBy)
    {
        if (Objects.Count >= Limit)
        {
            LimitExceeded = true;
            return null;
        }

        var entry = new ObjectEntry(symbol, name, true, root, reachedBy);
        objects[symbol] = entry;
        Objects.Add(entry);
        return entry;
    }
}
