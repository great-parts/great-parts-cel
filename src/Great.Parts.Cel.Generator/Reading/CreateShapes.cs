using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The creation rows of an object literal: the shape that the created code builds from a CEL value for a member, by the .NET type of
// the member and the shape that the read of the member has (so the closure and the object types are those of the reading). A type
// without a row — an enumeration, object, JsonElement, Decimal128, a set, a collection of collections — gives null: no literal sets
// such a member. A collection is one level deep.
internal static class CreateShapes
{
    internal static CreateShape? Of(ITypeSymbol type, Shape shape, bool belowCollection) => shape switch
    {
        ScalarShape scalar => Scalar(type, scalar),
        NullableShape nullable => OfNullable(type, nullable, belowCollection),
        ObjectShape objectShape => new CreateShape(
            CreateKind.Object,
            TypeNames.ClrName(type, true),
            objectShape.Nullability,
            TypeNames.ClrName(type, false),
            null,
            null,
            type.IsValueType),
        CollectionShape collection when !belowCollection => OfCollection(type, collection),
        _ => null,
    };

    private static CreateShape? OfNullable(ITypeSymbol type, NullableShape shape, bool belowCollection)
    {
        if (type is not INamedTypeSymbol { TypeArguments.Length: 1 } named)
        {
            return null;
        }

        CreateShape? inner = Of(named.TypeArguments[0], shape.Underlying, belowCollection);
        bool collection = inner?.Kind is CreateKind.Array or CreateKind.List or CreateKind.ImmutableArray or CreateKind.Dictionary;
        return inner is null || collection
            ? null
            : new CreateShape(CreateKind.Nullable, TypeNames.ClrName(type, true), Nullability.NotNull, string.Empty, inner, null);
    }

    private static CreateShape? Scalar(ITypeSymbol type, ScalarShape shape)
    {
        string keyword = string.Empty;
        CreateKind? kind = shape.Kind switch
        {
            ScalarKind.Int => KeywordOf(type, SpecialType.System_Int64, "long", SpecialType.System_Int32, "int", SpecialType.System_Int16, "short", SpecialType.System_SByte, "sbyte", out keyword) ? CreateKind.Int : null,
            ScalarKind.UInt => KeywordOf(type, SpecialType.System_UInt64, "ulong", SpecialType.System_UInt32, "uint", SpecialType.System_UInt16, "ushort", SpecialType.System_Byte, "byte", out keyword) ? CreateKind.UInt : null,
            ScalarKind.Double => DoubleKeyword(type, out keyword) ? CreateKind.Double : null,
            ScalarKind.Bool => CreateKind.Bool,
            ScalarKind.String => CreateKind.String,
            ScalarKind.Guid => CreateKind.Guid,
            ScalarKind.Char => CreateKind.Char,
            ScalarKind.Int128 => CreateKind.Int128,
            ScalarKind.UInt128 => CreateKind.UInt128,
            ScalarKind.BigInteger => CreateKind.BigInteger,
            ScalarKind.Date => CreateKind.Date,
            ScalarKind.TimeOfDay => CreateKind.TimeOfDay,
            ScalarKind.Bytes => CreateKind.Bytes,
            ScalarKind.BytesMemory => CreateKind.BytesMemory,
            ScalarKind.Timestamp => CreateKind.Timestamp,
            ScalarKind.DateTimeOffset => CreateKind.DateTimeOffset,
            ScalarKind.DateTime => CreateKind.DateTime,
            ScalarKind.Duration => CreateKind.Duration,
            ScalarKind.TimeSpan => CreateKind.TimeSpan,
            ScalarKind.Decimal => CreateKind.Decimal,
            ScalarKind.CelValue => CreateKind.CelValue,
            ScalarKind.CelList => CreateKind.CelList,
            ScalarKind.CelMap => CreateKind.CelMap,
            _ => null,
        };
        return kind is { } value
            ? new CreateShape(value, TypeNames.ClrName(type, true), shape.Nullability, keyword, null, null)
            : null;
    }

    private static bool KeywordOf(
        ITypeSymbol type,
        SpecialType first,
        string firstKeyword,
        SpecialType second,
        string secondKeyword,
        SpecialType third,
        string thirdKeyword,
        SpecialType fourth,
        string fourthKeyword,
        out string keyword)
    {
        SpecialType special = type.SpecialType;
        keyword = special == first ? firstKeyword
            : special == second ? secondKeyword
            : special == third ? thirdKeyword
            : special == fourth ? fourthKeyword
            : string.Empty;
        return keyword.Length > 0;
    }

    private static bool DoubleKeyword(ITypeSymbol type, out string keyword)
    {
        keyword = type.SpecialType switch
        {
            SpecialType.System_Double => "double",
            SpecialType.System_Single => "float",
            _ => type is INamedTypeSymbol named && TypeNames.FullName(named) == "System.Half" ? "global::System.Half" : string.Empty,
        };
        return keyword.Length > 0;
    }

    // An array, a List<T>, an IReadOnlyList<T> or an IList<T>, an ImmutableArray<T>, a Dictionary<K, V> or an IReadOnlyDictionary<K, V>
    // whose element, value and key have a row of their own.
    private static CreateShape? OfCollection(ITypeSymbol type, CollectionShape shape)
    {
        string clrName = TypeNames.ClrName(type, true);
        if (type is IArrayTypeSymbol { Rank: 1 } array)
        {
            CreateShape? element = Of(array.ElementType, shape.Element, true);
            return element is null ? null : new CreateShape(CreateKind.Array, clrName, shape.Nullability, string.Empty, element, null);
        }

        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        switch (TypeNames.FullName(named))
        {
            case "System.Collections.Generic.List`1":
            case "System.Collections.Generic.IReadOnlyList`1":
            case "System.Collections.Generic.IList`1":
                return Sequence(named, shape, CreateKind.List, clrName);
            case "System.Collections.Immutable.ImmutableArray`1":
                return Sequence(named, shape, CreateKind.ImmutableArray, clrName);
            case "System.Collections.Generic.Dictionary`2":
            case "System.Collections.Generic.IReadOnlyDictionary`2":
            case "System.Collections.Generic.IDictionary`2":
                return Map(named, shape, clrName);
            default:
                return null;
        }
    }

    private static CreateShape? Sequence(INamedTypeSymbol named, CollectionShape shape, CreateKind kind, string clrName)
    {
        CreateShape? element = Of(named.TypeArguments[0], shape.Element, true);
        return element is null ? null : new CreateShape(kind, clrName, shape.Nullability, string.Empty, element, null);
    }

    private static CreateShape? Map(INamedTypeSymbol named, CollectionShape shape, string clrName)
    {
        CreateShape? key = shape.Key is null ? null : Of(named.TypeArguments[0], shape.Key, true);
        CreateShape? value = Of(named.TypeArguments[1], shape.Element, true);
        bool keyed = key?.Kind is CreateKind.Int or CreateKind.UInt or CreateKind.Bool or CreateKind.String or CreateKind.Guid or CreateKind.Char;
        return keyed && value is not null
            ? new CreateShape(CreateKind.Dictionary, clrName, shape.Nullability, string.Empty, value, key)
            : null;
    }
}
