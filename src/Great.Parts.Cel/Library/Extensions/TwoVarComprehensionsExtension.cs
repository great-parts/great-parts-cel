using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set of two-variable comprehensions, version 0: the macros all, exists, existsOne, exists_one, transformList,
// transformMap and transformMapEntry with the names of two iteration variables (in Syntax), and the two overloads of cel.@mapInsert
// that transformMap and transformMapEntry expand to. No text can write the name of the function. In the step of a macro the planner
// puts the pairs into the map builder of the accumulator itself (MapInsertPlanNode); the implementations here serve a call anywhere
// else, which only a tree built by the library can contain. They copy the first map, which the
// table of costs does not charge, and charge each pair put in: 2, and ⌈L/10⌉ more for a string key of length L, before the pair is in.
internal static class TwoVarComprehensionsExtension
{
    internal const string SetName = "two-var-comprehensions";
    internal const uint Version = 0;
    private const string Source = "extension two-var-comprehensions v0";

    // The key is kept by ErrorValueParts and written as a CEL literal, as the duplicate keys of a map literal are.
    private static readonly Func<CelError, string> DuplicateMapKeyText = static error =>
    {
        var text = new StringBuilder("duplicate map key: ");
        Printer.AppendConstant(text, ErrorValueParts.ValueOf(error));
        return text.ToString();
    };

    // The definition of the version, or null for a version the set does not have. The set brings the seven macros.
    internal static CatalogExtension? Create(uint version) =>
        version == Version
            ? new CatalogExtension(SetName, Version, Overloads(), macros: ExtensionMacros.TwoVariableComprehensions)
            : null;

    // cel.@mapInsert(m, k, v), (map(K, V), K, V) -> map(K, V): the map and the pair, in the order of the map and then the pair.
    internal static CelValue InsertPair(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetMap(out CelMap? map))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (!TryCopy(map, out MapBuilder? builder, out CelValue failure, in site))
        {
            return failure;
        }

        CelValue? error = Insert(builder, arguments[1], arguments[2], in site);
        if (error is { } failed)
        {
            builder.Release();
            return failed;
        }

        return CelValue.FromMap(builder.ToMap());
    }

    // cel.@mapInsert(m, n), (map(K, V), map(K, V)) -> map(K, V): the pairs of m and then those of n, in their order.
    internal static CelValue InsertMap(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetMap(out CelMap? map) || !arguments[1].TryGetMap(out CelMap? other))
        {
            return site.NoMatchingOverload(arguments);
        }

        if (!TryCopy(map, out MapBuilder? builder, out CelValue failure, in site))
        {
            return failure;
        }

        // As the range of a comprehension: a map with an error among its keys fails at once, and a view that is sorted pays for the
        // sort before its first pair.
        if (other.TryGetKeyError(site.Meter, out CelValue keyError, leadingUnit: true))
        {
            builder.Release();
            return site.Placed(keyError);
        }

        if (site.Meter is { } meter)
        {
            other.ChargeEnumeration(meter);
        }

        // An empty map gives no pair: no enumerator is made for it.
        if (other.Count == 0)
        {
            return CelValue.FromMap(builder.ToMap());
        }

        using IEnumerator<KeyValuePair<CelValue, CelValue>> entries = other.EnumerateEntries(site.Meter, readValues: true);
        while (entries.MoveNext())
        {
            KeyValuePair<CelValue, CelValue> entry = entries.Current;
            if (entry.Value.Kind == CelValueKind.Error)
            {
                builder.Release();
                return site.Placed(entry.Value);
            }

            if (Insert(builder, entry.Key, entry.Value, in site) is { } error)
            {
                builder.Release();
                return error;
            }
        }

        return CelValue.FromMap(builder.ToMap());
    }

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType key = CelType.TypeParameter("K");
        CelType value = CelType.TypeParameter("V");
        CelType map = CelType.MapOf(key, value);
        return
        [
            Function("@mapInsert_map_key_value", [map, key, value], map, InsertPair),
            Function("@mapInsert_map_map", [map, map], map, InsertMap),
        ];
    }

    // A global overload without a unit of its own: the pairs are charged as they are put in.
    private static OverloadBinding Function(string overloadId, ImmutableArray<CelType> parameters, CelType result, OverloadImplementation implementation) =>
        new(ExtensionMacros.MapInsertFunction, new CelOverloadDecl(overloadId, false, parameters, result), implementation, 0, null, Source);

    // A builder with the pairs of the map, which the call does not charge. An error value that a pair of a view is, is the outcome.
    private static bool TryCopy(CelMap source, [NotNullWhen(true)] out MapBuilder? builder, out CelValue failure, in CallSite site)
    {
        builder = null;
        failure = default;
        if (source.TryGetKeyError(null, out CelValue keyError))
        {
            failure = site.Placed(keyError);
            return false;
        }

        var copy = new MapBuilder();
        using IEnumerator<KeyValuePair<CelValue, CelValue>> entries = source.EnumerateEntries(null, readValues: true);
        while (entries.MoveNext())
        {
            KeyValuePair<CelValue, CelValue> entry = entries.Current;
            if (entry.Value.Kind == CelValueKind.Error)
            {
                copy.Release();
                failure = site.Placed(entry.Value);
                return false;
            }

            _ = copy.TryAdd(entry.Key, entry.Value);
        }

        builder = copy;
        return true;
    }

    // Puts one pair in: a key of a kind that is no map key is UnsupportedMapKeyType, then the pair is charged, then a key equal
    // to one that is in is DuplicateMapKey. Null when the pair is in.
    private static CelValue? Insert(MapBuilder builder, CelValue key, CelValue value, in CallSite site)
    {
        if (!KeyHashRules.IsKeyKind(key.Kind))
        {
            return site.Error(CelErrorCode.UnsupportedMapKeyType, "unsupported map key type '" + ValueTypeRules.NameOf(key, site.Types) + "'");
        }

        site.Charge(key.TryGetString(out string? text) ? 2 + Costs.Tenths((ulong)text.Length) : 2);
        return builder.TryAdd(key, value) ? null : site.ErrorWithValue(CelErrorCode.DuplicateMapKey, DuplicateMapKeyText, null, key);
    }
}
