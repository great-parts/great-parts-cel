using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The CEL type that the type mapping gives a .NET type of a delegate, and the .NET type: Type is null outside the input
// rows; ParameterType is the CEL type that the expected-type table gives it, which the build compares with the
// inferred one.
internal readonly record struct ClrMapping(CelType? Type, Type ClrType, CelType? ParameterType);

// A delegate form of AddFunction: the function name, the delegate, the .NET types of its parameters and result with their CEL
// types, and the way to make the implementation once the overload identifier is known. The identifier and the overload
// come from the CEL types, so a delegate with a type of the application among them has them only at Build, when the type
// index says which object type a type source describes for the .NET type.
internal sealed class DelegateDeclaration
{
    private readonly ImmutableArray<ClrMapping> _parameters;
    private readonly ClrMapping _result;
    private readonly Func<string, OverloadImplementation> _implementation;

    internal DelegateDeclaration(
        string name,
        Delegate original,
        ImmutableArray<ClrMapping> parameters,
        ClrMapping result,
        Func<string, OverloadImplementation> implementation)
    {
        Name = name;
        Original = original;
        _parameters = parameters;
        _result = result;
        _implementation = implementation;
    }

    internal string Name { get; }

    internal Delegate Original { get; }

    // Whether a .NET type among the parameters and the result is a type of the application that Build can still resolve.
    internal bool HasApplicationType
    {
        get
        {
            foreach (ClrMapping parameter in _parameters)
            {
                if (parameter.Type is null && ConversionRules.IsApplicationObject(parameter.ClrType))
                {
                    return true;
                }
            }

            return _result.Type is null && (ConversionRules.IsApplicationObject(_result.ClrType) || ConversionRules.ApplicationElementOf(_result.ClrType) is not null);
        }
    }

    // The overload and its implementation when every CEL type is known: from the type mapping, or for a type of the
    // application from the type index, which is null at the call of the builder. Otherwise false, and unresolved is the
    // first .NET type without a CEL type, the parameters before the result, with the identifier that names the declaration
    // in the error, in which each such type is written as its .NET type.
    internal bool TryResolve(
        TypeIndex? types,
        out string overloadId,
        out CelFunctionDecl? function,
        out ImplementationEntry? implementation,
        out Type? unresolved)
    {
        var parameterTypes = new CelType[_parameters.Length];
        var parts = new string[_parameters.Length];
        var expectedTypes = new CelType?[_parameters.Length];
        unresolved = null;
        for (int i = 0; i < _parameters.Length; i++)
        {
            ClrMapping parameter = _parameters[i];
            expectedTypes[i] = parameter.ParameterType;
            if (Resolve(parameter, types) is CelType type)
            {
                parts[i] = DeclarationText.Json(type);
                parameterTypes[i] = type;
                if (parameter.Type is null)
                {
                    expectedTypes[i] = type;
                }
            }
            else
            {
                parts[i] = parameter.ClrType.ToString();
                unresolved ??= parameter.ClrType;
            }
        }

        overloadId = DeclarationText.DerivedOverloadId(Name, parts);
        CelType? resultType = Resolve(_result, types);
        if (unresolved is not null || resultType is null)
        {
            unresolved ??= ConversionRules.ApplicationElementOf(_result.ClrType) ?? _result.ClrType;
            function = null;
            implementation = null;
            return false;
        }

        // The typed delegate is checked against the inferred overload like a typed implementation.
        function = new CelFunctionDecl(Name, CelOverloadDecl.Global(overloadId, parameterTypes, resultType));
        implementation = new ImplementationEntry(overloadId, Original, _implementation(overloadId), [.. expectedTypes], resultType, default, false);
        return true;
    }

    // The CEL type of a .NET type: from the type mapping, or the object type that the type index describes for a type of
    // the application; null when neither.
    // A list of a class of the application is the list of its object type.
    private static CelType? Resolve(ClrMapping mapping, TypeIndex? types)
    {
        if (mapping.Type is not null || types is null)
        {
            return mapping.Type;
        }

        if (ConversionRules.IsApplicationObject(mapping.ClrType))
        {
            return types.TryGetByClrType(mapping.ClrType, out CelObjectType? described) ? described.Type : null;
        }

        return ConversionRules.ApplicationElementOf(mapping.ClrType) is { } element && types.TryGetByClrType(element, out CelObjectType? elementType)
            ? CelType.ListOf(elementType.Type)
            : null;
    }
}
