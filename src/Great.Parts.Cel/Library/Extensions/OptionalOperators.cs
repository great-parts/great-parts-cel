using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The operators of the extension set optional, version 2, in the order of its table: the optional selection e.?f, which the planner
// evaluates itself as a special form, and the six overloads of indexes, which are ordinary overloads with implementations.
//
//   select_optional_field                 _?._(dyn, string) -> optional_type(V)
//   list_optindex_optional_int            _[?_](list(V), int) -> optional_type(V)
//   optional_list_optindex_optional_int   _[?_](optional_type(list(V)), int) -> optional_type(V)
//   map_optindex_optional_value           _[?_](map(K, V), K) -> optional_type(V)
//   optional_map_optindex_optional_value  _[?_](optional_type(map(K, V)), K) -> optional_type(V)
//   optional_list_index_int               _[_](optional_type(list(V)), int) -> optional_type(V)
//   optional_map_index_value              _[_](optional_type(map(K, V)), K) -> optional_type(V)
//
// An index with ? gives the empty optional where the index of the standard library is an error — an index outside the list, a key
// that the map does not have — and optional.of(element) where it has one; an element that is an error value is the result. The index
// is read by its kind, as the optional qualifiers of cel-go read it: an int, and also a uint or a double with an integral value, as
// index_list takes them, except that an index outside the list is the empty optional (a uint above the range of int64, a double
// outside the open interval (-2^63, 2^63) and a double that is no integer stay errors, as in cel-go); a key of another kind is
// EvalNoMatchingOverload. A map finds nothing
// for a key of a kind that is no key. The overloads over an optional take a step before: an empty operand gives the empty optional
// for a key of any kind, a non-empty one is read through the held value by the kind of that value, not by the kind that the overload
// names: a held list as a list and a held map as a map, so the four overloads over an optional share one implementation. A held value
// that is neither is EvalNoMatchingOverload, where cel-go fails too (no such key). They cost what index_list and index_map cost for the
// held value, plus the unit of the overload, and 1 for an empty operand.
internal static class OptionalOperators
{
    internal const string SelectFunction = "_?._";
    internal const string OptionalIndexFunction = "_[?_]";
    internal const string IndexFunction = "_[_]";

    internal const string SelectId = "select_optional_field";
    internal const string ListOptIndexId = "list_optindex_optional_int";
    internal const string OptionalListOptIndexId = "optional_list_optindex_optional_int";
    internal const string MapOptIndexId = "map_optindex_optional_value";
    internal const string OptionalMapOptIndexId = "optional_map_optindex_optional_value";
    internal const string OptionalListIndexId = "optional_list_index_int";
    internal const string OptionalMapIndexId = "optional_map_index_value";

    // The units of an index over an optional beyond the unit of the overload: those of the index of the held list or map, by the kind of
    // the held value, and none for an empty optional.
    private static readonly CelCostFunction OptionalIndexCost = static arguments => IndexCostOfHeld(arguments);

    // The seven bindings, with the source that names the set in build errors. The extension puts them where the table of the set has
    // them, after optional_orValue_value and before list_first.
    internal static ImmutableArray<OverloadBinding> Overloads(string source)
    {
        CelType value = CelType.TypeParameter("V");
        CelType key = CelType.TypeParameter("K");
        CelType @int = CelType.Int;
        CelType list = CelType.ListOf(value);
        CelType map = CelType.MapOf(key, value);
        CelType optionalList = CelType.OptionalOf(list);
        CelType optionalMap = CelType.OptionalOf(map);
        CelType result = CelType.OptionalOf(value);
        return
        [
            // A special form like conditional: the planner evaluates it, and its cost is that of a selection. The name of the field
            // has to be a string literal, which the parser always writes.
            new OverloadBinding(SelectFunction, new CelOverloadDecl(SelectId, false, [CelType.Dyn, CelType.String], result), null, 0, null, source, validator: ValidateSelect),
            new OverloadBinding(OptionalIndexFunction, new CelOverloadDecl(ListOptIndexId, false, [list, @int], result), ListOptIndex, 1, Costs.IndexList, source),
            new OverloadBinding(OptionalIndexFunction, new CelOverloadDecl(OptionalListOptIndexId, false, [optionalList, @int], result), OptionalIndex, 1, OptionalIndexCost, source),
            new OverloadBinding(OptionalIndexFunction, new CelOverloadDecl(MapOptIndexId, false, [map, key], result), MapOptIndex, 1, Costs.IndexKey, source),
            new OverloadBinding(OptionalIndexFunction, new CelOverloadDecl(OptionalMapOptIndexId, false, [optionalMap, key], result), OptionalIndex, 1, OptionalIndexCost, source),
            new OverloadBinding(IndexFunction, new CelOverloadDecl(OptionalListIndexId, false, [optionalList, @int], result), OptionalIndex, 1, OptionalIndexCost, source),
            new OverloadBinding(IndexFunction, new CelOverloadDecl(OptionalMapIndexId, false, [optionalMap, key], result), OptionalIndex, 1, OptionalIndexCost, source),
        ];
    }

