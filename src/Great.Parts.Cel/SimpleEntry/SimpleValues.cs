using System;
using System.Numerics;
using System.Text.Json;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

// The only code that turns a value of the simple entry into a CEL value and its CEL type (docs/type-mapping.md, "Data of the simple entry"). The
// scalar rows are a closed list of `is` checks, in the order of the rows; a list or a dictionary with string keys goes through the
// same function as the indexer of CelBindings, the lists and dictionaries of the input table. A value that no row names is not
// converted: the type of the value, not part of the data, is the error. No reflection, no MakeGenericType, no Activator.
internal static class SimpleValues
{
    private static readonly CelType ListOfDyn = CelType.ListOf(CelType.Dyn);

    private static readonly CelType MapOfDyn = CelType.MapOf(CelType.Dyn, CelType.Dyn);

    internal enum Outcome
    {
        // The value is converted.
        Converted,

        // The type of the value is outside the closed list of values.
        Outside,

        // The value is of a listed type, but the row refuses it: a DateTime that is not UTC, an undefined JsonElement, a lone surrogate
        // as a char, an integer wider than 64 bits out of range.
        Refused,
    }

    // Converts a value by its run-time type. `type` is the CEL type of the variable that the value declares.
    internal static Outcome TryConvert(object? value, out CelValue converted, out CelType type)
    {
        CelType? scalar = ScalarType(value);
        if (scalar is not null)
        {
            converted = ConversionRules.FromRuntimeValue(value);
            type = scalar;
            return ConversionRules.IsRefusedByBindings(converted) ? Outcome.Refused : Outcome.Converted;
        }

        if (value is not null && ConversionRules.TryFromRuntimeCollection(value, out converted) && CollectionType(converted) is { } collection)
        {
            type = collection;
            return Outcome.Converted;
        }

        converted = default;
        type = CelType.Dyn;
        return Outcome.Outside;
    }

    // The scalar rows of the closed list in order; byte[] is bytes, checked before any list. null is a variable of the type dyn.
    private static CelType? ScalarType(object? value) => value switch
    {
        null => CelType.Dyn,
        bool => CelType.Bool,
        long or int or short or sbyte or Int128 or BigInteger => CelType.Int,
        ulong or uint or ushort or byte or UInt128 => CelType.UInt,
        double => CelType.Double,
        string or Guid or char => CelType.String,
        byte[] or ReadOnlyMemory<byte> => CelType.Bytes,
        DateTimeOffset or DateTime => CelType.Timestamp,
        TimeSpan => CelType.Duration,
        JsonElement or CelValue => CelType.Dyn,
        CelList => ListOfDyn,
        CelMap => MapOfDyn,
        _ => null,
    };

    // The CEL type of a list or a map that the input rows converted: the type of its row.
    private static CelType? CollectionType(CelValue converted)
    {
        if (converted.TryGetList(out CelList? list))
        {
            return list.RecordedType;
        }

        return converted.TryGetMap(out CelMap? map) ? map.RecordedType : null;
    }
}
