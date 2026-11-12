using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Great.Parts.Cel.Values;

// The type index of an environment: the object types of its type sources by full CEL name, the constants of their
// enumeration types by full name, and the descriptions that have a .NET type by that type. Its keys come from the
// declarations of the environment, never from an expression or from data; names of an expression are only looked
// up, also as spans. Build creates it after the checks, so names and .NET types are unique in it.
internal sealed class TypeIndex
{
    internal static readonly TypeIndex Empty = new(
        new Dictionary<string, CelObjectType>(StringComparer.Ordinal),
        new Dictionary<string, long>(StringComparer.Ordinal),
        new Dictionary<Type, CelObjectType>());

    private readonly FrozenDictionary<string, CelObjectType> _objectTypes;
    private readonly FrozenDictionary<string, CelObjectType>.AlternateLookup<ReadOnlySpan<char>> _objectTypesBySpan;
    private readonly FrozenDictionary<string, long> _enumConstants;
    private readonly FrozenDictionary<string, long>.AlternateLookup<ReadOnlySpan<char>> _enumConstantsBySpan;
    private readonly FrozenDictionary<Type, CelObjectType> _byClrType;

    internal TypeIndex(
        Dictionary<string, CelObjectType> objectTypes,
        Dictionary<string, long> enumConstants,
        Dictionary<Type, CelObjectType> byClrType)
    {
        _objectTypes = objectTypes.ToFrozenDictionary(StringComparer.Ordinal);
        _objectTypesBySpan = _objectTypes.GetAlternateLookup<ReadOnlySpan<char>>();
        _enumConstants = enumConstants.ToFrozenDictionary(StringComparer.Ordinal);
        _enumConstantsBySpan = _enumConstants.GetAlternateLookup<ReadOnlySpan<char>>();
        _byClrType = byClrType.ToFrozenDictionary();
    }

#if NET11_0_OR_GREATER
    private TypeIndex(TypeIndex other, bool decimalDeclared)
    {
        _objectTypes = other._objectTypes;
        _objectTypesBySpan = other._objectTypesBySpan;
        _enumConstants = other._enumConstants;
        _enumConstantsBySpan = other._enumConstantsBySpan;
        _byClrType = other._byClrType;
        DecimalDeclared = decimalDeclared;
    }

    // Whether the environment has the set decimal, which declares the type: true exactly when a surviving set has an abstract type of
    // the kind Decimal. The checker reads it for the fields of the type decimal and the nodes that read a value of the kind Decimal
    // for the values that the environment cannot have. A type index that no environment made has it false.
    internal bool DecimalDeclared { get; }

    // This index with the flag set; the same instance when it has the value already, so the shared empty index stays false.
    internal TypeIndex WithDecimalDeclared(bool decimalDeclared) =>
        decimalDeclared == DecimalDeclared ? this : new TypeIndex(this, decimalDeclared);
#endif

    // The object types by full name, for the identifiers of type checking.
    internal FrozenDictionary<string, CelObjectType> ObjectTypes => _objectTypes;

    internal bool TryGetObjectType(string name, [NotNullWhen(true)] out CelObjectType? objectType) =>
        _objectTypes.TryGetValue(name, out objectType);

    internal bool TryGetObjectType(ReadOnlySpan<char> name, [NotNullWhen(true)] out CelObjectType? objectType) =>
        _objectTypesBySpan.TryGetValue(name, out objectType);

    // A constant of an enumeration type by its full name <type>.<constant>, with its int value.
    internal bool TryGetEnumConstant(string name, out long value) => _enumConstants.TryGetValue(name, out value);

    internal bool TryGetEnumConstant(ReadOnlySpan<char> name, out long value) => _enumConstantsBySpan.TryGetValue(name, out value);

    // The description whose .NET type is exactly this type: the expected type of a result type.
    internal bool TryGetByClrType(Type type, [NotNullWhen(true)] out CelObjectType? description) =>
        _byClrType.TryGetValue(type, out description);

    // The description of a bare instance: of its exact .NET type, else of its nearest base class that has one;
    // interfaces are not considered. Null when no source describes the instance.
    internal CelObjectType? Describe(object instance)
    {
        if (_byClrType.Count == 0)
        {
            return null;
        }

        for (Type? type = instance.GetType(); type is not null; type = type.BaseType)
        {
            if (_byClrType.TryGetValue(type, out CelObjectType? description))
            {
                return description;
            }
        }

        return null;
    }
}
