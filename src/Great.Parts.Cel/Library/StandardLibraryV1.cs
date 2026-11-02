using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library;

// The table of the standard library, version 1. It never changes: a later version is a new table.
internal static class StandardLibraryV1
{
    internal const string Source = "standard library v1";

    // The overloads in the order of cel-go. Costs are units of the cost table: a call of constant time
    // without allocation is 1 unit; a logical operator costs nothing beyond the operands it evaluates.
    internal static ImmutableArray<OverloadBinding> Overloads { get; } =
    [
        Global("_||_", OverloadIds.LogicalOr, [CelType.Bool, CelType.Bool], CelType.Bool, null, 0),
        Global("_+_", OverloadIds.AddInt64, [CelType.Int, CelType.Int], CelType.Int, IntArithmetic.Add, 1),
        Global("_/_", OverloadIds.DivideInt64, [CelType.Int, CelType.Int], CelType.Int, IntArithmetic.Divide, 1),
        Global("-_", OverloadIds.NegateInt64, [CelType.Int], CelType.Int, IntArithmetic.Negate, 1),
    ];

    // The overloads of each function, in the order of the table.
    internal static FrozenDictionary<string, ImmutableArray<OverloadBinding>> ByFunction { get; } =
        Overloads
            .GroupBy(overload => overload.Function, StringComparer.Ordinal)
            .ToFrozenDictionary(group => group.Key, group => group.ToImmutableArray(), StringComparer.Ordinal);

    private static OverloadBinding Global(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation? implementation,
        ulong fixedCost) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, fixedCost, null, Source);
}