    // select_optional_field: the second argument has to be a string literal; any other argument is NoMatchingOverload at the call, with
    // the usual text. The parser never builds one, a tree made by the library can.
    internal static CallValidation ValidateSelect(CallShape call) =>
        call.Arguments[1].StringConstant is not null
            ? CallValidation.Ok
            : new CallValidation(CelErrorCode.NoMatchingOverload, string.Empty, null);

    // list_optindex_optional_int, (list(V), int) -> optional_type(V).
    private static CelValue ListOptIndex(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetList(out CelList? list)
            ? ReadList(list, arguments, in site)
            : site.NoMatchingOverload(arguments);

    // map_optindex_optional_value, (map(K, V), K) -> optional_type(V).
    private static CelValue MapOptIndex(ReadOnlySpan<CelValue> arguments, in CallSite site) =>
        arguments[0].TryGetMap(out CelMap? map)
            ? ReadMap(map, arguments[1], in site)
            : site.NoMatchingOverload(arguments);

    // The four overloads over an optional — optional_list_optindex_optional_int, optional_map_optindex_optional_value,
    // optional_list_index_int and optional_map_index_value — one implementation: the held value is read by its kind, as the optional
    // qualifiers of cel-go read it, whichever of the pair the static type of the operand names. An operand that is no optional is
    // EvalNoMatchingOverload, an empty one the empty optional without a read of the key, a held list or map the plain read, and a held
    // value of another kind EvalNoMatchingOverload.
    private static CelValue OptionalIndex(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetOptional(out CelOptional<CelValue> optional))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (!optional.HasValue)
        {
            return CelValue.FromOptional(default);
        }

        CelValue held = optional.Value;
        if (held.TryGetList(out CelList? list))
        {
            return ReadList(list, arguments, in site);
        }

        return held.TryGetMap(out CelMap? map) ? ReadMap(map, arguments[1], in site) : site.NoMatchingOverload(arguments);
    }

    // The element at the key of the list: an int, or a uint or a double with an integral value as index_list takes them. An index
    // that is no index at all stays an error as in index_list and cel-go: a double that is not integral (NaN and the infinities too)
    // is InvalidArgument, a uint above the range of int64 and a double outside the open interval (-2^63, 2^63) are IndexOutOfRange:
    // the interval is that of double_to_int64, and cel-go refuses -2^63 itself; a key of another kind is EvalNoMatchingOverload.
    private static CelValue ReadList(CelList list, ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        CelValue key = arguments[1];
        if (key.TryGetInt64(out long signed))
        {
            return ReadList(list, signed, in site);
        }

        if (key.TryGetUInt64(out ulong unsigned))
        {
            return unsigned <= long.MaxValue ? ReadList(list, (long)unsigned, in site) : Collections.OutOfRange(in site, key, list);
        }

        if (key.TryGetDouble(out double number))
        {
            // NaN and the infinities are not integral either; -0.0 is the index 0.
            if (!double.IsFinite(number) || Math.Floor(number) != number)
            {
                return site.Error(CelErrorCode.InvalidArgument, Collections.NotAnIntegerText, site.Function, number: BitConverter.DoubleToInt64Bits(number));
            }

            // The open interval, as double_to_int64 has it: -2^63 is a double that cel-go refuses as an index, and 2^63 is the first
            // double above int64.
            return number > -9223372036854775808.0 && number < 9223372036854775808.0
                ? ReadList(list, (long)number, in site)
                : Collections.OutOfRange(in site, key, list);
        }

        return site.NoMatchingOverload(arguments);
    }

    // The element at the index, or the empty optional for a negative index or one past the end, where index_list is an error.
    private static CelValue ReadList(CelList list, long index, in CallSite site) =>
        index >= 0 && index < list.Count
            ? OptionalOf(site.Placed(list.Read((int)index, site.Meter)))
            : CelValue.FromOptional(default);

    // The value of the key by the key lookup, across the numeric kinds, or the empty optional where index_map is NoSuchKey. A JSON
    // object that repeats the name counts the matches: two or more are the error DuplicateMapKey, which the lookup returns as the value.
    private static CelValue ReadMap(CelMap map, CelValue key, in CallSite site) =>
        map.TryRead(key, site.Meter, readValue: true, out CelValue value)
            ? OptionalOf(site.Placed(value))
            : CelValue.FromOptional(default);

    // optional.of(value) for a value, and the error value itself for an error value, which no optional holds.
    private static CelValue OptionalOf(CelValue value) =>
        value.Kind == CelValueKind.Error ? value : CelValue.FromOptional(new CelOptional<CelValue>(value));

    // The argument cost of an overload over an optional: nothing for an empty operand, a value that is no optional and a held value that
    // is neither list nor map, and for a held list or map what the plain overload of its kind answers for the held value and the key.
    private static ulong IndexCostOfHeld(ReadOnlySpan<CelValue> arguments)
    {
        if (arguments.Length != 2 || !arguments[0].TryGetOptional(out CelOptional<CelValue> optional) || !optional.HasValue)
        {
            return 0;
        }

        ReadOnlySpan<CelValue> held = [optional.Value, arguments[1]];
        if (optional.Value.TryGetList(out _))
        {
            return Costs.IndexList(held);
        }

        return optional.Value.TryGetMap(out _) ? Costs.IndexKey(held) : 0;
    }
}
