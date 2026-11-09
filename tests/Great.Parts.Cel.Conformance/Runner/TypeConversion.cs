using System;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Types;
using PrimitiveType = Cel.Expr.Type.Types.PrimitiveType;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a type between <see cref="CelType"/> and <c>cel.expr.Type</c> for the kinds the library supports, the two types of
/// time among them. Other kinds throw
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
        CelTypeKind.Bool or CelTypeKind.Int or CelTypeKind.UInt or CelTypeKind.Double or CelTypeKind.String or CelTypeKind.Bytes =>
            new Type { Primitive = ToPrimitive(type) },
        CelTypeKind.Timestamp => new Type { WellKnown = Type.Types.WellKnownType.Timestamp },
        CelTypeKind.Duration => new Type { WellKnown = Type.Types.WellKnownType.Duration },
        CelTypeKind.Wrapper => new Type { Wrapper = ToPrimitive(type.Parameters[0]) },
        CelTypeKind.Type => new Type { Type_ = ToExprType(type.Parameters[0]) },
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
        Type.TypeKindOneofCase.Primitive => FromPrimitive(type.Primitive),
        Type.TypeKindOneofCase.WellKnown => FromWellKnown(type.WellKnown),
        Type.TypeKindOneofCase.MessageType => FromMessageName(type.MessageType),
        Type.TypeKindOneofCase.Wrapper => CelType.WrapperOf(FromPrimitive(type.Wrapper)),
        Type.TypeKindOneofCase.ListType => CelType.ListOf(ToCelType(type.ListType.ElemType)),
        Type.TypeKindOneofCase.MapType => CelType.MapOf(ToCelType(type.MapType.KeyType), ToCelType(type.MapType.ValueType)),
        Type.TypeKindOneofCase.Type_ => CelType.TypeOf(ToCelType(type.Type_)),
        _ => throw new NotImplementedException(),
    };

    // The well-known types of time; the other well-known types (Any and the wrappers) come with the protobuf part.
    private static CelType FromWellKnown(Type.Types.WellKnownType wellKnown) => wellKnown switch
    {
        Type.Types.WellKnownType.Timestamp => CelType.Timestamp,
        Type.Types.WellKnownType.Duration => CelType.Duration,
        _ => throw new NotImplementedException(),
    };

    // The message names of the two types of time; other message names throw until the protobuf part.
    private static CelType FromMessageName(string name) => name switch
    {
        "google.protobuf.Timestamp" => CelType.Timestamp,
        "google.protobuf.Duration" => CelType.Duration,
        _ => throw new NotImplementedException(),
    };

    private static PrimitiveType ToPrimitive(CelType type) => type.Kind switch
    {
        CelTypeKind.Bool => PrimitiveType.Bool,
        CelTypeKind.Int => PrimitiveType.Int64,
        CelTypeKind.UInt => PrimitiveType.Uint64,
        CelTypeKind.Double => PrimitiveType.Double,
        CelTypeKind.String => PrimitiveType.String,
        CelTypeKind.Bytes => PrimitiveType.Bytes,
        _ => throw new NotImplementedException(),
    };

    private static CelType FromPrimitive(PrimitiveType primitive) => primitive switch
    {
        PrimitiveType.Bool => CelType.Bool,
        PrimitiveType.Int64 => CelType.Int,
        PrimitiveType.Uint64 => CelType.UInt,
        PrimitiveType.Double => CelType.Double,
        PrimitiveType.String => CelType.String,
        PrimitiveType.Bytes => CelType.Bytes,
        _ => throw new NotImplementedException(),
    };
}
