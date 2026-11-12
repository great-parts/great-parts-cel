namespace Great.Parts.Cel.Generator;

internal enum CelTypeKind : byte
{
    Int,
    UInt,
    Double,
    Bool,
    String,
    Bytes,
    Dyn,
    Timestamp,
    Duration,
    Decimal,

    // First is the primitive that the wrapper stands for.
    Wrapper,

    // First is the element type.
    List,

    // First is the key type, Second the value type.
    Map,

    // Name is the CEL name of an object type of the context.
    Object,

    // Name is the C# expression of the full name of a message of protobuf.
    Message,
}

// A tree that the created code writes as a CelType expression.
internal sealed record CelTypeModel(CelTypeKind Kind, string Name, CelTypeModel? First, CelTypeModel? Second)
{
    internal static CelTypeModel Of(CelTypeKind kind) => new(kind, string.Empty, null, null);

    internal static CelTypeModel WrapperOf(CelTypeModel primitive) => new(CelTypeKind.Wrapper, string.Empty, primitive, null);

    internal static CelTypeModel ListOf(CelTypeModel element) => new(CelTypeKind.List, string.Empty, element, null);

    internal static CelTypeModel MapOf(CelTypeModel key, CelTypeModel value) => new(CelTypeKind.Map, string.Empty, key, value);
}
