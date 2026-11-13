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
    private static readonly CelType AnyOptional = new(CelTypeKind.Optional, "optional_type", [CelType.Dyn]);

    // The parameter types reduced to their top level, which Matches checks for an unmarked argument
    // without descending and without charging.
    private readonly ImmutableArray<CelType> _topLevelTypes;
    private readonly bool _isIndexList;
    private readonly bool _isOptionalIndex;

    internal OverloadBinding(
        string function,
        CelOverloadDecl declaration,
        OverloadImplementation? implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost,
        string source,
        bool isApplication = false,
        bool dynOnly = false,
        bool shallowArguments = false,
        CallValidator? validator = null,
        ConstantArgument? constantArgument = null)
    {
        Function = function;
        Declaration = declaration;
        Implementation = implementation;
        FixedCost = fixedCost;
        ArgumentCost = argumentCost;
        Source = source;
        IsApplication = isApplication;
        DynOnly = dynOnly;
        ShallowArguments = shallowArguments;
        Validator = validator;
        ConstantArgument = constantArgument;
        _topLevelTypes = TopLevel(declaration.ParameterTypes);
        _isIndexList = !isApplication && string.Equals(declaration.OverloadId, IndexListId, StringComparison.Ordinal);
        _isOptionalIndex = !isApplication && IsOptionalIndex(declaration.OverloadId);
    }

    // The six overloads of indexes of the set optional: what a call does is decided by the kinds of its values, as the optional
    // qualifiers of cel-go decide it, so the key of any kind fits and the implementation reads it.
    private static bool IsOptionalIndex(string overloadId) =>
        overloadId is "list_optindex_optional_int"
            or "optional_list_optindex_optional_int"
            or "map_optindex_optional_value"
            or "optional_map_optindex_optional_value"
            or "optional_list_index_int"
            or "optional_map_index_value";

    // The same binding with another declaration of the same overload, whose types the environment resolved.
    internal OverloadBinding WithDeclaration(CelOverloadDecl declaration) =>
        ReferenceEquals(declaration, Declaration)
            ? this
            : new(Function, declaration, Implementation, FixedCost, ArgumentCost, Source, IsApplication, DynOnly, ShallowArguments, Validator, ConstantArgument);

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

    // Whether the planner marks no argument for the check at depth, whatever its static type: Matches checks the top level of
    // each argument and the implementation reads the elements itself. False for every overload of the standard library.
    internal bool ShallowArguments { get; }

    // The check of a call at compilation that the checker runs after the traversal of the tree; null for none, as for every
    // overload of the standard library.
    internal CallValidator? Validator { get; }

    // The argument that is prepared once when a call has a string literal there; null for every overload but matches and
    // matches_string. An overload of the application never has one.
    internal ConstantArgument? ConstantArgument { get; }

    // Whether argument values fit the declared parameter types: the top level of every argument, and at
    // any depth each argument that the planner marked, charging the descent to the meter. An argument
    // beyond the marks is unmarked. Type parameters fit any value. The type index of the environment describes object
    // values for object parameter types. An error value that an element of a marked argument is, is the outcome, the
    // first in the order of the arguments and of the walk: it is not a mismatch, and the caller returns it.
    internal TypeFit Matches(ReadOnlySpan<CelValue> arguments, ReadOnlySpan<bool> marks, IEvaluationMeter? meter, TypeIndex? types = null)
    {
        ImmutableArray<CelType> parameterTypes = Declaration.ParameterTypes;
        for (int i = 0; i < arguments.Length; i++)
        {
            CelValue argument = arguments[i];
            if (_isIndexList && i == 1)
            {
                if (argument.Kind is not (CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double))
                {
                    return TypeFit.No;
                }

                continue;
            }

            if (_isOptionalIndex && i == 1)
            {
                continue;
            }

            bool marked = i < marks.Length && marks[i];
            TypeFit fit = ValueTypeRules.Fits(argument, marked ? parameterTypes[i] : _topLevelTypes[i], marked ? meter : null, false, types);
            if (!fit.IsFit)
            {
                return fit;
            }
        }

        return TypeFit.Yes;
    }

    // list(E), map(K, V) and optional_type(E) with dyn parameters, so that Fits does not descend; other types are their own
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
                CelTypeKind.Optional => AnyOptional,
                _ => parameterType,
            });
        }

        return topLevel.MoveToImmutable();
    }
}
