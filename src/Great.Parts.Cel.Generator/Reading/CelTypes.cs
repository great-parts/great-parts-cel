namespace Great.Parts.Cel.Generator;

// The CEL type of a field, from how its value is read: the input table of the type mapping, with the wrapper types where the
// position can be null (a string or bytes that is nullable, and Nullable<T> of a type that has a wrapper).
internal static class CelTypes
{
    internal static CelTypeModel Of(Shape shape) => shape switch
    {
        ScalarShape scalar => OfScalar(scalar),
        EnumShape => CelTypeModel.Of(CelTypeKind.Int),
        NullableShape nullable => OfNullable(nullable),
        ObjectShape { Kind: ObjectKind.Message } message => new CelTypeModel(CelTypeKind.Message, message.CelName, null, null),
        ObjectShape objectShape => new CelTypeModel(CelTypeKind.Object, objectShape.CelName, null, null),
        CollectionShape collection => OfCollection(collection),
        _ => CelTypeModel.Of(CelTypeKind.Dyn),
    };

    private static CelTypeModel OfScalar(ScalarShape scalar)
    {
        bool nullable = scalar.Nullability != Nullability.NotNull;
        return scalar.Kind switch
        {
            ScalarKind.Int => CelTypeModel.Of(CelTypeKind.Int),
            ScalarKind.UInt => CelTypeModel.Of(CelTypeKind.UInt),
            ScalarKind.Double => CelTypeModel.Of(CelTypeKind.Double),
            ScalarKind.Bool => CelTypeModel.Of(CelTypeKind.Bool),
            ScalarKind.String => Wrapped(CelTypeKind.String, nullable),
            ScalarKind.Guid => CelTypeModel.Of(CelTypeKind.String),
            ScalarKind.Bytes => Wrapped(CelTypeKind.Bytes, nullable),
            ScalarKind.BytesMemory => CelTypeModel.Of(CelTypeKind.Bytes),
            ScalarKind.CelList => CelTypeModel.ListOf(CelTypeModel.Of(CelTypeKind.Dyn)),
            ScalarKind.CelMap => CelTypeModel.MapOf(CelTypeModel.Of(CelTypeKind.Dyn), CelTypeModel.Of(CelTypeKind.Dyn)),
            _ => OfOther(scalar.Kind),
        };
    }

    private static CelTypeModel OfOther(ScalarKind kind) => kind switch
    {
        ScalarKind.Timestamp or ScalarKind.DateTimeOffset or ScalarKind.DateTime => CelTypeModel.Of(CelTypeKind.Timestamp),
        ScalarKind.Duration or ScalarKind.TimeSpan => CelTypeModel.Of(CelTypeKind.Duration),
        ScalarKind.Decimal128 or ScalarKind.Decimal => CelTypeModel.Of(CelTypeKind.Decimal),
        _ => CelTypeModel.Of(CelTypeKind.Dyn),
    };

    private static CelTypeModel Wrapped(CelTypeKind kind, bool nullable) =>
        nullable ? CelTypeModel.WrapperOf(CelTypeModel.Of(kind)) : CelTypeModel.Of(kind);

    // Nullable<T>: the wrapper of the CEL type of T when it has one (bool, bytes, double, int, uint, string), the object type
    // of a structure, and the CEL type of T otherwise (timestamp, duration, decimal).
    private static CelTypeModel OfNullable(NullableShape shape)
    {
        CelTypeModel inner = Of(shape.Underlying);
        return inner.Kind is CelTypeKind.Int or CelTypeKind.UInt or CelTypeKind.Double or CelTypeKind.Bool or CelTypeKind.String or CelTypeKind.Bytes
            ? CelTypeModel.WrapperOf(inner)
            : inner;
    }

    private static CelTypeModel OfCollection(CollectionShape shape)
    {
        CelTypeModel element = Of(shape.Element);
        if (shape.Key is null)
        {
            return CelTypeModel.ListOf(element);
        }

        // A key is never null, so it is the primitive and not its wrapper.
        CelTypeModel key = Of(shape.Key);
        return CelTypeModel.MapOf(key.Kind == CelTypeKind.Wrapper ? key.First! : key, element);
    }
}
