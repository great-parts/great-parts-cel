using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// An implementation registered in a builder: the delegate of the application, the implementation made of
// it, and for a typed delegate the CEL types of its .NET types, which the build compares with the declared
// overload: of a parameter by the expected-type table, as for Compile<T>; of the result by the input rows. A .NET
// reference type whose CEL type is P, string or byte[], fits a declared wrapper(P) too, since it takes null. A parameter
// or result of a class, record, struct or interface of the application has no CEL type until Build: the entry keeps the
// .NET types, and Resolve gives the object types that the type index describes for them.
internal sealed class ImplementationEntry
{
    private readonly ImmutableArray<Type> _parameterClrTypes;
    private readonly Type? _resultClrType;

    internal ImplementationEntry(string overloadId, Delegate original, OverloadImplementation implementation)
    {
        OverloadId = overloadId;
        Original = original;
        Implementation = implementation;
    }

    internal ImplementationEntry(
        string overloadId,
        Delegate original,
        OverloadImplementation implementation,
        ImmutableArray<CelType?> parameterTypes,
        CelType? resultType,
        ImmutableArray<bool> parameterTakesNull,
        bool resultTakesNull,
        ImmutableArray<Type> parameterClrTypes = default,
        Type? resultClrType = null)
        : this(overloadId, original, implementation)
    {
        ParameterTypes = parameterTypes;
        ResultType = resultType;
        ParameterTakesNull = parameterTakesNull;
        ResultTakesNull = resultTakesNull;
        _parameterClrTypes = parameterClrTypes;
        _resultClrType = resultClrType;
    }

    internal string OverloadId { get; }

    internal Delegate Original { get; }

    internal OverloadImplementation Implementation { get; }

    // Default for a CelFunctionImplementation; an element is null for a .NET type outside the expected-type table.
    internal ImmutableArray<CelType?> ParameterTypes { get; }

    internal CelType? ResultType { get; }

    // Whether the .NET type of a parameter is a reference type with a wrapper, string or byte[]; default for none.
    internal ImmutableArray<bool> ParameterTakesNull { get; }

    // Whether the .NET type of the result is string or byte[].
    internal bool ResultTakesNull { get; }

    // After Resolve: the first .NET type of the application, the parameters before the result, that no type source
    // describes; null when there is none.
    internal Type? Unresolved { get; private init; }

    internal bool IsTyped => !ParameterTypes.IsDefault;

    // The entry with the CEL types of its .NET types of the application filled in from the type index: the object type that a
    // source describes for exactly that .NET type. A type that no source describes stays without a CEL type and is the first
    // Unresolved of the result. The entry itself when no parameter and no result is such a type.
    internal ImplementationEntry Resolve(TypeIndex types)
    {
        if (_parameterClrTypes.IsDefault)
        {
            return this;
        }

        ImmutableArray<CelType?>.Builder? parameters = null;
        Type? unresolved = null;
        for (int i = 0; i < _parameterClrTypes.Length; i++)
        {
            Type clrType = _parameterClrTypes[i];
            if (ParameterTypes[i] is null && ConversionRules.IsApplicationObject(clrType))
            {
                if (types.TryGetByClrType(clrType, out CelObjectType? described))
                {
                    parameters ??= ParameterTypes.ToBuilder();
                    parameters[i] = described.Type;
                }
                else
                {
                    unresolved ??= clrType;
                }
            }
        }

        CelType? result = ResultType;
        if (result is null && _resultClrType is not null)
        {
            result = ResolveResult(_resultClrType, types, ref unresolved);
        }

        if (parameters is null && ReferenceEquals(result, ResultType) && unresolved is null)
        {
            return this;
        }

        return new ImplementationEntry(
            OverloadId,
            Original,
            Implementation,
            parameters is null ? ParameterTypes : parameters.MoveToImmutable(),
            result,
            ParameterTakesNull,
            ResultTakesNull,
            _parameterClrTypes,
            _resultClrType)
        {
            Unresolved = unresolved,
        };
    }

    // The object type of a result that is a class of the application, or list(Class) for a list of such a class by the input rows;
    // null, with the class in unresolved unless it is set, when no source describes it or the result is no such type.
    private static CelType? ResolveResult(Type clrType, TypeIndex types, ref Type? unresolved)
    {
        Type? elementClrType = ConversionRules.ApplicationElementOf(clrType);
        Type? resultClass = elementClrType ?? (ConversionRules.IsApplicationObject(clrType) ? clrType : null);
        if (resultClass is null)
        {
            return null;
        }

        if (types.TryGetByClrType(resultClass, out CelObjectType? described))
        {
            return elementClrType is null ? described.Type : CelType.ListOf(described.Type);
        }

        unresolved ??= resultClass;
        return null;
    }

    // The same delegate registered twice under one identifier is one implementation.
    internal bool SameAs(ImplementationEntry other) =>
        string.Equals(OverloadId, other.OverloadId, StringComparison.Ordinal) && ReferenceEquals(Original, other.Original);
}
