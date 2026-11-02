using System;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Types;
using PrimitiveType = Cel.Expr.Type.Types.PrimitiveType;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a type between <see cref="CelType"/> and <c>cel.expr.Type</c> for the kinds the library supports. Other kinds throw
/// <see cref="NotImplementedException"/>.
/// </summary>
internal static class TypeConversion
{
    /// <summary>The type <c>dyn</c> of a case: the result type of an expression evaluated without a check.</summary>
    internal static Type Dyn => new() { Dyn = new Empty() };

    /// <summary>Converts a type of the library, such as the result type of a program, into a type of a case.</summary>
    /// <param name="type">The type of the library.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the type yet.</exception>
    internal static Type ToExprType(CelType type) => type.Kind switch
    {
        CelTypeKind.Dyn => Dyn,
        CelTypeKind.Null => new Type { Null = NullValue.NullValue },
        CelTypeKind.Bool => Primitive(PrimitiveType.Bool),
        CelTypeKind.Int => Primitive(PrimitiveType.Int64),
        CelTypeKind.UInt => Primitive(PrimitiveType.Uint64),
        CelTypeKind.Double => Primitive(PrimitiveType.Double),
        CelTypeKind.String => Primitive(PrimitiveType.String),
        CelTypeKind.Bytes => Primitive(PrimitiveType.Bytes),
        CelTypeKind.List => new Type { ListType = new Type.Types.ListType { ElemType = ToExprType(type.Parameters[0]) } },
        CelTypeKind.Map => new Type
        {
            MapType = new Type.Types.MapType { KeyType = ToExprType(type.Parameters[0]), ValueType = ToExprType(type.Parameters[1]) },
        },
        _ => throw new NotImplementedException(),
    };

    /// <summary>Converts a type of a case, such as the type of a declared variable, into a type of the library.</summary>
    /// <param name="type">The type of the case.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the type yet.</exception>
    internal static CelType ToCelType(Type type) => type.TypeKindCase switch
    {
        Type.TypeKindOneofCase.Dyn => CelType.Dyn,
        Type.TypeKindOneofCase.Null => CelType.Null,
        Type.TypeKindOneofCase.Primitive => type.Primitive switch
        {
            PrimitiveType.Bool => CelType.Bool,
            PrimitiveType.Int64 => CelType.Int,
            PrimitiveType.Uint64 => CelType.UInt,
            PrimitiveType.Double => CelType.Double,
            PrimitiveType.String => CelType.String,
            PrimitiveType.Bytes => CelType.Bytes,
            _ => throw new NotImplementedException(),
        },
        Type.TypeKindOneofCase.ListType => CelType.ListOf(ToCelType(type.ListType.ElemType)),
        Type.TypeKindOneofCase.MapType => CelType.MapOf(ToCelType(type.MapType.KeyType), ToCelType(type.MapType.ValueType)),
        _ => throw new NotImplementedException(),
    };

    private static Type Primitive(PrimitiveType primitive) => new() { Primitive = primitive };
}
