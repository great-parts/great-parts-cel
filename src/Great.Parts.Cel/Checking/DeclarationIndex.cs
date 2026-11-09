using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

// The declarations of one environment, indexed for the lookups of type checking and evaluation: the
// overloads of its standard library version merged with the declarations of the application, the type
// identifiers of the version, and the name resolution container. Its keys are declarations of the
// environment, never text of an expression; names of an expression are only looked up, also as spans,
// so that the candidates of the container are tried without creating strings.
internal sealed class DeclarationIndex
{
    private static readonly FrozenDictionary<string, CelType> NoTypeIdentifiers =
        new Dictionary<string, CelType>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

    private readonly FrozenDictionary<string, CelVariableDecl> _variables;

    private readonly FrozenDictionary<string, CelVariableDecl>.AlternateLookup<ReadOnlySpan<char>> _variablesBySpan;

    private readonly FrozenDictionary<string, CelType> _typeIdentifiers;

    private readonly FrozenDictionary<string, CelType>.AlternateLookup<ReadOnlySpan<char>> _typeIdentifiersBySpan;

    // The names of all functions, so that an unknown function is told from a known one whose overloads
    // do not take the arguments.
    private readonly FrozenSet<string> _functions;

    private readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> _functionsBySpan;

    // The overloads by function name, call style and argument count, the receiver counted, in the order
    // given to Create.
    private readonly FrozenDictionary<(string Function, bool IsReceiverStyle, int ArgumentCount), ImmutableArray<OverloadBinding>> _overloads;

    private DeclarationIndex(
        FrozenDictionary<string, CelVariableDecl> variables,
        FrozenDictionary<string, CelType> typeIdentifiers,
        FrozenSet<string> functions,
        FrozenDictionary<(string Function, bool IsReceiverStyle, int ArgumentCount), ImmutableArray<OverloadBinding>> overloads,
        string container,
        TypeIndex types)
    {
        _variables = variables;
        _variablesBySpan = variables.GetAlternateLookup<ReadOnlySpan<char>>();
        _typeIdentifiers = typeIdentifiers;
        _typeIdentifiersBySpan = typeIdentifiers.GetAlternateLookup<ReadOnlySpan<char>>();
        _functions = functions;
        _functionsBySpan = functions.GetAlternateLookup<ReadOnlySpan<char>>();
        _overloads = overloads;
        Container = container;
        Types = types;
    }

    // The name resolution container; empty without one.
    internal string Container { get; }

    // The type index of the type sources: object types, enumeration constants and descriptions by .NET type.
    internal TypeIndex Types { get; }

    // An index without type identifiers and container, for the tests of checking and evaluation.
    internal static DeclarationIndex Create(ImmutableArray<OverloadBinding> overloads, ImmutableArray<CelVariableDecl> variables) =>
        Create(overloads, variables, NoTypeIdentifiers, string.Empty, TypeIndex.Empty);

    // The overloads come in the order of their candidates: the standard library in the order of its
    // table, then the application by overload identifier. The variables have distinct names. The type
    // identifiers are keyed by ordinal comparison; the object types of the type index join them, each the
    // identifier of its type, and their names differ from those of the version.
    internal static DeclarationIndex Create(
        ImmutableArray<OverloadBinding> overloads,
        ImmutableArray<CelVariableDecl> variables,
        FrozenDictionary<string, CelType> typeIdentifiers,
        string container,
        TypeIndex? types = null)
    {
        types ??= TypeIndex.Empty;
        if (types.ObjectTypes.Count != 0)
        {
            var merged = new Dictionary<string, CelType>(typeIdentifiers, StringComparer.Ordinal);
            foreach (KeyValuePair<string, CelObjectType> objectType in types.ObjectTypes)
            {
                merged.Add(objectType.Key, objectType.Value.Type);
            }

            typeIdentifiers = merged.ToFrozenDictionary(StringComparer.Ordinal);
        }

        var variablesByName = new Dictionary<string, CelVariableDecl>(StringComparer.Ordinal);
        foreach (CelVariableDecl variable in variables)
        {
            variablesByName.TryAdd(variable.Name, variable);
        }

        var functions = new HashSet<string>(StringComparer.Ordinal);
        var overloadsByCall = new Dictionary<(string Function, bool IsReceiverStyle, int ArgumentCount), ImmutableArray<OverloadBinding>.Builder>();
        foreach (OverloadBinding overload in overloads)
        {
            functions.Add(overload.Function);
            CelOverloadDecl declaration = overload.Declaration;
            (string, bool, int) key = (overload.Function, declaration.IsReceiverStyle, declaration.ParameterTypes.Length);
            (CollectionsMarshal.GetValueRefOrAddDefault(overloadsByCall, key, out _) ??= ImmutableArray.CreateBuilder<OverloadBinding>()).Add(overload);
        }

        return new DeclarationIndex(
            variablesByName.ToFrozenDictionary(StringComparer.Ordinal),
            typeIdentifiers,
            functions.ToFrozenSet(StringComparer.Ordinal),
            overloadsByCall.ToFrozenDictionary(entry => entry.Key, entry => entry.Value.ToImmutable()),
            container,
            types);
    }

    internal bool TryGetVariable(string name, [NotNullWhen(true)] out CelVariableDecl? variable) =>
        _variables.TryGetValue(name, out variable);

    // The variable with the full name, a candidate of the container composed in a buffer.
    internal bool TryGetVariable(ReadOnlySpan<char> name, [NotNullWhen(true)] out CelVariableDecl? variable) =>
        _variablesBySpan.TryGetValue(name, out variable);

    // A type identifier of the standard library version, such as int, with the type it stands for: the
    // identifier has the static type type(T) of that type T and its type value.
    internal bool TryGetTypeIdentifier(string name, [NotNullWhen(true)] out CelType? type) =>
        _typeIdentifiers.TryGetValue(name, out type);

    internal bool TryGetTypeIdentifier(ReadOnlySpan<char> name, [NotNullWhen(true)] out CelType? type) =>
        _typeIdentifiersBySpan.TryGetValue(name, out type);

    internal bool IsFunction(string name) => _functions.Contains(name);

    // The declared function with the full name, and the name as the index keeps it.
    internal bool TryGetFunction(ReadOnlySpan<char> name, [NotNullWhen(true)] out string? function) =>
        _functionsBySpan.TryGetValue(name, out function);

    // The global overloads of a function with that argument count; empty when there are none.
    internal ImmutableArray<OverloadBinding> GetOverloads(string function, int argumentCount) =>
        GetOverloads(function, false, argumentCount);

    // The overloads of a function with that call style and argument count, the receiver counted; empty when
    // there are none.
    internal ImmutableArray<OverloadBinding> GetOverloads(string function, bool isReceiverStyle, int argumentCount) =>
        _overloads.TryGetValue((function, isReceiverStyle, argumentCount), out ImmutableArray<OverloadBinding> found) ? found : [];
}
