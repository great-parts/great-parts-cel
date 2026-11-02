using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// Whether argument values fit the parameter types of an overload, by the kinds of the values: dyn and
// type parameters fit any kind, list(…) fits a list, map(…) fits a map, and a primitive type fits a
// value of the same kind.
internal static class KindMatch
{
    internal static bool Fits(ReadOnlySpan<CelValue> arguments, ImmutableArray<CelType> parameterTypes)
    {
        for (int i = 0; i < arguments.Length; i++)
        {
            if (!Fits(arguments[i].Kind, parameterTypes[i].Kind))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Fits(CelValueKind value, CelTypeKind parameter) => parameter switch
    {
        CelTypeKind.Dyn or CelTypeKind.TypeParameter => true,
        CelTypeKind.Null => value == CelValueKind.Null,
        CelTypeKind.Bool => value == CelValueKind.Bool,
        CelTypeKind.Int => value == CelValueKind.Int,
        CelTypeKind.UInt => value == CelValueKind.UInt,
        CelTypeKind.Double => value == CelValueKind.Double,
        CelTypeKind.String => value == CelValueKind.String,
        CelTypeKind.Bytes => value == CelValueKind.Bytes,
        CelTypeKind.List => value == CelValueKind.List,
        CelTypeKind.Map => value == CelValueKind.Map,
        _ => throw new NotImplementedException(),
    };
}
