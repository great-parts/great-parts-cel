using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

// The read-only description of an environment (CelEnvironment.Variables, Functions, ObjectTypes, EnumTypes): the declarations the
// index already holds, in the order of string.CompareOrdinal by name, in immutable arrays. The arrays share the declaration objects,
// which are immutable, so no member of them changes the environment.
internal sealed class EnvironmentDescription
{
    private EnvironmentDescription(
        ImmutableArray<CelVariableDecl> variables,
        ImmutableArray<CelFunctionDecl> functions,
        ImmutableArray<CelObjectType> objectTypes,
        ImmutableArray<CelEnumType> enumTypes)
    {
        Variables = variables;
        Functions = functions;
        ObjectTypes = objectTypes;
        EnumTypes = enumTypes;
    }

    internal ImmutableArray<CelVariableDecl> Variables { get; }

    internal ImmutableArray<CelFunctionDecl> Functions { get; }

    internal ImmutableArray<CelObjectType> ObjectTypes { get; }

    internal ImmutableArray<CelEnumType> EnumTypes { get; }

    internal static EnvironmentDescription Create(DeclarationIndex index)
    {
        CelVariableDecl[] variables = [.. index.Variables];
        Array.Sort(variables, static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        CelObjectType[] objectTypes = [.. index.Types.ObjectTypes.Values];
        Array.Sort(objectTypes, static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        CelEnumType[] enumTypes = [.. index.Types.EnumTypes];
        Array.Sort(enumTypes, static (left, right) => string.CompareOrdinal(left.Name, right.Name));

        return new EnvironmentDescription(
            ImmutableCollectionsMarshal.AsImmutableArray(variables),
            FunctionsOf(index.AllOverloads),
            ImmutableCollectionsMarshal.AsImmutableArray(objectTypes),
            ImmutableCollectionsMarshal.AsImmutableArray(enumTypes));
    }

    // The functions that an expression calls by name: operators and names with @ are left out. The overloads of a function are in
    // the order of their identifiers.
    private static ImmutableArray<CelFunctionDecl> FunctionsOf(ImmutableArray<OverloadBinding> bindings)
    {
        var byName = new Dictionary<string, List<CelOverloadDecl>>(StringComparer.Ordinal);
        foreach (OverloadBinding binding in bindings)
        {
            if (ReferencedNames.IsName(binding.Function))
            {
                (CollectionsMarshal.GetValueRefOrAddDefault(byName, binding.Function, out _) ??= []).Add(binding.Declaration);
            }
        }

        string[] names = [.. byName.Keys];
        Array.Sort(names, StringComparer.Ordinal);
        CelFunctionDecl[] functions = new CelFunctionDecl[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            List<CelOverloadDecl> overloads = byName[names[i]];
            overloads.Sort(static (left, right) => string.CompareOrdinal(left.OverloadId, right.OverloadId));
            functions[i] = new CelFunctionDecl(names[i], CollectionsMarshal.AsSpan(overloads));
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(functions);
    }
}
