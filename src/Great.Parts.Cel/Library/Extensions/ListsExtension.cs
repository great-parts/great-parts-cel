using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions;

// The table of the extension set lists, version 4: 22 overloads with the identifiers of cel-go, each with its implementation and the
// units that the call node charges before the function runs. The set brings the macro sortBy, which expands to a call of
// @sortByAssociatedKeys: no text can write that name. The functions are in ListFunctions and ListSort.
internal static class ListsExtension
{
    internal const string SetName = "lists";
    internal const uint Version = 4;
    private const string Source = "extension lists v4";

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, Overloads(), macros: ExtensionMacros.Lists) : null;

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType element = CelType.TypeParameter("T");
        CelType elements = CelType.ListOf(element);
        CelType @int = CelType.Int;
        ImmutableArray<OverloadBinding>.Builder table = ImmutableArray.CreateBuilder<OverloadBinding>(22);
        table.Add(new OverloadBinding(
            "slice",
            new CelOverloadDecl("list_slice", true, [elements, @int, @int], elements),
            ListFunctions.Slice,
            1,
            static arguments => ListFunctions.SliceSize(arguments),
            Source));

        // The two forms of flatten read the elements themselves, which a function of any depth cannot declare: the planner checks no
        // element at depth, and a list that is already flat is returned as it is.
        table.Add(new OverloadBinding(
            "flatten",
            new CelOverloadDecl("list_flatten", true, [CelType.ListOf(elements)], elements),
            ListFunctions.Flatten,
            1,
            Costs.ListSizes,
            Source,
            shallowArguments: true));
        CelType values = CelType.ListOf(CelType.Dyn);
        table.Add(new OverloadBinding(
            "flatten",
            new CelOverloadDecl("list_flatten_int", true, [values, @int], values),
            ListFunctions.Flatten,
            1,
            Costs.ListSizes,
            Source,
            shallowArguments: true));
        foreach ((CelType type, string name, bool text) in Comparable())
        {
            table.Add(new OverloadBinding(
                "sort",
                new CelOverloadDecl("list_" + name + "_sort", true, [CelType.ListOf(type)], CelType.ListOf(type)),
                ListSort.Sort,
                1,
                text ? ListSort.SortTextUnits : ListSort.SortUnits,
                Source));
        }

        foreach ((CelType type, string name, bool text) in Comparable())
        {
            table.Add(new OverloadBinding(
                "@sortByAssociatedKeys",
                new CelOverloadDecl("list_" + name + "_sortByAssociatedKeys", true, [elements, CelType.ListOf(type)], elements),
                ListSort.SortByKeys,
                1,
                text ? ListSort.SortByTextKeysUnits : ListSort.SortByKeysUnits,
                Source));
        }

        table.Add(new OverloadBinding(
            "lists.range",
            new CelOverloadDecl("lists_range", false, [@int], CelType.ListOf(@int)),
            ListFunctions.Range,
            1,
            static arguments => ListFunctions.RangeSize(arguments),
            Source));
        table.Add(new OverloadBinding(
            "reverse",
            new CelOverloadDecl("list_reverse", true, [elements], elements),
            ListFunctions.Reverse,
            1,
            Costs.ListSizes,
            Source));
        table.Add(new OverloadBinding(
            "distinct",
            new CelOverloadDecl("list_distinct", true, [elements], elements),
            ListFunctions.Distinct,
            1,
            Costs.ListSizes,
            Source));
        return table.MoveToImmutable();
    }

    // The eight comparable types of the set, in the order of cel-go, by the type name of its identifiers; the strings and the bytes pay
    // for the length of the longest element.
    private static (CelType Type, string Name, bool Text)[] Comparable() =>
    [
        (CelType.Int, "int", false),
        (CelType.UInt, "uint", false),
        (CelType.Double, "double", false),
        (CelType.Bool, "bool", false),
        (CelType.Duration, "google.protobuf.Duration", false),
        (CelType.Timestamp, "google.protobuf.Timestamp", false),
        (CelType.String, "string", true),
        (CelType.Bytes, "bytes", true),
    ];
}
