using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The scalar rows of the input table of the type mapping, by the special type or by the full name of the type. A type from
// another assembly with the same full name is the same row.
internal static class ScalarKinds
{
    private static readonly Dictionary<SpecialType, ScalarKind> Special = new()
    {
        [SpecialType.System_Int64] = ScalarKind.Int,
        [SpecialType.System_Int32] = ScalarKind.Int,
        [SpecialType.System_Int16] = ScalarKind.Int,
        [SpecialType.System_SByte] = ScalarKind.Int,
        [SpecialType.System_IntPtr] = ScalarKind.Int,
        [SpecialType.System_UInt64] = ScalarKind.UInt,
        [SpecialType.System_UInt32] = ScalarKind.UInt,
        [SpecialType.System_UInt16] = ScalarKind.UInt,
        [SpecialType.System_Byte] = ScalarKind.UInt,
        [SpecialType.System_UIntPtr] = ScalarKind.UInt,
        [SpecialType.System_Double] = ScalarKind.Double,
        [SpecialType.System_Single] = ScalarKind.Double,
        [SpecialType.System_Boolean] = ScalarKind.Bool,
        [SpecialType.System_String] = ScalarKind.String,
        [SpecialType.System_Decimal] = ScalarKind.Decimal,
        [SpecialType.System_Object] = ScalarKind.Object,
        [SpecialType.System_DateTime] = ScalarKind.DateTime,
    };

    private static readonly Dictionary<string, ScalarKind> Names = new(StringComparer.Ordinal)
    {
        ["System.Half"] = ScalarKind.Double,
        ["System.Guid"] = ScalarKind.Guid,
        ["System.DateTimeOffset"] = ScalarKind.DateTimeOffset,
        ["System.TimeSpan"] = ScalarKind.TimeSpan,
        [WellKnown.Decimal128] = ScalarKind.Decimal128,
        ["System.Text.Json.JsonElement"] = ScalarKind.JsonElement,
        ["Great.Parts.Cel.Values.CelTimestamp"] = ScalarKind.Timestamp,
        ["Great.Parts.Cel.Values.CelDuration"] = ScalarKind.Duration,
        ["Great.Parts.Cel.Values.CelValue"] = ScalarKind.CelValue,
        ["Great.Parts.Cel.Values.CelList"] = ScalarKind.CelList,
        ["Great.Parts.Cel.Values.CelMap"] = ScalarKind.CelMap,
    };

    internal static ScalarKind? Of(INamedTypeSymbol type)
    {
        if (Special.TryGetValue(type.SpecialType, out ScalarKind special))
        {
            return special;
        }

        string fullName = TypeNames.FullName(type);
        if (Names.TryGetValue(fullName, out ScalarKind named))
        {
            return named;
        }

        bool bytes = fullName == WellKnown.ReadOnlyMemory && type.TypeArguments[0].SpecialType == SpecialType.System_Byte;
        return bytes ? ScalarKind.BytesMemory : null;
    }
}
