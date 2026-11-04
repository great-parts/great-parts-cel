using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

// An implementation registered in a builder: the delegate of the application, the implementation made of
// it, and for a typed delegate the CEL types of its .NET types, which the build compares with the declared
// overload: of a parameter by the expected-type table, as for Compile<T>; of the result by the input rows. A .NET
// reference type whose CEL type is P, string or byte[], fits a declared wrapper(P) too, since it takes null.
internal sealed class ImplementationEntry
{
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
        bool resultTakesNull)
        : this(overloadId, original, implementation)
    {
        ParameterTypes = parameterTypes;
        ResultType = resultType;
        ParameterTakesNull = parameterTakesNull;
        ResultTakesNull = resultTakesNull;
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

    internal bool IsTyped => !ParameterTypes.IsDefault;

    // The same delegate registered twice under one identifier is one implementation.
    internal bool SameAs(ImplementationEntry other) =>
        string.Equals(OverloadId, other.OverloadId, StringComparison.Ordinal) && ReferenceEquals(Original, other.Original);
}
