using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The size S of a structure for the cost of its read ("Charges of a read" of the page of the created code): the sum of the sizes of
// all instance fields, private ones included, without padding, so it is an estimate of the box and not a measurement. The sizes of
// the fields: 1 byte for bool, byte and sbyte; 2 for short, ushort, char and Half; 4 for int, uint and float; 8 for long, ulong,
// double, nint, nuint, DateTime, TimeSpan and every reference type; 16 for Guid, decimal, Decimal128, DateTimeOffset, Int128 and
// UInt128; an enumeration by its underlying type; Nullable<X> as X and one byte more; a field that is a structure by its own size,
// at any depth, also a structure of a referenced assembly. An empty structure is 1.
internal static class StructSizes
{
    private const long Limit = int.MaxValue;

    // The size of a structure in bytes, at least 1 and at most int.MaxValue.
    internal static int Of(ITypeSymbol type) => (int)System.Math.Min(SizeOf(type), Limit);

    private static long SizeOf(ITypeSymbol type)
    {
        long known = Known(type);
        if (known > 0)
        {
            return known;
        }

        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType: { } underlying })
        {
            return SizeOf(underlying);
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return SizeOf(nullable.TypeArguments[0]) + 1;
        }

        return type.IsValueType ? Fields(type) : 8;
    }

    private static long Fields(ITypeSymbol type)
    {
        long sum = 0;
        foreach (ISymbol member in type.GetMembers())
        {
            if (member is IFieldSymbol { IsStatic: false } field)
            {
                sum += FieldSize(field);
                if (sum > Limit)
                {
                    return Limit;
                }
            }
        }

        return sum == 0 ? 1 : sum;
    }

    // A fixed-size buffer is its element type, FixedSize times.
    private static long FieldSize(IFieldSymbol field)
    {
        if (field.IsFixedSizeBuffer && field.Type is IPointerTypeSymbol pointer)
        {
            return SizeOf(pointer.PointedAtType) * field.FixedSize;
        }

        return SizeOf(field.Type);
    }

    private static long Known(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
                return 1;
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Char:
                return 2;
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Single:
                return 4;
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Double:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
            case SpecialType.System_DateTime:
                return 8;
            case SpecialType.System_Decimal:
                return 16;
            default:
                return type is INamedTypeSymbol named ? ByName(TypeNames.FullName(named)) : 0;
        }
    }

    private static long ByName(string fullName) => fullName switch
    {
        "System.Half" => 2,
        "System.TimeSpan" => 8,
        "System.Guid" or WellKnown.Decimal128 or "System.DateTimeOffset" or "System.Int128" or "System.UInt128" => 16,
        _ => 0,
    };
}
