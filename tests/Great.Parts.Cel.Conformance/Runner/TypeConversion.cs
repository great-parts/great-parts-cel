using System;
using System.Collections.Immutable;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Types;
using PrimitiveType = Cel.Expr.Type.Types.PrimitiveType;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a type between <see cref="CelType"/> and <c>cel.expr.Type</c> for the kinds the library supports: the primitive and
/// container types, the types of time, <c>Any</c>, the wrappers and the object types of messages by name, and the abstract types
/// of the extension sets. The enumeration <c>WellKnownType</c> of <c>cel.expr.Type</c> has <c>ANY</c>, <c>TIMESTAMP</c> and
/// <c>DURATION</c>; <c>Struct</c>, <c>Value</c> and <c>ListValue</c> come as <c>message_type</c>, <c>map</c> and <c>dyn</c>. Other
/// kinds throw <see cref="NotImplementedException"/>.
/// </summary>
internal static class TypeConversion
{
    /// <summary>The name of the abstract type of the set <c>optional</c> in <c>cel.expr.Type</c>.</summary>
    internal const string OptionalTypeName = "optional_type";

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
        CelTypeKind.Wrapper => new Type { Wrapper = ToPrimitive(type.Parameters[0]) },
        CelTypeKind.Type => new Type { Type_ = ToExprType(type.Parameters[0]) },
        CelTypeKind.List => new Type { ListType = new Type.Types.ListType { ElemType = ToExprType(type.Parameters[0]) } },
        CelTypeKind.Map => new Type
        {
            MapType = new Type.Types.MapType { KeyType = ToExprType(type.Parameters[0]), ValueType = ToExprType(type.Parameters[1]) },
        },
        CelTypeKind.Abstract => new Type
        {
            AbstractType = new Type.Types.AbstractType { Name = type.Name, ParameterTypes = { type.Parameters.Select(ToExprType) } },
        },
        CelTypeKind.Optional => new Type
        {
            AbstractType = new Type.Types.AbstractType { Name = OptionalTypeName, ParameterTypes = { ToExprType(type.Parameters[0]) } },
        },
        _ => ToExprNamedType(type),
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
        Type.TypeKindOneofCase.AbstractType => FromAbstract(type.AbstractType),
        Type.TypeKindOneofCase.TypeParam => CelType.TypeParameter(type.TypeParam),
        _ => throw new NotImplementedException(),
    };

    // optional_type with one parameter is the type of the set optional; every other name is an abstract type by name, which the runner
    // declares with the first form of it that a case writes. optional_type with another number of parameters is no type of the library.
    private static CelType FromAbstract(Type.Types.AbstractType abstractType)
    {
        ImmutableArray<CelType> parameters = [.. abstractType.ParameterTypes.Select(ToCelType)];
        if (!string.Equals(abstractType.Name, OptionalTypeName, StringComparison.Ordinal))
        {
            return CelType.AbstractNamed(abstractType.Name, parameters);
        }

        return parameters.Length == 1 ? CelType.OptionalOf(parameters[0]) : throw new NotImplementedException();
    }

    // The types that cel.expr.Type names: the two types of time and Any as well-known types, an object type by its message name.
    private static Type ToExprNamedType(CelType type) => type.Kind switch
    {
        CelTypeKind.Timestamp => new Type { WellKnown = Type.Types.WellKnownType.Timestamp },
        CelTypeKind.Duration => new Type { WellKnown = Type.Types.WellKnownType.Duration },
        CelTypeKind.Any => new Type { WellKnown = Type.Types.WellKnownType.Any },
        CelTypeKind.Object => new Type { MessageType = type.Name },
        _ => throw new NotImplementedException(),
    };

    // The well-known types that cel.expr.Type has: Any and the two types of time.
    private static CelType FromWellKnown(Type.Types.WellKnownType wellKnown) => wellKnown switch
    {
        Type.Types.WellKnownType.Any => CelType.Any,
        Type.Types.WellKnownType.Timestamp => CelType.Timestamp,
        Type.Types.WellKnownType.Duration => CelType.Duration,
        _ => throw new NotImplementedException(),
    };

    // The names of the two types of time are their types; any other name is an object type by name, which the library turns into the
    // type of a known name (Any, the wrappers, Struct, ListValue, Value) or the description of a message of a provider.
    private static CelType FromMessageName(string name) => name switch
    {
        "google.protobuf.Timestamp" => CelType.Timestamp,
        "google.protobuf.Duration" => CelType.Duration,
        _ => CelType.ObjectNamed(name),
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
