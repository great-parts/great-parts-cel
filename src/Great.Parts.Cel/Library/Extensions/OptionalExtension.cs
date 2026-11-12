using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set optional, version 2: the type optional_type(T), the overloads of its functions, the macros optMap and optFlatMap and the
// flag of the optional syntax. Version 2 is the only version of the catalog. The overloads of the operators — the selection _?._, the
// indexes _[?_] and _[_] over an optional — are those of OptionalOperators, in the table between orValue and first.
internal static class OptionalExtension
{
    internal const string SetName = "optional";
    internal const uint Version = 2;

    // The name of the abstract type that the set declares; the set that declares it is the hint of TypeNotDeclared.
    internal const string TypeName = "optional_type";

    // The identifiers of the two overloads that the planner evaluates itself, by the candidates of the call.
    internal const string OrId = "optional_or_optional";
    internal const string OrValueId = "optional_orValue_value";

    private const string Source = "extension optional v2";

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version
            ? new CatalogExtension(
                SetName,
                Version,
                Overloads(),
                abstractTypes: [new AbstractTypeDeclaration(TypeName, CelType.OptionalOf(CelType.Dyn))],
                macros: ExtensionMacros.Optional,
                optionalSyntax: true)
            : null;

    // The set that declares the abstract type of a full name; null for a name that this set does not declare.
    internal static string? SetOfType(string typeName) => string.Equals(typeName, TypeName, StringComparison.Ordinal) ? SetName : null;

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType element = CelType.TypeParameter("V");
        CelType optional = CelType.OptionalOf(element);
        CelType elements = CelType.ListOf(element);
        CelType optionals = CelType.ListOf(optional);
        ImmutableArray<OverloadBinding>.Builder table = ImmutableArray.CreateBuilder<OverloadBinding>(18);
        table.Add(Function("optional.of", "optional_of", false, [element], optional, OptionalFunctions.Of));
        table.Add(Function("optional.ofNonZeroValue", "optional_ofNonZeroValue", false, [element], optional, OptionalFunctions.OfNonZeroValue));
        table.Add(Function("optional.none", "optional_none", false, [], optional, OptionalFunctions.None));
        table.Add(Function("value", "optional_value", true, [optional], element, OptionalFunctions.Value));
        table.Add(Function("hasValue", "optional_hasValue", true, [optional], CelType.Bool, OptionalFunctions.HasValue));

        // or and orValue are special forms of the planner, like conditional: no implementation, and the node charges the 1 unit after
        // the receiver, before the argument.
        table.Add(Function("or", OrId, true, [optional, optional], optional, null));
        table.Add(Function("orValue", OrValueId, true, [optional, element], element, null));

        // The seven overloads of the operators, between orValue and first.
        table.AddRange(OptionalOperators.Overloads(Source));
        table.Add(Function("first", "list_first", true, [elements], optional, OptionalFunctions.First));
        table.Add(Function("last", "list_last", true, [elements], optional, OptionalFunctions.Last));
        table.Add(new OverloadBinding(
            "optional.unwrap",
            new CelOverloadDecl("optional_unwrap", false, [optionals], elements),
            OptionalFunctions.Unwrap,
            1,
            Costs.ListSizes,
            Source));
        table.Add(new OverloadBinding(
            "unwrapOpt",
            new CelOverloadDecl("optional_unwrapOpt", true, [optionals], elements),
            OptionalFunctions.Unwrap,
            1,
            Costs.ListSizes,
            Source));
        return table.MoveToImmutable();
    }

    // An overload of 1 unit, whose cost beyond the unit is charged by the implementation or the node.
    private static OverloadBinding Function(
        string function,
        string overloadId,
        bool receiverStyle,
        ImmutableArray<CelType> parameters,
        CelType result,
        OverloadImplementation? implementation) =>
        new(function, new CelOverloadDecl(overloadId, receiverStyle, parameters, result), implementation, 1, null, Source);
}
