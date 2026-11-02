using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Declarations;

namespace Great.Parts.Cel.Checking;

// The declarations of one environment, indexed for the lookups of type checking and evaluation: the
// overloads of its standard library version merged with the declarations of the application.
internal sealed class DeclarationIndex
{
    private readonly FrozenDictionary<string, CelVariableDecl> _variables;

    // The names of all functions, so that an unknown function is told from a known one whose overloads
    // do not take the arguments.
    private readonly FrozenSet<string> _functions;

    // The global overloads by function name and argument count, in the order of their declaration.
    private readonly FrozenDictionary<(string Function, int ArgumentCount), ImmutableArray<OverloadBinding>> _overloads;

    private DeclarationIndex(
        FrozenDictionary<string, CelVariableDecl> variables,
        FrozenSet<string> functions,
        FrozenDictionary<(string Function, int ArgumentCount), ImmutableArray<OverloadBinding>> overloads)
    {
        _variables = variables;
        _functions = functions;
        _overloads = overloads;
    }

    internal static DeclarationIndex Create(ImmutableArray<OverloadBinding> overloads, ImmutableArray<CelVariableDecl> variables)
    {
        var variablesByName = new Dictionary<string, CelVariableDecl>(StringComparer.Ordinal);
        foreach (CelVariableDecl variable in variables)
        {
            if (!variablesByName.TryAdd(variable.Name, variable) && !variablesByName[variable.Name].Type.Equals(variable.Type))
            {
                // Two different declarations of one name are the build error DuplicateVariable, which comes
                // with the build errors about declarations; the same declaration twice is one declaration.
                throw new NotImplementedException();
            }
        }

        var functions = new HashSet<string>(StringComparer.Ordinal);
        var overloadsByCall = new Dictionary<(string Function, int ArgumentCount), ImmutableArray<OverloadBinding>.Builder>();
        foreach (OverloadBinding overload in overloads)
        {
            functions.Add(overload.Function);
            (string, int) key = (overload.Function, overload.Declaration.ParameterTypes.Length);
            if (!overloadsByCall.TryGetValue(key, out ImmutableArray<OverloadBinding>.Builder? list))
            {
                list = ImmutableArray.CreateBuilder<OverloadBinding>();
                overloadsByCall.Add(key, list);
            }

            list.Add(overload);
        }

        return new DeclarationIndex(
            variablesByName.ToFrozenDictionary(StringComparer.Ordinal),
            functions.ToFrozenSet(StringComparer.Ordinal),
            overloadsByCall.ToFrozenDictionary(entry => entry.Key, entry => entry.Value.ToImmutable()));
    }

    internal bool TryGetVariable(string name, [NotNullWhen(true)] out CelVariableDecl? variable) =>
        _variables.TryGetValue(name, out variable);

    internal bool IsFunction(string name) => _functions.Contains(name);

    // The overloads of a function with that argument count; empty when there are none.
    internal ImmutableArray<OverloadBinding> GetOverloads(string function, int argumentCount) =>
        _overloads.TryGetValue((function, argumentCount), out ImmutableArray<OverloadBinding> found) ? found : [];
}
