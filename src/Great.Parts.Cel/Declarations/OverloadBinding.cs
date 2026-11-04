using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// One overload of an environment: its declaration with the code that evaluation calls and the cost of
// a call. Type checking records bindings as the candidates of a call, and evaluation calls them.
internal sealed class OverloadBinding
{
    // The overload of the standard library that also takes an index of the kinds uint and double
    // (cel-spec lists/index/zero_based_*).
    private const string IndexListId = "index_list";

    private static readonly CelType AnyList = CelType.ListOf(CelType.Dyn);
    private static readonly CelType AnyMap = CelType.MapOf(CelType.Dyn, CelType.Dyn);

    // The parameter types reduced to their top level, which Matches checks for an unmarked argument
    // without descending and without charging.
    private readonly ImmutableArray<CelType> _topLevelTypes;
    private readonly bool _isIndexList;

    internal OverloadBinding(
        string function,
        CelOverloadDecl declaration,
        OverloadImplementation? implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost,
        string source,
        bool isApplication = false,
        bool dynOnly = false)
    {
        Function = function;
        Declaration = declaration;
        Implementation = implementation;
        FixedCost = fixedCost;
        ArgumentCost = argumentCost;
        Source = source;
        IsApplication = isApplication;
        DynOnly = dynOnly;
        _topLevelTypes = TopLevel(declaration.ParameterTypes);
        _isIndexList = !isApplication && string.Equals(declaration.OverloadId, IndexListId, StringComparison.Ordinal);
    }

    // The name of the function in the syntax tree, such as _+_.
    internal string Function { get; }

    internal CelOverloadDecl Declaration { get; }

    // Null for a special form that the planner evaluates itself, such as _||_.
    internal OverloadImplementation? Implementation { get; }

    // The cost of a call in units: the fixed part, and the part computed from the arguments, if any.
    internal ulong FixedCost { get; }

    internal CelCostFunction? ArgumentCost { get; }

    // The source that build errors name, such as "standard library v1".
    internal string Source { get; }

    // Whether the overload is declared by the application, whose implementation is application code.
    internal bool IsApplication { get; }

    // Whether the overload is a candidate only when an argument has the static type dyn: the ordering
    // across int, uint and double.
    internal bool DynOnly { get; }

    // Whether argument values fit the declared parameter types: the top level of every argument, and at
    // any depth each argument that the planner marked, charging the descent to the meter. An argument
    // beyond the marks is unmarked. Type parameters fit any value.
    internal bool Matches(ReadOnlySpan<CelValue> arguments, ReadOnlySpan<bool> marks, IEvaluationMeter? meter)
    {
        ImmutableArray<CelType> parameterTypes = Declaration.ParameterTypes;
        for (int i = 0; i < arguments.Length; i++)
        {
            CelValue argument = arguments[i];
            if (_isIndexList && i == 1)
            {
                if (argument.Kind is not (CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double))
                {
                    return false;
                }

                continue;
            }

            bool marked = i < marks.Length && marks[i];
            if (!ValueTypeRules.Fits(argument, marked ? parameterTypes[i] : _topLevelTypes[i], marked ? meter : null))
            {
                return false;
            }
        }

        return true;
    }

    // list(E) and map(K, V) with dyn parameters, so that Fits does not descend; other types are their own
    // top level.
    private static ImmutableArray<CelType> TopLevel(ImmutableArray<CelType> parameterTypes)
    {
        ImmutableArray<CelType>.Builder topLevel = ImmutableArray.CreateBuilder<CelType>(parameterTypes.Length);
        foreach (CelType parameterType in parameterTypes)
        {
            topLevel.Add(parameterType.Kind switch
            {
                CelTypeKind.List => AnyList,
                CelTypeKind.Map => AnyMap,
                _ => parameterType,
            });
        }

        return topLevel.MoveToImmutable();
    }
}
